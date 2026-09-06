// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  LIVE_GATEWAY_AUTH_TIMEOUT_MS,
  LIVE_GATEWAY_MAX_MESSAGE_BYTES,
  LIVE_GATEWAY_MAX_QUEUED_BYTES,
  LIVE_GATEWAY_MAX_QUEUED_MESSAGES,
  LIVE_GATEWAY_MAX_TURNS,
  LIVE_GATEWAY_REPLY_RESERVE_MS,
  LIVE_USER_TURN_MAX_MS,
  LIVE_GATEWAY_VIDEO_FRAME_INTERVAL_MS,
  type LiveGatewayClientMessage,
  type LiveGatewayBillingSummary,
  type LiveGatewayServerMessage,
} from '../../shared/liveGatewayProtocol';
import {
  createLiveGatewayUsageCheckpoint,
  observeLiveGatewayClientMessage,
  observeLiveGatewayProviderMessage,
  type LiveGatewayUsageCheckpoint,
} from '../../shared/billing/liveGateway';

export interface GatewayTicketSession {
  sessionId: string;
  uid: string;
  model: string;
  config?: Record<string, unknown>;
  deadlineAt: number;
}

export interface GatewayFinalization {
  status: 'finalizing' | 'settled' | 'released';
  billedCredits: number;
  billedUsd: number;
  usefulOutput: boolean;
  usageSource: string;
  billingSummary?: LiveGatewayBillingSummary;
}

export interface LiveGatewayBillingPort {
  consumeTicket(ticket: string): Promise<GatewayTicketSession>;
  checkpoint(sessionId: string, checkpoint: LiveGatewayUsageCheckpoint): Promise<void>;
  finalize(
    sessionId: string,
    reason: string,
    checkpoint: LiveGatewayUsageCheckpoint,
  ): Promise<GatewayFinalization>;
}

export interface LiveProviderSession {
  sendRealtimeInput(input: Record<string, unknown>): unknown;
  sendClientContent?(input: Record<string, unknown>): unknown;
  sendToolResponse?(input: Record<string, unknown>): unknown;
  close(): unknown;
}

export interface LiveProviderCallbacks {
  onmessage(message: unknown): void;
  onerror(error: unknown): void;
  onclose(event: unknown): void;
}

export interface LiveProviderConnector {
  connect(params: {
    model: string;
    config?: Record<string, unknown>;
    callbacks: LiveProviderCallbacks;
  }): Promise<LiveProviderSession>;
}

export interface GatewayTransportPort {
  isOpen(): boolean;
  send(text: string): void;
  close(code: number, reason: string): void;
}

export interface LiveGatewayConnectionOptions {
  transport: GatewayTransportPort;
  billing: LiveGatewayBillingPort;
  provider: LiveProviderConnector;
  authTimeoutMs?: number;
  providerConnectTimeoutMs?: number;
  now?: () => number;
  sleep?: (milliseconds: number) => Promise<void>;
  log?: (level: 'info' | 'warn' | 'error', message: string, details?: unknown) => void;
}

type Phase = 'unauthenticated' | 'authenticating' | 'ready' | 'closing' | 'closed';

const getErrorMessage = (error: unknown): string => {
  if (error instanceof Error && error.message) return error.message;
  if (typeof error === 'string' && error) return error;
  return 'Unknown error';
};

const asObject = (value: unknown): Record<string, unknown> | null => (
  value && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null
);

const parseClientMessage = (text: string): LiveGatewayClientMessage => {
  let value: unknown;
  try {
    value = JSON.parse(text);
  } catch {
    throw new Error('Message must be valid JSON.');
  }
  const record = asObject(value);
  if (!record || typeof record.type !== 'string') throw new Error('Message type is required.');
  if (record.type === 'authenticate') {
    if (typeof record.ticket !== 'string' || record.ticket.length < 10 || record.ticket.length > 256) {
      throw new Error('A valid one-use ticket is required.');
    }
    return { type: 'authenticate', ticket: record.ticket };
  }
  if (record.type === 'close') return { type: 'close' };
  if (record.type === 'realtimeInput' || record.type === 'clientContent' || record.type === 'toolResponse') {
    const input = asObject(record.input);
    if (!input) throw new Error(`${record.type} input must be an object.`);
    return { type: record.type, input } as LiveGatewayClientMessage;
  }
  throw new Error('Unsupported managed Live gateway message type.');
};

const hasProviderAccountingBoundary = (value: unknown): boolean => {
  const message = asObject(value);
  if (!message) return false;
  const serverContent = asObject(message.serverContent);
  return Boolean(message.usageMetadata) || Boolean(serverContent?.turnComplete);
};

const isBase64Payload = (value: unknown): value is string => (
  typeof value === 'string'
  && value.length > 0
  && value.length % 4 === 0
  && /^[A-Za-z0-9+/]*={0,2}$/.test(value)
);

const validateRealtimeInput = (input: Record<string, unknown>): void => {
  const allowed = new Set(['audio', 'video', 'activityStart', 'activityEnd', 'audioStreamEnd']);
  const keys = Object.keys(input);
  if (keys.length === 0 || keys.some(key => !allowed.has(key))) {
    throw new Error('Managed Live realtime input contains unsupported fields.');
  }
  if (input.audio !== undefined) {
    const audio = asObject(input.audio);
    if (
      !audio
      || !isBase64Payload(audio.data)
      || !/^audio\/pcm;rate=(?:16000|24000)$/i.test(String(audio.mimeType || ''))
    ) {
      throw new Error('Managed Live audio must be base64 PCM at 16 kHz or 24 kHz.');
    }
  }
  if (input.video !== undefined) {
    const video = asObject(input.video);
    if (
      !video
      || !isBase64Payload(video.data)
      || !/^image\/(?:jpeg|png|webp)$/i.test(String(video.mimeType || ''))
    ) {
      throw new Error('Managed Live video must be a base64 JPEG, PNG, or WebP frame.');
    }
  }
  for (const key of ['activityStart', 'activityEnd']) {
    if (input[key] !== undefined && !asObject(input[key])) {
      throw new Error(`Managed Live ${key} must be an object signal.`);
    }
  }
  if (input.audioStreamEnd !== undefined && input.audioStreamEnd !== true) {
    throw new Error('Managed Live audioStreamEnd must be true.');
  }
};

const waitFor = async <T>(promise: Promise<T>, timeoutMs: number, label: string): Promise<T> => {
  let timeout: ReturnType<typeof setTimeout> | undefined;
  try {
    return await Promise.race([
      promise,
      new Promise<T>((_, reject) => {
        timeout = setTimeout(() => reject(new Error(`${label} timed out.`)), timeoutMs);
      }),
    ]);
  } finally {
    if (timeout) clearTimeout(timeout);
  }
};

/** One WebSocket's ordered, testable state machine. */
export class LiveGatewayConnection {
  private readonly timingStartedAt = performance.now();
  private readonly timingEvents: Array<{ name: string; elapsedMs: number; metrics?: Record<string, number> }> = [];
  private readonly timingSeen = new Set<string>();
  private lastAudioReceivedAt = 0;
  private lastAudioForwardedAt = 0;
  private maxInputQueueWaitMs = 0;

  private markTiming(name: string, metrics?: Record<string, number>, at = performance.now()): void {
    if (this.timingEvents.length < 40) this.timingEvents.push({ name, elapsedMs: at - this.timingStartedAt, ...(metrics ? { metrics } : {}) });
  }

  private markTimingOnce(name: string, at = performance.now()): void {
    if (this.timingSeen.has(name)) return;
    this.timingSeen.add(name);
    this.markTiming(name, undefined, at);
  }
  private phase: Phase = 'unauthenticated';
  private serial: Promise<void> = Promise.resolve();
  private ticketSession: GatewayTicketSession | null = null;
  private providerSession: LiveProviderSession | null = null;
  private checkpointState = createLiveGatewayUsageCheckpoint();
  private authTimer: ReturnType<typeof setTimeout> | null = null;
  private deadlineTimer: ReturnType<typeof setTimeout> | null = null;
  private transportDisconnected = false;
  private providerClosed = false;
  private finalization: Promise<void> | null = null;
  private providerInputPacingStartedAt: number | null = null;
  private providerInputDurationScheduledMs = 0;
  private providerVideoPacingStartedAt: number | null = null;
  private providerVideoFramesScheduled = 0;
  private inputTimer: ReturnType<typeof setTimeout> | null = null;
  private inputEnded = false;
  private inputStartedAt: number | null = null;
  private inputDeadlineAt: number | null = null;
  private inputDeadlineReason: 'duration-limit' | 'reply-window' = 'duration-limit';
  private inputAudioDurationMs = 0;
  private inputSampleRate: number | null = null;
  private manualActivityStarted = false;
  private closingRequested = false;
  private queuedBytes = 0;
  private queuedMessages = 0;
  private readonly pacingCancellations = new Set<() => void>();
  private providerClose: Promise<void> | null = null;

  constructor(private readonly options: LiveGatewayConnectionOptions) {
    this.authTimer = setTimeout(
      () => this.enqueue(() => this.failUnauthenticated('Authentication timed out.', 4001)),
      Math.max(100, options.authTimeoutMs ?? LIVE_GATEWAY_AUTH_TIMEOUT_MS),
    );
  }

  receive(text: string): void {
    if (this.closingRequested || this.phase === 'closed') return;
    const receivedAt = performance.now();
    const bytes = Buffer.byteLength(text, 'utf8');
    try {
      if (bytes > LIVE_GATEWAY_MAX_MESSAGE_BYTES) throw new Error('Managed Live message is too large.');
      const message = parseClientMessage(text);
      if (message.type === 'close') {
        this.requestShutdown('client-close');
        return;
      }
      if (message.type === 'realtimeInput' && this.inputEnded) return;
      if (this.queuedBytes + bytes > LIVE_GATEWAY_MAX_QUEUED_BYTES
        || this.queuedMessages >= LIVE_GATEWAY_MAX_QUEUED_MESSAGES) {
        if (this.phase === 'ready') void this.endInput('buffer-limit').catch(error => this.handleFatal(error));
        else this.requestShutdown('input-buffer-limit');
        return;
      }
      this.queuedBytes += bytes;
      this.queuedMessages += 1;
      this.enqueue(async () => {
        try {
          if (!this.closingRequested) await this.handleClientMessage(message, receivedAt);
        } finally {
          this.queuedBytes -= bytes;
          this.queuedMessages -= 1;
        }
      });
    } catch (error) {
      this.inputEnded = true;
      this.cancelPacing();
      this.enqueue(() => this.handleFatal(error));
    }
  }

  /** Allows graceful process shutdowns and deterministic state-machine tests. */
  async whenIdle(): Promise<void> {
    let current: Promise<void>;
    do {
      current = this.serial;
      await current;
    } while (current !== this.serial);
  }

  disconnect(): void {
    this.transportDisconnected = true;
    this.requestShutdown('client-disconnect');
  }

  /** Stop paid work immediately; ledger finalization still follows observed output. */
  private requestShutdown(reason: string): void {
    if (this.closingRequested || this.phase === 'closed') return;
    this.closingRequested = true;
    this.inputEnded = true;
    this.clearTimers();
    this.cancelPacing();
    void this.closeProvider();
    this.enqueue(() => this.shutdown(reason, true));
  }

  private closeProvider(): Promise<void> {
    if (this.providerClose) return this.providerClose;
    if (!this.providerSession || this.providerClosed) return Promise.resolve();
    this.providerClosed = true;
    try {
      this.providerClose = Promise.resolve(this.providerSession.close()).then(() => undefined).catch(error => {
        this.options.log?.('warn', 'Gemini Live provider close failed.', getErrorMessage(error));
      });
    } catch (error) {
      this.options.log?.('warn', 'Gemini Live provider close failed.', getErrorMessage(error));
      this.providerClose = Promise.resolve();
    }
    return this.providerClose;
  }

  private enqueue(task: () => Promise<void> | void): void {
    this.serial = this.serial
      .then(task)
      .catch((error) => this.handleFatal(error));
  }

  private send(message: LiveGatewayServerMessage): boolean {
    if (this.transportDisconnected || !this.options.transport.isOpen()) return false;
    try {
      this.options.transport.send(JSON.stringify(message));
      return true;
    } catch (error) {
      this.transportDisconnected = true;
      this.options.log?.('warn', 'Client WebSocket send failed.', getErrorMessage(error));
      return false;
    }
  }

  private closeTransport(code: number, reason: string): void {
    if (this.transportDisconnected || !this.options.transport.isOpen()) return;
    try {
      this.options.transport.close(code, reason.slice(0, 100));
    } finally {
      this.transportDisconnected = true;
    }
  }

  private clearTimers(): void {
    if (this.authTimer) clearTimeout(this.authTimer);
    if (this.deadlineTimer) clearTimeout(this.deadlineTimer);
    if (this.inputTimer) clearTimeout(this.inputTimer);
    this.authTimer = null;
    this.deadlineTimer = null;
    this.inputTimer = null;
  }

  private now(): number { return this.options.now?.() ?? Date.now(); }

  private usesManualActivity(): boolean {
    const realtime = asObject(this.ticketSession?.config?.realtimeInputConfig);
    return asObject(realtime?.automaticActivityDetection)?.disabled === true;
  }

  private cancelPacing(): void {
    for (const cancel of this.pacingCancellations) cancel();
  }

  private waitForPacing(delayMs: number): Promise<boolean> {
    if (this.inputEnded || this.closingRequested) return Promise.resolve(false);
    if (delayMs <= 0) return Promise.resolve(true);
    return new Promise((resolve, reject) => {
      let timer: ReturnType<typeof setTimeout> | undefined;
      const finish = (ready: boolean) => {
        if (timer) clearTimeout(timer);
        this.pacingCancellations.delete(cancel);
        resolve(ready);
      };
      const cancel = () => finish(false);
      this.pacingCancellations.add(cancel);
      if (this.options.sleep) {
        void this.options.sleep(delayMs).then(() => finish(true), error => {
          this.pacingCancellations.delete(cancel);
          reject(error);
        });
      } else timer = setTimeout(() => finish(true), delayMs);
    });
  }

  private startInputWindow(): void {
    if (this.inputStartedAt !== null) return;
    this.inputStartedAt = this.now();
    const durationDeadline = this.inputStartedAt + LIVE_USER_TURN_MAX_MS;
    const remainingMs = Math.max(0, this.ticketSession!.deadlineAt - this.inputStartedAt);
    // Short configured windows still get an input turn; divide their remaining
    // time instead of ending before the first sample has reached the model.
    const replyReserveMs = Math.min(LIVE_GATEWAY_REPLY_RESERVE_MS, remainingMs / 2);
    const replyDeadline = this.ticketSession!.deadlineAt - replyReserveMs;
    this.inputDeadlineAt = Math.min(durationDeadline, replyDeadline);
    this.inputDeadlineReason = replyDeadline < durationDeadline ? 'reply-window' : 'duration-limit';
    this.inputTimer = setTimeout(
      () => { void this.endInput(this.inputDeadlineReason).catch(error => this.handleFatal(error)); },
      Math.max(0, this.inputDeadlineAt - this.now()),
    );
  }

  private async canForwardInput(): Promise<boolean> {
    if (this.closingRequested || this.inputEnded || this.phase !== 'ready') return false;
    if (this.now() >= this.ticketSession!.deadlineAt) {
      this.requestShutdown('session-deadline');
      return false;
    }
    if (this.inputDeadlineAt !== null && this.now() >= this.inputDeadlineAt) {
      await this.endInput(this.inputDeadlineReason);
      return false;
    }
    return true;
  }

  private async forwardInput(input: Record<string, unknown>): Promise<void> {
    if (!await this.canForwardInput()) return;
    await this.providerSession!.sendRealtimeInput(input);
    this.checkpointState = observeLiveGatewayClientMessage(this.checkpointState, input);
  }

  /** End the user's turn without closing the connection or cutting off the answer. */
  private async endInput(reason: 'client-end' | 'duration-limit' | 'reply-window' | 'buffer-limit'): Promise<void> {
    if (this.inputEnded || this.closingRequested || this.phase !== 'ready') return;
    this.inputEnded = true;
    if (this.inputTimer) clearTimeout(this.inputTimer);
    this.inputTimer = null;
    this.cancelPacing();
    const end = this.usesManualActivity() ? { activityEnd: {} } : { audioStreamEnd: true };
    await this.providerSession!.sendRealtimeInput(end);
    // One logical boundary, regardless of the provider's automatic/manual VAD mode.
    this.checkpointState = observeLiveGatewayClientMessage(this.checkpointState, { audioStreamEnd: true });
    this.markTiming('input.last-audio-received', undefined, this.lastAudioReceivedAt);
    this.markTiming('input.last-audio-forwarded', undefined, this.lastAudioForwardedAt);
    this.markTiming('input.activity-end-forwarded', { inputAudioDurationMs: this.inputAudioDurationMs });
    if (reason !== 'client-end') {
      this.send({ type: 'providerMessage', message: {}, inputTurnEnded: { reason, maxDurationMs: LIVE_USER_TURN_MAX_MS } });
    }
    this.enqueue(() => this.persistCheckpoint());
  }

  private async handleClientMessage(message: LiveGatewayClientMessage, receivedAt: number): Promise<void> {
    if (message.type === 'authenticate') {
      if (this.phase !== 'unauthenticated') throw new Error('Authentication may only be sent once.');
      await this.authenticate(message.ticket);
      return;
    }
    if (this.phase !== 'ready' || !this.providerSession) {
      throw new Error('Managed Live gateway is not ready.');
    }
    if (message.type === 'close') {
      await this.shutdown('client-close', true);
      return;
    }
    if (message.type === 'realtimeInput') {
      if (this.inputEnded) return;
      validateRealtimeInput(message.input);
      if (message.input.audio) {
        this.markTimingOnce('input.first-audio-received', receivedAt);
        this.lastAudioReceivedAt = receivedAt;
        this.maxInputQueueWaitMs = Math.max(this.maxInputQueueWaitMs, performance.now() - receivedAt);
      }
      if (message.input.activityEnd) this.markTiming('input.activity-end-received', undefined, receivedAt);
      const hasNewMediaOrTurn = Boolean(
        message.input.audio
        || message.input.video
        || message.input.activityStart
        || message.input.audioStreamEnd,
      );
      if (this.checkpointState.providerTurnCompleteCount >= LIVE_GATEWAY_MAX_TURNS && hasNewMediaOrTurn) {
        throw new Error(`Managed Live sessions are limited to ${LIVE_GATEWAY_MAX_TURNS} turns.`);
      }
      if (
        hasNewMediaOrTurn
        && this.checkpointState.clientTurnBoundaryCount >= LIVE_GATEWAY_MAX_TURNS
      ) {
        throw new Error(`Managed Live sessions are limited to ${LIVE_GATEWAY_MAX_TURNS} turns.`);
      }
      if (message.input.audio || message.input.video || message.input.activityStart) this.startInputWindow();
      if (!await this.canForwardInput()) return;
      if (this.usesManualActivity() && !this.manualActivityStarted
        && (message.input.activityStart || message.input.audio || message.input.video)) {
        this.manualActivityStarted = true;
        await this.forwardInput({ activityStart: {} });
      }
      if (message.input.audio) {
        const audio = message.input.audio as { data: string; mimeType: string };
        const sampleRate = Number(audio.mimeType.split('=')[1]);
        if (this.inputSampleRate !== null && this.inputSampleRate !== sampleRate) {
          throw new Error('Managed Live audio sample rate cannot change within a turn.');
        }
        this.inputSampleRate = sampleRate;
        const pcm = Buffer.from(audio.data, 'base64');
        if (pcm.length % 2 !== 0) throw new Error('Managed Live PCM must contain complete 16-bit samples.');
        // Split large client frames so each pacing wait is at microphone cadence.
        const packetBytes = sampleRate / 10 * 2;
        for (let offset = 0; offset < pcm.length; offset += packetBytes) {
          if (!await this.canForwardInput()) return;
          const remainingBytes = Math.max(0, Math.round((LIVE_USER_TURN_MAX_MS - this.inputAudioDurationMs) * sampleRate / 1000)) * 2;
          const packet = pcm.subarray(offset, offset + Math.min(packetBytes, remainingBytes));
          if (!packet.length) { await this.endInput('duration-limit'); return; }
          if (!await this.paceProviderAudio(packet.length, sampleRate) || !await this.canForwardInput()) return;
          await this.forwardInput({ audio: { data: packet.toString('base64'), mimeType: audio.mimeType } });
          this.inputAudioDurationMs += packet.length / 2 / sampleRate * 1000;
          this.markTimingOnce('input.first-audio-forwarded');
          this.lastAudioForwardedAt = performance.now();
          if (this.inputAudioDurationMs >= LIVE_USER_TURN_MAX_MS) { await this.endInput('duration-limit'); return; }
        }
      }
      if (message.input.video) {
        if (!await this.paceProviderVideo(1) || !await this.canForwardInput()) return;
        await this.forwardInput({ video: message.input.video });
      }
      if (message.input.activityEnd || message.input.audioStreamEnd) await this.endInput('client-end');
      return;
    }
    if (message.type === 'clientContent') {
      throw new Error('Managed Live client content is not supported.');
    }
    throw new Error('Managed Live tool responses are not supported.');
  }

  /**
   * The provider's realtime input deliberately has no deterministic ordering
   * guarantee under bursts. A slow model/socket handoff can queue seconds of
   * microphone PCM, so replay that queue at its captured cadence.
   */
  private async paceProviderAudio(audioBytes: number, sampleRate: number): Promise<boolean> {
    if (audioBytes <= 0 || sampleRate <= 0) return true;
    const now = this.options.now?.() ?? Date.now();
    this.providerInputPacingStartedAt ??= now;
    const dueAt = this.providerInputPacingStartedAt + this.providerInputDurationScheduledMs;
    this.providerInputDurationScheduledMs += (audioBytes / 2 / sampleRate) * 1_000;
    const delayMs = dueAt - now;
    return this.waitForPacing(delayMs);
  }

  private async paceProviderVideo(frameCount: number): Promise<boolean> {
    if (frameCount <= 0) return true;
    const now = this.options.now?.() ?? Date.now();
    this.providerVideoPacingStartedAt ??= now;
    const dueAt = this.providerVideoPacingStartedAt
      + this.providerVideoFramesScheduled * LIVE_GATEWAY_VIDEO_FRAME_INTERVAL_MS;
    this.providerVideoFramesScheduled += frameCount;
    const delayMs = dueAt - now;
    return this.waitForPacing(delayMs);
  }

  private async authenticate(ticket: string): Promise<void> {
    this.phase = 'authenticating';
    if (this.authTimer) clearTimeout(this.authTimer);
    this.authTimer = null;
    this.ticketSession = await this.options.billing.consumeTicket(ticket);
    if (this.transportDisconnected || this.closingRequested) {
      await this.shutdown('client-disconnect-before-provider', false);
      return;
    }

    const callbacks: LiveProviderCallbacks = {
      onmessage: (message) => {
        const content = (message as { serverContent?: { inputTranscription?: unknown; outputTranscription?: unknown; modelTurn?: { parts?: Array<{ inlineData?: unknown }> } } })?.serverContent;
        if (content?.inputTranscription) this.markTimingOnce('input.first-provider-transcript-received');
        if (content?.outputTranscription) this.markTimingOnce('response.first-transcript-received');
        if (content?.modelTurn?.parts?.some(part => part.inlineData)) this.markTimingOnce('response.first-audio-received');
        this.enqueue(() => this.handleProviderMessage(message));
      },
      onerror: (error) => this.enqueue(() => this.handleProviderError(error)),
      onclose: (event) => this.enqueue(() => this.handleProviderClose(event)),
    };
    this.markTiming('provider.connect-start');
    const connectPromise = this.options.provider.connect({
      model: this.ticketSession.model,
      ...(this.ticketSession.config ? { config: this.ticketSession.config } : {}),
      callbacks,
    });
    void connectPromise.then((lateSession) => {
      if (this.phase === 'closing' || this.phase === 'closed') {
        try { lateSession.close(); } catch { /* best effort */ }
      }
    }).catch(() => undefined);
    this.providerSession = await waitFor(
      connectPromise,
      Math.max(1_000, this.options.providerConnectTimeoutMs ?? 20_000),
      'Gemini Live provider connection',
    );
    if (this.transportDisconnected || this.closingRequested) {
      await this.shutdown('client-disconnect-before-ready', true);
      return;
    }
    this.phase = 'ready';
    const deadlineDelay = Math.max(0, this.ticketSession.deadlineAt - (this.options.now?.() ?? Date.now()));
    this.deadlineTimer = setTimeout(
      () => this.requestShutdown('session-deadline'),
      Math.min(deadlineDelay, 2_147_483_647),
    );
    this.send({
      type: 'ready',
      sessionId: this.ticketSession.sessionId,
      deadlineAt: this.ticketSession.deadlineAt,
    });
  }

  private async handleProviderMessage(message: unknown): Promise<void> {
    if (this.phase !== 'ready' || !this.ticketSession) return;
    const wasUseful = this.checkpointState.usefulOutput;
    this.checkpointState = observeLiveGatewayProviderMessage(this.checkpointState, message);
    const firstUsefulOutput = !wasUseful && this.checkpointState.usefulOutput;

    // The first useful byte is committed before delivery. A client cannot take
    // an answer, disconnect, and then claim that the provider returned nothing.
    if (firstUsefulOutput || hasProviderAccountingBoundary(message)) {
      await this.persistCheckpoint();
    }
    this.send({ type: 'providerMessage', message });
    if (firstUsefulOutput) this.markTimingOnce('response.first-useful-output-forwarded');
  }

  private async handleProviderError(error: unknown): Promise<void> {
    if (this.phase === 'closed') return;
    this.options.log?.('error', 'Gemini Live provider reported an error.', getErrorMessage(error));
    this.send({
      type: 'error',
      message: 'Managed Live provider connection failed.',
      code: 'LIVE_PROVIDER_ERROR',
      retryable: true,
    });
    await this.shutdown('provider-error', false);
  }

  private async handleProviderClose(_event: unknown): Promise<void> {
    this.providerClosed = true;
    if (this.phase === 'closed') return;
    await this.shutdown('provider-close', false);
  }

  private async persistCheckpoint(): Promise<void> {
    if (!this.ticketSession) return;
    await this.options.billing.checkpoint(this.ticketSession.sessionId, this.checkpointState);
  }

  private async shutdown(reason: string, closeProvider: boolean): Promise<void> {
    if (this.finalization) return this.finalization;
    this.finalization = this.performShutdown(reason, closeProvider);
    return this.finalization;
  }

  private async performShutdown(reason: string, closeProvider: boolean): Promise<void> {
    this.markTiming('session.closing');
    if (this.ticketSession) this.options.log?.('info', 'Live turn timing', {
      sessionId: this.ticketSession.sessionId,
      clock: 'gateway-monotonic',
      events: this.timingEvents,
    });
    this.phase = 'closing';
    this.closingRequested = true;
    this.inputEnded = true;
    this.clearTimers();
    this.cancelPacing();
    if (closeProvider) await this.closeProvider();
    if (this.ticketSession) {
      const result = await this.options.billing.finalize(
        this.ticketSession.sessionId,
        reason,
        this.checkpointState,
      );
      this.send({ type: 'billing', ...result });
    }
    this.phase = 'closed';
    this.closeTransport(1000, 'session-complete');
  }

  private async failUnauthenticated(message: string, closeCode: number): Promise<void> {
    if (this.phase !== 'unauthenticated') return;
    this.send({ type: 'error', message, code: 'LIVE_GATEWAY_AUTH', retryable: true });
    this.phase = 'closed';
    this.closeTransport(closeCode, 'authentication-failed');
  }

  private async handleFatal(error: unknown): Promise<void> {
    if (this.phase === 'closed') return;
    this.options.log?.('error', 'Managed Live gateway connection failed.', getErrorMessage(error));
    this.send({
      type: 'error',
      message: this.ticketSession
        ? 'Managed Live session failed. Reserved credits will be reconciled automatically.'
        : getErrorMessage(error),
      code: this.ticketSession ? 'LIVE_GATEWAY_SESSION' : 'LIVE_GATEWAY_AUTH',
      retryable: true,
    });
    try {
      await this.shutdown('gateway-error', true);
    } catch (finalizationError) {
      this.options.log?.('error', 'Managed Live billing finalization deferred to recovery.', getErrorMessage(finalizationError));
      this.phase = 'closed';
      this.closeTransport(1011, 'accounting-recovery');
    }
  }
}

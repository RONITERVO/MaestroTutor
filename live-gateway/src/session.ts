// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  LIVE_GATEWAY_AUTH_TIMEOUT_MS,
  LIVE_GATEWAY_MAX_TURNS,
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

const isAudioBoundary = (input: Record<string, unknown>): boolean => (
  Boolean(input.audioStreamEnd)
);

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

  constructor(private readonly options: LiveGatewayConnectionOptions) {
    this.authTimer = setTimeout(
      () => this.enqueue(() => this.failUnauthenticated('Authentication timed out.', 4001)),
      Math.max(100, options.authTimeoutMs ?? LIVE_GATEWAY_AUTH_TIMEOUT_MS),
    );
  }

  receive(text: string): void {
    const receivedAt = performance.now();
    this.enqueue(() => this.handleClientMessage(parseClientMessage(text), receivedAt));
  }

  /** Allows graceful process shutdowns and deterministic state-machine tests. */
  whenIdle(): Promise<void> {
    return this.serial;
  }

  disconnect(): void {
    this.transportDisconnected = true;
    this.clearTimers();
    this.enqueue(() => this.shutdown('client-disconnect', true));
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
    this.authTimer = null;
    this.deadlineTimer = null;
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
      const previousInputBytes = this.checkpointState.inputAudioBytes;
      const previousVideoFrames = this.checkpointState.inputVideoFrameCount;
      this.checkpointState = observeLiveGatewayClientMessage(this.checkpointState, message.input);
      const addedAudioBytes = this.checkpointState.inputAudioBytes - previousInputBytes;
      const addedVideoFrames = this.checkpointState.inputVideoFrameCount - previousVideoFrames;
      await this.paceProviderAudio(addedAudioBytes, this.checkpointState.inputAudioSampleRate);
      await this.paceProviderVideo(addedVideoFrames);
      await this.providerSession.sendRealtimeInput(message.input);
      if (message.input.audio) {
        this.markTimingOnce('input.first-audio-forwarded');
        this.lastAudioForwardedAt = performance.now();
      }
      if (message.input.activityEnd) {
        this.markTiming('input.last-audio-received', undefined, this.lastAudioReceivedAt);
        this.markTiming('input.last-audio-forwarded', undefined, this.lastAudioForwardedAt);
        this.markTiming('input.activity-end-forwarded', { maxInputQueueWaitMs: this.maxInputQueueWaitMs });
      }
      if (isAudioBoundary(message.input)) await this.persistCheckpoint();
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
  private async paceProviderAudio(audioBytes: number, sampleRate: number): Promise<void> {
    if (audioBytes <= 0 || sampleRate <= 0) return;
    const now = this.options.now?.() ?? Date.now();
    this.providerInputPacingStartedAt ??= now;
    const dueAt = this.providerInputPacingStartedAt + this.providerInputDurationScheduledMs;
    this.providerInputDurationScheduledMs += (audioBytes / 2 / sampleRate) * 1_000;
    const delayMs = dueAt - now;
    if (delayMs <= 0) return;
    const sleep = this.options.sleep
      || ((milliseconds: number) => new Promise<void>(resolve => setTimeout(resolve, milliseconds)));
    await sleep(delayMs);
  }

  private async paceProviderVideo(frameCount: number): Promise<void> {
    if (frameCount <= 0) return;
    const now = this.options.now?.() ?? Date.now();
    this.providerVideoPacingStartedAt ??= now;
    const dueAt = this.providerVideoPacingStartedAt
      + this.providerVideoFramesScheduled * LIVE_GATEWAY_VIDEO_FRAME_INTERVAL_MS;
    this.providerVideoFramesScheduled += frameCount;
    const delayMs = dueAt - now;
    if (delayMs <= 0) return;
    const sleep = this.options.sleep
      || ((milliseconds: number) => new Promise<void>(resolve => setTimeout(resolve, milliseconds)));
    await sleep(delayMs);
  }

  private async authenticate(ticket: string): Promise<void> {
    this.phase = 'authenticating';
    if (this.authTimer) clearTimeout(this.authTimer);
    this.authTimer = null;
    this.ticketSession = await this.options.billing.consumeTicket(ticket);
    if (this.transportDisconnected) {
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
    if (this.transportDisconnected) {
      await this.shutdown('client-disconnect-before-ready', true);
      return;
    }
    this.phase = 'ready';
    const deadlineDelay = Math.max(0, this.ticketSession.deadlineAt - (this.options.now?.() ?? Date.now()));
    this.deadlineTimer = setTimeout(
      () => this.enqueue(() => this.shutdown('session-deadline', true)),
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
    this.clearTimers();
    if (closeProvider && this.providerSession && !this.providerClosed) {
      this.providerClosed = true;
      try {
        await this.providerSession.close();
      } catch (error) {
        this.options.log?.('warn', 'Gemini Live provider close failed.', getErrorMessage(error));
      }
    }
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

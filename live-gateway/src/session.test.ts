// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import { describe, it } from 'vitest';
import type { LiveGatewayUsageCheckpoint } from '../../shared/billing/liveGateway';
import type { LiveGatewayServerMessage } from '../../shared/liveGatewayProtocol';
import {
  LiveGatewayConnection,
  type GatewayFinalization,
  type GatewayTransportPort,
  type LiveGatewayBillingPort,
  type LiveProviderCallbacks,
  type LiveProviderConnector,
  type LiveProviderSession,
} from './session';

const tick = () => new Promise<void>((resolve) => setImmediate(resolve));

class FakeTransport implements GatewayTransportPort {
  open = true;
  messages: LiveGatewayServerMessage[] = [];
  events: string[] = [];
  closeCalls: Array<{ code: number; reason: string }> = [];

  isOpen() { return this.open; }
  send(text: string) {
    const message = JSON.parse(text) as LiveGatewayServerMessage;
    this.messages.push(message);
    this.events.push(`send:${message.type}`);
  }
  close(code: number, reason: string) {
    this.closeCalls.push({ code, reason });
    this.open = false;
  }
}

class FakeBilling implements LiveGatewayBillingPort {
  checkpoints: LiveGatewayUsageCheckpoint[] = [];
  finalizations: Array<{ reason: string; checkpoint: LiveGatewayUsageCheckpoint }> = [];
  events: string[] = [];

  async consumeTicket(ticket: string) {
    assert.equal(ticket, 'ticket.secret-value');
    return {
      sessionId: 'session-1',
      uid: 'user-1',
      model: 'gemini-live-test',
      config: { responseModalities: ['AUDIO'] },
      deadlineAt: Date.now() + 60_000,
    };
  }
  async checkpoint(_sessionId: string, checkpoint: LiveGatewayUsageCheckpoint) {
    this.checkpoints.push(structuredClone(checkpoint));
    this.events.push('checkpoint');
  }
  async finalize(_sessionId: string, reason: string, checkpoint: LiveGatewayUsageCheckpoint): Promise<GatewayFinalization> {
    this.finalizations.push({ reason, checkpoint: structuredClone(checkpoint) });
    this.events.push('finalize');
    return checkpoint.usefulOutput
      ? {
          status: 'settled',
          billedCredits: 3,
          billedUsd: 0.003,
          usefulOutput: true,
          usageSource: checkpoint.providerUsageMetadata ? 'provider' : 'transport',
        }
      : {
          status: 'released',
          billedCredits: 0,
          billedUsd: 0,
          usefulOutput: false,
          usageSource: 'none',
        };
  }
}

class FakeProvider implements LiveProviderConnector {
  callbacks: LiveProviderCallbacks | null = null;
  realtimeInputs: Record<string, unknown>[] = [];
  clientContents: Record<string, unknown>[] = [];
  toolResponses: Record<string, unknown>[] = [];
  closeCount = 0;
  connectError: Error | null = null;

  readonly session: LiveProviderSession = {
    sendRealtimeInput: (input) => { this.realtimeInputs.push(input); },
    sendClientContent: (input) => { this.clientContents.push(input); },
    sendToolResponse: (input) => { this.toolResponses.push(input); },
    close: () => { this.closeCount += 1; },
  };

  async connect(params: { callbacks: LiveProviderCallbacks }): Promise<LiveProviderSession> {
    this.callbacks = params.callbacks;
    if (this.connectError) throw this.connectError;
    return this.session;
  }
}

const createHarness = (overrides: {
  authTimeoutMs?: number;
  provider?: FakeProvider;
  now?: () => number;
  sleep?: (milliseconds: number) => Promise<void>;
} = {}) => {
  const transport = new FakeTransport();
  const billing = new FakeBilling();
  const provider = overrides.provider || new FakeProvider();
  const connection = new LiveGatewayConnection({
    transport,
    billing,
    provider,
    authTimeoutMs: overrides.authTimeoutMs,
    now: overrides.now,
    sleep: overrides.sleep,
  });
  const authenticate = async () => {
    connection.receive(JSON.stringify({ type: 'authenticate', ticket: 'ticket.secret-value' }));
    await connection.whenIdle();
  };
  const close = async () => {
    connection.receive(JSON.stringify({ type: 'close' }));
    await connection.whenIdle();
  };
  return { transport, billing, provider, connection, authenticate, close };
};

describe('managed Live gateway connection', () => {
  it('releases the full reservation when setup succeeds but no useful output arrives', async () => {
    const harness = createHarness();
    await harness.authenticate();
    harness.provider.callbacks!.onmessage({ setupComplete: {} });
    await harness.connection.whenIdle();
    await harness.close();

    assert.equal(harness.billing.finalizations.length, 1);
    assert.equal(harness.billing.finalizations[0].checkpoint.setupComplete, true);
    assert.equal(harness.billing.finalizations[0].checkpoint.usefulOutput, false);
    const billingMessage = harness.transport.messages.find((message) => message.type === 'billing');
    assert.deepEqual(billingMessage, {
      type: 'billing',
      status: 'released',
      billedCredits: 0,
      billedUsd: 0,
      usefulOutput: false,
      usageSource: 'none',
    });
  });

  it('durably checkpoints the first useful provider output before forwarding it', async () => {
    const harness = createHarness();
    harness.billing.events = harness.transport.events;
    await harness.authenticate();
    harness.provider.callbacks!.onmessage({
      serverContent: {
        modelTurn: {
          parts: [{ inlineData: { data: 'AQIDBA==', mimeType: 'audio/pcm;rate=24000' } }],
        },
      },
    });
    await harness.connection.whenIdle();

    assert.deepEqual(harness.transport.events.slice(-2), ['checkpoint', 'send:providerMessage']);
    assert.equal(harness.billing.checkpoints[0].usefulOutput, true);
    assert.equal(harness.billing.checkpoints[0].outputAudioBytes, 4);
    await harness.close();
  });

  it('forwards validated realtime input and settles from provider usage metadata', async () => {
    const harness = createHarness();
    await harness.authenticate();
    harness.connection.receive(JSON.stringify({
      type: 'realtimeInput',
      input: { audio: { data: 'AQIDBA==', mimeType: 'audio/pcm;rate=16000' }, audioStreamEnd: true },
    }));
    await harness.connection.whenIdle();
    harness.provider.callbacks!.onmessage({
      usageMetadata: {
        promptTokenCount: 10,
        responseTokenCount: 4,
        totalTokenCount: 14,
        promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 10 }],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 4 }],
      },
      serverContent: { modelTurn: { parts: [{ text: 'hello' }] }, turnComplete: true },
    });
    await harness.connection.whenIdle();
    await harness.close();

    assert.equal(harness.provider.realtimeInputs.length, 1);
    assert.equal(harness.provider.clientContents.length, 0);
    assert.equal(harness.provider.toolResponses.length, 0);
    assert.equal(harness.billing.finalizations[0].checkpoint.inputAudioBytes, 4);
    assert.equal(harness.billing.finalizations[0].checkpoint.providerUsageMetadata?.totalTokenCount, 14);
    assert.equal(harness.transport.messages.some(
      (message) => message.type === 'billing' && message.usageSource === 'provider',
    ), true);
  });

  it('rejects unmetered client-content and tool-response methods', async () => {
    for (const message of [
      { type: 'clientContent', input: { turns: [] } },
      { type: 'toolResponse', input: { functionResponses: [] } },
    ]) {
      const harness = createHarness();
      await harness.authenticate();
      harness.connection.receive(JSON.stringify(message));
      await harness.connection.whenIdle();

      assert.equal(harness.billing.finalizations.length, 1);
      assert.equal(harness.billing.finalizations[0].reason, 'gateway-error');
      assert.equal(harness.provider.clientContents.length, 0);
      assert.equal(harness.provider.toolResponses.length, 0);
    }
  });

  it('paces a burst of buffered PCM before forwarding it to the provider', async () => {
    let now = 1_000;
    const sentAt: number[] = [];
    const provider = new FakeProvider();
    provider.session.sendRealtimeInput = (input) => {
      provider.realtimeInputs.push(input);
      sentAt.push(now);
    };
    const harness = createHarness({
      provider,
      now: () => now,
      sleep: async milliseconds => { now += milliseconds; },
    });
    await harness.authenticate();
    const audio = Buffer.alloc(3_200).toString('base64');
    for (let index = 0; index < 3; index += 1) {
      harness.connection.receive(JSON.stringify({
        type: 'realtimeInput',
        input: { audio: { data: audio, mimeType: 'audio/pcm;rate=16000' } },
      }));
    }
    await harness.connection.whenIdle();

    assert.deepEqual(sentAt, [1_000, 1_100, 1_200]);
    assert.equal(provider.realtimeInputs.length, 3);
    await harness.close();
  });

  it('paces camera frames at one frame per second', async () => {
    let now = 1_000;
    const sentAt: number[] = [];
    const provider = new FakeProvider();
    provider.session.sendRealtimeInput = (input) => {
      provider.realtimeInputs.push(input);
      sentAt.push(now);
    };
    const harness = createHarness({
      provider,
      now: () => now,
      sleep: async milliseconds => { now += milliseconds; },
    });
    await harness.authenticate();
    for (let index = 0; index < 3; index += 1) {
      harness.connection.receive(JSON.stringify({
        type: 'realtimeInput',
        input: { video: { data: 'AA==', mimeType: 'image/jpeg' } },
      }));
    }
    await harness.connection.whenIdle();

    assert.deepEqual(sentAt, [1_000, 2_000, 3_000]);
    assert.equal(harness.billing.finalizations.length, 0);
    await harness.close();
  });

  it('refuses a seventh turn before it reaches the provider', async () => {
    const harness = createHarness();
    await harness.authenticate();
    for (let turn = 0; turn < 7; turn += 1) {
      harness.connection.receive(JSON.stringify({
        type: 'realtimeInput',
        input: { audioStreamEnd: true },
      }));
    }
    await harness.connection.whenIdle();

    assert.equal(harness.provider.realtimeInputs.length, 6);
    assert.equal(harness.billing.finalizations.length, 1);
    assert.equal(harness.billing.finalizations[0].checkpoint.clientTurnBoundaryCount, 6);
  });

  it('releases a consumed ticket when the provider connection fails', async () => {
    const provider = new FakeProvider();
    provider.connectError = new Error('provider refused connection');
    const harness = createHarness({ provider });
    await harness.authenticate();
    await tick();
    await harness.connection.whenIdle();

    assert.equal(harness.billing.finalizations.length, 1);
    assert.equal(harness.billing.finalizations[0].reason, 'gateway-error');
    assert.equal(harness.billing.finalizations[0].checkpoint.usefulOutput, false);
    assert.equal(harness.transport.closeCalls.length, 1);
  });

  it('finalizes exactly once across disconnect and provider-close races', async () => {
    const harness = createHarness();
    await harness.authenticate();
    harness.connection.disconnect();
    harness.provider.callbacks!.onclose({});
    await harness.connection.whenIdle();

    assert.equal(harness.provider.closeCount, 1);
    assert.equal(harness.billing.finalizations.length, 1);
    assert.equal(harness.billing.finalizations[0].reason, 'client-disconnect');
  });

  it('does not touch billing when an unauthenticated socket times out', async () => {
    const harness = createHarness({ authTimeoutMs: 20 });
    await new Promise((resolve) => setTimeout(resolve, 130));
    await harness.connection.whenIdle();

    assert.equal(harness.billing.finalizations.length, 0);
    assert.equal(harness.transport.messages.some(
      (message) => message.type === 'error' && message.code === 'LIVE_GATEWAY_AUTH',
    ), true);
    assert.equal(harness.transport.closeCalls.length, 1);
  });
});

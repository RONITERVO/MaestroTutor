// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createManagedGeminiClient, type ManagedGatewaySocket } from './managedGeminiClient';
import { createLiveOpenReason, LIVE_OPEN_TRIGGER } from '../../shared/liveOpenReason';
afterEach(() => vi.useRealTimers());
function setup(timeout?: number) {
  vi.useFakeTimers();
  const handlers = new Map<string, Set<(event: any) => void>>();
  const socket: ManagedGatewaySocket = { readyState: 1, send: vi.fn(), close: vi.fn(),
    addEventListener: (type, handler) => { if (!handlers.has(type)) handlers.set(type, new Set()); handlers.get(type)!.add(handler); },
    removeEventListener: (type, handler) => { handlers.get(type)?.delete(handler); },
  };
  const acceptBilling = vi.fn();
  const client = createManagedGeminiClient({ acceptLiveGatewayBillingSummary: acceptBilling, createLiveGatewayTicket: async () => ({ gatewayUrl: 'wss://gateway.example.com', ticket: 'test-ticket' }) } as any,
    { createGatewaySocket: () => socket, gatewayConnectTimeoutMs: timeout });
  const callbacks = { onopen: vi.fn(), onmessage: vi.fn(), onclose: vi.fn() };
  const result = client.live.connect({ model: 'test-live', callbacks,
    liveOpenReason: createLiveOpenReason(LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE, { requestId: 'connect-test' }),
  }).catch(error => error);
  const emit = (type: string, data?: unknown) => handlers.get(type)?.forEach(handler => handler(data));
  return { socket, handlers, callbacks, result, emit, acceptBilling };
}
describe('managed gateway connection lifecycle', () => {
  it('allows bounded scale-from-zero startup before provider readiness', async () => {
    const f = setup(); await vi.advanceTimersByTimeAsync(29_000);
    expect(f.socket.close).not.toHaveBeenCalled();
    f.emit('open'); expect(f.socket.send).toHaveBeenCalledWith(JSON.stringify({ type: 'authenticate', ticket: 'test-ticket' }));
    await vi.advanceTimersByTimeAsync(15_000);
    f.emit('message', { data: JSON.stringify({ type: 'ready', sessionId: 'native', deadlineAt: Date.now() + 60_000 }) });
    const session = await f.result;
    expect(f.callbacks.onopen).toHaveBeenCalledOnce();
    expect(typeof session.sendRealtimeInput).toBe('function');
    session.close(); f.emit('close');
    expect(vi.getTimerCount()).toBe(0);
  });
  it('removes listeners on timeout and ignores already queued late readiness', async () => {
    const f = setup(1_000); await vi.advanceTimersByTimeAsync(0);
    const late = [...f.handlers.get('message')!][0];
    await vi.advanceTimersByTimeAsync(1_000);
    expect((await f.result).code).toBe('LIVE_GATEWAY_TIMEOUT');
    expect([...f.handlers.values()].every(set => set.size === 0)).toBe(true);
    late({ data: JSON.stringify({ type: 'ready', sessionId: 'late', deadlineAt: Date.now() + 60_000 }) });
    await vi.advanceTimersByTimeAsync(0);
    expect(f.callbacks.onopen).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });
  it('drains final provider data and billing before notifying close on a ready session', async () => {
    const f = setup(); await vi.advanceTimersByTimeAsync(0);
    f.emit('message', { data: JSON.stringify({ type: 'ready', sessionId: 'ready', deadlineAt: Date.now() + 60_000 }) });
    await f.result;
    const order: string[] = [];
    f.callbacks.onmessage.mockImplementation(() => { order.push('message'); });
    f.acceptBilling.mockImplementation(() => { order.push('billing'); });
    f.callbacks.onclose.mockImplementation(() => { order.push('close'); });
    f.emit('message', { data: JSON.stringify({ type: 'providerMessage', message: { serverContent: { turnComplete: true } } }) });
    f.emit('message', { data: JSON.stringify({ type: 'billing', billingSummary: { reservedCredits: 0 } }) });
    f.emit('close');
    await vi.advanceTimersByTimeAsync(0);
    expect(order).toEqual(['message', 'billing', 'close']);
    expect(f.acceptBilling).toHaveBeenCalledWith({ reservedCredits: 0 });
    expect(vi.getTimerCount()).toBe(0);
  });
  it('cleans up a socket closed before readiness without waiting for the deadline', async () => {
    const f = setup(); await vi.advanceTimersByTimeAsync(0);
    f.emit('close');
    expect((await f.result).message).toContain('closed before it was ready');
    expect([...f.handlers.values()].every(set => set.size === 0)).toBe(true);
    expect(vi.getTimerCount()).toBe(0);
  });
});

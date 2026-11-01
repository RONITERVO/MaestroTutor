// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { ROOM_LIVE_EXECUTE, ROOM_LIVE_OBSERVE, ROOM_LIVE_TOOLS } from './prompts/room';
import {
  LIVE_ROOM_MAX_CALLS, LIVE_ROOM_MAX_CALL_BYTES,
  LIVE_ROOM_MAX_RESPONSE_BYTES, LIVE_ROOM_MAX_TOTAL_RESPONSE_BYTES,
} from './liveGatewayProtocol';

const record = (v: unknown): v is Record<string, unknown> => v !== null && typeof v === 'object' && !Array.isArray(v);
export const roomLiveJsonBytes = (value: unknown): number => new TextEncoder().encode(JSON.stringify(value)).byteLength;
const canonical = (v: unknown): string => {
  if (Array.isArray(v)) return '[' + v.map(canonical).join(',') + ']';
  if (record(v)) return '{' + Object.keys(v).sort().map(k => JSON.stringify(k) + ':' + canonical(v[k])).join(',') + '}';
  return JSON.stringify(v);
};
/** A ticket pins definitions, descriptions, names and synchronous behaviour. */
export const isRoomLiveTools = (value: unknown): boolean => canonical(value) === canonical(ROOM_LIVE_TOOLS);
const id = (value: unknown): value is string => typeof value === 'string' && value.length > 0 && value.length <= 256 && !/[\u0000-\u001f\u007f]/.test(value);
const name = (value: unknown): value is string => value === ROOM_LIVE_OBSERVE || value === ROOM_LIVE_EXECUTE;
const only = (value: Record<string, unknown>, fields: string[]) => Object.keys(value).every(key => fields.includes(key));
export interface RoomLiveCall { id: string; name: string; args: Record<string, unknown> }
export interface RoomLiveResponse { id: string; name: string; response: Record<string, unknown> }
interface CallState { call: RoomLiveCall; signature: string; state: 'pending' | 'answered' | 'cancelled' }

/** Per-socket protocol ownership, shared by managed transport and future clients.
 * This validates envelopes, not native permissions/commands. Nothing here executes
 * a room action. Repeated provider calls are suppressed, never re-executed. */
export class RoomLiveProtocol {
  private calls = new Map<string, CallState>();
  private cancelled = new Set<string>();
  private sealed = false;
  private responseBytes = 0;
  constructor(private readonly enabled: boolean) {}

  /** Run at provider callback arrival, before any asynchronous queue wait. */
  receiveProvider(input: unknown): unknown {
    if (!record(input)) return input;
    if (input.serverContent && record(input.serverContent)
      && (input.serverContent.interrupted || input.serverContent.turnComplete)) this.seal();
    if (input.toolCallCancellation !== undefined) {
      const cancellation = input.toolCallCancellation;
      if (!record(cancellation) || !only(cancellation,['ids']) || !Array.isArray(cancellation.ids)
        || cancellation.ids.length > LIVE_ROOM_MAX_CALLS || !cancellation.ids.every(id)) throw new Error('Invalid Live room tool cancellation.');
      for (const value of cancellation.ids) {
        // A cancellation can overtake a queued call. Bound unknown tombstones too.
        if (!this.cancelled.has(value) && this.cancelled.size >= LIVE_ROOM_MAX_CALLS) throw new Error('Too many Live room tool cancellations.');
        this.cancelled.add(value);
        const call = this.calls.get(value);
        if (call?.state === 'pending') call.state = 'cancelled';
      }
    }
    if (input.toolCall === undefined) return input;
    if (!this.enabled) throw new Error('Live room tools were not enabled by the ticket.');
    const tool = input.toolCall;
    if (!record(tool) || !only(tool,['functionCalls']) || !Array.isArray(tool.functionCalls)
      || !tool.functionCalls.length || tool.functionCalls.length > LIVE_ROOM_MAX_CALLS
      || roomLiveJsonBytes(tool) > LIVE_ROOM_MAX_CALL_BYTES) throw new Error('Invalid or oversized Live room tool call.');
    const next: CallState[] = [];
    const batch = new Map<string,string>();
    for (const value of tool.functionCalls) {
      if (!record(value) || !only(value,['id','name','args','willContinue']) || (value.willContinue !== undefined && value.willContinue !== false) || !id(value.id) || !name(value.name)
        || (value.args !== undefined && !record(value.args))) throw new Error('Invalid Live room tool call.');
      const call = {id:value.id,name:value.name,args:(value.args ?? {}) as Record<string,unknown>};
      const signature = canonical(call);
      const previous = this.calls.get(call.id)?.signature ?? batch.get(call.id);
      if (previous !== undefined && previous !== signature) throw new Error('Live room tool call ID was reused with different arguments.');
      if (previous !== undefined) continue;
      batch.set(call.id,signature);
      next.push({call,signature,state:this.sealed || this.cancelled.has(call.id) ? 'cancelled' : 'pending'});
    }
    if (this.calls.size + next.length > LIVE_ROOM_MAX_CALLS) throw new Error('Live room tool call limit reached.');
    next.forEach(call => this.calls.set(call.call.id,call));
    const functionCalls = next.filter(call => call.state === 'pending').map(call => call.call);
    const result = {...input};
    if (functionCalls.length) result.toolCall = {functionCalls};
    else delete result.toolCall;
    return result;
  }

  /** Validate the whole response batch before consuming any pending ID. A late
   * reply to a cancelled call is discarded, including after turn completion. */
  consumeResponses(input: unknown): {functionResponses: RoomLiveResponse[]} {
    if (!this.enabled) throw new Error('Managed Live tool responses are not supported for this ticket.');
    if (!record(input) || !only(input,['functionResponses']) || !Array.isArray(input.functionResponses)
      || !input.functionResponses.length || input.functionResponses.length > LIVE_ROOM_MAX_CALLS
      || roomLiveJsonBytes(input) > LIVE_ROOM_MAX_RESPONSE_BYTES) throw new Error('Invalid or oversized Live room tool response.');
    const seen = new Set<string>();
    const responses: RoomLiveResponse[] = [];
    for (const value of input.functionResponses) {
      if (!record(value) || !only(value,['id','name','response']) || !id(value.id) || !name(value.name)
        || !record(value.response) || seen.has(value.id)) throw new Error('Invalid Live room tool response.');
      seen.add(value.id);
      const call = this.calls.get(value.id);
      if (!call || call.call.name !== value.name || call.state === 'answered') throw new Error('Live room tool response has no matching pending call.');
      if (call.state === 'cancelled') continue;
      responses.push(value as unknown as RoomLiveResponse);
    }
    const bytes = responses.length ? roomLiveJsonBytes({functionResponses:responses}) : 0;
    if (this.responseBytes + bytes > LIVE_ROOM_MAX_TOTAL_RESPONSE_BYTES) throw new Error('Live room tool response budget exhausted.');
    this.responseBytes += bytes;
    responses.forEach(response => { this.calls.get(response.id)!.state = 'answered'; });
    return {functionResponses:responses};
  }

  /** Recheck after persistence/queue waits: cancelled work must not be offered
   * to a client merely because it was valid when the provider sent it. */
  pendingMessage(input: unknown): unknown {
    if (!record(input) || !record(input.toolCall) || !Array.isArray(input.toolCall.functionCalls)) return input;
    const functionCalls = input.toolCall.functionCalls.filter(call => record(call) && typeof call.id === 'string'
      && this.calls.get(call.id)?.state === 'pending');
    const result = {...input};
    if (functionCalls.length) result.toolCall = {functionCalls};
    else delete result.toolCall;
    return result;
  }

  seal(): void {
    this.sealed = true;
    for (const call of this.calls.values()) if (call.state === 'pending') call.state = 'cancelled';
  }
}

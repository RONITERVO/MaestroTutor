// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { ROOM_LIVE_TOOLS, ROOM_LIVE_OBSERVE, ROOM_LIVE_EXECUTE } from './prompts/room';
import { LIVE_ROOM_MAX_CALLS, LIVE_ROOM_MAX_CALL_BYTES, LIVE_ROOM_MAX_RESPONSE_BYTES } from './liveGatewayProtocol';
import { isRoomLiveTools, RoomLiveProtocol, roomLiveJsonBytes } from './roomLiveProtocol';
const call = (id = 'call-1', name = ROOM_LIVE_OBSERVE, args = {}) => ({toolCall:{functionCalls:[{id,name,args}]}});
const reply = (id = 'call-1', name = ROOM_LIVE_OBSERVE, response = {}) => ({functionResponses:[{id,name,response}]});

describe('closed room Live protocol', () => {
  it('pins exact declarations while permitting JSON key order differences', () => {
    expect(isRoomLiveTools(JSON.parse(JSON.stringify(ROOM_LIVE_TOOLS)))).toBe(true);
    const reversed = ROOM_LIVE_TOOLS.map(tool => ({functionDeclarations:tool.functionDeclarations.map(fn => Object.fromEntries(Object.entries(fn).reverse()))}));
    expect(isRoomLiveTools(reversed)).toBe(true);
    for (const tools of [[],[{googleSearch:{}}],[{functionDeclarations:[{name:ROOM_LIVE_OBSERVE}]}],
      [...ROOM_LIVE_TOOLS,{codeExecution:{}}], ROOM_LIVE_TOOLS.map(tool => ({...tool,extra:true}))]) expect(isRoomLiveTools(tools)).toBe(false);
  });
  it('rejects unsolicited calls and responses when tools are disabled', () => {
    const protocol = new RoomLiveProtocol(false);
    expect(() => protocol.receiveProvider(call())).toThrow(/enabled/);
    expect(() => protocol.consumeResponses(reply())).toThrow(/not supported/);
  });
  it('matches names and IDs, consumes a response once and ignores identical provider retransmissions', () => {
    const protocol = new RoomLiveProtocol(true);
    expect(protocol.receiveProvider(call())).toEqual(call());
    expect(protocol.receiveProvider(call())).toEqual({});
    expect(() => protocol.consumeResponses(reply('unknown'))).toThrow(/matching/);
    expect(() => protocol.consumeResponses(reply('call-1',ROOM_LIVE_EXECUTE))).toThrow(/matching/);
    expect(protocol.consumeResponses(reply())).toEqual(reply());
    expect(() => protocol.consumeResponses(reply())).toThrow(/matching/);
    expect(protocol.receiveProvider(call())).toEqual({});
    expect(() => protocol.receiveProvider(call('call-1',ROOM_LIVE_OBSERVE,{changed:true}))).toThrow(/reused/);
  });
  it('rejects unsupported tools, ambiguous streaming calls and media responses', () => {
    const protocol = new RoomLiveProtocol(true);
    for (const message of [call('x','runCode'),{toolCall:{functionCalls:[{id:'x',name:ROOM_LIVE_OBSERVE,willContinue:true}]}},
      {toolCall:{functionCalls:[{name:ROOM_LIVE_OBSERVE}]}},call('x',ROOM_LIVE_OBSERVE,[]),{toolCall:{functionCalls:[]}}]) {
      expect(() => protocol.receiveProvider(message)).toThrow(/Invalid/);
    }
    protocol.receiveProvider(call());
    expect(() => protocol.consumeResponses({functionResponses:[{...reply().functionResponses[0],parts:[]}]})).toThrow(/Invalid/);
    expect(protocol.consumeResponses(reply())).toEqual(reply());
  });
  it('accepts a complete non-streaming call with an opaque provider ID', () => {
    const protocol = new RoomLiveProtocol(true);
    const message = call('opaque/provider+id=');
    expect(protocol.receiveProvider({toolCall:{functionCalls:[{...message.toolCall.functionCalls[0],willContinue:false}]}})).toEqual(message);
    expect(protocol.consumeResponses(reply('opaque/provider+id='))).toEqual(reply('opaque/provider+id='));
  });
  it('validates entire batches before consuming any ID', () => {
    const protocol = new RoomLiveProtocol(true);
    protocol.receiveProvider(call());
    expect(() => protocol.consumeResponses({functionResponses:[...reply().functionResponses,...reply('missing').functionResponses]})).toThrow();
    expect(() => protocol.consumeResponses({functionResponses:[...reply().functionResponses,...reply().functionResponses]})).toThrow();
    expect(protocol.consumeResponses(reply())).toEqual(reply());
  });
  it('bounds unique calls across messages, including cancelled calls', () => {
    const protocol = new RoomLiveProtocol(true);
    for (let i=0;i<LIVE_ROOM_MAX_CALLS;i++) {
      protocol.receiveProvider(call(String(i)));
      protocol.receiveProvider({toolCallCancellation:{ids:[String(i)]}});
    }
    expect(() => protocol.receiveProvider(call('extra'))).toThrow(/limit/);
  });
  it('applies UTF-8 envelope and total response bounds without consuming rejected responses', () => {
    const protocol = new RoomLiveProtocol(true);
    expect(() => protocol.receiveProvider(call('large',ROOM_LIVE_OBSERVE,{text:'界'.repeat(LIVE_ROOM_MAX_CALL_BYTES/3)}))).toThrow(/oversized/);
    protocol.receiveProvider(call());
    expect(() => protocol.consumeResponses(reply('call-1',ROOM_LIVE_OBSERVE,{text:'界'.repeat(LIVE_ROOM_MAX_RESPONSE_BYTES/3)}))).toThrow(/oversized/);
    const big = {text:'x'.repeat(70*1024)};
    expect(roomLiveJsonBytes(reply('call-1',ROOM_LIVE_OBSERVE,big))).toBeLessThan(LIVE_ROOM_MAX_RESPONSE_BYTES);
    protocol.consumeResponses(reply('call-1',ROOM_LIVE_OBSERVE,big));
    protocol.receiveProvider(call('call-2'));
    protocol.consumeResponses(reply('call-2',ROOM_LIVE_OBSERVE,big));
    protocol.receiveProvider(call('call-3'));
    expect(() => protocol.consumeResponses(reply('call-3',ROOM_LIVE_OBSERVE,big))).toThrow(/budget/);
    expect(protocol.consumeResponses(reply('call-3'))).toEqual(reply('call-3'));
  });
  it('suppresses cancelled queued calls and drops late responses without an extra provider turn', () => {
    const protocol = new RoomLiveProtocol(true);
    const queued = protocol.receiveProvider(call());
    protocol.receiveProvider({toolCallCancellation:{ids:['call-1']}});
    expect(protocol.pendingMessage(queued)).toEqual({});
    expect(protocol.consumeResponses(reply())).toEqual({functionResponses:[]});
    protocol.receiveProvider({toolCallCancellation:{ids:['future']}});
    expect(protocol.receiveProvider(call('future'))).toEqual({});
  });
  it.each(['interrupted','turnComplete'])('seals pending and future actions on %s', reason => {
    const protocol = new RoomLiveProtocol(true);
    protocol.receiveProvider(call());
    protocol.receiveProvider({serverContent:{[reason]:true}});
    expect(protocol.consumeResponses(reply())).toEqual({functionResponses:[]});
    expect(protocol.receiveProvider(call('later'))).toEqual({});
  });
});

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect,vi,beforeEach} from 'vitest';
import {roomTaskProvider,roomReplyHasToolRequest} from './roomTaskProvider';
import {runTutorTextTurn} from '../chat/tutorTextTurn';
import {parseStrictTutorResponseText} from '../chat/tutorResponse';
import type {RoomAgentState} from './roomAgent';
vi.mock('../chat/tutorTextTurn',()=>({runTutorTextTurn:vi.fn()}));
const input={model:'test',prompt:'Ask the agent to paint it red.',nativeLanguageCode:'en',history:[{role:'user',text:'Earlier definition'}],systemInstruction:'Original tutor instruction',currentFileParts:[{fileUri:'test://attachment',mimeType:'image/png'}]};
const scene={version:1,session:'scene',sceneRevision:2,objects:[]} as unknown as RoomAgentState;
const result={receipts:[scene],scene,budgetExhausted:false};
const output=(text:string)=>({rawResponse:text,parsed:parseStrictTutorResponseText(text,'en'),response:{text,usageMetadata:{totalTokenCount:5}},operationId:'reply',searchQueryCount:0});
beforeEach(()=>vi.mocked(runTutorTextTurn).mockReset());
describe('shared room result reply',()=>{
 it('requests a report of observed work without reusing the original action as the current instruction',async()=>{
  const source=structuredClone(input),usage=vi.fn(),signal=new AbortController().signal;
  vi.mocked(runTutorTextTurn).mockResolvedValue(output('Listo.\n[EN] Done.') as never);
  const reply=await roomTaskProvider(()=>({} as never),usage).reply(input,result,signal);
  const [request,options]=vi.mocked(runTutorTextTurn).mock.calls[0];
  expect(request.prompt).not.toBe(input.prompt);expect(request.prompt).toContain(JSON.stringify({originalUserRequest:input.prompt}));
  expect(request.systemInstruction).toContain(JSON.stringify(scene));expect(request.history).toEqual(source.history);expect(request.currentFileParts).toEqual(source.currentFileParts);
  expect(options.signal).toBe(signal);expect(input).toEqual(source);expect(reply.rawResponse).toBe('Listo.\n[EN] Done.');expect(usage).toHaveBeenCalledOnce();
 });
 it('includes the exact acknowledged operations when reporting what changed',async()=>{
  const operations=[{commands:[{action:'delete' as const,target:'b'.repeat(32)}],receiptIndex:0}];
  vi.mocked(runTutorTextTurn).mockResolvedValue(output('Listo.\n[EN] Done.') as never);
  await roomTaskProvider(()=>({} as never),vi.fn()).reply(input,{...result,operations},new AbortController().signal);
  expect(vi.mocked(runTutorTextTurn).mock.calls[0][0].systemInstruction).toContain(JSON.stringify(operations));
 });
 it('corrects only result text once, accounts both calls and never publishes the new tool proposal',async()=>{
  const rejected='I will ask the agent.\n```maestro-tool\n{"tool":"agent"}\n```',usage=vi.fn();
  vi.mocked(runTutorTextTurn).mockResolvedValueOnce(output(rejected) as never).mockResolvedValueOnce(output('Está roja.\n[EN] It is red.') as never);
  const reply=await roomTaskProvider(()=>({} as never),usage).reply(input,result,new AbortController().signal);
  expect(vi.mocked(runTutorTextTurn)).toHaveBeenCalledTimes(2);expect(usage).toHaveBeenCalledTimes(2);
  expect(vi.mocked(runTutorTextTurn).mock.calls[1][0].prompt).toContain(JSON.stringify({rejectedReply:rejected}));expect(reply.rawResponse).not.toContain('maestro-tool');
  expect(roomReplyHasToolRequest('~~~maestro-tool\n{"tool":"image"}')).toBe(true);
 });
 it('refuses a second tool proposal within a fixed two-reply bound',async()=>{
  vi.mocked(runTutorTextTurn).mockResolvedValue(output('```maestro-tool invalid') as never);const usage=vi.fn();
  await expect(roomTaskProvider(()=>({} as never),usage).reply(input,result,new AbortController().signal)).rejects.toThrow(/were not repeated/);
  expect(vi.mocked(runTutorTextTurn)).toHaveBeenCalledTimes(2);expect(usage).toHaveBeenCalledTimes(2);
 });
 it('does not correct transport errors or continue after cancellation',async()=>{
  vi.mocked(runTutorTextTurn).mockRejectedValueOnce(new Error('network failed'));const provider=roomTaskProvider(()=>({} as never),vi.fn());
  await expect(provider.reply(input,result,new AbortController().signal)).rejects.toThrow('network failed');expect(vi.mocked(runTutorTextTurn)).toHaveBeenCalledOnce();
  vi.mocked(runTutorTextTurn).mockClear();const controller=new AbortController();
  vi.mocked(runTutorTextTurn).mockImplementationOnce(async()=>{controller.abort();return output('```maestro-tool {}') as never;});
  await expect(provider.reply(input,result,controller.signal)).rejects.toThrow();expect(vi.mocked(runTutorTextTurn)).toHaveBeenCalledOnce();
 });
});

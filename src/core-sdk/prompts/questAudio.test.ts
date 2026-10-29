// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {afterEach,describe,expect,it,vi} from 'vitest';
import {composeMaestroSystemInstruction,QUEST_AUDIO_CONTEXT,QUEST_AUDIO_GUIDE,QUEST_AUDIO_SETUP,withQuestAudioContext} from '../../core/config/prompts';
import {isNativeQuestBook} from '../../platform/quest/questIntegrityBridge';
import {generateGeminiResponse} from '../gemini/generative';
import {createManagedGeminiClient,type CoreGeminiClient} from '../managedGeminiClient';
import {buildCoreLiveSystemInstruction} from '../chat/liveContext';
import {runSyntheticLiveJourney} from '../media/syntheticLiveJourney';
import {createSyntheticPcmSource} from '../media/pcmInput';
import {createCoreRuntime} from '../runtime';
import {LIVE_OPEN_TRIGGER} from '../../../shared/liveOpenReason';

vi.mock('../diagnostics',()=>({debugLogService:{logRequest:()=>({complete(){},error(){}})}}));
vi.mock('../../shared/utils/costTracker',()=>({trackGeminiUsage(){}}));
afterEach(()=>vi.unstubAllGlobals());

describe('Quest audio context',()=>{
 it('preserves an absent tutor context instead of creating a headphone-only Live instruction',()=>{
  expect(withQuestAudioContext('',true)).toBe('');
  expect(buildCoreLiveSystemInstruction({basePrompt:withQuestAudioContext('',true),messages:[]})).toBe('');
 });
 it('shares the welcome wording with help and context without turning it into a connection observation',()=>{
  for(const text of [QUEST_AUDIO_SETUP.requirement,QUEST_AUDIO_SETUP.setup,QUEST_AUDIO_SETUP.detection]){
   expect(QUEST_AUDIO_GUIDE).toContain(text);expect(QUEST_AUDIO_CONTEXT).toContain(text);
  }
  expect(QUEST_AUDIO_GUIDE).toContain('Reading this guide does not start audio');
  expect(QUEST_AUDIO_CONTEXT).toContain('never a detected connection');
  expect(QUEST_AUDIO_CONTEXT).toContain('not repeat it in unrelated language practice');
 });
 it('adds context only for the local top-level Quest book; ordinary browser, embedded and headless contexts are unchanged',()=>{
  const base='Original tutor context.';
  vi.stubGlobal('window',undefined);expect(withQuestAudioContext(base,isNativeQuestBook())).toBe(base);
  const target={location:{origin:'https://appassets.androidplatform.net',search:'?surface=quest-book'},top:null as unknown};
  target.top=target;vi.stubGlobal('window',target);
  const enriched=withQuestAudioContext(base,isNativeQuestBook());
  expect(enriched).toBe(base+'\n\n'+QUEST_AUDIO_CONTEXT);
  expect(withQuestAudioContext(enriched,true)).toBe(enriched);
  expect(withQuestAudioContext(enriched,false)).toBe(enriched);
  target.top={};expect(withQuestAudioContext(base,isNativeQuestBook())).toBe(base);target.top=target;
  for(const origin of ['https://chatwithmaestro.com','http://localhost:5173','https://appassets.androidplatform.net.evil.test']){
   target.location.origin=origin;expect(withQuestAudioContext(base,isNativeQuestBook())).toBe(base);
  }
  target.location.origin='https://appassets.androidplatform.net';target.location.search='';
  expect(withQuestAudioContext(base,isNativeQuestBook())).toBe(base);
 });
 it.each(['byok','managed'] as const)('changes only the text system context at the %s provider boundary',async mode=>{
  const send=vi.fn(async(_request:any)=>(async function*(){yield{text:'Hola.\n[EN] Hello.'};})());
  const aiClient=mode==='managed'?createManagedGeminiClient({generateContentStream:send} as any):{models:{generateContentStream:send}} as unknown as CoreGeminiClient;
  for(const nativeQuest of [false,true]){
   const base=withQuestAudioContext('Original tutor context.',nativeQuest);
   await generateGeminiResponse('audio-context-test','How do I set up audio?',[],{
    aiClient,systemInstruction:composeMaestroSystemInstruction(base),useGoogleSearch:false,
   });
  }
  const before=send.mock.calls[0][0],after=send.mock.calls[1][0];
  expect(before.config.systemInstruction).not.toContain(QUEST_AUDIO_CONTEXT);
  expect(after.config.systemInstruction).toBe(composeMaestroSystemInstruction(withQuestAudioContext('Original tutor context.',true)));
  const wire=(request:any)=>({...request,config:{...request.config,...(request.config.abortSignal?{abortSignal:{aborted:request.config.abortSignal.aborted}}:{})}});
  expect(wire({...after,config:{...after.config,systemInstruction:before.config.systemInstruction}})).toEqual(wire(before));
 });
 it.each([false,true])('preserves the shared Live request with explicit native Quest context=%s',async nativeQuest=>{
  const stop=new Error('Captured local Live request');
  const connect=vi.fn(async(_request:any)=>{throw stop;});
  const ai={live:{connect}} as unknown as CoreGeminiClient;
  const base=withQuestAudioContext('Original tutor context.',nativeQuest);
  const instruction=buildCoreLiveSystemInstruction({basePrompt:base,messages:[{id:'u',role:'user',text:'Hola',timestamp:1}]});
  const runtime=createCoreRuntime({clock:{now:()=>1000,sleep:async()=>{},setInterval:()=>0,clearInterval(){}}});
  await expect(runSyntheticLiveJourney(ai,{
   liveOpenTrigger:LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
   source:createSyntheticPcmSource({pcm:new Int16Array([1,2]),sampleRate:16000,pace:false}),
   gateInputOnSpeech:false,systemInstruction:instruction,
  },{runtime})).rejects.toThrow(stop.message);
  expect(connect).toHaveBeenCalledOnce();
  const request=connect.mock.calls[0][0];
  expect(request.config.systemInstruction).toBe(instruction);
  expect(instruction).toContain('User: Hola');
  expect(instruction.includes(QUEST_AUDIO_CONTEXT)).toBe(nativeQuest);
  expect(request.config.realtimeInputConfig.activityHandling).toBe('NO_INTERRUPTION');
 });
});

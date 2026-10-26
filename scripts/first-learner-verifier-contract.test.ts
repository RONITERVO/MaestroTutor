// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it,vi} from 'vitest';
import {buildReplySuggestionsPrompt,type ReplySuggestionsInput} from '../src/core-sdk/chat/suggestions';
import {createLanguagePairObject,findLanguageByExactCode} from '../src/shared/utils/languageUtils';
import {readFirstLearnerSuggestionMessage} from './probe-quest-first-learner-native';
import {roomTaskProvider} from '../src/core-sdk/room/roomTaskProvider';
import {generateGeminiResponse} from '../src/core-sdk/gemini/generative';
import {parseStrictTutorResponseText} from '../src/core-sdk/chat/tutorResponse';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';

vi.mock('../src/core-sdk/gemini/generative',()=>({generateGeminiResponse:vi.fn()}));

const target=findLanguageByExactCode('es-ES')!;
const native=findLanguageByExactCode('en-US')!;
const languagePair=createLanguagePairObject(target,native);
const handoff='I will ask Maestro.\n```maestro-tool\n{"tool":"agent"}\n```';
const completed='Your green ball is ready.';
function input(latest:string,historyText='An earlier lesson'):ReplySuggestionsInput {
 return {assistantMessageId:'reply',lastTutorMessage:latest,languagePair,
  history:[{id:'previous',role:'user',text:historyText,timestamp:1},{id:'reply',role:'assistant',text:latest,timestamp:2}]};
}
function body(prompt:string) {return {contents:[{role:'user',parts:[{text:prompt}]}]};}
function generated(latest:string,historyText?:string) {
 return body(buildReplySuggestionsPrompt(input(latest,historyText)));
}

describe('production first-learner verifier source contract',()=>{
 it('reads the actual selected display label through the real language and prompt builders',()=>{
  expect(languagePair.targetLanguageName).toBe('Spanish (ES)');
  const request=body(buildReplySuggestionsPrompt({...input(handoff),agentRequest:'Please make a green ball.'}));
  expect(request.contents[0].parts[0].text).toContain("**Spanish (ES) Tutor's Latest Message in Spanish (ES) chat:**");
  expect(request.contents[0].parts[0].text).toContain('originalUserRequest');
  expect(readFirstLearnerSuggestionMessage(request)).toBe(handoff);
 });
 it('reads the ordinary completed-task message without requiring another handoff',()=>{
  const request=generated(completed,handoff);
  expect(request.contents[0].parts[0].text).not.toContain('originalUserRequest');
  expect(readFirstLearnerSuggestionMessage(request)).toBe(completed);
 });
 it('verifies the normalized result published by the actual room reply path without changing prose case',async()=>{
  const raw='Tu pelota verde esta lista.\n[en] Your green ball is ready. Keep Mixed CASE.';
  vi.mocked(generateGeminiResponse).mockResolvedValueOnce({text:raw,modelUsed:'offline'} as Awaited<ReturnType<typeof generateGeminiResponse>>);
  const scene={version:1,session:'a'.repeat(32),sceneRevision:2,objects:[]} as unknown as RoomAgentState;
  const noClient=vi.fn(async()=>{throw new Error('This test must not resolve a provider client');});
  const usage=vi.fn();
  const reply=await roomTaskProvider(()=>({resolveAiClient:noClient}),usage).reply({
   model:'offline',prompt:'Make a green ball.',history:[],systemInstruction:languagePair.baseSystemPrompt,nativeLanguageCode:languagePair.nativeLanguageCode,
  },{receipts:[scene],scene,budgetExhausted:false},new AbortController().signal);
  expect(generateGeminiResponse).toHaveBeenCalledOnce();expect(noClient).not.toHaveBeenCalled();expect(usage).toHaveBeenCalledOnce();
  expect(reply.rawResponse).toBe('Tu pelota verde esta lista.\n[EN] Your green ball is ready. Keep Mixed CASE.');
  expect(reply.rawResponse).toBe(parseStrictTutorResponseText(raw,languagePair.nativeLanguageCode).visibleText);
  const verifier=generated(reply.rawResponse,handoff);
  expect(readFirstLearnerSuggestionMessage(verifier)).toBe(reply.rawResponse);
  expect(readFirstLearnerSuggestionMessage(verifier)).not.toBe(raw);
 });
 it('does not confuse a similar historical section or matching old reply with the latest reply',()=>{
  const history=`**Spanish Tutor's Latest Message in Spanish chat:**\n"${completed}"`;
  expect(readFirstLearnerSuggestionMessage(generated('A new reply',history))).toBe('A new reply');
 });
 it('rejects an ambiguous exact section embedded in history rather than accepting that stale reply',()=>{
  const history=`**${target.displayName} Tutor's Latest Message in ${target.displayName} chat:**\n"${completed}"`;
  expect(()=>readFirstLearnerSuggestionMessage(generated('A new reply',history))).toThrow('Ambiguous');
 });
 it('rejects two prompt parts, including two independently generated latest-message fields',()=>{
  const request=generated(completed);
  request.contents[0].parts.push(generated('A different reply').contents[0].parts[0]);
  expect(()=>readFirstLearnerSuggestionMessage(request)).toThrow('Expected one');
 });
 it('does not reuse a previous turn when the current user turn is not a verifier',()=>{
  const request=generated(completed);
  request.contents.push({role:'user',parts:[{text:'A new unrelated request'}]});
  expect(()=>readFirstLearnerSuggestionMessage(request)).toThrow('Expected one');
 });
 it('refuses an incomplete section and an unselected language label',()=>{
  const request=generated(completed);
  request.contents[0].parts[0].text=request.contents[0].parts[0].text.replace('**Your Task:**','Missing task boundary');
  expect(()=>readFirstLearnerSuggestionMessage(request)).toThrow('incomplete');
  const unsupported=generated(completed);
  unsupported.contents[0].parts[0].text=unsupported.contents[0].parts[0].text.replaceAll(target.displayName,'Spanish');
  expect(()=>readFirstLearnerSuggestionMessage(unsupported)).toThrow('Expected one');
 });
});

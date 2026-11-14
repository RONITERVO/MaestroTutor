// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it} from 'vitest';
import {buildReplySuggestionsPrompt,type ReplySuggestionsInput} from '../src/core-sdk/chat/suggestions';
import {parseStrictTutorResponseText} from '../src/core-sdk/chat/tutorResponse';
import {createLanguagePairObject,findLanguageByExactCode} from '../src/shared/utils/languageUtils';
import {LIVE_REPLY,LIVE_REQUEST} from './first-learner-live.mjs';
import {DELETE_RESULT} from './probe-quest-first-learner-live';
import {readFirstLearnerSuggestionMessage} from './probe-quest-first-learner-native';

const languagePair=createLanguagePairObject(findLanguageByExactCode('es-ES')!,findLanguageByExactCode('en-US')!);
function prompt(latest:string,previous:string,handoff=false) {
 const input:ReplySuggestionsInput={assistantMessageId:'current',lastTutorMessage:latest,languagePair,
  ...(handoff?{responseSource:'live' as const,agentRequest:LIVE_REQUEST}:{}),
  history:[{id:'previous',role:'assistant',text:previous,timestamp:1},{id:'current',role:'assistant',text:latest,timestamp:2}]};
 return {contents:[{role:'user',parts:[{text:buildReplySuggestionsPrompt(input)}]}]};
}

describe('production Live continuity verifier prompt contracts',()=>{
 it('reads the actual raw bilingual Live reply passed by the Live callback',()=>{
  expect(LIVE_REPLY).toContain('[en]');
  const request=prompt(LIVE_REPLY,'Our tree and separate apple are ready.',true);
  expect(request.contents[0].parts[0].text).toContain("**Spanish (ES) Tutor's Latest Message in Spanish (ES) chat:**");
  expect(request.contents[0].parts[0].text).toContain(JSON.stringify({originalUserTranscript:LIVE_REQUEST}));
  expect(request.contents[0].parts[0].text).not.toContain('originalUserRequest');
  expect(readFirstLearnerSuggestionMessage(request)).toBe(LIVE_REPLY);
 });
 it('reads the actual delete result after room-reply normalization, not the preceding Live reply',()=>{
  const normalized=parseStrictTutorResponseText(DELETE_RESULT,languagePair.nativeLanguageCode).visibleText;
  expect(normalized).toBe(DELETE_RESULT.replace('[en]','[EN]'));
  const request=prompt(normalized,LIVE_REPLY);
  expect(readFirstLearnerSuggestionMessage(request)).toBe(normalized);
  expect(readFirstLearnerSuggestionMessage(request)).not.toBe(LIVE_REPLY);
  expect(readFirstLearnerSuggestionMessage(request)).not.toBe(DELETE_RESULT);
 });
 it('rejects a new unrelated turn instead of accepting a historical Live verifier',()=>{
  const request=prompt(LIVE_REPLY,'Our tree and separate apple are ready.',true);
  request.contents.push({role:'user',parts:[{text:'How do I say apple in Spanish?'}]});
  expect(()=>readFirstLearnerSuggestionMessage(request)).toThrow('Expected one');
 });
});

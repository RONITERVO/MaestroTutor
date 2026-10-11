// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real book, chat coordinator, task journal and native wire. The runner selects offline or real provider access.
import {useRef} from 'react';
import {createRoot} from 'react-dom/client';
import {QuestBookSurface} from '../../src/platform/quest/QuestBookSurface';
import {ChatInterface,useTutorConversation,getChatHistoryDB} from '../../src/features/chat';
import {useMaestroStore,initialSettings} from '../../src/store';
// Read-only test evidence; the internal task journal is not a production feature API.
// eslint-disable-next-line no-restricted-imports
import {roomTaskStore} from '../../src/features/chat/services/roomTaskStore';
import type {ManagedAccessSession} from '../../src/core/contracts/backend';
import {saveManagedAccessSession} from '../../src/core/security/managedAccessSessionStorage';
import {firebaseAuthBridgeService} from '../../src/services/auth/firebaseAuthBridgeService';
import {maestroFirebaseService} from '../../src/services/firebase/maestroFirebaseService';
// eslint-disable-next-line no-restricted-imports
import {roomAgentTasks} from '../../src/features/chat/services/roomAgentTasks';
import {setApiKey} from '../../src/core/security/apiKeyStorage';
import {selectIsAgentWorking,selectIsSending} from '../../src/store/slices/uiSlice';
import {enTranslations} from '../../src/core/i18n/en';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import type {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import '../../src/app/index.css';
declare global {interface Window {
 nativeBookCredentials?:()=>Promise<{mode:'byok';apiKey:string}|{mode:'managed';session:ManagedAccessSession;appCheckToken:string}>;
 nativeBookStop?:()=>void;
 nativeBookChatImage?:()=>string;
 maestroNativeExchange?:(snapshot:ReturnType<RoomAgentClient['snapshot']>)=>Promise<{state?:unknown;capture?:unknown}>;
 nativeBookEvidence?:()=>{state:RoomAgentState|null;errors:string[];messages:ReturnType<typeof useMaestroStore.getState>['messages'];agentWorking:boolean;inputBlocked:boolean};
 nativeBookTask?:(id:string)=>ReturnType<typeof roomTaskStore.get>;
}}
if(!import.meta.env.DEV||!window.maestroNativeExchange)throw new Error('Launch this isolated fixture through Run-QuestRoomProbe -Journey Book.');
// Credential setup only: the real-provider runner supplies test credentials in
// memory to this owned top-level page. No provider/native responses are replaced.
// This does not test interactive sign-in, Quest integrity or device attestation.
if(window.nativeBookCredentials){
 const credentials=await window.nativeBookCredentials();
 if(credentials.mode==='byok')await setApiKey(credentials.apiKey);
 else {
  await setApiKey('');
  await saveManagedAccessSession(credentials.session);
  Object.assign(firebaseAuthBridgeService,{getCurrentIdentity:async()=>credentials.session});
  Object.assign(maestroFirebaseService,{getAppCheckToken:async()=>credentials.appCheckToken});
 }
}else await setApiKey('maestro-offline-fixture-not-a-real-api-key');
window.nativeBookStop=()=>roomAgentTasks.stopAll();
const store=useMaestroStore.getState();
const pair=store.languagePairs.find(p=>p.targetLanguageCode==='es-ES'&&p.nativeLanguageCode==='en-US');
if(!pair)throw new Error('Spanish/English pair missing');
useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:pair.id,sendWithSnapshotEnabled:false,enableGoogleSearch:false,imageFocusedModeEnabled:false},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false});
store.setMessages(await getChatHistoryDB(pair.id));
window.nativeBookChatImage=()=>{
 const canvas=document.createElement('canvas');canvas.width=64;canvas.height=64;const ctx=canvas.getContext('2d')!;
 ctx.fillStyle='#e74835';ctx.fillRect(0,0,64,64);ctx.fillStyle='#ffffff';ctx.fillRect(0,0,32,32);
 const dataUrl=canvas.toDataURL('image/png');
 store.addMessage({role:'assistant',text:'Synthetic chat texture fixture.',imageUrl:dataUrl,imageMimeType:'image/png',imageOrigin:'generated',attachmentName:'Chat checker.png'});
 return dataUrl;
};
const errors:string[]=[];let observed:RoomAgentState|null=null,busy=false;
window.nativeBookEvidence=()=>({state:observed,errors:[...errors],messages:useMaestroStore.getState().messages,agentWorking:selectIsAgentWorking(useMaestroStore.getState()),inputBlocked:selectIsSending(useMaestroStore.getState())});
window.nativeBookTask=roomTaskStore.get;
const timer=setInterval(()=>{if(busy||!window.maestroBook)return;busy=true;void(async()=>{
 try{
  const bridge=window.maestroBook!;const result=await window.maestroNativeExchange!(bridge.roomSnapshot());
  if(result.state){const state=result.state as RoomAgentState;
   if(bridge.roomState(state))observed=structuredClone(state);
   else if(!observed||state.session!==observed.session||state.revision>observed.revision)throw new Error('The actual book rejected a fresh native observation.');
  }
  if(result.capture)bridge.roomCapture(result.capture);
 }catch(error){errors.push(String(error));clearInterval(timer);window.maestroBook?.lifecycle(true);}
 finally{busy=false;}
})();},100);
const noop=()=>{};
function Conversation(){
 const refs=useRef({schedule:{current:noop},cancel:{current:noop},uri:{current:null as string|null},mime:{current:null as string|null}}).current;
 const bubbles=useRef(new Map<string,HTMLDivElement>());
 const conversation=useTutorConversation({t:key=>enTranslations[key as keyof typeof enTranslations]||key,setSettings:store.setSettings,addMessage:store.addMessage,updateMessage:store.updateMessage,setMessages:store.setMessages,
  getHistoryRespectingBookmark:messages=>messages,computeMaxMessagesForArray:()=>undefined,captureSnapshot:async()=>null,speakMessage:noop,isSpeechSynthesisSupported:false,stopListening:async()=>{},startListening:noop,clearTranscript:noop,hasPendingQueueItems:()=>false,claimRecordedUtterance:()=>null,
  scheduleReengagementRef:refs.schedule,cancelReengagementRef:refs.cancel,transcript:'',currentSystemPromptText:pair!.baseSystemPrompt,setReplySuggestions:store.setReplySuggestions,maestroAvatarUriRef:refs.uri,maestroAvatarMimeTypeRef:refs.mime});
 return <div className="flex flex-col min-h-screen"><ChatInterface onSendMessage={conversation.handleSendMessageInternal} onDeleteMessage={noop} onBookmarkAt={noop} onChangeMaxVisibleMessages={noop} bubbleWrapperRefs={bubbles}
  onSetAttachedImage={noop} onSttToggle={noop} speakText={noop} stopSpeaking={noop} onToggleSpeakNativeLang={noop} onUserInputActivity={noop} onToggleSendWithSnapshot={noop} onToggleUseVisualContextForReengagement={noop}
  onSuggestionClick={conversation.handleSuggestionInteraction} onToggleImageFocusedMode={noop} onStartLiveSession={noop} onStopLiveSession={noop} onStopSilentObserver={noop} onToggleSuggestionMode={noop} onCreateSuggestion={conversation.handleCreateSuggestion}/></div>;
}
createRoot(document.getElementById('root')!).render(<QuestBookSurface><Conversation/></QuestBookSurface>);

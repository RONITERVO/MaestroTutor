// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {CHAT_AUDIO_BYTES} from '../../../shared/chatAudio';
import {ChatMediaSource,type ChatMediaInput} from './chatMediaSource';
import type {ChatMessage} from '../../core/types';
const prefix=/^data:audio\/(wav|wave|x-wav);base64,/;
/** Only completed, local attachments from the visible conversation. No URL,
 * provider, microphone, TTS-cache or private Live recording is opened here. */
export function chatAudioSources(messages:readonly ChatMessage[],bookmark?:string|null):ChatMediaInput[] {
 const index=bookmark?messages.findIndex(m=>m.id===bookmark&&!m.thinking):-1;
 return (index>=0?messages.slice(index+1):messages).slice().reverse().filter(m=>!m.thinking&&!m.isGeneratingToolAttachment&&!m.toolAttachmentPhase).flatMap(m=>{
  const dataUrl=m.imageUrl?.startsWith('data:audio/')?m.imageUrl:m.storageOptimizedImageUrl;
  const generated=m.maestroToolKind==='audio-note'||m.maestroToolKind==='music';
  return dataUrl&&prefix.test(dataUrl)?[{messageId:m.id.slice(0,128),name:(m.attachmentName||(generated?'Generated chat sound':'Chat sound')).slice(0,80),origin:generated?'generated':m.role==='user'?'user-attachment':'unknown',dataUrl}]:[];
 }).slice(0,8);
}
export class ChatAudioSource extends ChatMediaSource<'assetHash'> {
 constructor(){super({hashKey:'assetHash',minimumBytes:44,maximumBytes:CHAT_AUDIO_BYTES,pattern:/^data:audio\/(wav|wave|x-wav);base64,([A-Za-z0-9+/]+={0,2})$/,fallbackName:'Chat sound'});}
}

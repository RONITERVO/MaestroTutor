// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {CHAT_MEDIA_CHUNK,validChatMediaRequest,type ChatMediaOffer,type ChatMediaRequest} from './chatMedia';
export const CHAT_AUDIO_BYTES=32*1024*1024;
export const CHAT_AUDIO_CHUNK=CHAT_MEDIA_CHUNK;
export type ChatAudioOffer=ChatMediaOffer<'assetHash'>;
export type ChatAudioRequest=ChatMediaRequest<'assetHash'>;
export const validChatAudioRequest=(v:unknown):v is ChatAudioRequest=>validChatMediaRequest(v,'assetHash',44,CHAT_AUDIO_BYTES);

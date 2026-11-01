// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {CHAT_MEDIA_CHUNK,validChatMediaRequest,type ChatMediaOffer,type ChatMediaRequest} from './chatMedia';
export const CHAT_IMAGE_BYTES=16*1024*1024;
export const CHAT_IMAGE_CHUNK=CHAT_MEDIA_CHUNK;
export type ChatImageOffer=ChatMediaOffer<'imageHash'>;
export type ChatImageRequest=ChatMediaRequest<'imageHash'>;
export const validChatImageRequest=(v:unknown):v is ChatImageRequest=>validChatMediaRequest(v,'imageHash',24,CHAT_IMAGE_BYTES);

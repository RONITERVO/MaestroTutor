// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export const CHAT_MEDIA_CHUNK = 16384;
export type ChatMediaOffer<K extends string> = Record<K,string> & {name:string;messageId:string;origin:string;bytes:number};
export type ChatMediaRequest<K extends string> = Record<K,string> & {requestId:string;offerSet:string;offset:number;bytes:number};
export function validChatMediaRequest<K extends string>(v:unknown,key:K,minimum:number,maximum:number):v is ChatMediaRequest<K> {
 if(v===null||typeof v!=='object'||Array.isArray(v))return false;
 const r=v as Record<string,unknown>;
 return Object.keys(r).length===5&&['requestId','offerSet'].every(k=>typeof r[k]==='string'&&/^[a-f0-9]{32}$/.test(r[k] as string))&&
  typeof r[key]==='string'&&/^[a-f0-9]{64}$/.test(r[key] as string)&&Number.isInteger(r.bytes)&&Number(r.bytes)>=minimum&&Number(r.bytes)<=maximum&&
  Number.isInteger(r.offset)&&Number(r.offset)>=0&&Number(r.offset)<Number(r.bytes)&&Number(r.offset)%CHAT_MEDIA_CHUNK===0;
}

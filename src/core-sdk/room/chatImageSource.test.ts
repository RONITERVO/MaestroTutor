// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {it, expect} from 'vitest';
import {webcrypto, createHash} from 'node:crypto';
import {ChatImageSource, chatImageSources} from './chatImageSource';
import {CHAT_IMAGE_CHUNK, validChatImageRequest} from '../../../shared/chatImages';
import type {ChatMessage} from '../../core/types';
Object.defineProperty(globalThis, 'crypto', {value:webcrypto, configurable:true});
const id = 'a'.repeat(32);
const source = (bytes:Uint8Array, messageId='image1') => ({messageId,name:'Generated tiles',origin:'generated',dataUrl:'data:image/png;base64,'+Buffer.from(bytes).toString('base64')});
it('transfers exact bytes across every base64 group alignment without exposing pixels in offers', async()=>{
 const bytes=Uint8Array.from({length:CHAT_IMAGE_CHUNK*3+17},(_,i)=>i%251), host=new ChatImageSource();
 await host.update('English-Spanish',[source(bytes)]);
 const offers=host.snapshot(null); expect(offers.offers).toHaveLength(1);
 const offer=offers.offers[0];expect(offer.imageHash).toBe(createHash('sha256').update(bytes).digest('hex'));
 expect(JSON.stringify(offers)).not.toContain('base64');expect(offers).not.toHaveProperty('chunk');
 const chunks:Buffer[]=[];
 for(let offset=0;offset<bytes.length;offset+=CHAT_IMAGE_CHUNK){
  const request={requestId:id,offerSet:offers.offerSet,imageHash:offer.imageHash,offset,bytes:bytes.length};
  expect(validChatImageRequest(request)).toBe(true);
  const chunk=host.snapshot(request).chunk!;expect(chunk.offset).toBe(offset);
  expect(chunk).toEqual(host.snapshot(request).chunk);chunks.push(Buffer.from(chunk.data,'base64'));
 }
 expect(Buffer.concat(chunks)).toEqual(Buffer.from(bytes));
});
it('fences conversation changes, altered bytes, invalid sizes and fabricated hashes',async()=>{
 const host=new ChatImageSource();await host.update('old',[source(new Uint8Array(50))]);
 const offered=host.snapshot(null),request={requestId:id,offerSet:offered.offerSet,imageHash:offered.offers[0].imageHash,offset:0,bytes:50};
 expect(host.snapshot({...request,bytes:49})).not.toHaveProperty('chunk');
 expect(host.snapshot({...request,offset:1})).not.toHaveProperty('chunk');
 expect(host.snapshot({...request,imageHash:'b'.repeat(64)})).not.toHaveProperty('chunk');
 await host.update('new',[source(new Uint8Array(50))]);expect(host.snapshot(request)).not.toHaveProperty('chunk');
 const current=host.snapshot(null);await host.update('new',[source(new Uint8Array(50))]);expect(host.snapshot(null)).toEqual(current);
 await host.update('new',[]);expect(host.snapshot(null).offers).toEqual([]);
});
it('does not restore superseded async image preparation',async()=>{
 const host=new ChatImageSource();const earlier=host.update('old',[source(new Uint8Array(20000))]);
 await host.update('new',[]);await earlier;expect(host.snapshot(null).offers).toEqual([]);
});
it('uses current local PNG/JPEG content and preserves source provenance without fetching remote links',async()=>{
 const base=source(new Uint8Array(50)).dataUrl;
 const messages=[{id:'one',imageUrl:base,imageOrigin:'generated',attachmentName:'Tiles'}, {id:'remote',imageUrl:'https://example.com/private.png'},{id:'pending',imageUrl:base,isGeneratingImage:true}] as ChatMessage[];
 expect(chatImageSources(messages)).toEqual([{messageId:'one',name:'Tiles',origin:'generated',dataUrl:base}]);
 const host=new ChatImageSource();await host.update('chat',[...chatImageSources(messages),source(new Uint8Array(50),'duplicate'),{...source(new Uint8Array(50)),dataUrl:'https://example.com/private.png'}]);
 expect(host.snapshot(null).offers).toHaveLength(1);expect(host.snapshot(null).offers[0].messageId).toBe('one');
});

it('honors the original history bookmark boundary for offered images',()=>{
 const dataUrl=source(new Uint8Array(50)).dataUrl;
 const messages=[{id:'before',imageUrl:dataUrl},{id:'bookmark',imageUrl:dataUrl},{id:'after',imageUrl:dataUrl}] as ChatMessage[];
 expect(chatImageSources(messages,'bookmark').map(v=>v.messageId)).toEqual(['after']);
 expect(chatImageSources(messages,'missing').map(v=>v.messageId)).toEqual(['after','bookmark','before']);
});

it('offers an optimized local image when only that saved variant is present',async()=>{
 const dataUrl=source(new Uint8Array(50)).dataUrl;
 const messages=[{id:'saved',storageOptimizedImageUrl:dataUrl,imageOrigin:'generated'}] as ChatMessage[];
 const sources=chatImageSources(messages);expect(sources).toHaveLength(1);expect(sources[0].dataUrl).toBe(dataUrl);
 const host=new ChatImageSource();await host.update('chat',sources);
 expect(host.snapshot(null).offers[0].imageHash).toBe(createHash('sha256').update(new Uint8Array(50)).digest('hex'));
});

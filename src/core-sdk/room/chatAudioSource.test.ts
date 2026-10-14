// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {it,expect} from 'vitest';
import {webcrypto,createHash} from 'node:crypto';
import {ChatAudioSource,chatAudioSources} from './chatAudioSource';
import {CHAT_AUDIO_CHUNK,validChatAudioRequest} from '../../../shared/chatAudio';
import {RoomAgentClient} from './roomAgentClient';
import type {ChatMessage} from '../../core/types';
Object.defineProperty(globalThis,'crypto',{value:webcrypto,configurable:true});
const source=(bytes:Uint8Array)=>({messageId:'sound',name:'Rain',origin:'generated',dataUrl:'data:audio/wav;base64,'+Buffer.from(bytes).toString('base64')});
it('offers finished local audio after the bookmark without fetching private or pending media',()=>{
 const data=source(new Uint8Array(80)).dataUrl;
 const m=(id:string,extra:Partial<ChatMessage>={}):ChatMessage=>({id,role:'assistant',timestamp:1,imageUrl:data,maestroToolKind:'music',...extra});
 const messages=[m('old'),m('bookmark'),m('good'),m('stream',{isGeneratingToolAttachment:true}),m('finalizing',{toolAttachmentPhase:'finalizing'}),m('remote',{imageUrl:'https://private/sound'}),m('webm',{imageUrl:'data:audio/webm;base64,AAAA'}),m('cache',{imageUrl:undefined,ttsAudioCache:[{key:'x',langCode:'es',provider:'gemini-live',updatedAt:1,audioDataUrl:data}]})];
 expect(chatAudioSources(messages,'bookmark')).toEqual([{messageId:'good',name:'Generated chat sound',origin:'generated',dataUrl:data}]);
});
it('keeps exact bytes and hash across every chunk boundary, without payload in catalog metadata',async()=>{
 const bytes=Uint8Array.from({length:CHAT_AUDIO_CHUNK*3+17},(_,i)=>i%251),host=new ChatAudioSource();await host.update('chat',[source(bytes)]);
 const offer=host.snapshot(null),hash=createHash('sha256').update(bytes).digest('hex');expect(offer.offers[0].assetHash).toBe(hash);expect(JSON.stringify(offer)).not.toContain('base64');
 const chunks:Buffer[]=[];for(let offset=0;offset<bytes.length;offset+=CHAT_AUDIO_CHUNK){const request={requestId:'b'.repeat(32),offerSet:offer.offerSet,assetHash:hash,offset,bytes:bytes.length};expect(validChatAudioRequest(request)).toBe(true);chunks.push(Buffer.from(host.snapshot(request).chunk!.data,'base64'));}
 expect(Buffer.concat(chunks)).toEqual(Buffer.from(bytes));
});
it('rejects stale generations, image requests and forged or mismatched audio requests',async()=>{
 const host=new ChatAudioSource();await host.update('old',[source(new Uint8Array(80))]);const offers=host.snapshot(null),request={requestId:'b'.repeat(32),offerSet:offers.offerSet,assetHash:offers.offers[0].assetHash,offset:0,bytes:80};
 expect(host.snapshot({...request,bytes:81}).chunk).toBeUndefined();expect(host.snapshot({...request,offset:1}).chunk).toBeUndefined();expect(host.snapshot({...request,imageHash:request.assetHash}).chunk).toBeUndefined();
 await host.update('new',[source(new Uint8Array(80))]);expect(host.snapshot(request).chunk).toBeUndefined();await host.update('',[]);expect(host.snapshot(null).offers).toEqual([]);
});
it('newest conversation wins asynchronous hashing and duplicate bytes produce one offer',async()=>{
 const host=new ChatAudioSource();const old=host.update('old',[source(new Uint8Array(40000))]);const fresh=source(new Uint8Array(80));await host.update('new',[fresh,{...fresh,messageId:'duplicate'}]);await old;expect(host.snapshot(null).offers).toHaveLength(1);expect(host.snapshot(null).offers[0].bytes).toBe(80);
});

it('multiplexes image and sound chunks fairly within the unchanged native wire limit',async()=>{
 const client=new RoomAgentClient(),bytes=new Uint8Array(CHAT_AUDIO_CHUNK*2),base=source(bytes);
 await client.chatAudio.update('chat',[base]);await client.chatImages.update('chat',[{...base,dataUrl:base.dataUrl.replace('audio/wav','image/png')}]);
 const audio=client.chatAudio.snapshot(null),images=client.chatImages.snapshot(null);
 const request={requestId:'c'.repeat(32),offset:0,bytes:bytes.length};
 const state={version:1,session:'d'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',created:[],objects:[],canUndo:false,canRedo:false,physicsRunning:false,capabilities:['chatImages.v1','chatAudio.v1'],chatAudio:{...request,offerSet:audio.offerSet,assetHash:audio.offers[0].assetHash},chatImage:{...request,offerSet:images.offerSet,imageHash:images.offers[0].imageHash}};
 expect(client.receive(state)).toBe(true);const first=client.snapshot(),second=client.snapshot();expect(JSON.stringify(first).length).toBeLessThanOrEqual(32768);expect(JSON.stringify(second).length).toBeLessThanOrEqual(32768);
 expect([first,second].some(x=>x.images?.chunk)).toBe(true);expect([first,second].some(x=>x.sounds?.chunk)).toBe(true);
 expect(client.receive({...state,revision:2,chatAudio:{...state.chatAudio,offset:1}})).toBe(false);
});

it.each(['mpeg','mp3','x-mp3','ogg','vorbis'])('offers and transfers completed local %s using its original byte hash',async mime=>{
 const bytes=Uint8Array.from({length:90},(_,i)=>i),input={...source(bytes),dataUrl:source(bytes).dataUrl.replace('audio/wav',`audio/${mime}`)};
 const offered=chatAudioSources([{id:'compressed',role:'user',timestamp:1,imageUrl:input.dataUrl,attachmentName:'My sound'}]);
 expect(offered).toHaveLength(1);expect(offered[0].origin).toBe('user-attachment');
 const host=new ChatAudioSource();await host.update('compressed-chat',offered);const snapshot=host.snapshot(null),hash=createHash('sha256').update(bytes).digest('hex');
 expect(snapshot.offers[0].assetHash).toBe(hash);
 const chunk=host.snapshot({requestId:'c'.repeat(32),offerSet:snapshot.offerSet,assetHash:hash,offset:0,bytes:bytes.length}).chunk;
 expect(Buffer.from(chunk!.data,'base64')).toEqual(Buffer.from(bytes));
});

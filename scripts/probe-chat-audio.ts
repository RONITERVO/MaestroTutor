// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import type {HeadlessRoomTransport} from '../src/headless/roomTransport';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
export function chatSoundFixture(){
 const frames=24000,bytes=Buffer.alloc(44+frames*2);bytes.write('RIFF');bytes.writeUInt32LE(bytes.length-8,4);bytes.write('WAVEfmt ',8);bytes.writeUInt32LE(16,16);bytes.writeUInt16LE(1,20);bytes.writeUInt16LE(1,22);bytes.writeUInt32LE(24000,24);bytes.writeUInt32LE(48000,28);bytes.writeUInt16LE(2,32);bytes.writeUInt16LE(16,34);bytes.write('data',36);bytes.writeUInt32LE(frames*2,40);
 for(let i=0;i<frames;i++)bytes.writeInt16LE(Math.round(4000*Math.sin(i*2*Math.PI*330/24000)),44+i*2);
 return {dataUrl:'data:audio/wav;base64,'+bytes.toString('base64'),hash:createHash('sha256').update(bytes).digest('hex')};
}
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeChatAudio(transport:HeadlessRoomTransport,execute:Execute,directory:string){
 const fixture=chatSoundFixture();transport.setChatSource(()=>({scope:'sound-probe',messages:[{id:'generated-sound',role:'assistant',timestamp:1,maestroToolKind:'audio-note',imageUrl:fixture.dataUrl,attachmentName:'Chat rain.wav'}]}));
 const read=async(capability:string,args?:Record<string,unknown>,version=1)=>{const reply=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version,...(args===undefined?{}:{arguments:args})}}]));assert.equal(reply.available,true,JSON.stringify(reply));return reply.value as Record<string,unknown>;};
 const invoke=async(id:string,args:Record<string,unknown>)=>{
  let result=(await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}])).execution?.selected;
  assert.ok(result);const runId=result.id,deadline=Date.now()+15000;
  while(!['completed','failed','cancelled'].includes(result.phase)&&Date.now()<deadline){
   await new Promise(r=>setTimeout(r,100));result=(await execute([{action:'execution',execution:{operation:'inspect',runId}}])).execution?.selected;
   assert.ok(result);assert.equal(result.id,runId);
  }
  assert.equal(result.phase,'completed',JSON.stringify(result));return result;
 };
 const deadline=Date.now()+15000;let offers=await read('audio.chat.library',{offset:0});while(Number(offers.total)===0&&Date.now()<deadline){await new Promise(r=>setTimeout(r,100));offers=await read('audio.chat.library',{offset:0});}
 assert.equal((offers.entries as {assetHash:string}[])[0].assetHash,fixture.hash);
 const selected=await invoke('audio.import',{operation:'selectChat',offerSet:offers.offerSet,assetHash:fixture.hash});
 let preview=await read('audio.import.selection');while(preview.phase!=='preview'&&Date.now()<deadline){assert.ok(!['failed','cancelled'].includes(String(preview.phase)),JSON.stringify(preview));await new Promise(r=>setTimeout(r,100));preview=await read('audio.import.selection');}
 assert.equal(preview.phase,'preview');const accepted=await invoke('audio.import',{operation:'accept',requestId:selected.output!.requestId,assetHash:fixture.hash});assert.equal(accepted.output!.assetHash,fixture.hash);
 const definition=await read('audio.source.definition',{id:accepted.output!.sourceId},2);assert.equal((definition.definition as {clip:{assetHash:string}}).clip.assetHash,fixture.hash);
 await execute([{action:'undo'}]);const removed=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.source.definition',version:2,arguments:{id:accepted.output!.sourceId}}}]));assert.equal(removed.available,false);
 transport.setChatSource(()=>({scope:'sound-probe-ended',messages:[]}));
 await writeFile(join(directory,'chat-audio.json'),JSON.stringify({boundary:'Finished local synthetic WAV in the shared chat source, actual bounded native transfer, preview, checked import and Undo. No provider, microphone or headset usage.',hash:fixture.hash,offers,selected,preview,accepted,definition},null,2));
}

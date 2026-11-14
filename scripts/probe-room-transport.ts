// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {RoomProbeChannel,publishRoomProbeFile} from '../src/headless/roomTransport';
import {RoomAgentClient} from '../src/core-sdk/room/roomAgentClient';
import {roomSnapshotTransport} from '../shared/behaviourCatalog';
import native from '../test-fixtures/browser/environmentWorld.json';
const sleep=(ms:number)=>new Promise<void>(resolve=>setTimeout(resolve,ms));
async function main(){
 const directory=process.argv[2];assert.ok(directory,'Supply a fresh native probe directory');
 const channel=await RoomProbeChannel.connect(directory,120000),client=new RoomAgentClient();
 try{
  const deadline=Date.now()+20000;
  while(!client.lease()){
   assert.ok(Date.now()<deadline,'Native handshake timed out');
   const result=await channel.exchange(client.snapshot());if(result.state)client.receive(result.state);await sleep(50);
  }
  const initial=client.lease()!.state();assert.ok(initial.capabilities?.includes(roomSnapshotTransport.feature));
  const call=structuredClone(native.call);call.arguments.revision=2147483647;call.arguments.name='Landscape 界';
  call.arguments.members.forEach(member=>member.revision=2147483647);
  const basis=call.arguments.members.map(member=>({id:member.target,objectRevision:member.revision}));
  const pending=client.request([{action:'execution',execution:{operation:'start',call}}],initial,basis);void pending.catch(()=>{});
  const snapshot=client.snapshot(),characters=JSON.stringify(snapshot).length;
  assert.ok(characters>roomSnapshotTransport.legacyMaximumCharacters&&characters<=roomSnapshotTransport.maximumCharacters);
  assert.deepEqual(snapshot.request!.conditions,basis.map(member=>({id:member.id,revision:member.objectRevision})));
  const envelope=JSON.stringify({version:1,id:channel.id,operation:'exchange',snapshot});
  // JSON permits escaped names and values. This must carry the same frame, not
  // silently shrink its read set to fit an adapter's unrelated byte budget.
  const escaped=envelope.replace(/"(?:\\.|[^"\\])*"/g,literal=>'"'+(JSON.parse(literal) as string).split('').map(c=>'\\u'+c.charCodeAt(0).toString(16).padStart(4,'0')).join('')+'"');
  assert.deepEqual(JSON.parse(escaped),JSON.parse(envelope));assert.ok(Buffer.byteLength(escaped)>40000);
  await publishRoomProbeFile(join(directory,'request.json'),escaped);
  const replyDeadline=Date.now()+12000;
  while(client.getSnapshot().pending){
   assert.ok(Date.now()<replyDeadline,'The escaped large frame did not reach native revision checks');
   const state=JSON.parse(await readFile(join(directory,'state.json'),'utf8'));
   assert.equal(state.version,1);assert.equal(state.id,channel.id);
   if(state.clientId===snapshot.clientId)client.receive(state.state);
   await sleep(50);
  }
  const refused=await pending;assert.equal(refused.ack,snapshot.request!.sequence);assert.equal(refused.ok,false);
  assert.match(refused.status,/target changed/i);assert.equal(refused.sceneRevision,initial.sceneRevision);
  assert.deepEqual(refused.created,[]);assert.equal(refused.canUndo,initial.canUndo);assert.equal(refused.physicsRunning,initial.physicsRunning);
  const followup=client.request([{action:'catalog',catalog:{operation:'check',call:{id:'time.wait',version:1,arguments:{seconds:1}}}}],refused,[]);void followup.catch(()=>{});
  const followupDeadline=Date.now()+12000;
  while(client.getSnapshot().pending){
   assert.ok(Date.now()<followupDeadline,'A subsequent compact request did not complete');
   const result=await channel.exchange(client.snapshot());if(result.state)client.receive(result.state);await sleep(50);
  }
  const after=await followup;assert.equal(after.ok,true);assert.equal(after.sceneRevision,initial.sceneRevision);
  await writeFile(join(directory,'transport-boundary.json'),JSON.stringify({version:1,boundary:'Real Unity app through the shared local-file adapter; offline, no headset or provider',
   frameCharacters:characters,encodedBytes:Buffer.byteLength(escaped),members:258,snapshot,refused,after,sceneUnchanged:true},null,2));
  console.log('Escaped full-world frame reached native revision validation; saved state unchanged and next request completed.');
 }finally{client.cancel();await channel.stop();}
}
main().catch(error=>{console.error(error);process.exitCode=1;});

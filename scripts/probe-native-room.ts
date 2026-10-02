// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {HeadlessRoomTransport} from '../src/headless/roomTransport';
import {runRoomActionTask,type RoomCommand} from '../src/core-sdk/room/roomAgent';
import {createHeadlessClient} from '../src/headless/client';
import {getGeminiModels} from '../src/core-sdk/modelRegistry';
const directory=process.argv[2];if(!directory)throw new Error('Supply the explicitly started native probe directory.');
const prompt=process.env.MAESTRO_ROOM_PROBE_PROMPT;
const transport=await HeadlessRoomTransport.connect(directory,120000);
const observations:unknown[]=[];
try{
 const lease=transport.lease();const initial=structuredClone(lease.state());
 const execute=async(commands:RoomCommand[])=>{
  const state=lease.state();const result=await lease.execute(commands,state.sceneRevision,state.objects);observations.push(structuredClone(result));
  if(!result.ok)throw new Error('Native action refused: '+result.status);return result;
 };
 let outcome:unknown;
 if(prompt){
  const client=await createHeadlessClient({profileName:process.env.MAESTRO_ROOM_PROBE_PROFILE||'quest-probe'});
  const controller=new AbortController();const timer=setTimeout(()=>controller.abort(),240000);
  const usage:unknown[]=[];
  try{outcome=await runRoomActionTask({model:getGeminiModels().text.default,prompt,history:[],nativeLanguageCode:'en-US',timeoutMs:45000},
   {aiClient:client.ai},lease,response=>usage.push({model:response.modelUsed,usage:response.usageMetadata}),{signal:controller.signal,onReceipt:receipt=>{observations.push(structuredClone(receipt));}});
  }finally{clearTimeout(timer);await client.save();}
  outcome={accessMode:client.accessMode,usage,result:outcome};
 }else{
  const catalog=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'object.create',version:1}}]);
  const definition=catalog.catalog?.definition as {example?:Record<string,unknown>}|undefined;
  if(!definition?.example)throw new Error('The native create capability has no example.');
  const created=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Native probe ball'}}}}]);
  const selected=created.execution?.selected;
  if(selected?.phase!=='completed'||typeof selected.output?.objectId!=='string')throw new Error('Creation did not produce a completed native receipt.');
  const target=selected.output.objectId;const original=created.objects.find(object=>object.id===target);
  if(!original||created.objects.length!==initial.objects.length+1)throw new Error('The object was not created in the real room.');
  const painted=await execute([{action:'paint',target,color:{r:1,g:0,b:0,a:1}}]);
  if(painted.objects.find(object=>object.id===target)?.color.r!==1)throw new Error('Native paint did not apply.');
  const undoPaint=await execute([{action:'undo'}]);
  if(JSON.stringify(undoPaint.objects.find(object=>object.id===target)?.color)!==JSON.stringify(original.color))throw new Error('Native Undo did not restore the previous paint.');
  const undoCreate=await execute([{action:'undo'}]);
  if(undoCreate.objects.some(object=>object.id===target)||undoCreate.objects.length!==initial.objects.length)throw new Error('Native Undo did not remove the created object.');
  const diagnostic=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.modelBudget',version:1}}]);
  if(!diagnostic.catalog?.available)throw new Error('Native resource diagnostics are unavailable.');
  const repeat=Number(process.env.MAESTRO_ROOM_PROBE_REPEATS??1);if(!Number.isInteger(repeat)||repeat<1||repeat>32)throw new Error('Probe repeats must be 1–32.');
  for(let cycle=0;cycle<repeat;cycle++){
  const cases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/lathe-contract.json','utf8'));
  const cup=cases[0].recipe;
  const lathe=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:'Native profile cup',x:.2,y:1,z:.5,scale:1,recipe:cup}}}}]);
  const cupId=lathe.execution?.selected?.output?.objectId;if(typeof cupId!=='string')throw new Error('Lathe creation did not return an object.');
  const inspected=await execute([{action:'inspect',target:cupId}]);
  if(inspected.inspection?.recipe?.parts[0].shape!=='lathe')throw new Error('The shared client cannot inspect the native lathe.');
  const changed=structuredClone(inspected.inspection.recipe.parts[0]);changed.profile[1].x=.48;changed.segments=32;
  const edited=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.recipe.edit',version:1,arguments:{target:cupId,revision:inspected.inspection.objectRevision,parts:[changed],removeParts:[],tracks:[],removeTracks:[],duration:cup.duration,loop:false}}}}]);
  const revision=edited.objects.find(object=>object.id===cupId)?.objectRevision;
  const profile=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.recipe.profile',version:1,arguments:{target:cupId,revision,part:'Body',offset:0}}}]);
  const value=profile.catalog?.value as {segments?:number;points?:{x:number;y:number}[]}|undefined;
  if(!profile.catalog?.available||value?.segments!==32||Math.abs((value.points?.[1].x??0)-.48)>.00001)throw new Error('Native lathe readback differs from the edit.');
  await execute([{action:'undo'}]);const restored=await execute([{action:'inspect',target:cupId}]);
  if(restored.inspection?.recipe?.parts[0].segments!==24)throw new Error('Lathe Undo did not restore the profile.');
  const removed=await execute([{action:'undo'}]);if(removed.objects.some(object=>object.id===cupId))throw new Error('Lathe Undo did not remove the created geometry.');
  outcome={createdId:target,createReceipt:selected,paintVerified:true,undoPaintVerified:true,undoCreateVerified:true,diagnostics:diagnostic.catalog.value,lathe:{createReceipt:lathe.execution?.selected,profile:value,editAndUndoVerified:true},latheCycles:cycle+1};
  }
 }
 await writeFile(join(directory,'journey.json'),JSON.stringify({version:1,boundary:'Real Unity Editor app and shared room protocol; no Quest input, WebView, scan or Store proof',providerUsed:!!prompt,initial,observations,outcome},null,2));
 console.log(JSON.stringify({providerUsed:!!prompt,observations:observations.length,output:join(directory,'journey.json')}));
}catch(error){transport.checkHealth();throw error;}finally{await transport.close();}

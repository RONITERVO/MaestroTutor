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
  const collisionCases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/collision-contract.json','utf8'));
  const collisionCatalog=await execute([{action:'catalog',catalog:{operation:'search',query:'Edit collision shapes',offset:0}}]);
  const collisionDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.collision.edit',version:1}}]);
  const collisionCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:cupId}}}]);
  const collided=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.collision.edit',version:1,arguments:{target:cupId,revision:restored.inspection!.objectRevision,collision:collisionCases[0].collision}}}}]);
  const collisionRevision=collided.objects.find(object=>object.id===cupId)?.objectRevision;
  const summary=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:cupId}}}]);
  const summaryValue=summary.catalog?.value as {pieces?:number;shapes?:number;customActive?:boolean}|undefined;
  if(summaryValue?.pieces!==13||summaryValue.shapes!==2||summaryValue.customActive!==true)throw new Error('Native compound collision summary differs from the edit.');
  const wall=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision.shape',version:1,arguments:{target:cupId,revision:collisionRevision,index:1}}}]);
  if((wall.catalog?.value as {shape?:{id?:string;shape?:string}})?.shape?.shape!=='ring')throw new Error('Native collision shape readback is missing the hollow wall.');
  await execute([{action:'undo'}]);
  const collisionRestored=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:cupId}}}]);
  if((collisionRestored.catalog?.value as {shapes?:number})?.shapes!==0)throw new Error('Collision Undo did not restore the default proxy.');
  await writeFile(join(directory,'collision-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:restored,search:collisionCatalog,definition:collisionDefinition,current:collisionCurrent,after:collided,summary,wall},null,2));
  const removed=await execute([{action:'undo'}]);if(removed.objects.some(object=>object.id===cupId))throw new Error('Lathe Undo did not remove the created geometry.');
  const templateBefore=structuredClone(lease.state());
  const templateSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Create object',offset:0}}]);
  const templateDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}}]);
  const templateSchema=templateDefinition.catalog?.definition as {input:{oneOf:{examples:Record<string,unknown>[]}[]}};
  const templateArgs=templateSchema.input.oneOf.find(branch=>branch.examples[0]?.kind==='template')?.examples[0];if(!templateArgs)throw new Error('Starter templates unavailable');
  const templateAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:templateArgs}}}]);
  const templateId=templateAfter.execution?.selected?.output?.objectId;if(typeof templateId!=='string')throw new Error('Template creation returned no object');
  const templateRead=await execute([{action:'inspect',target:templateId}]);
  if(templateRead.objects.find(object=>object.id===templateId)?.name!=='Cup'||templateRead.inspection?.recipe?.parts.length!==2)throw new Error('Template geometry did not expand');
  const templateCollision=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:templateId}}}]);
  if((templateCollision.catalog?.value as {pieces?:number})?.pieces!==21)throw new Error('Template collision did not expand');
  const templatePhysics=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.physics.settings',version:1,arguments:{target:templateId}}}]);
  if((templatePhysics.catalog?.value as {mode?:string})?.mode!=='solid')throw new Error('Template physics did not expand');
  const templateUndo=await execute([{action:'undo'}]);if(templateUndo.objects.some(object=>object.id===templateId))throw new Error('Single Undo did not remove the whole template');
  await writeFile(join(directory,'template-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:templateBefore,search:templateSearch,definition:templateDefinition,after:templateAfter,read:templateRead,collision:templateCollision,physics:templatePhysics,undo:templateUndo},null,2));
  const brickSource=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/brick.json','utf8'));
  const {createHash}=await import('node:crypto');const brickHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/brick.json')).digest('hex');
  const layoutIds:string[]=[];let layoutBefore=templateUndo;
  for(let i=0;i<2;i++){
   layoutBefore=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:brickHash,name:brickSource.name+' '+(i+1),x:.4+i*.25,y:1.2,z:.7,scale:1}}}}]);
   const id=layoutBefore.execution?.selected?.output?.objectId;if(typeof id!=='string')throw new Error('Layout member missing');layoutIds.push(id);
  }
  const placements=layoutIds.map((target,i)=>({target,position:{x:.3+i*.25,y:1,z:.8},rotation:{x:0,y:0,z:0,w:1},scale:1}));
  const layoutSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Arrange or reset objects',offset:0}}]);
  const layoutDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.layout.apply',version:1}}]);
  const layoutAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.layout.apply',version:1,arguments:{placements}}}}]);
  if(layoutAfter.execution?.selected?.output?.count!==2)throw new Error('Layout did not complete both members');
  const layoutFacts=[];for(const p of placements){
   const fact=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target:p.target}}}]);
   const value=fact.catalog?.value as {position?:{x:number;y:number;z:number};scale?:number};
   if(!value?.position||Math.abs(value.position.x-p.position.x)>.00001||Math.abs(value.position.y-p.position.y)>.00001||Math.abs(value.position.z-p.position.z)>.00001||value.scale!==1)throw new Error('Layout live readback differs');layoutFacts.push(fact);
  }
  const layoutUndo=await execute([{action:'undo'}]);for(let i=0;i<2;i++)if(Math.abs((layoutUndo.objects.find(o=>o.id===layoutIds[i])?.position.x??0)-(.4+i*.25))>.00001)throw new Error('Single Undo did not restore both layout members');
  await writeFile(join(directory,'layout-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:layoutBefore,search:layoutSearch,definition:layoutDefinition,after:layoutAfter,facts:layoutFacts,undo:layoutUndo},null,2));
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  const batchProgram=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-batch-create.json','utf8'));
  const batchArgs=batchProgram.functions[0].body[0].arguments,batchBefore=structuredClone(lease.state());
  const batchSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Create a structure',offset:0}}]);
  const batchDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.batch.create',version:1}}]);
  const batchAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:batchArgs}}}]);
  const batchIds=batchAfter.execution?.selected?.output?.objectIds,batchSlots=batchAfter.execution?.selected?.output?.slots;
  if(!Array.isArray(batchIds)||batchIds.length!==6||!batchIds.every(id=>typeof id==='string')||new Set(batchIds).size!==6||JSON.stringify(batchSlots)!==JSON.stringify(batchArgs.blueprint.pieces.map((p:{slot:string})=>p.slot)))throw new Error('Batch result lost piece identity/order');
  if(batchAfter.objects.length!==batchBefore.objects.length+6||batchIds.some(id=>!batchAfter.objects.some(o=>o.id===id)))throw new Error('Native structure pieces were not all created');
  const batchUndo=await execute([{action:'undo'}]);if(batchIds.some(id=>batchUndo.objects.some(o=>o.id===id))||batchUndo.objects.length!==batchBefore.objects.length)throw new Error('One Undo did not remove the whole batch');
  await writeFile(join(directory,'batch-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:batchBefore,search:batchSearch,definition:batchDefinition,after:batchAfter,undo:batchUndo},null,2));
  outcome={batch:{createReceipt:batchAfter.execution?.selected,identitiesAndSingleUndoVerified:true},layout:{applyReceipt:layoutAfter.execution?.selected,liveReadAndSingleUndoVerified:true},template:{hash:templateArgs.templateHash,createReceipt:templateAfter.execution?.selected,componentsAndSingleUndoVerified:true},createdId:target,createReceipt:selected,paintVerified:true,undoPaintVerified:true,undoCreateVerified:true,diagnostics:diagnostic.catalog.value,lathe:{createReceipt:lathe.execution?.selected,profile:value,editAndUndoVerified:true},collision:{summary:summaryValue,editAndUndoVerified:true},latheCycles:cycle+1};
  }
 }
 await writeFile(join(directory,'journey.json'),JSON.stringify({version:1,boundary:'Real Unity Editor app and shared room protocol; no Quest input, WebView, scan or Store proof',providerUsed:!!prompt,initial,observations,outcome},null,2));
 console.log(JSON.stringify({providerUsed:!!prompt,observations:observations.length,output:join(directory,'journey.json')}));
}catch(error){transport.checkHealth();throw error;}finally{await transport.close();}

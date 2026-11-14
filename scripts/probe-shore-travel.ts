// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
import {assertWorldLocation,type WorldLocation} from './probe-world-location';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeShoreTravel(execute:Execute,directory:string){
 const inspect=async(capability:string,args?:Record<string,unknown>)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:behaviourFact(capability)!.version,arguments:args}}]));
 const read=async(capability:string,args?:Record<string,unknown>)=>{
  const result=await inspect(capability,args);
  if(!result.available){
   // Keep the original unavailable result before any diagnostic exchanges.
   // These are bounded passive reads: never reload an actor to make the check pass.
   const diagnostics:unknown[]=[];
   const queries:[string,Record<string,unknown>?][]=[['world.viewpoint'],['physics.simulation']];
   if(typeof args?.target==='string')for(const fact of ['object.presence','object.observation','object.navigation'])if(fact!==capability)queries.push([fact,{target:args.target}]);
   for(const [fact,arguments_] of queries){
    try{const reply=await inspect(fact,arguments_);diagnostics.push({capability:fact,arguments:arguments_,available:reply.available,status:reply.status,value:reply.value});}
    catch(error){diagnostics.push({capability:fact,error:String(error).slice(0,1000)});}
   }
   await writeFile(join(directory,'native-shore-fact-unavailable.json'),JSON.stringify({failed:{capability,arguments:args,available:result.available,status:result.status},diagnostics},null,2));
  }
  assert.equal(result.available,true,result.status);return result.value as Record<string,unknown>;
 };
 const invoke=async(id:string,args:Record<string,unknown>)=>{
  const state=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]));
  assert.equal(state.execution?.selected?.phase,'completed',JSON.stringify(state.execution?.selected));return state.execution!.selected!;
 };
 assert.equal((await read('physics.simulation')).running,false);
 const observerBefore=await read('world.viewpoint') as unknown as WorldLocation;
 assert.equal(observerBefore.located,true);
 // The native desktop camera starts at the authored origin with a 1.55 m eye
 // and a 30 m far plane. Shore x=[-5,-3], z=[1.5,2.5] and leader x=-5.45,z=2
 // are outside its original forward view. Aim at their shared centre from the
 // existing clear floor position: no new ground or arbitrary retention owner.
 const observerTarget={position:{...observerBefore.position},yaw:Math.atan2(-4.35-observerBefore.position.x,2-observerBefore.position.z)*180/Math.PI};
 const observerEvidence:Record<string,unknown>={boundary:'Ordinary shared viewpoint relocation keeps the shore fixture and independent leader in view; no retirement override, native pin or fact-triggered loading.',before:observerBefore,target:observerTarget,verification:'pending'};
 let scenarioError:unknown;
 try{
  observerEvidence.receipt=await invoke('world.viewpoint.set',{stateId:observerBefore.stateId,...observerTarget});
  const observed=await read('world.viewpoint') as unknown as WorldLocation;assert.equal(observed.located,true);assertWorldLocation(observed,observerTarget);
  observerEvidence.observed=observed;
  await writeFile(join(directory,'native-shore-observer.json'),JSON.stringify(observerEvidence,null,2));
  const source=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/snow-patch.json','utf8')).definition;
  const rotation={x:0,y:0,z:0,w:1},position={x:0,y:0,z:0},color={r:.6,g:.4,b:.2,a:1};
  const field={...source.heightFields[0],cells:8,width:2,depth:1,maxHeight:.5,frame:{position:{x:0,y:.2,z:0},rotation},material:'Earth',color,
   heights:Array.from({length:81},(_,i)=>(i%9)*.05)};
  const prototype={version:6,geometry:{kind:'recipe',recipe:source.recipe},collision:source.collision,physics:source.physics,color,surfaces:[],drawingTips:[],heightFields:[field]};
  const piece={...source.recipe.parts[0],id:'Body',parent:'',shape:'box',position:{x:0,y:.05,z:0},rotation,size:{x:.1,y:.1,z:.1},color};
  const create=await invoke('object.batch.create',{blueprint:{version:1,pieces:[
   {slot:'shore',name:'Sloped shore',position,rotation,scale:1,source:{kind:'prototype',prototype}},
   {slot:'actor',name:'Shore robot',position:{x:.75,y:.55,z:0},rotation,scale:1,source:{kind:'recipe',recipe:{...source.recipe,parts:[piece]},
     physics:{mode:'fixed',shape:'box',mass:1}}},
   {slot:'leader',name:'Shore guide',position:{x:-1.45,y:.3,z:0},rotation,scale:1,source:{kind:'recipe',recipe:{...source.recipe,parts:[piece]},
     physics:{mode:'fixed',shape:'box',mass:1}}}
  ]},position:{x:-4,y:0,z:2},rotation,scale:1});
  const [pool,actor,leader]=create.output!.objectIds as string[];
  const container=await read('object.container',{target:pool});
  await invoke('object.container.edit',{operation:'configure',target:pool,revision:container.revision,definition:{
   ...(container.definition as Record<string,unknown>),terrainFloor:true,height:.5,amountMl:225000,capacityMl:1
  }});
  const presence=await read('object.presence',{target:actor});assert.equal(presence.saved,true);
  const policy=await read('object.water.traversal.settings',{target:actor});
  await writeFile(join(directory,'native-shore-setup.json'),JSON.stringify({create,pool,actor,presence,policy},null,2));
  const policyExample=capabilityDefinition('object.water.traversal.configure')?.example;
  assert.ok(policyExample,'The shared water settings action must be discoverable');
  await invoke('object.water.traversal.configure',{...structuredClone(policyExample),target:actor,revision:policy.revision,mode:'avoid',maxDepthMetres:.02});
  await invoke('physics.simulation.set',{operation:'start',stateId:(await read('physics.simulation')).stateId});
  const journeys:unknown[]=[];
  for(const destination of [{kind:'surface',x:-4.75,z:2},{kind:'ground',x:-3.25,y:.55,z:2}]){
   const call={id:'object.navigate',version:1,arguments:{target:actor,mode:'shore',habitat:pool,destination,immersion:.6,speed:.4,facing:'travel'}};
   const check=await execute([{action:'catalog',catalog:{operation:'check',call}}]);assert.ok(check.catalog?.operation==='check');assert.equal(check.catalog.available,true,JSON.stringify(check.catalog));
   let state=await execute([{action:'execution',execution:{operation:'start',call}}]);const runId=state.execution?.selected?.id;assert.ok(runId);
   const samples:Record<string,unknown>[]=[];const deadline=Date.now()+30000;
   while(state.execution?.selected?.phase==='preparing'&&Date.now()<deadline){
    samples.push(await read('object.navigation',{target:actor}));
    await new Promise(resolve=>setTimeout(resolve,80));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);
   }
   const receipt=state.execution?.selected;assert.equal(receipt?.phase,'completed',JSON.stringify(receipt));
   const after=await read('object.position',{target:actor});
   assert.ok(Math.abs(Number(after.x)-destination.x)<.006&&Math.abs(Number(after.z)-2)<.006);
   if(destination.kind==='ground')assert.ok(Math.abs(Number(after.y)-.55)<.006);
   else assert.ok(Number(after.y)>.3&&Number(after.y)<.45,'The robot must be buoyant above the lake bed');
   assert.ok(samples.some(s=>s.mode==='shore'&&s.active===true));
   assert.ok(samples.some(s=>{const p=s.position as {x:number};return p.x<-3.4&&p.x>-4.6;}),'No observed intermediate shore position');
   assert.equal((await read('object.water.traversal.settings',{target:actor})).mode,'avoid');
   const replay=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);assert.deepEqual(replay.execution?.selected,receipt);
   journeys.push({call,check:check.catalog,samples,receipt,after});
  }
  await writeFile(join(directory,'native-shore-travel.json'),JSON.stringify({boundary:'Real shared catalog creation of a sloped native terrain reservoir and robot, dry-to-buoyant travel and return to dry ground, observed intermediate root poses, unchanged saved avoidance and receipt replay. No provider/headset or calibrated limb claim.',create,pool,actor,journeys},null,2));
  const followCall={id:'object.follow',version:1,arguments:{target:actor,mode:'shore',habitat:pool,leader:{kind:'object',objectId:leader},distance:.8,speed:.4,seconds:7,immersion:.6,facing:'keep'}};
  let following=await execute([{action:'execution',execution:{operation:'start',call:followCall}}]);
  const followRun=following.execution?.selected?.id;assert.ok(followRun);
  const followingSamples:Record<string,unknown>[]=[];const followingDeadline=Date.now()+20000;
  while(['preparing','running'].includes(following.execution?.selected?.phase??'')&&Date.now()<followingDeadline){
   followingSamples.push(await read('object.navigation',{target:actor}));
   await new Promise(resolve=>setTimeout(resolve,80));following=await execute([{action:'execution',execution:{operation:'inspect',runId:followRun}}]);
  }
  const followingReceipt=following.execution?.selected;
  // Keep failure samples and the two already verified navigation legs even when
  // a later assertion stops the overall journey. A receipt alone is not a pass.
  await writeFile(join(directory,'native-water-following.json'),JSON.stringify({verification:'pending',pool,actor,leader,call:followCall,samples:followingSamples,receipt:followingReceipt},null,2));
  assert.equal(followingReceipt?.phase,'completed',JSON.stringify(followingReceipt));
  assert.equal(followingReceipt.output!.state,'keepingDistance');assert.ok(Number(followingReceipt.output!.distance)>1);
  const followingPosition=await read('object.position',{target:actor});
  assert.ok(Math.abs(Number(followingPosition.x)+4.65)<.06&&Math.abs(Number(followingPosition.z)-2)<.01);
  assert.ok(followingSamples.some(s=>s.active===true&&s.mode==='follow'&&s.movement==='shore'));
  assert.equal((await read('object.water.traversal.settings',{target:actor})).mode,'avoid');
  const followReplay=await execute([{action:'execution',execution:{operation:'start',runId:followRun,call:followCall}}]);assert.deepEqual(followReplay.execution?.selected,followingReceipt);
  await writeFile(join(directory,'native-water-following.json'),JSON.stringify({verification:'passed',boundary:'Shared catalog shore following of an independent fixed leader, observed native ground-to-water movement, exact habitat, unchanged avoidance and receipt replay. No provider/headset or moving-leader claim.',pool,actor,leader,call:followCall,samples:followingSamples,receipt:followingReceipt,position:followingPosition},null,2));
  await invoke('physics.simulation.set',{operation:'pause',stateId:(await read('physics.simulation')).stateId});
 }catch(error){scenarioError=error;throw error;}
 finally{
  try{
   // The fixture starts paused. Restore that state before moving its view back,
   // also on a failed assertion; do not teleport a running physical scene.
   const physics=await read('physics.simulation');
   if(physics.running)await invoke('physics.simulation.set',{operation:'pause',stateId:physics.stateId});
   const current=await read('world.viewpoint') as unknown as WorldLocation;assert.equal(current.located,true);
   observerEvidence.restored=await invoke('world.viewpoint.set',{stateId:current.stateId,position:observerBefore.position,yaw:observerBefore.yaw});
   const final=await read('world.viewpoint') as unknown as WorldLocation;assert.equal(final.located,true);assertWorldLocation(final,observerBefore);
   observerEvidence.final=final;observerEvidence.verification=scenarioError===undefined?'passed':'scenario_failed_view_restored';
   await writeFile(join(directory,'native-shore-observer.json'),JSON.stringify(observerEvidence,null,2));
  }catch(error){
   observerEvidence.verification='restoration_failed';observerEvidence.error=String(error).slice(0,2000);
   await writeFile(join(directory,'native-shore-observer.json'),JSON.stringify(observerEvidence,null,2));
   if(scenarioError!==undefined)throw new AggregateError([scenarioError,error],'Shore scenario failed and its original viewpoint could not be restored');
   throw error;
  }
 }
}

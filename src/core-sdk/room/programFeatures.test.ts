// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram,type BehaviourProgram,type Value} from './programs';
import {parseRoomCommands} from './roomAgent';
import {moduleHash} from './programModules';
const base=['behaviourPrograms.v3','eventPrograms.v1','structuredValues.v1'];
const program=(literal:Value):BehaviourProgram=>({version:3,dataVersion:1,entry:'main',resources:[],state:[{name:'saved',initial:literal}],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'copy',initial:literal}],body:[{id:'copy',op:'set',variable:'copy',value:{value:literal}}]}]});
const commands=(p:BehaviourProgram)=>parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{id:'a'.repeat(32),name:'Data example',repeat:false,interruption:0,program:JSON.stringify(p)}}]}}]});
const literals:Value[]=[
 {op:'invoke',capability:'object.delete',arguments:{target:'book'},results:{objectId:'example'},waitForChannels:2},
 {op:'invoke',capability:'object.create',arguments:{kind:'recipe'},results:{objectId:'example'}},
 {op:'checkpoint',memoryVersion:1},
 {op:'parallel',parallelVersion:1},
 {op:'awaitCondition'},
 {op:'awaitEvent',event:'object.collided',fields:{speed:'value'}},
 {fact:'object.position'},
 {id:'animation.play',arguments:{source:'library',target:'maestro',motionId:'a'.repeat(32)}}
];
it.each(literals)('treats declaration and expression records as data: %j',literal=>{
 const p=program(literal);expect(parseProgram(JSON.stringify(p)).error).toBeNull();
 expect(()=>requireRoomCapabilities(commands(p),{capabilities:base})).not.toThrow();
});
it('checks real blocks inside imported programs',()=>{
 const child=program(3);child.functions[0].body=[{id:'wait',op:'invoke',capability:'time.wait',version:1,arguments:{seconds:1},bindings:{},waitForChannels:{value:3}}];const module={version:1 as const,name:'Typed child',exports:['main'],program:child};
 const p:BehaviourProgram={version:3,dataVersion:1,moduleVersion:1,entry:'main',resources:[],state:[],events:[],imports:[{alias:'child',hash:moduleHash(module),module,signals:{}}],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'child',op:'call',module:'child',function:'main',args:[]}]}]};
 expect(parseProgram(JSON.stringify(p)).error).toBeNull();
 const capabilities=[...base,'programModules.v1'];
 expect(()=>requireRoomCapabilities(commands(p),{capabilities})).toThrow('channelWaits.v1');
 expect(()=>requireRoomCapabilities(commands(p),{capabilities:[...capabilities,'channelWaits.v1']})).not.toThrow();
});
it('reads module source only in native schema-declared program payloads',()=>{
 const p=program(literals[0]);const definition={version:1 as const,name:'Data example',exports:['main'],program:p};
 const call={id:'program.module.import',version:1,arguments:{hash:moduleHash(definition),definition}};
 const request=()=>parseRoomCommands({commands:[{action:'execution',execution:{operation:'start',call}}]});
 const capabilities=['execution.v1','moduleLibraryFiles.v1','structuredValues.v1'];
 expect(()=>requireRoomCapabilities(request(),{capabilities})).not.toThrow();
 p.functions[0].body.push({id:'wait',op:'invoke',capability:'time.wait',version:1,arguments:{seconds:1},bindings:{},waitForChannels:{value:3}});
 call.arguments.hash=moduleHash(definition);
 expect(()=>requireRoomCapabilities(request(),{capabilities})).toThrow('channelWaits.v1');
 expect(()=>requireRoomCapabilities(request(),{capabilities:[...capabilities,'channelWaits.v1']})).not.toThrow();
});
it('dispatches valid data unchanged through the book bridge and still rejects real unsupported blocks',async()=>{
 const state=JSON.parse(readFileSync('test-fixtures/browser/eventProgramStates.json','utf8')).waiting;
 state.capabilities=base;
 const client=new RoomAgentClient();expect(client.receive(state)).toBe(true);
 const p=program(literals[0]),request=commands(p),lease=client.lease()!;
 const pending=lease.execute(request,state.sceneRevision,state.objects);
 try {
  const sent=client.snapshot().request!;expect(sent.commands).toEqual(request);
  expect(client.receive({...state,revision:state.revision+1,ack:sent.sequence})).toBe(true);
  await expect(pending).resolves.toMatchObject({ack:sent.sequence});
 }finally{client.cancel();await pending.catch(()=>undefined);}
 const next=new RoomAgentClient();expect(next.receive(state)).toBe(true);
 p.functions[0].body.push({id:'wait',op:'invoke',capability:'time.wait',version:1,arguments:{seconds:1},bindings:{},waitForChannels:{value:3}});
 expect(()=>next.lease()!.execute(commands(p),state.sceneRevision,state.objects)).toThrow('channelWaits.v1');
 expect(next.snapshot().request).toBeNull();next.cancel();
});

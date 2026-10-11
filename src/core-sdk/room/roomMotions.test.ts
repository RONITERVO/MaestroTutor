// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {parseRoomCommands} from './roomAgent';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {validMotionSearchView,type MotionSearchView} from '../../../shared/roomMotions';
const fixtures=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/room-motions.json','utf8')) as {cases:{name:string;valid:boolean;json:string}[]};
const page=():MotionSearchView=>({targetId:'maestro',modelHash:'a'.repeat(64),ready:true,status:'Found',query:{query:'wave',offset:0,includeShort:false,favouritesOnly:false,archivedOnly:false},offset:0,total:1,pageSize:12,entries:[{id:'b'.repeat(32),name:'Wave',tags:['greeting'],duration:1,shortClip:false,favourite:false,archived:false,downloaded:true}]});
describe('native motion discovery contract',()=>{
 it.each(fixtures.cases)('$name',sample=>{
  const run=()=>parseRoomCommands(JSON.parse(sample.json));if(sample.valid)expect(run()).toEqual(JSON.parse(sample.json).commands);else expect(run).toThrow();
 });
 it('requires advertised support and validates bounded compatible pages',()=>{
  expect(()=>requireRoomCapabilities([{action:'motions'}],{})).toThrow('does not support');
  expect(()=>requireRoomCapabilities([{action:'motions'}],{capabilities:['motions.v1']})).not.toThrow();
  expect(validMotionSearchView(page())).toBe(true);
  for(const change of [{total:2},{offset:1},{entries:[...page().entries,...page().entries],total:2},{ready:false},{pageSize:100},{query:{...page().query,offset:-1}},{entries:[{...page().entries[0],duration:Infinity}]},{entries:[{...page().entries[0],shortClip:true}]},{entries:[{...page().entries[0],archived:true}]}])expect(validMotionSearchView({...page(),...change})).toBe(false);
  expect(validMotionSearchView({...page(),modelHash:'',ready:false,total:0,entries:[]})).toBe(true);
  expect(validMotionSearchView({...page(),query:{...page().query,offset:1024},offset:12,total:13})).toBe(true);
 });
});

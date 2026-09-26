// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {parseRoomCommands} from './roomAgent';
import {validObjectPhysics,validAvatarMovement,requireRoomCapabilities} from '../../../shared/roomControls';
const fixtures=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/room-controls.json','utf8')) as {cases:{name:string;valid:boolean;json:string}[]};
describe('shared native/web room control contract',()=>{
 it.each(fixtures.cases)('$name',sample=>{
  const run=()=>parseRoomCommands(JSON.parse(sample.json));
  if(sample.valid)expect(run()).toEqual(JSON.parse(sample.json).commands);else expect(run).toThrow();
 });
 it('rejects nonfinite runtime arguments and absent native capabilities',()=>{
  expect(validObjectPhysics({mode:'solid',shape:'box',mass:NaN})).toBe(false);
  expect(validAvatarMovement({distance:1,speed:Infinity})).toBe(false);
  expect(()=>requireRoomCapabilities([{action:'avatarMotion'}],{})).toThrow('does not support');
  expect(()=>requireRoomCapabilities([{action:'avatarMotion'}],{capabilities:['avatarMotion.v1']})).not.toThrow();
  expect(()=>requireRoomCapabilities([{action:'create'}],{})).not.toThrow();
 });
});

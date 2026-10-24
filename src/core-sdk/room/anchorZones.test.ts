// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {behaviourEvent,eventArgumentType,validateEventArguments} from '../../../shared/behaviourEvents';
import {capabilityFeatures,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram,type BehaviourProgram} from './programs';
import {validRuleView} from './rules';
import {validRoomOwnership} from '../../../shared/roomOwnership';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-anchor-zone.json','utf8')) as BehaviourProgram;
const event='object.anchor.proximity.changed';
it('shares the exact native anchored-zone program and typed nested argument contract',()=>{
 const p=source(),wait=p.functions[0].body[0];if(wait.op!=='awaitEvent')throw Error('fixture');expect(parseProgram(JSON.stringify(p)).program).toEqual(p);
 expect(behaviourEvent(event)?.features).toContain('anchorZones.v1');expect(validateEventArguments(event,2,wait.arguments)).toBeNull();
 expect(eventArgumentType(event,'holder.revision',wait.arguments)).toBe('number');expect(eventArgumentType(event,'holder.kind',wait.arguments)).toBeNull();expect(eventArgumentType(event,'holder.hand',wait.arguments)).toBeNull();
 expect(validateEventArguments(event,2,{...wait.arguments,radius:0})).not.toBeNull();expect(validateEventArguments(event,2,{...wait.arguments,physics:'yes'})).not.toBeNull();
 wait.bindings={'holder.revision':{value:1},'offset.z':{value:.35}};expect(parseProgram(JSON.stringify(p)).error).toBeNull();wait.bindings['holder.kind']={value:'object'};expect(parseProgram(JSON.stringify(p)).program).toBeNull();
});
it('requires anchor support for zone waits and optional reach without breaking old hold calls',()=>{
 const p=source(),branch=p.functions[0].body[1];if(branch.op!=='if'||branch.then[0].op!=='invoke')throw Error('fixture');const node=branch.then[0];
 expect(validateCapabilityArguments(node.capability,1,node.arguments)).toBeNull();expect(capabilityFeatures(node.capability,node.arguments)).toContain('anchorZones.v1');
 const old={...node.arguments};delete old.reach;expect(validateCapabilityArguments(node.capability,1,old)).toBeNull();expect(capabilityFeatures(node.capability,old)).not.toContain('anchorZones.v1');
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','eventFields.v1','eventSubscriptions.v1','objectAttachments.v1','actionResults.v1'];
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(p)}}]}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('anchorZones.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'anchorZones.v1']})).not.toThrow();
 const call={id:node.capability,version:1,arguments:node.arguments};expect(()=>requireRoomCapabilities([{action:'execution',execution:{operation:'start',call}}],{capabilities:[...capabilities,'execution.v1']})).toThrow('anchorZones.v1');
});
it('accepts real native watching, pickup and completed observations through the browser wire',()=>{
 const capture=JSON.parse(readFileSync('test-fixtures/browser/anchorZoneState.json','utf8'));expect(parseProgram(JSON.stringify(capture.program)).error).toBeNull();
 for(const phase of ['saved','watching','holding','completed']){expect(validRuleView(capture[phase].rules),phase).toBe(true);expect(validRoomOwnership(capture[phase].ownership),phase).toBe(true);}
 expect(capture.watching.rules.running[0]).toMatchObject({waiting:true,waitEvent:event});expect(capture.watching.ownership.owners.every((owner:{role:string})=>owner.role==='ambient')).toBe(true);
 expect(capture.holding.rules.running[0].nodeId).toBe('pickup');expect(capture.completed.rules.running).toEqual([]);expect(capture.completed.rules.outcomes.at(-1).phase).toBe('completed');
});

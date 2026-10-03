// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-structure-reset.json','utf8'));
it('shares capture/reset programs and their exact member authority with native execution',()=>{
 const p=source();expect(parseProgram(JSON.stringify(p)).error).toBeNull();expect(programFeatureRequirements(p as BehaviourProgram).has('structures.v1')).toBe(true);
 for(const call of p.functions[0].body)expect(capabilityResources(call.capability,call.arguments)).toEqual(p.resources);
 p.resources.pop();expect(parseProgram(JSON.stringify(p)).error).not.toBeNull();
});
it('rejects duplicate slot keys or targets before capture and invalid definition baselines',()=>{
 const original=source().functions[0].body[0].arguments;
 const check=(update:(v:typeof original)=>void)=>{const a=structuredClone(original);update(a);expect(validateCapabilityArguments('structure.save',1,a)).not.toBeNull();};
 check(a=>{a.source.members[1].slot=a.source.members[0].slot;});check(a=>{a.source.members[1].target=a.source.members[0].target;});
 const args=structuredClone(original);args.source.kind='definition';args.source.slots=args.source.members.map((m:{slot:string;target:string})=>({slot:m.slot,placement:{target:m.target,position:{x:1,y:1,z:1},rotation:{x:0,y:0,z:0,w:1},scale:1}}));delete args.source.members;
 expect(validateCapabilityArguments('structure.save',1,args)).toBeNull();args.source.slots[0].placement.position={x:25,y:25,z:0};expect(validateCapabilityArguments('structure.save',1,args)).not.toBeNull();
});
it('requires explicit member resources for reset while forgetting edits metadata only',()=>{
 const p=source(),reset=p.functions[0].body[1];expect(capabilityResources('structure.reset',reset.arguments)).toEqual(p.resources);
 const forget={id:'c'.repeat(32),revision:1};expect(validateCapabilityArguments('structure.forget',1,forget)).toBeNull();expect(capabilityResources('structure.forget',forget)).toEqual([]);
});

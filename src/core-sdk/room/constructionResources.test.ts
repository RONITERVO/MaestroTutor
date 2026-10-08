// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {validateCapabilityArguments,capabilityResources,capabilityFeatures} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/construction-resources-contract.json','utf8')) as {name:string;valid:boolean;arguments:Record<string,unknown>}[];
it.each(cases)('matches native portable-construction boundary: $name',c=>expect(validateCapabilityArguments('object.batch.create',1,c.arguments)===null).toBe(c.valid));
it('local definition symbols never authorize destination objects',()=>expect(capabilityResources('object.batch.create',cases[0].arguments)).toEqual([]));
it('requires advertised resource support before dispatching or installing a constructor',()=>{
 const args=cases[0].arguments,features=capabilityFeatures('object.batch.create',args);
 expect(features).toContain('constructionResources.v1');
 const commands=[{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1',...features.filter(f=>f!=='constructionResources.v1')]})).toThrow('constructionResources.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1',...features]})).not.toThrow();
});

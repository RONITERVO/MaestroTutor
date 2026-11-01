// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {boundedCapabilityCall,validCatalogRequest} from '../../../shared/roomCatalog';
import {validateCapabilityArguments,type CapabilityInvocation} from '../../../shared/capabilities';
import {validExecutionRequest} from '../../../shared/roomExecutions';
import {parseRoomCommands} from './roomAgent';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/catalog-call-bounds.json','utf8')) as {name:string;call:CapabilityInvocation;bounded:boolean;valid:boolean}[];
it.each(cases)('matches native transport and domain bounds: $name',({call,bounded,valid})=>{
 expect(boundedCapabilityCall(call)).toBe(bounded);
 expect(validCatalogRequest({operation:'check',call})).toBe(bounded);
 expect(validateCapabilityArguments(call.id,call.version,call.arguments)===null).toBe(valid);
 const execution={operation:'start',call};
 expect(validExecutionRequest(execution)).toBe(bounded&&valid);
 const parse=()=>parseRoomCommands({commands:[{action:'execution',execution}]});
 if(bounded&&valid)expect(parse).not.toThrow();else expect(parse).toThrow();
});

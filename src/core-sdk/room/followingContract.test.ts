// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityFeatures,resolveCapabilitySchema,validateCapabilityArguments,type CapabilitySchema} from '../../../shared/capabilities';
import {validExecutionRequest} from '../../../shared/roomExecutions';
import {parseRoomCommands} from './roomAgent';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/following-contract.json','utf8')) as {name:string;arguments:Record<string,unknown>;valid:boolean;schemaValid?:boolean;water:boolean}[];
it.each(cases)('shares the native following contract: $name',({arguments:args,valid,schemaValid=valid,water})=>{
 const before=JSON.stringify(args),call={id:'object.follow',version:1,arguments:args};
 expect(validateCapabilityArguments(call.id,1,args)===null).toBe(schemaValid);
 expect(validExecutionRequest({operation:'start',call})).toBe(schemaValid);
 const parse=()=>parseRoomCommands({commands:[{action:'execution',execution:{operation:'start',call}}]});
 if(schemaValid){expect(parse).not.toThrow();expect(capabilityFeatures(call.id,args).includes('waterFollowing.v1')).toBe(water);}
 else expect(parse).toThrow();
 // Domain relations (including following oneself), readiness and ownership remain native authority.
 expect(JSON.stringify(args)).toBe(before);
});
it('requires exactly one explicitly optional selector branch and rejects null or unknown modes',()=>{
 const branch=(mode:string,optional:boolean):CapabilitySchema=>({type:'object',properties:{mode:{type:'string',enum:[mode]}},required:optional?[]:['mode'],additionalProperties:false});
 const schema:CapabilitySchema={type:'object',oneOf:[branch('walk',true),branch('swim',false)],'x-discriminators':['mode']};
 expect(resolveCapabilitySchema(schema,{})).toBe(schema.oneOf![0]);
 for(const mode of [null,'fly',1])expect(resolveCapabilitySchema(schema,{mode})).toBeUndefined();
 schema.oneOf!.push(branch('surface',true));expect(resolveCapabilitySchema(schema,{})).toBeUndefined();
});

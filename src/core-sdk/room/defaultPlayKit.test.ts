// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseRoomCommands} from './roomAgent';
import {parseProgram} from './programs';
import {moduleHash} from './programModules';
import {validateCapabilityArguments} from '../../../shared/capabilities';
const root='unity/MaestroQuest/Assets/Maestro/';
it.each([['SmallFort','small-fort',16],['Spinner','spinner',2],['ChessWhite','chess-white',16],['ChessBlack','chess-black',16]] as const)('shares %s source and exact pins with the native library', (resource,fixture,count)=>{
 const source=JSON.parse(readFileSync(root+'Tests/Fixtures/program-'+fixture+'.json','utf8'));
 const module=JSON.parse(readFileSync(root+'Resources/Programs/Modules/'+resource+'.json','utf8'));
 expect(source.imports[0].module).toEqual(module);expect(source.imports[0].hash).toBe(moduleHash(module));
 expect(parseProgram(JSON.stringify(source)).error).toBeNull();
 expect(()=>parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:999999,edits:[{kind:'save',reference:'kit',sequence:{id:'',name:'Native '+module.name+' probe',interruption:0,repeat:false,program:JSON.stringify(source)}}]}}]})).not.toThrow();
 const args=module.program.functions[1].body[0].arguments;
 expect(validateCapabilityArguments('object.batch.create',1,args)).toBeNull();expect(args.blueprint.pieces).toHaveLength(count);
 const changed=structuredClone(source);changed.imports[0].module.name='My editable copy';
 expect(parseProgram(JSON.stringify(changed)).error).toContain('pinned hash');
 changed.imports[0].hash=moduleHash(changed.imports[0].module);expect(parseProgram(JSON.stringify(changed)).error).toBeNull();
});

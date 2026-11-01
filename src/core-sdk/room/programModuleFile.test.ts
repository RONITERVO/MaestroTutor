// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {moduleHash} from '../../../shared/programModuleIdentity';
import {boundedCapabilityCall} from '../../../shared/roomCatalog';
import {capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {decodeModuleFile,encodeModuleFile} from './programModuleFile';
const module=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-modules-nested.json','utf8')).imports[0].module;
const hash=moduleHash(module);
it('round-trips nested immutable definitions without remapping pins or mutating the source',()=>{
 const before=JSON.stringify(module),file=decodeModuleFile(encodeModuleFile(hash,module));expect(file.definition).toEqual(module);expect(file.hash).toBe(hash);expect(JSON.stringify(module)).toBe(before);
 const args={hash,definition:file.definition};expect(validateCapabilityArguments('program.module.import',1,args)).toBeNull();expect(boundedCapabilityCall({id:'program.module.import',version:1,arguments:args})).toBe(true);expect(capabilityResources('program.module.import',args)).toEqual([]);
});
it.each(['hash','definition','format','version','extra'])('rejects tampered file %s before dispatch',part=>{
 const file=JSON.parse(encodeModuleFile(hash,module));if(part==='hash')file.hash='0'.repeat(64);if(part==='definition')file.definition.name='Tampered';if(part==='format')file.format='some other file';if(part==='version')file.version=2;if(part==='extra')file.extra=true;
 expect(()=>decodeModuleFile(JSON.stringify(file))).toThrow();
});
it('rejects duplicates, oversized files and correctly hashed programs that cannot compile',()=>{
 const source=encodeModuleFile(hash,module);expect(()=>decodeModuleFile(source.replace('"format":','"version":1,"format":'))).toThrow('Duplicate');expect(()=>decodeModuleFile(' '.repeat(96001))).toThrow('96,000');
 const invalid=structuredClone(module);invalid.exports=['missing'];expect(()=>encodeModuleFile(moduleHash(invalid),invalid)).toThrow('Module cannot be imported');
});
it('does not relax ordinary action arguments or treat a module field as object authority',()=>{
 const args={hash,definition:module};expect(boundedCapabilityCall({id:'future.import',version:1,arguments:args})).toBe(false);
 expect(boundedCapabilityCall({id:'time.wait',version:1,arguments:{seconds:1,definition:module}})).toBe(false);
});

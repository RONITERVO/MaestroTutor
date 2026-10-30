// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validModuleRecord,type ModuleRecord} from '../../../shared/programModuleIdentity';
import {parseProgram,strictProgramJson} from './programs';
export const MODULE_FILE_MAX_BYTES=96000;
export interface ProgramModuleFile {format:'maestro-program-module';version:1;hash:string;definition:ModuleRecord}
/** Portable definitions contain code/data, never executable JS/C# or bundled assets. */
export function checkedModuleFile(value:unknown):ProgramModuleFile {
 const f=value as ProgramModuleFile;
 if(!f||typeof f!=='object'||Array.isArray(f)||Object.keys(f).length!==4||f.format!=='maestro-program-module'||f.version!==1||typeof f.hash!=='string'||!validModuleRecord(f.definition,f.hash))throw new Error('Invalid Maestro module file or mismatched content ID.');
 const p=f.definition.program;
 const wrapper={version:3,moduleVersion:1,...(p.parallelVersion===1?{parallelVersion:1}:{}),...(p.dataVersion===1?{dataVersion:1}:{}),entry:'main',resources:p.resources,state:[],events:p.events,
  functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}],
  imports:[{alias:'module',hash:f.hash,module:f.definition,signals:Object.fromEntries((p.events as {name:string}[]).map(e=>[e.name,e.name]))}]};
 const parsed=parseProgram(JSON.stringify(wrapper));if(!parsed.program)throw new Error('Module cannot be imported: '+parsed.error);
 return JSON.parse(JSON.stringify(f)) as ProgramModuleFile;
}
export function decodeModuleFile(source:string):ProgramModuleFile {
 if(new TextEncoder().encode(source).byteLength>MODULE_FILE_MAX_BYTES)throw new Error('Module file exceeds 96,000 bytes.');
 return checkedModuleFile(strictProgramJson(source));
}
export function encodeModuleFile(hash:string,definition:ModuleRecord):string {
 const source=JSON.stringify(checkedModuleFile({format:'maestro-program-module',version:1,hash,definition}))+'\n';
 if(new TextEncoder().encode(source).byteLength>MODULE_FILE_MAX_BYTES)throw new Error('Module file exceeds 96,000 bytes.');return source;
}

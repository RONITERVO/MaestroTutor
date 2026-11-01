// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {type ModuleRecord} from '../../../shared/programModuleIdentity';
import {parseProgram,type BehaviourProgram} from '../../core-sdk/room/programs';
import {copyVerifiedProgramModule} from '../../core-sdk/room/programImportReferences';
export interface ModuleImportDraft {alias:string;replace?:string;signals:Record<string,string>;grantResources:boolean}
/** Explicit draft edit. Pins are verified and copied; no execution or library mutation occurs. */
export function editProgramImport(program:BehaviourProgram,hash:string,module:ModuleRecord,draft:ModuleImportDraft):BehaviourProgram {
 const definition=copyVerifiedProgramModule(hash,module);
 if(!/^[a-zA-Z0-9_]{1,32}$/.test(draft.alias))throw new Error('Use 1–32 letters, digits or underscores for the import name.');
 const next:BehaviourProgram=JSON.parse(JSON.stringify(program));next.version=3;next.state??=[];next.events??=[];next.moduleVersion=1;next.imports??=[];
 const previous=next.imports.find(i=>i.alias===draft.replace);
 if(draft.replace&&(!previous||draft.alias!==draft.replace))throw new Error('The import changed. Reopen the library before replacing it.');
 if(!draft.replace&&next.imports.some(i=>i.alias===draft.alias))throw new Error('Choose a different import name or explicitly replace that import.');
 if(!Array.isArray(definition.program.resources)||!Array.isArray(definition.program.events))throw new Error('Invalid module program.');
 const additional=definition.program.resources.filter(id=>!next.resources.includes(id));
 if(additional.length&&!draft.grantResources)throw new Error('Allow the displayed additional objects before importing this module.');
 next.resources=[...new Set([...next.resources,...additional])];
 for(const event of definition.program.events){
  const name=draft.signals[event.name];if(!/^user\.[a-zA-Z0-9_]{1,32}$/.test(name??''))throw new Error('Connect every module signal to a named user signal.');
  const existing=next.events.find(e=>e.name===name);if(existing&&existing.type!==event.type)throw new Error(name+' already has a different payload type.');if(!existing)next.events.push({name,type:event.type});
 }
 if(definition.program.parallelVersion===1)next.parallelVersion=1;
 if(definition.program.dataVersion===1)next.dataVersion=1;
 const imported={alias:draft.alias,hash,module:definition,signals:{...draft.signals}};
 if(previous)next.imports[next.imports.indexOf(previous)]=imported;else next.imports.push(imported);
 const result=parseProgram(JSON.stringify(next));if(!result.program)throw new Error(result.error??'Invalid imported program.');return result.program;
}

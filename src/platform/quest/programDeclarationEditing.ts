// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {inferDataType,checkedDataValue} from '../../../shared/programValues';
import {parseProgram,type BehaviourProgram,type Value,type ValueType,type ScalarType} from '../../core-sdk/room/programs';
import {visitProgramNodes,visitNodeExpressions} from './programEditingTraversal';
export interface DeclarationDraft {
 state:{origin:number|null;name:string;type:ValueType;initial:Value}[];
 events:{origin:number|null;name:string;type:ScalarType}[];
}
export const declarationDraft=(program:BehaviourProgram):DeclarationDraft=>JSON.parse(JSON.stringify({
 state:(program.state??[]).map((v,origin)=>({...v,origin,type:v.type??inferDataType(v.initial)})),
 events:(program.events??[]).map((v,origin)=>({...v,origin}))
}));
function renames(before:{name:string}[],after:{origin:number|null;name:string}[]){
 const result=new Map<string,string>(),seen=new Set<number>();
 for(const item of after){if(item.origin===null)continue;if(!Number.isInteger(item.origin)||item.origin<0||item.origin>=before.length||seen.has(item.origin))throw new Error('Declarations changed. Discard this editor draft.');seen.add(item.origin);result.set(before[item.origin].name,item.name);}
 return (name:string)=>{const next=result.get(name);if(next===undefined)throw new Error(name+' is still used. Keep it or remove its uses first.');return next;};
}
/** Atomic refactoring in this program. A named signal in another program keeps its existing identity. */
export function editProgramDeclarations(program:BehaviourProgram,draft:DeclarationDraft):BehaviourProgram {
 for(const state of draft.state)checkedDataValue(state.initial,state.type);
 const next:BehaviourProgram=JSON.parse(JSON.stringify(program));
 const stateName=renames(next.state??[],draft.state),eventName=renames(next.events??[],draft.events);
 for(const fn of next.functions)visitProgramNodes(fn.body,n=>{
  visitNodeExpressions(n,e=>{if('state' in e)e.state=stateName(e.state);});
  if(n.op==='setState')n.variable=stateName(n.variable);
  if((n.op==='awaitEvent'||n.op==='emitEvent')&&n.event.startsWith('user.'))n.event=eventName(n.event);
 });
 next.version=3;next.state=draft.state.map(({name,type,initial})=>({name,initial,...(typeof type==='object'?{type}:{})}));next.events=draft.events.map(({name,type})=>({name,type}));
 if(draft.state.some(s=>typeof s.type==='object'))next.dataVersion=1;
 const result=parseProgram(JSON.stringify(next));if(!result.program)throw new Error(result.error??'Invalid declarations');return result.program;
}

import {visitProgramNodes as visit,visitNodeExpressions} from './programEditingTraversal';
import {defaultDataValue} from '../../../shared/programValues';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {parseProgram,type BehaviourProgram,type ProgramFunction,type ProgramNode,type Value,type ValueType} from '../../core-sdk/room/programs';

export interface FunctionDraft {
 name:string;returns:ProgramFunction['returns'];
 parameters:{origin:number|null;name:string;type:ValueType}[];
 locals:{origin:number|null;name:string;initial:Value;type?:ValueType}[];
}
export const functionDraft=(fn:ProgramFunction):FunctionDraft=>({name:fn.name,returns:fn.returns,
 parameters:fn.parameters.map((p,origin)=>({...p,origin})),locals:fn.locals.map((v,origin)=>({...v,origin}))});
export const initialValue=defaultDataValue;
export const initialExpression=(type:ValueType)=>({value:initialValue(type),...(typeof type==='object'?{type}:{})});
/** Rename variable references and destinations, leaving other namespaces and literal data alone. */
function variables(body:ProgramNode[],map:(name:string)=>string) {
 visit(body,n=>{
  visitNodeExpressions(n,e=>{if('var' in e)e.var=map(e.var);});
  if(n.op==='set')n.variable=map(n.variable);
  if(n.op==='call'&&n.result)n.result=map(n.result);
  if(n.op==='invoke'&&n.results)for(const key of Object.keys(n.results))n.results[key]=map(n.results[key]);
  if(n.op==='awaitEvent'){n.received=map(n.received);n.value=map(n.value);if(n.fields)for(const key of Object.keys(n.fields))n.fields[key]=map(n.fields[key]);}
 });
}
function origins(items:{origin:number|null}[],length:number){
 const seen=new Set<number>();for(const {origin} of items){if(origin===null)continue;
  if(!Number.isInteger(origin)||origin<0||origin>=length||seen.has(origin))throw new Error('Function declarations changed. Discard this editor draft.');seen.add(origin);
 }
}
/** Produce one validated, detached program. Renames are simultaneous and scoped. */
export function editProgramFunction(program:BehaviourProgram,originalName:string|null,draft:FunctionDraft):BehaviourProgram {
 const next:BehaviourProgram=JSON.parse(JSON.stringify(program));
 const index=originalName===null?-1:next.functions.findIndex(f=>f.name===originalName);
 if(originalName!==null&&index<0)throw new Error('Function changed. Discard this editor draft.');
 const previous=index<0?undefined:next.functions[index];
 if(typeof draft.returns==='object'||draft.parameters.some(p=>typeof p.type==='object')||draft.locals.some(p=>typeof p.initial==='object')){next.version=3;next.dataVersion=1;next.state??=[];next.events??=[];}
 origins(draft.parameters,previous?.parameters.length??0);origins(draft.locals,previous?.locals.length??0);
 const fn:ProgramFunction={name:draft.name,returns:draft.returns,parameters:draft.parameters.map(({name,type})=>({name,type})),locals:draft.locals.map(({name,initial,type})=>({name,initial,...(type?{type}:{})})),body:previous?.body??[]};
 const rename=new Map<string,string>();
 for(const [old,items] of [[previous?.parameters??[],draft.parameters],[previous?.locals??[],draft.locals]] as const)
  old.forEach((v,i)=>{const changed=items.find(p=>p.origin===i);if(changed)rename.set(v.name,changed.name);});
 variables(fn.body,name=>{const value=rename.get(name);if(value===undefined)throw new Error('Variable '+name+' is still used. Keep it or remove its uses first.');return value;});
 if(index<0)next.functions.push(fn);else next.functions[index]=fn;
 if(previous){
  if(next.entry===originalName)next.entry=fn.name;
  for(const f of next.functions)visit(f.body,n=>{if(n.op==='call'&&!n.module&&n.function===originalName){
   n.function=fn.name;n.args=draft.parameters.map(p=>p.origin===null?initialExpression(p.type):n.args[p.origin]);
  }});
 }
 // Supply a visible return block for a new typed function (or a formerly void body).
 // Existing return calculations and callers are never silently discarded/retyped.
 if(fn.returns!=='void'){
  let found=false;visit(fn.body,n=>{if(n.op==='return'){found=true;if(!n.value)n.value=initialExpression(fn.returns as ValueType);}});
  if(!found){const ids=new Set<string>();for(const f of next.functions)visit(f.body,n=>ids.add(n.id));let i=1;while(ids.has('return_'+i))i++;
   fn.body.push({id:'return_'+i,op:'return',value:initialExpression(fn.returns)});
  }
 }
 const result=parseProgram(JSON.stringify(next));if(!result.program)throw new Error(result.error??'Invalid function edit');return result.program;
}

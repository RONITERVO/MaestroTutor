// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {Expression,ProgramNode} from './programs';
export const programBranches=(n:ProgramNode):ProgramNode[][]=>n.op==='if'?[n.then,n.else]:n.op==='repeat'||n.op==='forever'?[n.body]:n.op==='switch'?[...n.cases.map(c=>c.body),n.default]:[];
export function visitProgramNodes(body:ProgramNode[],action:(node:ProgramNode)=>void){for(const n of body){action(n);for(const child of programBranches(n))visitProgramNodes(child,action);}}
/** Typed expression positions only. Literal records and native payloads are data, never references. */
export function visitNodeExpressions(n:ProgramNode,action:(expression:Expression)=>void){
 const visit=(e:Expression)=>{action(e);if('op' in e)e.args.forEach(visit);if('fact' in e)Object.values(e.bindings??{}).forEach(visit);};
 switch(n.op){
  case 'set':case 'setState':case 'emitEvent':case 'switch':visit(n.value);break;
  case 'if':visit(n.test);break;case 'repeat':visit(n.count);break;case 'sleep':visit(n.seconds);break;
  case 'return':if(n.value)visit(n.value);break;case 'call':n.args.forEach(visit);break;
  case 'invoke':Object.values(n.bindings).forEach(visit);break;
  case 'awaitEvent':visit(n.timeout);Object.values(n.bindings??{}).forEach(visit);break;
 }
}

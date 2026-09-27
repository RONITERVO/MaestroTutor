// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {type RuleStep} from './ruleSteps';
import {behaviourFactTypes} from '../../../shared/behaviourCatalog';
import {validateCapabilityArguments,capabilityParameterType,capabilityResources} from '../../../shared/capabilities';
import {stepInvocation,invocationStep} from './capabilitySteps';
export type Value=number|boolean|string;
export type ValueType='number'|'boolean'|'text';
export type Expression={value:Value}|{var:string}|{fact:string}|{op:string;args:Expression[]};
export type ProgramNode={id:string}&(
 {op:'set';variable:string;value:Expression}|{op:'if';test:Expression;then:ProgramNode[];else:ProgramNode[]}|
 {op:'repeat';count:Expression;body:ProgramNode[]}|{op:'switch';value:Expression;cases:{value:Value;body:ProgramNode[]}[];default:ProgramNode[]}|
 {op:'call';function:string;args:Expression[];result?:string}|{op:'return';value?:Expression}|
 {op:'invoke';capability:string;version:number;arguments:Record<string,unknown>;bindings:Record<string,Expression>});
export interface ProgramFunction {name:string;returns:ValueType|'void';parameters:{name:string;type:ValueType}[];locals:{name:string;initial:Value}[];body:ProgramNode[]}
export interface BehaviourProgram {version:2;entry:string;resources:string[];functions:ProgramFunction[]}
export const programFacts=behaviourFactTypes;
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
function need(condition:unknown,message:string):asserts condition {if(!condition)throw new Error(message);}
const obj=(v:unknown)=>{need(record(v),'Expected an object');return v;};
const array=(v:unknown,max:number)=>{need(Array.isArray(v)&&v.length<=max,'Missing or oversized list');return v as unknown[];};
const text=(v:unknown)=>{need(typeof v==='string','Expected text');return v;};
const name=(v:unknown)=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
const target=(v:unknown)=>v==='maestro'||v==='book'||typeof v==='string'&&/^[a-fA-F0-9]{32}$/.test(v);
function keys(v:Record<string,unknown>,required:string,optional='') {const a=required.split(' ').filter(Boolean),b=[...a,...optional.split(' ')];need(a.every(k=>Object.prototype.hasOwnProperty.call(v,k))&&Object.keys(v).every(k=>b.includes(k)),'Missing or unknown program field');}
function type(v:unknown):ValueType|'void' {need(['number','boolean','text','void'].includes(v as string),'Unknown value type');return v as ValueType|'void';}
function literal(v:unknown):ValueType {
 need(typeof v==='boolean'||typeof v==='number'&&Number.isFinite(v)&&Math.abs(v)<=1000000||typeof v==='string'&&v.length<=128&&!/[\u0000-\u001f\u007f-\u009f]/.test(v),'Expected bounded number, boolean or text');
 return typeof v==='string'?'text':typeof v as ValueType;
}
/** Reject ambiguous duplicate keys and excessive JSON depth before typed validation. */
function strictJson(source:string):unknown {
 const result:unknown=JSON.parse(source);const tokens=source.match(/"(?:[^"\\]|\\.)*"|[{}\[\]:,]|[^\s{}\[\]:,]+/g)??[];let at=0;
 const read=(depth:number)=>{need(depth<=48,'JSON nesting limit exceeded');const token=tokens[at++];
  if(token==='{') {const seen=new Set<string>();if(tokens[at]!=='}')do {const key=JSON.parse(tokens[at++]) as string;need(!seen.has(key),'Duplicate program field');seen.add(key);at++;read(depth+1);}while(tokens[at++]===',');else at++;}
  else if(token==='[') {if(tokens[at]!==']')do {read(depth+1);}while(tokens[at++]===',');else at++;}
 };read(0);return result;
}
/** Authoring validator only. Unity is the sole program executor. Shared fixtures cover both validators. */
export function parseProgram(source:unknown):{program:BehaviourProgram|null;error:string|null} {
 try {
  need(typeof source==='string'&&source.length<=24000,'Program exceeds its size limit');const root=obj(strictJson(source));keys(root,'version entry resources functions');need(root.version===2,'Unsupported program version');
  const resources=new Set<string>();for(const value of array(root.resources,16)){need(target(value)&&!resources.has(value as string),'Invalid or duplicate resource');resources.add(value as string);}
  const functions=new Map<string,{source:Record<string,unknown>;types:Map<string,ValueType>}>(),calls=new Map<string,Set<string>>();
  for(const value of array(root.functions,16)) {
   const f=obj(value);keys(f,'name returns parameters locals body');const id=text(f.name);need(name(id)&&!functions.has(id),'Invalid or duplicate function name');type(f.returns);array(f.body,128);
   const types=new Map<string,ValueType>();
   for(const value of array(f.parameters,8)){const p=obj(value);keys(p,'name type');const t=type(p.type);need(name(p.name)&&!types.has(p.name as string)&&t!=='void','Invalid or duplicate parameter');types.set(p.name as string,t);}
   for(const value of array(f.locals,16)){const p=obj(value);keys(p,'name initial');need(name(p.name)&&!types.has(p.name as string),'Invalid or duplicate local');types.set(p.name as string,literal(p.initial));}
   functions.set(id,{source:f,types});calls.set(id,new Set());
  }
  const entry=functions.get(text(root.entry));need(entry&&array(entry.source.parameters,8).length===0,'Entry must name a function with no parameters');
  const ids=new Set<string>();let expressions=0;
  const expr=(value:unknown,types:Map<string,ValueType>,depth=0):ValueType=>{
   need(depth<=8&&++expressions<=512,'Expression limit exceeded');const e=obj(value);
   if(Object.prototype.hasOwnProperty.call(e,'value')){keys(e,'value');return literal(e.value);}
   if(Object.prototype.hasOwnProperty.call(e,'var')){keys(e,'var');const t=types.get(text(e.var));need(t,'Unknown variable');return t;}
   if(Object.prototype.hasOwnProperty.call(e,'fact')){keys(e,'fact');const fact=text(e.fact);need(Object.prototype.hasOwnProperty.call(programFacts,fact),'Unknown room fact');return programFacts[fact];}
   keys(e,'op args');const op=text(e.op),args=array(e.args,2);need(args.length===(op==='not'?1:2),'Invalid expression argument count');const ts=args.map(a=>expr(a,types,depth+1));
   if(['not','and','or'].includes(op)){need(ts.every(t=>t==='boolean'),'Logic needs booleans');return 'boolean';}
   if(['eq','ne'].includes(op)){need(ts[0]===ts[1],'Comparison types differ');return 'boolean';}
   need(['add','sub','mul','div','mod','lt','le','gt','ge'].includes(op)&&ts.every(t=>t==='number'),'Unknown operation or nonnumeric argument');return ['lt','le','gt','ge'].includes(op)?'boolean':'number';
  };
  const returns=(nodes:unknown[]):boolean=>nodes.some(value=>{const n=obj(value);return n.op==='return'||n.op==='if'&&returns(n.then as unknown[])&&returns(n.else as unknown[])||n.op==='switch'&&returns(n.default as unknown[])&&(n.cases as {body:unknown[]}[]).every(c=>returns(c.body));});
  const body=(nodes:unknown[],f:{source:Record<string,unknown>;types:Map<string,ValueType>},depth=0)=>{
   need(depth<=8,'Block nesting limit exceeded');for(const value of nodes) {
    const n=obj(value);need(name(n.id)&&!ids.has(n.id as string)&&ids.size<128,'Invalid, duplicate or excessive block identities');ids.add(n.id as string);
    const child=(key:string)=>body(array(n[key],128),f,depth+1),expect=(key:string,t:ValueType)=>need(expr(n[key],f.types)===t,'Expression type differs from its use');
    switch(n.op) {
     case 'set': {keys(n,'id op variable value');const t=f.types.get(text(n.variable));need(t,'Unknown assigned variable');expect('value',t);break;}
     case 'if':keys(n,'id op test then else');expect('test','boolean');child('then');child('else');break;
     case 'repeat':keys(n,'id op count body');expect('count','number');child('body');break;
     case 'switch': {keys(n,'id op value cases default');const t=expr(n.value,f.types),values=new Set<unknown>();for(const value of array(n.cases,16)){const arm=obj(value);keys(arm,'value body');need(literal(arm.value)===t&&!values.has(arm.value),'Duplicate or differently typed case');values.add(arm.value);body(array(arm.body,128),f,depth+1);}child('default');break;}
     case 'call': {keys(n,'id op function args','result');const callee=functions.get(text(n.function));need(callee,'Unknown function');calls.get(f.source.name as string)!.add(n.function as string);const args=array(n.args,8),params=array(callee.source.parameters,8);need(args.length===params.length,'Wrong function argument count');args.forEach((a,i)=>need(expr(a,f.types)===obj(params[i]).type,'Function argument type differs'));if(Object.prototype.hasOwnProperty.call(n,'result')){const t=f.types.get(text(n.result));need(t&&t===callee.source.returns,'Invalid return destination');}break;}
     case 'return':keys(n,f.source.returns==='void'?'id op':'id op value');if(f.source.returns!=='void')expect('value',f.source.returns as ValueType);break;
     case 'invoke': {
      keys(n,'id op capability version arguments bindings');const capability=text(n.capability),args=obj(n.arguments);
      need(typeof n.version==='number','Capability version must be numeric');
      const error=validateCapabilityArguments(capability,n.version,args);need(!error,error??'Invalid capability arguments');
      need(capabilityResources(capability,args).every(id=>resources.has(id)),'Declare every action resource');
      for(const [key,value] of Object.entries(obj(n.bindings))){const t=capabilityParameterType(capability,key);need(t,'Unsupported capability argument binding');need(expr(value,f.types)===t,'Capability argument type differs');}break;
     }
     default:throw new Error('Unknown program block');
    }
   }
  };
  for(const f of functions.values()){const nodes=array(f.source.body,128);body(nodes,f);need(f.source.returns==='void'||returns(nodes),'A value-returning function must return on every path');}
  const visiting=new Set<string>(),depths=new Map<string,number>();
  const depth=(name:string):number=>{need(!visiting.has(name),'Recursive function calls are unsupported');const cached=depths.get(name);if(cached)return cached;visiting.add(name);let d=1;for(const c of calls.get(name)!)d=Math.max(d,1+depth(c));visiting.delete(name);need(d<=8,'Function call depth exceeds its limit');depths.set(name,d);return d;};
  for(const name of functions.keys())depth(name);
  return {program:root as unknown as BehaviourProgram,error:null};
 }catch(error){return {program:null,error:error instanceof Error?error.message:'Invalid program'};}
}
export function sequenceProgram(steps:RuleStep[]):BehaviourProgram {
 const resources=new Set<string>();const body:ProgramNode[]=steps.map((step,i)=>{
  const call=stepInvocation(step);if(step.action!==2)resources.add(step.targetId);if(step.propId)resources.add(step.propId);
  return {id:step.id||'action_'+(i+1),op:'invoke',capability:call.id,version:call.version,arguments:call.arguments,bindings:{}};
 });
 return {version:2,entry:'main',resources:[...resources],functions:[{name:'main',returns:'void',parameters:[],locals:[],body}]};
}
/** A detached action-only view for the simple editor, not another saved format.
 * Draft numeric values may be temporarily invalid; validSequence guards Apply.
 */
export function simpleProgramSteps(source:string):RuleStep[]|null {
 try {
  const p=JSON.parse(source) as BehaviourProgram;
  if(p.version!==2||p.functions.length!==1)return null;const f=p.functions[0];
  if(f.name!==p.entry||f.returns!=='void'||f.parameters.length||f.locals.length||f.body.some(n=>n.op!=='invoke'||Object.keys(n.bindings).length))return null;
  return f.body.map(n=>{if(n.op!=='invoke')throw new Error('Expected action');return invocationStep({id:n.capability,version:n.version,arguments:n.arguments},n.id);});
 }catch{return null;}
}
export function withSimpleProgramSteps(source:string,steps:RuleStep[]):string {
 const previous=simpleProgramSteps(source);if(previous===null)throw new Error('Edit this program in the function editor');
 const p=JSON.parse(source) as BehaviourProgram,next=sequenceProgram(steps),oldResources=new Set(sequenceProgram(previous).resources);
 p.resources=[...new Set([...p.resources.filter(id=>!oldResources.has(id)),...next.resources])];
 p.functions[0].body=next.functions[0].body;return JSON.stringify(p);
}
export function expressionLabel(e:Expression):string {
 if('value' in e)return JSON.stringify(e.value);if('var' in e)return e.var;if('fact' in e)return e.fact;
 const symbols:Record<string,string>={add:'+',sub:'−',mul:'×',div:'÷',mod:'mod',lt:'<',le:'≤',gt:'>',ge:'≥',eq:'=',ne:'≠',and:'and',or:'or'};
 return e.op==='not'?`not (${expressionLabel(e.args[0])})`:`(${expressionLabel(e.args[0])} ${symbols[e.op]??e.op} ${expressionLabel(e.args[1])})`;
}

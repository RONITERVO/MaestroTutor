import {linkProgram,compiledProgramName,type ProgramImport} from './programModules';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourEvent,eventFieldType,validateEventArguments,eventArgumentType} from '../../../shared/behaviourEvents';
import {type RuleStep} from './ruleSteps';
import {behaviourFactTypes,behaviourFact} from '../../../shared/behaviourCatalog';
import {validateFactArguments,factArgumentType} from '../../../shared/behaviourFacts';
import {validateCapabilityArguments,capabilityParameterType,argumentValue,capabilityOutputType,literalCapabilityResources} from '../../../shared/capabilities';
import {stepInvocation,invocationStep} from './capabilitySteps';
import {checkedDataValue,readDataType,sameDataType,dataOperationType,type DataValue,type DataType,type ScalarType} from '../../../shared/programValues';
export type Value=DataValue;
export type ValueType=DataType;
export type {ScalarType};
export type Expression={value:Value;type?:ValueType}|{var:string}|{state:string}|{fact:string;version?:number;arguments?:Record<string,unknown>;bindings?:Record<string,Expression>}|{op:string;args:Expression[]};
export interface ProgramCall {module?:string;function:string;args:Expression[];result?:string}
export type ProgramNode={id:string}&(
 {op:'awaitCondition';test:Expression;transition:'true'|'false'|'either';initial:'baseline'|'report';stableSeconds:Expression;timeout:Expression;received:string;value:string}|
 {op:'set'|'setState';variable:string;value:Expression}|{op:'forever';body:ProgramNode[]}|{op:'sleep';seconds:Expression}|{op:'awaitEvent';event:string;source:string;timeout:Expression;received:string;value:string;fields?:Record<string,string>;version?:number;arguments?:Record<string,unknown>;bindings?:Record<string,Expression>}|{op:'emitEvent';event:string;value:Expression}|{op:'if';test:Expression;then:ProgramNode[];else:ProgramNode[]}|
 {op:'repeat';count:Expression;body:ProgramNode[]}|{op:'switch';value:Expression;cases:{value:Value;body:ProgramNode[]}[];default:ProgramNode[]}|
 {op:'parallel';branches:ProgramCall[]}|{op:'call';module?:string;function:string;args:Expression[];result?:string}|{op:'return';value?:Expression}|
 {op:'invoke';capability:string;version:number;arguments:Record<string,unknown>;bindings:Record<string,Expression>;results?:Record<string,string>});
export interface ProgramFunction {name:string;returns:ValueType|'void';parameters:{name:string;type:ValueType}[];locals:{name:string;initial:Value;type?:ValueType}[];body:ProgramNode[]}
export interface BehaviourProgram {version:2|3;parallelVersion?:1;dataVersion?:1;moduleVersion?:1;imports?:ProgramImport[];entry:string;resources:string[];functions:ProgramFunction[];state?:{name:string;initial:Value;type?:ValueType}[];events?:{name:string;type:ScalarType}[]}
export const programFacts=behaviourFactTypes;
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
function need(condition:unknown,message:string):asserts condition {if(!condition)throw new Error(message);}
const obj=(v:unknown)=>{need(record(v),'Expected an object');return v;};
const array=(v:unknown,max:number)=>{need(Array.isArray(v)&&v.length<=max,'Missing or oversized list');return v as unknown[];};
const text=(v:unknown)=>{need(typeof v==='string','Expected text');return v;};
const name=(v:unknown)=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
const target=(v:unknown)=>v==='maestro'||v==='book'||typeof v==='string'&&/^[a-fA-F0-9]{32}$/.test(v);
function keys(v:Record<string,unknown>,required:string,optional='') {const a=required.split(' ').filter(Boolean),b=[...a,...optional.split(' ')];need(a.every(k=>Object.prototype.hasOwnProperty.call(v,k))&&Object.keys(v).every(k=>b.includes(k)),'Missing or unknown program field');}
function type(v:unknown):ValueType|'void' {return v==='void'?'void':readDataType(v);}
const literal=(v:unknown,declared?:unknown):ValueType=>checkedDataValue(v,declared);
/** Reject ambiguous duplicate keys and excessive JSON depth before typed validation. */
export function strictProgramJson(source:string):unknown {
 const result:unknown=JSON.parse(source);const tokens=source.match(/"(?:[^"\\]|\\.)*"|[{}\[\]:,]|[^\s{}\[\]:,]+/g)??[];let at=0;
 const read=(depth:number)=>{need(depth<=48,'JSON nesting limit exceeded');const token=tokens[at++];
  if(token==='{') {const seen=new Set<string>();if(tokens[at]!=='}')do {const key=JSON.parse(tokens[at++]) as string;need(!seen.has(key),'Duplicate program field');seen.add(key);at++;read(depth+1);}while(tokens[at++]===',');else at++;}
  else if(token==='[') {if(tokens[at]!==']')do {read(depth+1);}while(tokens[at++]===',');else at++;}
 };read(0);return result;
}
/** Authoring validator only. Unity is the sole program executor. Shared fixtures cover both validators. */
export function parseProgram(source:unknown):{program:BehaviourProgram|null;linked?:BehaviourProgram;error:string|null} {
 try {need(typeof source==='string'&&source.length<=24000,'Program exceeds its size limit');const raw=obj(strictProgramJson(source));const linked=linkProgram(raw,validateProgram);return {program:raw as unknown as BehaviourProgram,linked,error:null};}
 catch(error){return {program:null,error:error instanceof Error?error.message:'Invalid program'};}
}
function validateProgram(root:Record<string,unknown>):void {
  need(root.version===2||root.version===3,'Unsupported program version');keys(root,root.version===3?'version entry resources functions state events':'version entry resources functions','dataVersion parallelVersion');need(root.parallelVersion===undefined||root.version===3&&root.parallelVersion===1,'Unsupported parallel-program version');need(root.dataVersion===undefined||root.version===3&&root.dataVersion===1,'Unsupported structured-value version');
  const state=new Map<string,ValueType>(),events=new Map<string,ScalarType>();
  const supported=(t:ValueType|'void')=>{need(root.version===3&&root.dataVersion===1||typeof t==='string','Structured values need version 3 and dataVersion 1');return t;};
  if(root.version===3){
   for(const value of array(root.state,16)){const v=obj(value);keys(v,'name initial','type');need(compiledProgramName(v.name)&&!state.has(v.name as string),'Invalid or duplicate state name');need(v.type===undefined||root.dataVersion===1,'Explicit value types need dataVersion 1');const t=literal(v.initial,v.type);supported(t);state.set(v.name as string,t);}
   for(const value of array(root.events,16)){const v=obj(value);keys(v,'name type');const t=type(v.type);need(typeof v.name==='string'&&/^user\.[a-zA-Z0-9_]{1,32}$/.test(v.name)&&!events.has(v.name)&&t!=='void'&&typeof t==='string','Invalid or duplicate custom event');events.set(v.name,t as ScalarType);}
  }
  const resources=new Set<string>();for(const value of array(root.resources,16)){need(target(value)&&!resources.has(value as string),'Invalid or duplicate resource');resources.add(value as string);}
  const functions=new Map<string,{source:Record<string,unknown>;types:Map<string,ValueType>}>(),calls=new Map<string,Set<string>>();
  for(const value of array(root.functions,16)) {
   const f=obj(value);keys(f,'name returns parameters locals body');const id=text(f.name);need(compiledProgramName(id)&&!functions.has(id),'Invalid or duplicate function name');supported(type(f.returns));array(f.body,128);
   const types=new Map<string,ValueType>();
   for(const value of array(f.parameters,8)){const p=obj(value);keys(p,'name type');const t=supported(type(p.type));need(name(p.name)&&!types.has(p.name as string)&&t!=='void','Invalid or duplicate parameter');types.set(p.name as string,t);}
   for(const value of array(f.locals,16)){const p=obj(value);keys(p,'name initial','type');need(name(p.name)&&!types.has(p.name as string),'Invalid or duplicate local');need(p.type===undefined||root.dataVersion===1,'Explicit value types need dataVersion 1');const t=literal(p.initial,p.type);supported(t);types.set(p.name as string,t);}
   functions.set(id,{source:f,types});calls.set(id,new Set());
  }
  const entry=functions.get(text(root.entry));need(entry&&array(entry.source.parameters,8).length===0,'Entry must name a function with no parameters');
  const ids=new Set<string>();let expressions=0;
  const expr=(value:unknown,types:Map<string,ValueType>,depth=0):ValueType=>{
   need(depth<=8&&++expressions<=512,'Expression limit exceeded');const e=obj(value);
   if(Object.prototype.hasOwnProperty.call(e,'value')){keys(e,'value','type');need(e.type===undefined||root.dataVersion===1,'Explicit value types need dataVersion 1');const t=literal(e.value,e.type);supported(t);return t;}
   if(Object.prototype.hasOwnProperty.call(e,'var')){keys(e,'var');const t=types.get(text(e.var));need(t,'Unknown variable');return t;}
   if(Object.prototype.hasOwnProperty.call(e,'state')){keys(e,'state');const t=state.get(text(e.state));need(root.version===3&&t,'Unknown program state');return t;}
   if(Object.prototype.hasOwnProperty.call(e,'fact')){
    const id=text(e.fact),fact=behaviourFact(id);need(fact,'Unknown room fact');
    if(fact.input){need(root.version===3,'Fact queries need program version 3');keys(e,'fact version arguments bindings');need(typeof e.version==='number'&&Number.isInteger(e.version),'Invalid fact version');const error=validateFactArguments(id,e.version,obj(e.arguments));need(!error,error??'Invalid fact query');for(const [path,value] of Object.entries(obj(e.bindings))){const t=factArgumentType(id,path,obj(e.arguments));need(t&&expr(value,types,depth+1)===t,'Invalid fact argument binding');}}
    else keys(e,'fact');supported(fact.type);return fact.type;
   }
   keys(e,'op args');const op=text(e.op),args=array(e.args,3);need(args.length>0,'Invalid expression argument count');const ts=args.map(a=>expr(a,types,depth+1));
   const dataType=dataOperationType(op,ts,args.length>1?obj(args[1]).value:undefined);if(dataType){need(root.version===3&&root.dataVersion===1,'Structured values need version 3 and dataVersion 1');return dataType;}
   need(args.length===(op==='not'?1:2),'Invalid expression argument count');
   if(['not','and','or'].includes(op)){need(ts.every(t=>t==='boolean'),'Logic needs booleans');return 'boolean';}
   if(['eq','ne'].includes(op)){need(sameDataType(ts[0],ts[1]),'Comparison types differ');return 'boolean';}
   need(['add','sub','mul','div','mod','lt','le','gt','ge'].includes(op)&&ts.every(t=>t==='number'),'Unknown operation or nonnumeric argument');return ['lt','le','gt','ge'].includes(op)?'boolean':'number';
  };
  const returns=(nodes:unknown[]):boolean=>nodes.some(value=>{const n=obj(value);return n.op==='return'||n.op==='if'&&returns(n.then as unknown[])&&returns(n.else as unknown[])||n.op==='switch'&&returns(n.default as unknown[])&&(n.cases as {body:unknown[]}[]).every(c=>returns(c.body));});
  const body=(nodes:unknown[],f:{source:Record<string,unknown>;types:Map<string,ValueType>},depth=0)=>{
   need(depth<=8,'Block nesting limit exceeded');for(const value of nodes) {
    const n=obj(value);need(compiledProgramName(n.id)&&!ids.has(n.id as string)&&ids.size<128,'Invalid, duplicate or excessive block identities');ids.add(n.id as string);
    const child=(key:string)=>body(array(n[key],128),f,depth+1),expect=(key:string,t:ValueType)=>need(sameDataType(expr(n[key],f.types),t),'Expression type differs from its use');
    switch(n.op) {
     case 'setState': {need(root.version===3,'State needs program version 3');keys(n,'id op variable value');const t=state.get(text(n.variable));need(t,'Unknown program state');expect('value',t);break;}
     case 'forever':need(root.version===3,'Events need program version 3');keys(n,'id op body');child('body');break;
     case 'sleep':need(root.version===3,'Timers need program version 3');keys(n,'id op seconds');expect('seconds','number');break;
     case 'awaitCondition': {
      need(root.version===3,'Conditions need program version 3');keys(n,'id op test transition initial stableSeconds timeout received value');
      need(['true','false','either'].includes(text(n.transition))&&['baseline','report'].includes(text(n.initial)),'Invalid condition transition or initial policy');
      need(f.types.get(text(n.received))==='boolean'&&f.types.get(text(n.value))==='boolean'&&n.received!==n.value,'Condition destinations need distinct boolean locals');
      expect('test','boolean');expect('stableSeconds','number');expect('timeout','number');break;
     }
     case 'awaitEvent': {
      need(root.version===3,'Events need program version 3');keys(n,'id op event source timeout received value','fields version arguments bindings');const eventName=text(n.event),definition=behaviourEvent(eventName),t=events.get(eventName)??(definition?'text':null);need(t,'Unknown event');
      const source=text(n.source);need(source===''||definition?.objectEvent&&target(source),'Only object events accept a source');
      if(definition?.input) {
       need(typeof n.version==='number'&&Number.isInteger(n.version)&&n.version===definition.version,'Unsupported event subscription version');
       const error=validateEventArguments(eventName,n.version,obj(n.arguments));need(!error,error??'Invalid event arguments');
       for(const [path,value] of Object.entries(obj(n.bindings))){const type=eventArgumentType(eventName,path,obj(n.arguments));need(type,'Unsupported event argument binding');need(expr(value,f.types)===type,'Event argument type differs');}
      } else need(n.version===undefined&&n.arguments===undefined&&n.bindings===undefined,'This event has no subscription arguments');
      need(f.types.get(text(n.received))==='boolean','Event received needs a boolean local');need(f.types.get(text(n.value))===t,'Event value needs a matching local');need(n.received!==n.value,'Event destinations must differ');
      if(n.fields!==undefined){const assigned=new Set([n.received,n.value]);for(const [key,destination] of Object.entries(obj(n.fields))){const fieldType=eventFieldType(eventName,key);need(fieldType&&typeof destination==='string'&&f.types.get(destination)===fieldType&&!assigned.has(destination),'Invalid or duplicate event field destination');assigned.add(destination);}}
      expect('timeout','number');break;
     }
     case 'emitEvent': {need(root.version===3,'Events need program version 3');keys(n,'id op event value');const t=events.get(text(n.event));need(t,'Only declared custom events may be emitted');expect('value',t);break;}
     case 'set': {keys(n,'id op variable value');const t=f.types.get(text(n.variable));need(t,'Unknown assigned variable');expect('value',t);break;}
     case 'if':keys(n,'id op test then else');expect('test','boolean');child('then');child('else');break;
     case 'repeat':keys(n,'id op count body');expect('count','number');child('body');break;
     case 'switch': {keys(n,'id op value cases default');const t=expr(n.value,f.types),values=new Set<unknown>();need(typeof t==='string','Cases require a scalar value');for(const value of array(n.cases,16)){const arm=obj(value);keys(arm,'value body');need(literal(arm.value)===t&&!values.has(arm.value),'Duplicate or differently typed case');values.add(arm.value);body(array(arm.body,128),f,depth+1);}child('default');break;}
     case 'parallel': {
      need(root.parallelVersion===1,'Parallel calls need parallelVersion 1');keys(n,'id op branches');const branches=array(n.branches,4);need(branches.length>=2,'Parallel needs two to four branches');const destinations=new Set<string>();
      for(const token of branches){const branch=obj(token);keys(branch,'function args','result');const callee=functions.get(text(branch.function));need(callee,'Unknown parallel function');calls.get(f.source.name as string)!.add(branch.function as string);const args=array(branch.args,8),params=array(callee.source.parameters,8);need(args.length===params.length,'Wrong parallel argument count');args.forEach((a,i)=>need(sameDataType(expr(a,f.types),readDataType(obj(params[i]).type)),'Parallel argument type differs'));if(branch.result!==undefined){const destination=text(branch.result),t=f.types.get(destination);need(!destinations.has(destination)&&t&&sameDataType(t,type(callee.source.returns)),'Invalid or duplicate parallel result destination');destinations.add(destination);}}
      break;
     }
     case 'call': {keys(n,'id op function args','result');const callee=functions.get(text(n.function));need(callee,'Unknown function');calls.get(f.source.name as string)!.add(n.function as string);const args=array(n.args,8),params=array(callee.source.parameters,8);need(args.length===params.length,'Wrong function argument count');args.forEach((a,i)=>need(sameDataType(expr(a,f.types),readDataType(obj(params[i]).type)),'Function argument type differs'));if(Object.prototype.hasOwnProperty.call(n,'result')){const t=f.types.get(text(n.result));need(t&&sameDataType(t,type(callee.source.returns)),'Invalid return destination');}break;}
     case 'return':keys(n,f.source.returns==='void'?'id op':'id op value');if(f.source.returns!=='void')expect('value',f.source.returns as ValueType);break;
     case 'invoke': {
      keys(n,'id op capability version arguments bindings','results');const capability=text(n.capability),args=obj(n.arguments);
      need(typeof n.version==='number','Capability version must be numeric');
      const error=validateCapabilityArguments(capability,n.version,args);need(!error,error??'Invalid capability arguments');
      need(literalCapabilityResources(capability,args,obj(n.bindings),root.version as number).every(id=>resources.has(id)),'Declare every action resource');
      if(n.results!==undefined) {
       need(root.version===3,'Action results need program version 3');const assigned=new Set<string>();
       for(const [key,destination] of Object.entries(obj(n.results))){const t=capabilityOutputType(capability,key);need(t&&typeof destination==='string'&&f.types.get(destination)===t&&!assigned.has(destination),'Invalid or duplicate action result destination');assigned.add(destination as string);}
      }
      for(const [key,value] of Object.entries(obj(n.bindings))){const t=capabilityParameterType(capability,key,obj(n.arguments));need(t,'Unsupported capability argument binding');need(argumentValue(n.arguments,key)!==undefined,'A bound argument needs a literal placeholder');need(expr(value,f.types)===t,'Capability argument type differs');}break;
     }
     default:throw new Error('Unknown program block');
    }
   }
  };
  for(const f of functions.values()){const nodes=array(f.source.body,128);body(nodes,f);need(f.source.returns==='void'||returns(nodes),'A value-returning function must return on every path');}
  const visiting=new Set<string>(),depths=new Map<string,number>();
  const depth=(name:string):number=>{need(!visiting.has(name),'Recursive function calls are unsupported');const cached=depths.get(name);if(cached)return cached;visiting.add(name);let d=1;for(const c of calls.get(name)!)d=Math.max(d,1+depth(c));visiting.delete(name);need(d<=8,'Function call depth exceeds its limit');depths.set(name,d);return d;};
  for(const name of functions.keys())depth(name);
}
export function sequenceProgram(steps:RuleStep[]):BehaviourProgram {
 const resources=new Set<string>();const body:ProgramNode[]=steps.map((step,i)=>{
  const call=stepInvocation(step);if(![2,12,13].includes(step.action))resources.add(step.targetId);if(step.propId)resources.add(step.propId);
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
  if(f.name!==p.entry||f.returns!=='void'||f.parameters.length||f.locals.length||f.body.some(n=>n.op!=='invoke'||Object.keys(n.bindings).length||n.results!==undefined))return null;
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
 if('value' in e)return JSON.stringify(e.value);if('var' in e)return e.var;if('state' in e)return 'state.'+e.state;if('fact' in e)return e.fact;
 const symbols:Record<string,string>={add:'+',sub:'−',mul:'×',div:'÷',mod:'mod',lt:'<',le:'≤',gt:'>',ge:'≥',eq:'=',ne:'≠',and:'and',or:'or'};
 if(['length','at','append','replace','remove','field','withField'].includes(e.op))return e.op+'('+e.args.map(expressionLabel).join(', ')+')';
 return e.op==='not'?`not (${expressionLabel(e.args[0])})`:`(${expressionLabel(e.args[0])} ${symbols[e.op]??e.op} ${expressionLabel(e.args[1])})`;
}

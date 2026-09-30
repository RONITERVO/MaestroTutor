// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {sha256} from '@noble/hashes/sha2.js';
import type {BehaviourProgram,ProgramFunction} from './programs';
import {visitProgramNodes,visitNodeExpressions} from './programTraversal';
export interface ProgramModule {version:1;name:string;exports:string[];program:BehaviourProgram}
export interface ProgramImport {alias:string;hash:string;module:ProgramModule;signals:Record<string,string>}
const own=(v:object,k:PropertyKey)=>Object.prototype.hasOwnProperty.call(v,k);
const need=(ok:unknown,message:string)=>{if(!ok)throw new Error(message);};
const plain=(s:unknown):s is string=>typeof s==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(s);
export const compiledProgramName=(s:unknown)=>typeof s==='string'&&/^[a-zA-Z0-9_]{1,32}(\.[a-zA-Z0-9_]{1,32}){0,3}$/.test(s);
const object=(v:unknown):Record<string,unknown>=>{need(v!==null&&typeof v==='object'&&!Array.isArray(v),'Expected a module object');return v as Record<string,unknown>;};
const list=(v:unknown,max:number):unknown[]=>{need(Array.isArray(v)&&v.length<=max,'Missing or oversized module list');return v as unknown[];};
function fields(value:Record<string,unknown>,keys:string[]){need(Object.keys(value).length===keys.length&&keys.every(k=>own(value,k)),'Missing or unknown module field');}
/** Versioned canonical encoding: UTF-16 strings, ordinal keys, IEEE-754 numbers. Not authentication. */
export function moduleHash(module:unknown):string {
 let nodes=0;
 const encode=(v:unknown,depth=0):string=>{
  need(depth<=48&&++nodes<=32768,'Module hash input limit exceeded');
  if(v===null)return 'N';if(typeof v==='boolean')return v?'T':'F';
  if(typeof v==='number'){need(Number.isFinite(v),'Module numbers must be finite');const b=new DataView(new ArrayBuffer(8));b.setFloat64(0,v===0?0:v);return 'D'+Array.from(new Uint8Array(b.buffer),x=>x.toString(16).padStart(2,'0')).join('');}
  if(typeof v==='string'){let result='S'+v.length+':';for(let i=0;i<v.length;i++)result+=v.charCodeAt(i).toString(16).padStart(4,'0');return result;}
  if(Array.isArray(v))return 'A'+v.length+'['+v.map(x=>encode(x,depth+1)).join('')+']';
  const o=object(v),keys=Object.keys(o).sort();return 'O'+keys.length+'{'+keys.map(k=>encode(k,depth+1)+encode(o[k],depth+1)).join('')+'}';
 };
 return Array.from(sha256(new TextEncoder().encode('Maestro.Module.v1\n'+encode(module))),b=>b.toString(16).padStart(2,'0')).join('');
}
/** Each scope is validated before inclusion; final validation enforces combined limits. Source is never mutated. */
export function linkProgram(source:Record<string,unknown>,validate:(value:Record<string,unknown>)=>void):BehaviourProgram {
 let instances=0;
 const scope=(raw:Record<string,unknown>,depth:number):BehaviourProgram=>{
  need(depth<=3,'Module nesting exceeds three levels');
  const p=JSON.parse(JSON.stringify(raw)) as BehaviourProgram;
  const imports=new Map<string,{item:ProgramImport;linked:BehaviourProgram}>();
  if(own(raw,'moduleVersion')||own(raw,'imports')){
   need(raw.version===3&&raw.moduleVersion===1&&own(raw,'imports'),'Modules need version 3 and moduleVersion 1');
   for(const token of list(raw.imports,4)){
    need(++instances<=4,'A program may include at most four module instances');
    const imp=object(token);fields(imp,['alias','hash','module','signals']);need(plain(imp.alias)&&!imports.has(imp.alias),'Invalid or duplicate module alias');
    const m=object(imp.module);fields(m,['version','name','exports','program']);need(m.version===1&&typeof m.name==='string'&&m.name.trim().length>0&&m.name.length<=64&&!/[\u0000-\u001f\u007f-\u009f]/.test(m.name),'Invalid module definition');
    need(typeof imp.hash==='string'&&/^[a-f0-9]{64}$/.test(imp.hash)&&moduleHash(m)===imp.hash,'Module content does not match its pinned hash');
    const child=object(m.program);need(child.version===3,'A module needs program version 3');
    const exports=list(m.exports,16);need(exports.length>0&&exports.every(plain)&&new Set(exports).size===exports.length,'Invalid module exports');
    need(exports.every(n=>list(child.functions,16).some(f=>object(f).name===n)),'Export must name a local function');
    need(list(child.resources,16).every(r=>list(raw.resources,16).includes(r)),'Declare every imported module resource in its caller');
    need(child.dataVersion===undefined||raw.dataVersion===1,'Caller must enable imported structured values');
    const linked=scope(child,depth+1),signals=object(imp.signals),events=list(raw.events,16).map(object);
    need(Object.keys(signals).length===(linked.events??[]).length,'Connect every module signal explicitly');
    for(const event of linked.events??[])need(own(signals,event.name)&&events.some(e=>e.name===signals[event.name]&&e.type===event.type),'Module signal needs a matching caller declaration');
    imports.set(imp.alias as string,{item:imp as unknown as ProgramImport,linked});
   }
  }
  // Validate identifiers in source before generating qualified internal identifiers.
  need(plain(p.entry),'Entry must use a local function name');
  for(const state of list(raw.state??[],16)){const s=object(state);need(plain(s.name),'State names must be local');}
  for(const token of list(raw.functions,16)){
   const f=object(token);need(plain(f.name),'Function names must be local');list(f.body,128);
   visitProgramNodes((f as unknown as ProgramFunction).body,n=>{
    need(plain(n.id),'Block identities must be local');
    visitNodeExpressions(n,e=>{if('state' in e)need(plain(e.state),'State references must be local');});
    if(n.op==='setState')need(plain(n.variable),'State destinations must be local');
   });
  }
  for(const f of p.functions)visitProgramNodes(f.body,n=>{
   if(n.op!=='call')return;need(plain(n.function),'Call functions must be local names');
   if(own(n,'module')){need(plain(n.module),'Invalid module alias');const imported=imports.get(n.module!);need(imported&&imported.item.module.exports.includes(n.function),'Unknown module or unexported function');n.function=n.module+'.'+n.function;delete n.module;}
  });
  for(const [alias,{item,linked}] of imports){
   const prefix=alias+'.';
   for(const state of linked.state??[])state.name=prefix+state.name;
   for(const f of linked.functions){f.name=prefix+f.name;visitProgramNodes(f.body,n=>{
    n.id=prefix+n.id;visitNodeExpressions(n,e=>{if('state' in e)e.state=prefix+e.state;});
    if(n.op==='setState')n.variable=prefix+n.variable;if(n.op==='call')n.function=prefix+n.function;
    if((n.op==='awaitEvent'||n.op==='emitEvent')&&own(item.signals,n.event))n.event=item.signals[n.event];
   });}
   p.functions.push(...linked.functions);p.state!.push(...linked.state!);
  }
  delete p.moduleVersion;delete p.imports;validate(p as unknown as Record<string,unknown>);return p;
 };
 return scope(source,0);
}
export function programCallables(program:BehaviourProgram):{key:string;module?:string;fn:ProgramFunction}[]{
 return [...program.functions.map(fn=>({key:fn.name,fn})),...(program.imports??[]).flatMap(i=>i.module.program.functions.filter(f=>i.module.exports.includes(f.name)).map(fn=>({key:i.alias+'.'+fn.name,module:i.alias,fn})))];
}

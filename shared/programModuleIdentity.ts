import {programResourceLimit} from './programLimits';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {sha256} from '@noble/hashes/sha2.js';
export interface ModuleRecord {version:1;name:string;exports:string[];program:Record<string,unknown>}
const need=(ok:unknown,message:string)=>{if(!ok)throw new Error(message);};
const object=(v:unknown):Record<string,unknown>=>{need(v!==null&&typeof v==='object'&&!Array.isArray(v),'Expected a module object');return v as Record<string,unknown>;};
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
/** Wire identity/shape only. Full program validation happens when preparing an import. */
export function validModuleRecord(value:unknown,hash:string):value is ModuleRecord {
 try {const m=object(value),p=object(m.program);return Object.keys(m).length===4&&m.version===1&&typeof m.name==='string'&&m.name.trim().length>0&&m.name.length<=64&&!/[\u0000-\u001f\u007f-\u009f]/.test(m.name)&&Array.isArray(m.exports)&&m.exports.length>0&&m.exports.length<=16&&m.exports.every(n=>typeof n==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(n))&&new Set(m.exports).size===m.exports.length&&p.version===3&&Array.isArray(p.resources)&&p.resources.length<=programResourceLimit&&p.resources.every(id=>typeof id==='string'&&/^(maestro|book|[a-fA-F0-9]{32})$/.test(id))&&Array.isArray(p.events)&&p.events.length<=16&&p.events.every(e=>{const event=object(e);return Object.keys(event).length===2&&typeof event.name==='string'&&/^user\.[a-zA-Z0-9_]{1,32}$/.test(event.name)&&['number','text','boolean'].includes(event.type as string);})&&JSON.stringify(m).length<=24000&&moduleHash(m)===hash;}catch{return false;}
}

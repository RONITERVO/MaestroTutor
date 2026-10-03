// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {checkedDataValue,readDataType} from '../../../shared/programValues';
import {strictProgramJson} from './programs';
export interface ProgramMemoryCell {id:string;name:string;typeJson:string;valueJson:string;saved:boolean;declared:boolean}
export interface ProgramMemoryView {ready:boolean;pending:boolean;busy:boolean;temporary?:boolean;sessionId?:string;error:string;revision:string;programId:string;page:number;count:number;programs:{id:string;name:string;cells:number}[];cells:ProgramMemoryCell[]}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const text=(v:unknown,max:number):v is string=>typeof v==='string'&&v.length<=max;
const id=(v:unknown)=>typeof v==='string'&&/^[a-fA-F0-9]{32}$/.test(v);
const integer=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=max;
export function validProgramMemoryView(v:unknown):v is ProgramMemoryView {
 if(!record(v)||!['ready','pending','busy'].every(k=>typeof v[k]==='boolean')||!text(v.error,2048)||!text(v.revision,32)||!/^$|^initial$|^[a-f0-9]{32}$/.test(v.revision)||!(v.programId===''||id(v.programId))||!integer(v.page,0,15)||!integer(v.count,0,48))return false;
 if((v.sessionId!==undefined||v.temporary!==undefined)&&(!id(v.sessionId)||typeof v.temporary!=='boolean'))return false;
 if(!Array.isArray(v.programs)||v.programs.length>64||!v.programs.every(p=>record(p)&&id(p.id)&&text(p.name,64)&&integer(p.cells,1,32))||new Set(v.programs.map(p=>p.id)).size!==v.programs.length||v.programs.reduce((sum,p)=>sum+p.cells,0)>512)return false;
 if(!Array.isArray(v.cells)||v.cells.length>4||v.cells.length>v.count||new Set(v.cells.map(c=>record(c)?c.id:null)).size!==v.cells.length)return false;
 return v.cells.every(c=>{
  if(!record(c)||!text(c.id,32)||!/^[a-f0-9]{32}$/.test(c.id)||!text(c.name,32)||!/^[a-zA-Z0-9_]{1,32}$/.test(c.name)||typeof c.saved!=='boolean'||typeof c.declared!=='boolean'||!text(c.typeJson,2048)||!text(c.valueJson,8192))return false;
  try{const type=readDataType(strictProgramJson(c.typeJson));checkedDataValue(strictProgramJson(c.valueJson),type);return true;}catch{return false;}
 });
}

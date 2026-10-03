// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {type DataType,type DataValue,defaultDataValue,dataTypeLabel} from '../../../shared/programValues';
export function ProgramDataTypeEditor({value,label,onChange,allowVoid=false,structured=true,depth=0}:{value:DataType|'void';label:string;onChange:(type:DataType|'void')=>void;allowVoid?:boolean;structured?:boolean;depth?:number}) {
 const [field,setField]=useState('');const kind=typeof value==='string'?value:'list' in value?'list':'record';
 const fields=typeof value==='object'&&'record' in value?value.record:null;
 const validField=/^[a-zA-Z0-9_]{1,32}$/.test(field)&&!['__proto__','constructor','prototype'].includes(field)&&!Object.prototype.hasOwnProperty.call(fields??{},field);
 return <div><label>{label}<select aria-label={label} value={kind} onChange={e=>onChange(e.target.value==='list'?{list:'text'}:e.target.value==='record'?{record:{}}:e.target.value as DataType|'void')}>
  {allowVoid&&<option value="void">Nothing</option>}{['number','text','boolean'].map(t=><option key={t} value={t}>{t}</option>)}
  <option value="list" disabled={!structured||depth>=4}>List</option><option value="record" disabled={!structured||depth>=4}>Record</option>
 </select></label>
 {typeof value==='object'&&'list' in value&&<ProgramDataTypeEditor value={value.list} label={label+' item type'} depth={depth+1} structured={structured} onChange={t=>onChange({list:t as DataType})}/>}
 {fields&&<fieldset><legend>{label} fields</legend>{Object.entries(fields).map(([name,type])=><div key={name}>
  <ProgramDataTypeEditor value={type} label={label+' '+name+' type'} depth={depth+1} structured={structured} onChange={t=>onChange({record:{...fields,[name]:t as DataType}})}/>
  <button onClick={()=>onChange({record:Object.fromEntries(Object.entries(fields).filter(([k])=>k!==name))})}>Remove {label} field {name}</button>
 </div>)}
 <label>New field name<input aria-label={label+' new field name'} maxLength={32} value={field} onChange={e=>setField(e.target.value)}/></label>
 <button disabled={!validField||Object.keys(fields).length>=8||depth>=4} onClick={()=>{onChange({record:{...fields,[field]:'number'}});setField('');}}>Add {label} field</button>
 </fieldset>}
 </div>;
}
export function ProgramDataValueEditor({value,type,label,onChange}:{value:DataValue;type:DataType;label:string;onChange:(value:DataValue)=>void}) {
 if(typeof type==='object')return <fieldset><legend>{label} ({dataTypeLabel(type)})</legend>{'list' in type?<>
  {(Array.isArray(value)?value:[]).map((v,i)=><div key={i}><ProgramDataValueEditor value={v} type={type.list} label={label+' item '+(i+1)} onChange={v=>onChange((value as DataValue[]).map((old,j)=>i===j?v:old))}/><button onClick={()=>onChange((value as DataValue[]).filter((_,j)=>j!==i))}>Remove {label} item {i+1}</button></div>)}
  <button disabled={(value as DataValue[]).length>=32} onClick={()=>onChange([...(value as DataValue[]),defaultDataValue(type.list)])}>Add {label} item</button>
 </>:Object.entries(type.record).map(([k,t])=><ProgramDataValueEditor key={k} value={(value as Record<string,DataValue>)[k]} type={t} label={label+' '+k} onChange={v=>onChange({...value as Record<string,DataValue>,[k]:v})}/>)}</fieldset>;
 return <label>{label}{type==='boolean'?<select aria-label={label} value={String(value)} onChange={e=>onChange(e.target.value==='true')}><option value="false">False</option><option value="true">True</option></select>:<input aria-label={label} type={type==='number'?'number':'text'} step="any" maxLength={128} value={String(value)} onChange={e=>onChange(type==='number'?Number(e.target.value):e.target.value)}/>}</label>;
}

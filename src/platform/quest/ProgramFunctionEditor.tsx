// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {ValueType} from '../../core-sdk/room/programs';
import {type FunctionDraft,initialValue} from './programFunctionEditing';
const types=['number','text','boolean'] as const;
export function ProgramFunctionEditor({value,onChange,entry}:{value:FunctionDraft;onChange:(draft:FunctionDraft)=>void;entry:boolean}) {
 const typeSelect=(label:string,type:string,change:(type:ValueType)=>void)=><label>{label}<select aria-label={label} value={type} onChange={e=>change(e.target.value as ValueType)}>{types.map(t=><option key={t} value={t}>{t}</option>)}</select></label>;
 const freshName=(prefix:string)=>{let i=1;while([...value.parameters,...value.locals].some(p=>p.name===prefix+i))i++;return prefix+i;};
 return <div className="program-function-editor">
  <label>Function name<input aria-label="Function name" maxLength={32} value={value.name} onChange={e=>onChange({...value,name:e.target.value})}/></label>
  <label>Returns<select aria-label="Function return type" value={value.returns} onChange={e=>onChange({...value,returns:e.target.value as FunctionDraft['returns']})}><option value="void">Nothing</option>{types.map(t=><option key={t} value={t}>{t}</option>)}</select></label>
  <p>Names use letters, numbers and underscores. Renames update references. Existing calculations stay intact; incompatible edits are rejected. A typed function without a return gets a visible default return block.</p>
  <fieldset><legend>Parameters</legend><p>New parameters start with 0, false or empty text at every call. Removing a parameter removes that argument at every call. A parameter still used by this function cannot be removed.</p>
   {value.parameters.map((p,i)=><div key={i}>
    <label>Parameter {i+1} name<input aria-label={'Parameter '+(i+1)+' name'} maxLength={32} value={p.name} onChange={e=>onChange({...value,parameters:value.parameters.map((v,j)=>i===j?{...v,name:e.target.value}:v)})}/></label>
    {typeSelect('Parameter '+(i+1)+' type',p.type,type=>onChange({...value,parameters:value.parameters.map((v,j)=>i===j?{...v,type}:v)}))}
    <div className="room-workspace-actions"><button disabled={i===0} onClick={()=>{const parameters=[...value.parameters];[parameters[i-1],parameters[i]]=[parameters[i],parameters[i-1]];onChange({...value,parameters});}}>Move parameter {i+1} up</button><button onClick={()=>onChange({...value,parameters:value.parameters.filter((_,j)=>j!==i)})}>Remove parameter {i+1}</button></div>
   </div>)}
   <button disabled={entry||value.parameters.length>=8} onClick={()=>onChange({...value,parameters:[...value.parameters,{origin:null,name:freshName('input_'),type:'number'}]})}>Add parameter</button>
   {entry&&<p>The starting function has no parameters.</p>}
  </fieldset>
  <fieldset><legend>Local variables</legend><p>Each call starts with these values. Removing a variable still used by a block is rejected.</p>
   {value.locals.map((v,i)=>{const type=typeof v.initial==='string'?'text':typeof v.initial as ValueType;return <div key={i}>
    <label>Variable {i+1} name<input aria-label={'Variable '+(i+1)+' name'} maxLength={32} value={v.name} onChange={e=>onChange({...value,locals:value.locals.map((p,j)=>i===j?{...p,name:e.target.value}:p)})}/></label>
    {typeSelect('Variable '+(i+1)+' type',type,next=>onChange({...value,locals:value.locals.map((p,j)=>i===j?{...p,initial:initialValue(next)}:p)}))}
    <label>Initial value {i+1}{type==='boolean'?<select aria-label={'Initial value '+(i+1)} value={String(v.initial)} onChange={e=>onChange({...value,locals:value.locals.map((p,j)=>i===j?{...p,initial:e.target.value==='true'}:p)})}><option value="false">False</option><option value="true">True</option></select>:<input aria-label={'Initial value '+(i+1)} type={type==='number'?'number':'text'} step="any" maxLength={128} value={String(v.initial)} onChange={e=>onChange({...value,locals:value.locals.map((p,j)=>i===j?{...p,initial:type==='number'?Number(e.target.value):e.target.value}:p)})}/>}</label>
    <button onClick={()=>onChange({...value,locals:value.locals.filter((_,j)=>j!==i)})}>Remove variable {i+1}</button>
   </div>;})}
   <button disabled={value.locals.length>=16} onClick={()=>onChange({...value,locals:[...value.locals,{origin:null,name:freshName('value_'),initial:0}]})}>Add local variable</button>
  </fieldset>
 </div>;
}

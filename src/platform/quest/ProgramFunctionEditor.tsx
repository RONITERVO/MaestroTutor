// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {ProgramDataTypeEditor,ProgramDataValueEditor} from './ProgramDataEditor';
import {inferDataType} from '../../../shared/programValues';
import type {ValueType} from '../../core-sdk/room/programs';
import {type FunctionDraft,initialValue} from './programFunctionEditing';
export function ProgramFunctionEditor({value,onChange,entry,structured=false}:{value:FunctionDraft;onChange:(draft:FunctionDraft)=>void;entry:boolean;structured?:boolean}) {
 const freshName=(prefix:string)=>{let i=1;while([...value.parameters,...value.locals].some(p=>p.name===prefix+i))i++;return prefix+i;};
 return <div className="program-function-editor">
  <label>Function name<input aria-label="Function name" maxLength={32} value={value.name} onChange={e=>onChange({...value,name:e.target.value})}/></label>
  <ProgramDataTypeEditor value={value.returns} label="Function return type" allowVoid structured={structured} onChange={returns=>onChange({...value,returns})}/>

  <p>Names use letters, numbers and underscores. Renames update references. Existing calculations stay intact; incompatible edits are rejected. A typed function without a return gets a visible default return block.</p>
  <fieldset><legend>Parameters</legend><p>New parameters start with 0, false or empty text at every call. Removing a parameter removes that argument at every call. A parameter still used by this function cannot be removed.</p>
   {value.parameters.map((p,i)=><div key={i}>
    <label>Parameter {i+1} name<input aria-label={'Parameter '+(i+1)+' name'} maxLength={32} value={p.name} onChange={e=>onChange({...value,parameters:value.parameters.map((v,j)=>i===j?{...v,name:e.target.value}:v)})}/></label>
    <ProgramDataTypeEditor label={'Parameter '+(i+1)+' type'} value={p.type} structured={structured} onChange={type=>onChange({...value,parameters:value.parameters.map((v,j)=>i===j?{...v,type:type as ValueType}:v)})}/>
    <div className="room-workspace-actions"><button disabled={i===0} onClick={()=>{const parameters=[...value.parameters];[parameters[i-1],parameters[i]]=[parameters[i],parameters[i-1]];onChange({...value,parameters});}}>Move parameter {i+1} up</button><button onClick={()=>onChange({...value,parameters:value.parameters.filter((_,j)=>j!==i)})}>Remove parameter {i+1}</button></div>
   </div>)}
   <button disabled={entry||value.parameters.length>=8} onClick={()=>onChange({...value,parameters:[...value.parameters,{origin:null,name:freshName('input_'),type:'number'}]})}>Add parameter</button>
   {entry&&<p>The starting function has no parameters.</p>}
  </fieldset>
  <fieldset><legend>Local variables</legend><p>Each call starts with these values. Changing a type resets its initial value. Removing a variable still used by a block is rejected.</p>
   {value.locals.map((v,i)=>{const type=v.type??inferDataType(v.initial);return <div key={i}>
    <label>Variable {i+1} name<input aria-label={'Variable '+(i+1)+' name'} maxLength={32} value={v.name} onChange={e=>onChange({...value,locals:value.locals.map((p,j)=>i===j?{...p,name:e.target.value}:p)})}/></label>
    <ProgramDataTypeEditor label={'Variable '+(i+1)+' type'} value={type} structured={structured} onChange={next=>onChange({...value,locals:value.locals.map((p,j)=>i===j?{name:p.name,origin:p.origin,...(typeof next==='object'?{type:next}:{}),initial:initialValue(next as ValueType)}:p)})}/>
    <ProgramDataValueEditor label={'Initial value '+(i+1)} value={v.initial} type={type} onChange={initial=>onChange({...value,locals:value.locals.map((p,j)=>i===j?{...p,initial}:p)})}/>
    <button onClick={()=>onChange({...value,locals:value.locals.filter((_,j)=>j!==i)})}>Remove variable {i+1}</button>
   </div>;})}
   <button disabled={value.locals.length>=16} onClick={()=>onChange({...value,locals:[...value.locals,{origin:null,name:freshName('value_'),initial:0}]})}>Add local variable</button>
  </fieldset>
 </div>;
}

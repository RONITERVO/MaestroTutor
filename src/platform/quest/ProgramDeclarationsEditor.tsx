// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {defaultDataValue} from '../../../shared/programValues';
import type {ValueType,ScalarType} from '../../core-sdk/room/programs';
import {ProgramDataTypeEditor,ProgramDataValueEditor} from './ProgramDataEditor';
import type {DeclarationDraft} from './programDeclarationEditing';
export function ProgramDeclarationsEditor({value,onChange,structured}:{value:DeclarationDraft;onChange:(draft:DeclarationDraft)=>void;structured:boolean}) {
 const fresh=(prefix:string,names:string[])=>{let i=1;while(names.includes(prefix+i))i++;return prefix+i;};
 return <div className="program-function-editor">
  <fieldset><legend>Program state</legend><p>State is shared by this program's functions and stays between events until the run ends. Each new run starts with these values. Renames update this program's references; changing a type resets its initial value.</p>
   {value.state.map((s,i)=><div key={i}>
    <label>State {i+1} name<input aria-label={'State '+(i+1)+' name'} value={s.name} maxLength={32} onChange={e=>onChange({...value,state:value.state.map((v,j)=>j===i?{...v,name:e.target.value}:v)})}/></label>
    <ProgramDataTypeEditor label={'State '+(i+1)+' type'} value={s.type} structured={structured} onChange={type=>onChange({...value,state:value.state.map((v,j)=>j===i?{...v,type:type as ValueType,initial:defaultDataValue(type as ValueType)}:v)})}/>
    <ProgramDataValueEditor label={'State '+(i+1)+' initial value'} type={s.type} value={s.initial} onChange={initial=>onChange({...value,state:value.state.map((v,j)=>j===i?{...v,initial}:v)})}/>
    <button onClick={()=>onChange({...value,state:value.state.filter((_,j)=>i!==j)})}>Remove state {i+1}</button>
   </div>)}
   <button disabled={value.state.length>=16} onClick={()=>onChange({...value,state:[...value.state,{origin:null,name:fresh('state_',value.state.map(v=>v.name)),type:'number',initial:0}]})}>Add state variable</button>
  </fieldset>
  <fieldset><legend>Named signals</legend><p>Use a user. name, such as user.practice. Programs with the same name share signals and must agree on the payload type. Renaming updates waits and sends in this program only; other programs keep the old name. Nothing is sent when you apply an edit.</p>
   {value.events.map((s,i)=><div key={i}>
    <label>Signal {i+1} name<input aria-label={'Signal '+(i+1)+' name'} value={s.name} maxLength={37} onChange={e=>onChange({...value,events:value.events.map((v,j)=>i===j?{...v,name:e.target.value}:v)})}/></label>
    <label>Signal {i+1} payload type<select aria-label={'Signal '+(i+1)+' payload type'} value={s.type} onChange={e=>onChange({...value,events:value.events.map((v,j)=>i===j?{...v,type:e.target.value as ScalarType}:v)})}>{['number','text','boolean'].map(type=><option key={type} value={type}>{type}</option>)}</select></label>
    <button onClick={()=>onChange({...value,events:value.events.filter((_,j)=>i!==j)})}>Remove signal {i+1}</button>
   </div>)}
   <button disabled={value.events.length>=16} onClick={()=>onChange({...value,events:[...value.events,{origin:null,name:fresh('user.event_',value.events.map(v=>v.name)),type:'number'}]})}>Add named signal</button>
  </fieldset>
  <p>Removing a declaration still used by this program is rejected. Type changes must fit existing blocks; coordinated changes remain available in Source. Applying behaviour edits cancels active program runs; it does not start a new run.</p>
 </div>;
}

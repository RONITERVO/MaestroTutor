// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {roomObjectLabel} from '../../../shared/roomSelection';
import {useEffect,useRef,useState} from 'react';
import {moduleHash} from '../../../shared/programModuleIdentity';
import {checkedModuleFile} from '../../core-sdk/room/programModuleFile';
import {strictProgramJson} from '../../core-sdk/room/programs';
import {resolveCapabilitySchema,validateCapabilityValue,type CapabilitySchema} from '../../../shared/capabilities';

export type EditorObject = {id:string; name?:string};

// Shorten binary32 transport noise for display only. Keep doubles, integer guards
// and the actual draft value untouched; an unchanged form must submit exact state.
function displayNumber(value:number):number {
 if(!Number.isFinite(value)||Number.isInteger(value)||Math.fround(value)!==value)return value;
 for(let digits=7;digits<=9;digits++){
  const short=Number(value.toPrecision(digits));
  if(Math.fround(short)===value)return short;
 }
 return value;
}


/** A draft value only. The shared validator and native handler decide validity. */
export function initialCapabilityValue(schema:CapabilitySchema, objects:readonly EditorObject[]):unknown {
  if (schema.examples?.length) return JSON.parse(JSON.stringify(schema.examples[0]));
  if (schema.oneOf) return initialCapabilityValue(schema.oneOf[0],objects);
  if (schema.enum) return schema.enum[0];
  if (schema.type==='object') {
    const value=Object.fromEntries((schema.required??[]).map(key=>[key,initialCapabilityValue(schema.properties![key],objects)]));
    if(schema.format==='unitQuaternion') value.w=1;
    return value;
  }
  if(schema.type==='array') return Array.from({length:schema.minItems??0},()=>initialCapabilityValue(schema.items!,objects));
  if(schema.type==='boolean') return false;
  if(schema.type==='number'||schema.type==='integer') return Math.max(schema.minimum??0,Math.min(0,schema.maximum??0));
  if(schema['x-resource']==='object') return objects.find(x=>!schema.pattern||new RegExp(schema.pattern).test(x.id))?.id??'';
  return '';
}

export function changeCapabilityVariant(schema:CapabilitySchema,index:number,value:unknown,objects:readonly EditorObject[]):Record<string,unknown> {
 const selected=schema.oneOf![index],next=initialCapabilityValue(selected,objects) as Record<string,unknown>;
 const old=value&&typeof value==='object'?value as Record<string,unknown>:{};
 for(const [key,field] of Object.entries(selected.properties??{}))if(!field['x-static']&&Object.prototype.hasOwnProperty.call(old,key)&&validateCapabilityValue(old[key],field)===null)next[key]=old[key];
 return next;
}
export function CapabilityVariant({schema,value,onChange,objects,label=schema.title??'Variant'}:{schema:CapabilitySchema;value:unknown;onChange:(value:Record<string,unknown>)=>void;objects:readonly EditorObject[];label?:string}) {
 const selected=resolveCapabilitySchema(schema,value),index=schema.oneOf!.indexOf(selected!);
 return <label>{label}<select aria-label={label} value={index} onChange={e=>onChange(changeCapabilityVariant(schema,Number(e.target.value),value,objects))}>
  {index<0&&<option value={-1}>Unsupported selection</option>}
  {schema.oneOf!.map((branch,i)=><option key={i} value={i}>{branch.title??'Variant '+(i+1)}</option>)}
 </select></label>;
}

function ModuleDefinitionField({value,onChange,label}:{value:unknown;onChange:(value:unknown)=>void;label:string}) {
 const [draft,setDraft]=useState(()=>JSON.stringify(value,null,2)),[error,setError]=useState('');const sent=useRef(JSON.stringify(value));
 useEffect(()=>{const next=JSON.stringify(value);if(next!==sent.current){sent.current=next;setDraft(JSON.stringify(value,null,2));setError('');}},[value]);
 return <label>{label}<textarea aria-label={label} rows={8} maxLength={96000} value={draft} aria-invalid={Boolean(error)} onChange={event=>{
  const text=event.target.value;setDraft(text);try{const definition=strictProgramJson(text),file=checkedModuleFile({format:'maestro-program-module',version:1,hash:moduleHash(definition),definition});sent.current=JSON.stringify(file.definition);onChange(file.definition);setError('');}
  catch(e){sent.current='null';onChange(null);setError(e instanceof Error?e.message:'Invalid module definition.');}
 }}/>{error&&<span role="alert">{error}</span>}<small>Paste the complete module definition. Its content must match the separately supplied hash; changing a definition creates a different identity.</small></label>;
}

export function CapabilityFields({schema,value,onChange,label,objects,depth=0,locked,readOnly=false}:{
  schema:CapabilitySchema; value:unknown; onChange:(value:unknown)=>void;
  label:string; objects:readonly EditorObject[]; depth?:number;locked?:string[];readOnly?:boolean;
}) {
  if(readOnly)return <label>{label}<input aria-label={label} readOnly value={typeof value==='string'||typeof value==='number'?value:''}/><small>Filled by Load current values. Advanced arguments allow an explicit snapshot.</small></label>;
  if(schema.format==='programModule')return <ModuleDefinitionField value={value} onChange={onChange} label={label}/>;
  if(schema.oneOf){const selected=resolveCapabilitySchema(schema,value);return <><CapabilityVariant schema={schema} value={value} onChange={onChange} objects={objects} label={schema.title??label+' variant'}/>{selected&&<CapabilityFields schema={selected} value={value} onChange={onChange} objects={objects} label={label} depth={depth} locked={locked}/>}</>;}
  if(depth>12) return <p>Use the source editor for this deeply nested value.</p>;
  if(schema.nullable && value===null) return <div><span>{label}: none</span><button onClick={()=>onChange(initialCapabilityValue(schema,objects))}>Set {label}</button></div>;
  const optionalNull=schema.nullable&&<button onClick={()=>onChange(null)}>Clear {label}</button>;
  if(schema.type==='object') {
    const fields=(value??{}) as Record<string,unknown>;
    const entries=Object.entries(schema.properties??{}),references=entries.filter(([key])=>locked?.includes(key));
    const field=([key,child]:[string,CapabilitySchema])=>{
      const required=schema.required?.includes(key),present=Object.prototype.hasOwnProperty.call(fields,key),name=label+' '+key;
      return <div key={key}>
        {!required&&<label className="rule-checkbox"><input type="checkbox" aria-label={'Include '+name} checked={present} onChange={e=>{
          const next={...fields};if(e.target.checked)next[key]=initialCapabilityValue(child,objects);else delete next[key];onChange(next);
        }}/>Include {key}</label>}
        {(required||present)&&<CapabilityFields readOnly={locked?.includes(key)} locked={locked?.filter(p=>p.startsWith(key+'.')).map(p=>p.slice(key.length+1))} schema={child} value={fields[key]} label={name} objects={objects} depth={depth+1} onChange={next=>onChange({...fields,[key]:next})}/>}
      </div>;
    };
    return <fieldset><legend>{label}</legend>{entries.filter(([key])=>!locked?.includes(key)).map(field)}
      {references.length>0&&<details><summary>Current room references</summary>{references.map(field)}</details>}{optionalNull}</fieldset>;
  }
  if(schema.type==='array') {
    const values=Array.isArray(value)?value:[];
    return <details className="program-value-array"><summary>{label} · {values.length} entries</summary>
      {values.map((entry,index)=><fieldset key={index}><legend>{label} {index+1}</legend>
        <CapabilityFields schema={schema.items!} locked={locked?.filter(p=>p.startsWith(index+'.')).map(p=>p.slice(String(index).length+1))} value={entry} label={label+' '+(index+1)} objects={objects} depth={depth+1} onChange={next=>onChange(values.map((v,i)=>i===index?next:v))}/>
        <button disabled={values.length<=(schema.minItems??0)} onClick={()=>onChange(values.filter((_,i)=>i!==index))}>Remove {label} {index+1}</button>
      </fieldset>)}
      <button disabled={values.length>=(schema.maxItems??64)} onClick={()=>onChange([...values,initialCapabilityValue(schema.items!,objects)])}>Add {label} entry</button>{optionalNull}
    </details>;
  }
  const stringValue=typeof value==='string'?value:'';
  let options=schema.enum;
  if(schema['x-resource']==='object') options=objects.filter(o=>(!schema.pattern||new RegExp(schema.pattern).test(o.id))&&(!schema.enum||schema.enum.includes(o.id))).map(o=>o.id);
  const preview=schema['x-enum-images']?.[stringValue];
  const previewUrl=preview&&/^quest\/templates\/[a-f0-9]{64}\.png$/.test(preview)?import.meta.env.BASE_URL+preview:null;
  if(options) return <label>{label}<select aria-label={label} value={stringValue} onChange={e=>onChange(e.target.value)}>
    {!options.includes(stringValue)&&<option value={stringValue}>{stringValue||'Choose an object'}</option>}
    {options.map(option=><option key={option} value={option}>{schema['x-resource']==='object'?roomObjectLabel(objects.find(o=>o.id===option)!,objects):schema['x-enum-labels']?.[option]??option}</option>)}
  </select>{previewUrl&&<img className="maestro-capability-choice-preview" src={previewUrl} alt={(schema['x-enum-labels']?.[stringValue]??'Selected option')+' preview'}/>} {optionalNull}</label>;
  if(schema.type==='boolean') return <label>{label}<select aria-label={label} value={String(value===true)} onChange={e=>onChange(e.target.value==='true')}><option value="false">No</option><option value="true">Yes</option></select>{optionalNull}</label>;
  return <label>{label}<input aria-label={label} type={schema.type==='string'?'text':'number'} value={typeof value==='number'?displayNumber(value):typeof value==='string'?value:''}
    min={schema.minimum} max={schema.maximum} step={schema.type==='integer'?1:'any'} maxLength={schema.maxLength}
    onChange={e=>onChange(schema.type==='string'?e.target.value:e.target.value===''?'':Number(e.target.value))}/>{optionalNull}</label>;
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {resolveCapabilitySchema,validateCapabilityValue,type CapabilitySchema} from '../../../shared/capabilities';

export type EditorObject = {id:string; name?:string};

/** A draft value only. The shared validator and native handler decide validity. */
export function initialCapabilityValue(schema:CapabilitySchema, objects:readonly EditorObject[]):unknown {
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
export function CapabilityVariant({schema,value,onChange,objects,label='Animation source and channel'}:{schema:CapabilitySchema;value:unknown;onChange:(value:Record<string,unknown>)=>void;objects:readonly EditorObject[];label?:string}) {
 const selected=resolveCapabilitySchema(schema,value),index=schema.oneOf!.indexOf(selected!);
 return <label>{label}<select aria-label={label} value={index} onChange={e=>onChange(changeCapabilityVariant(schema,Number(e.target.value),value,objects))}>
  {index<0&&<option value={-1}>Unsupported selection</option>}
  {schema.oneOf!.map((branch,i)=><option key={i} value={i}>{branch.title??'Variant '+(i+1)}</option>)}
 </select></label>;
}

export function CapabilityFields({schema,value,onChange,label,objects,depth=0}:{
  schema:CapabilitySchema; value:unknown; onChange:(value:unknown)=>void;
  label:string; objects:readonly EditorObject[]; depth?:number;
}) {
  if(schema.oneOf){const selected=resolveCapabilitySchema(schema,value);return <><CapabilityVariant schema={schema} value={value} onChange={onChange} objects={objects} label={label+' variant'}/>{selected&&<CapabilityFields schema={selected} value={value} onChange={onChange} objects={objects} label={label} depth={depth}/>}</>;}
  if(depth>12) return <p>Use the source editor for this deeply nested value.</p>;
  if(schema.nullable && value===null) return <div><span>{label}: none</span><button onClick={()=>onChange(initialCapabilityValue(schema,objects))}>Set {label}</button></div>;
  const optionalNull=schema.nullable&&<button onClick={()=>onChange(null)}>Clear {label}</button>;
  if(schema.type==='object') {
    const fields=(value??{}) as Record<string,unknown>;
    return <fieldset><legend>{label}</legend>{Object.entries(schema.properties??{}).map(([key,child])=>{
      const required=schema.required?.includes(key),present=Object.prototype.hasOwnProperty.call(fields,key),name=label+' '+key;
      return <div key={key}>
        {!required&&<label className="rule-checkbox"><input type="checkbox" aria-label={'Include '+name} checked={present} onChange={e=>{
          const next={...fields};if(e.target.checked)next[key]=initialCapabilityValue(child,objects);else delete next[key];onChange(next);
        }}/>Include {key}</label>}
        {(required||present)&&<CapabilityFields schema={child} value={fields[key]} label={name} objects={objects} depth={depth+1} onChange={next=>onChange({...fields,[key]:next})}/>}
      </div>;
    })}{optionalNull}</fieldset>;
  }
  if(schema.type==='array') {
    const values=Array.isArray(value)?value:[];
    return <details className="program-value-array"><summary>{label} · {values.length} entries</summary>
      {values.map((entry,index)=><fieldset key={index}><legend>{label} {index+1}</legend>
        <CapabilityFields schema={schema.items!} value={entry} label={label+' '+(index+1)} objects={objects} depth={depth+1} onChange={next=>onChange(values.map((v,i)=>i===index?next:v))}/>
        <button disabled={values.length<=(schema.minItems??0)} onClick={()=>onChange(values.filter((_,i)=>i!==index))}>Remove {label} {index+1}</button>
      </fieldset>)}
      <button disabled={values.length>=(schema.maxItems??64)} onClick={()=>onChange([...values,initialCapabilityValue(schema.items!,objects)])}>Add {label} entry</button>{optionalNull}
    </details>;
  }
  const stringValue=typeof value==='string'?value:'';
  let options=schema.enum;
  if(schema['x-resource']==='object') options=objects.filter(o=>!schema.pattern||new RegExp(schema.pattern).test(o.id)).map(o=>o.id);
  if(options) return <label>{label}<select aria-label={label} value={stringValue} onChange={e=>onChange(e.target.value)}>
    {!options.includes(stringValue)&&<option value={stringValue}>{stringValue||'Choose an object'}</option>}
    {options.map(option=><option key={option} value={option}>{schema['x-resource']==='object'?objects.find(o=>o.id===option)?.name??option:option}</option>)}
  </select>{optionalNull}</label>;
  if(schema.type==='boolean') return <label>{label}<select aria-label={label} value={String(value===true)} onChange={e=>onChange(e.target.value==='true')}><option value="false">No</option><option value="true">Yes</option></select>{optionalNull}</label>;
  return <label>{label}<input aria-label={label} type={schema.type==='string'?'text':'number'} value={typeof value==='number'||typeof value==='string'?value:''}
    min={schema.minimum} max={schema.maximum} step={schema.type==='integer'?1:'any'} maxLength={schema.maxLength}
    onChange={e=>onChange(schema.type==='string'?e.target.value:e.target.value===''?'':Number(e.target.value))}/>{optionalNull}</label>;
}

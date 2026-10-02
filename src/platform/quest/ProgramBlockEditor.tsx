import {programCallables} from '../../core-sdk/room/programModules';
import {sameDataType} from '../../../shared/programValues';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {ReactNode} from 'react';
import {behaviourEvent,eventFieldType,eventArgumentType} from '../../../shared/behaviourEvents';
import {behaviourCatalog,behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityParameterType,capabilityInput,resolveCapabilitySchema,type CapabilitySchema} from '../../../shared/capabilities';
import type {BehaviourProgram,Expression,ProgramFunction,ProgramNode,ValueType} from '../../core-sdk/room/programs';
import {CapabilityFields,CapabilityVariant,initialCapabilityValue,type EditorObject} from './CapabilityFields';
import {ProgramValueEditor,defaultValue,valueExpression,expressionType,roomValueSources,valueType,type ValueSource} from './ProgramValueEditor';

export function ProgramBlockEditor({node,program,fn,objects,onChange,eventFieldsSupported=false,eventSubscriptionsSupported=false,factQueriesSupported=false}:{
  eventFieldsSupported?:boolean;eventSubscriptionsSupported?:boolean;factQueriesSupported?:boolean;node:ProgramNode;program:BehaviourProgram;fn:ProgramFunction;objects:readonly EditorObject[];onChange:(node:ProgramNode)=>void;
}) {
  const locals=[...fn.parameters,...fn.locals.map(v=>({name:v.name,type:valueType(v.initial,v.type)}))];
  const states=(program.state??[]).map(v=>({name:v.name,type:valueType(v.initial,v.type)}));
  const sources:ValueSource[]=[...roomValueSources.filter(s=>!behaviourFact(s.name)?.input||factQueriesSupported&&program.version===3&&(typeof s.type==='string'||program.dataVersion===1)),...locals.map(v=>({...v,kind:'var' as const})),...states.map(v=>({...v,kind:'state' as const}))];
  const expr=(label:string,value:Expression,type:ValueType,change:(value:Expression)=>void)=><ProgramValueEditor objects={objects} label={label} value={value} type={type} sources={sources} onChange={change}/>;
  const variable=(label:string,current:string,type:ValueType,change:(value:string)=>void,choices=locals)=><label>{label}<select aria-label={label} value={current} onChange={e=>change(e.target.value)}>
    {!choices.some(v=>v.name===current&&sameDataType(v.type,type))&&<option value={current}>{current||'Choose a variable'}</option>}
    {choices.filter(v=>sameDataType(v.type,type)).map(v=><option key={v.name} value={v.name}>{v.name}</option>)}
  </select></label>;
  switch(node.op) {
    case 'invoke': {
      const definition=capabilityDefinition(node.capability)!;
      const input=capabilityInput(node.capability,node.arguments);
      const field=(path:string,schema:CapabilitySchema,value:unknown,present:boolean,required:boolean,change:(value:unknown,remove?:boolean)=>void):ReactNode=>{
        if(schema.oneOf){const selected=resolveCapabilitySchema(schema,value);return <div key={path}><CapabilityVariant schema={schema} value={value} objects={objects} onChange={next=>change(next)}/>{selected&&field(path,selected,value,present,required,change)}</div>;}
        const type=capabilityParameterType(node.capability,path,node.arguments),bound=node.bindings[path];
        return <div key={path}>
          {!required&&<label className="rule-checkbox"><input type="checkbox" aria-label={'Include '+path} checked={present} onChange={e=>change(e.target.checked?initialCapabilityValue(schema,objects):undefined,!e.target.checked)}/>Include {path}</label>}
          {(required||present)&&<>
            {type&&<label>{path} input<select aria-label={path+' input mode'} value={bound?'expression':'literal'} onChange={e=>{
              const bindings={...node.bindings},args=JSON.parse(JSON.stringify(node.arguments)) as Record<string,unknown>;if(e.target.value==='expression'){bindings[path]={value:defaultValue(type)};if(schema['x-resource']==='object'&&!value){const parts=path.split('.');let parent=args;for(const key of parts.slice(0,-1))parent=parent[key] as Record<string,unknown>;parent[parts[parts.length-1]]='0'.repeat(32);}}else delete bindings[path];onChange({...node,arguments:args,bindings});
            }}><option value="literal">Value</option><option value="expression">Variable or calculation</option></select></label>}
            {bound&&type?expr(path,bound,type,value=>onChange({...node,bindings:{...node.bindings,[path]:value}})):
              schema.type==='object'?<fieldset><legend>{path}</legend>{Object.entries(schema.properties??{}).map(([key,child])=>{
                const obj=(value??{}) as Record<string,unknown>;
                return field(path+'.'+key,child,obj[key],Object.prototype.hasOwnProperty.call(obj,key),schema.required?.includes(key)??false,(next,remove)=>{const copy={...obj};if(remove)delete copy[key];else copy[key]=next;change(copy);});
              })}</fieldset>:<CapabilityFields label={path} schema={schema} value={value} objects={objects} onChange={value=>change(value)}/>}
          </>}
        </div>;
      };
      return <div>
        <label>Action<select aria-label="Block action" value={node.capability} onChange={e=>{
          const next=capabilityDefinition(e.target.value)!;
          onChange({id:node.id,op:'invoke',capability:next.id,version:next.version,arguments:next.example??initialCapabilityValue(next.input,objects) as Record<string,unknown>,bindings:{}});
        }}>{behaviourCatalog.actions.map(action=><option key={action.id} value={action.id}>{action.label}</option>)}</select></label>
        <p className="room-workspace-intro">Changing the action replaces its inputs and result assignments. Editing a value keeps all other fields.</p>
        {definition.description&&<p>{definition.description}</p>}
        {definition.input.oneOf&&<CapabilityVariant schema={definition.input} value={node.arguments} objects={objects} onChange={args=>{
          const bindings=Object.fromEntries(Object.entries(node.bindings).filter(([key])=>capabilityParameterType(node.capability,key,args)===capabilityParameterType(node.capability,key,node.arguments)&&capabilityParameterType(node.capability,key,args)!==null));
          onChange({...node,arguments:args,bindings});
        }}/>}
        {Object.entries(input?.properties??{}).map(([key,schema])=>field(key,schema,node.arguments[key],Object.prototype.hasOwnProperty.call(node.arguments,key),input?.required?.includes(key)??false,(value,remove)=>{
          const args={...node.arguments},bindings={...node.bindings};
          if(remove){delete args[key];for(const path of Object.keys(bindings))if(path===key||path.startsWith(key+'.'))delete bindings[path];}else args[key]=value;
          for(const path of Object.keys(bindings))if(capabilityParameterType(node.capability,path,args)===null)delete bindings[path];
          onChange({...node,arguments:args,bindings});
        }))}
      </div>;
    }
    case 'if': return expr('Condition',node.test,'boolean',test=>onChange({...node,test}));
    case 'repeat': return expr('Repetitions',node.count,'number',count=>onChange({...node,count}));
    case 'sleep': return expr('Seconds',node.seconds,'number',seconds=>onChange({...node,seconds}));
    case 'awaitCondition': return <>
      {expr('Watched condition',node.test,'boolean',test=>onChange({...node,test}))}
      <label>Detect condition<select aria-label="Detect condition" value={node.transition} onChange={e=>onChange({...node,transition:e.target.value as typeof node.transition})}><option value="true">Becomes true</option><option value="false">Becomes false</option><option value="either">Either change</option></select></label>
      <label>When watching starts<select aria-label="Initial condition" value={node.initial} onChange={e=>onChange({...node,initial:e.target.value as typeof node.initial})}><option value="report">Also report current match</option><option value="baseline">Wait for a later change</option></select></label>
      {expr('Stable seconds',node.stableSeconds,'number',stableSeconds=>onChange({...node,stableSeconds}))}
      {expr('Timeout seconds',node.timeout,'number',timeout=>onChange({...node,timeout}))}
      {variable('Condition received',node.received,'boolean',received=>onChange({...node,received}),locals.filter(v=>v.name!==node.value))}
      {variable('Condition value',node.value,'boolean',value=>onChange({...node,value}),locals.filter(v=>v.name!==node.received))}
      <p>Reads once when the wait starts, then at most 10 times per second without controlling objects. Stable seconds is 0–10; zero timeout waits until stopped. Facts refresh each sample; local values stay fixed while waiting. Missing facts fail the run. An observation gap over 0.3 seconds restarts the stable period and initial policy. Stop or app pause cancels without resuming.</p>
    </>;
    case 'forever': return <p>Repeats these blocks until stopped. Add a timer or event wait to yield between iterations.</p>;
    case 'set': case 'setState': {
      const choices=node.op==='setState'?states:locals,type=choices.find(v=>v.name===node.variable)?.type??expressionType(node.value,sources);
      return <>
        <label>Destination<select aria-label="Assignment destination" value={node.variable} onChange={e=>{
          const next=choices.find(v=>v.name===e.target.value)!;
          onChange({...node,variable:next.name,value:sameDataType(next.type,type)?node.value:valueExpression(next.type)});
        }}>{choices.map(v=><option key={v.name} value={v.name}>{v.name}</option>)}</select></label>
        {expr('Assigned value',node.value,type,value=>onChange({...node,value}))}
      </>;
    }
    case 'switch': {
      const type=expressionType(node.value,sources) as import('../../core-sdk/room/programs').ScalarType;
      return <>
        {expr('Case value',node.value,type,value=>onChange({...node,value}))}
        <p>Case bodies stay attached when their matching values change.</p>
        {node.cases.map((arm,index)=><div key={index}>
          <CapabilityFields schema={{type:type==='text'?'string':type}} value={arm.value} label={'Case '+(index+1)} objects={objects} onChange={value=>onChange({...node,cases:node.cases.map((v,i)=>i===index?{...v,value:value as typeof arm.value}:v)})}/>
          <button onClick={()=>onChange({...node,cases:node.cases.filter((_,i)=>i!==index)})}>Remove case {index+1} and its blocks</button>
        </div>)}
        <button disabled={node.cases.length>=16||type==='boolean'&&node.cases.length>=2} onClick={()=>{
          let value=defaultValue(type),i=0;
          while(node.cases.some(c=>c.value===value)){i++;value=type==='number'?i:type==='text'?'case_'+i:true;}
          onChange({...node,cases:[...node.cases,{value,body:[]}]});
        }}>Add case</button>
      </>;
    }
    case 'awaitEvent': {
      const events=[...behaviourCatalog.events.map(e=>({name:e.id,type:'text' as ValueType,objectEvent:e.objectEvent})),...(program.events??[]).map(e=>({...e,objectEvent:false}))];
      const selected=events.find(e=>e.name===node.event),type=selected?.type??'text',definition=behaviourEvent(node.event);
      const eventArgs=(args:Record<string,unknown>)=>onChange({...node,arguments:args,bindings:Object.fromEntries(Object.entries(node.bindings??{}).filter(([path])=>eventArgumentType(node.event,path,args)!==null))});
      const eventInput=resolveCapabilitySchema(definition?.input,node.arguments);
      return <>
        <label>Event<select aria-label="Await event" value={node.event} onChange={e=>{
          const event=events.find(v=>v.name===e.target.value)!;
          const definition=behaviourEvent(event.name),next:Extract<ProgramNode,{op:'awaitEvent'}>={...node,event:event.name,source:event.objectEvent?node.source:'',value:locals.find(v=>v.type===event.type&&v.name!==node.received)?.name??''};delete next.fields;
          if(definition?.input){next.version=definition.version;next.arguments=definition.example??initialCapabilityValue(definition.input,objects) as Record<string,unknown>;next.bindings={};}
          else {delete next.version;delete next.arguments;delete next.bindings;}onChange(next);
        }}>{events.map(e=><option key={e.name} value={e.name} disabled={!eventFieldsSupported&&behaviourEvent(e.name)?.features?.includes('eventFields.v1')===true||!eventSubscriptionsSupported&&Boolean(behaviourEvent(e.name)?.input)}>{e.name}</option>)}</select></label>
        {selected?.objectEvent&&<label>Event object<select aria-label="Event object" value={node.source} onChange={e=>onChange({...node,source:e.target.value})}><option value="">Any object</option>{objects.map(o=><option key={o.id} value={o.id}>{o.name??o.id}</option>)}</select></label>}
        {definition?.input&&<fieldset disabled={!eventSubscriptionsSupported}><legend>Event subscription</legend><p>Inputs are evaluated when the wait starts and stay fixed until it ends.</p>
          {definition.input.oneOf&&<CapabilityVariant schema={definition.input} value={node.arguments} objects={objects} onChange={value=>eventArgs(value as Record<string,unknown>)}/>}
          {Object.entries(eventInput?.properties??{}).map(([key,schema])=>{
            const type=eventArgumentType(node.event,key,node.arguments),bound=node.bindings?.[key],label=schema.title??'Event '+key;
            return <div key={key}>
              {type&&<label>{schema.title??key} input<select aria-label={label+' input mode'} value={bound?'expression':'literal'} onChange={e=>{const bindings={...node.bindings};if(e.target.value==='expression')bindings[key]={value:defaultValue(type)};else delete bindings[key];onChange({...node,bindings});}}><option value="literal">Value</option><option value="expression">Variable or calculation</option></select></label>}
              {bound&&type?expr(label,bound,type,value=>onChange({...node,bindings:{...node.bindings,[key]:value}})):<CapabilityFields label={label} schema={schema} value={node.arguments?.[key]} objects={objects} onChange={value=>eventArgs({...node.arguments,[key]:value})}/>}
              {schema.description&&<small>{schema.description}</small>}
            </div>;
          })}
        </fieldset>}
        {expr('Timeout seconds',node.timeout,'number',timeout=>onChange({...node,timeout}))}
        <p className="room-workspace-intro">Zero waits until the event arrives or the run is stopped.</p>
        {variable('Event received',node.received,'boolean',received=>onChange({...node,received}))}
        {variable('Event value',node.value,type,value=>onChange({...node,value}))}
        {definition?.description&&<p className="room-workspace-intro">{definition.description}</p>}
        {definition?.fields&&<fieldset disabled={!eventFieldsSupported}><legend>Store event details</legend><p>Choose existing variables below. Add variables to the function first if needed.</p>
          {Object.keys(definition.fields.properties??{}).map(key=><label key={key}>{key} ({eventFieldType(node.event,key)})<select aria-label={'Event field '+key} value={node.fields?.[key]??''} onChange={e=>{
            const fields={...node.fields};if(e.target.value)fields[key]=e.target.value;else delete fields[key];const next={...node};if(Object.keys(fields).length)next.fields=fields;else delete next.fields;onChange(next);
          }}><option value="">Do not store</option>{locals.filter(v=>v.type===eventFieldType(node.event,key)&&v.name!==node.received&&v.name!==node.value&&!Object.entries(node.fields??{}).some(([k,d])=>k!==key&&d===v.name)).map(v=><option key={v.name} value={v.name}>{v.name}</option>)}</select></label>)}
        </fieldset>}
      </>;
    }
    case 'emitEvent': {
      const type=program.events?.find(e=>e.name===node.event)?.type??'number';
      return <>
        <label>Named event<select aria-label="Send named event" value={node.event} onChange={e=>{
          const next=program.events!.find(v=>v.name===e.target.value)!;
          onChange({...node,event:next.name,value:sameDataType(next.type,type)?node.value:valueExpression(next.type)});
        }}>{program.events?.map(e=><option key={e.name} value={e.name} disabled={!eventFieldsSupported&&behaviourEvent(e.name)?.features?.includes('eventFields.v1')===true||!eventSubscriptionsSupported&&Boolean(behaviourEvent(e.name)?.input)}>{e.name}</option>)}</select></label>
        {expr('Event payload',node.value,type,value=>onChange({...node,value}))}
      </>;
    }
    case 'parallel':return <>
      <p>Run all branches together, then continue. Each gets a copy of state; only chosen return values reach this function. A failed or stopped branch cancels the group. Completed actions are not undone.</p>
      {node.branches.map((branch,index)=><fieldset key={index}><legend>Branch {index+1}</legend><ProgramBlockEditor node={{...branch,id:node.id,op:'call'}} program={program} fn={fn} objects={objects} factQueriesSupported={factQueriesSupported} eventFieldsSupported={eventFieldsSupported} eventSubscriptionsSupported={eventSubscriptionsSupported} onChange={value=>{if(value.op!=='call')return;const {id:_id,op:_op,...call}=value;onChange({...node,branches:node.branches.map((b,i)=>i===index?call:b)});}}/><button disabled={node.branches.length<=2} onClick={()=>onChange({...node,branches:node.branches.filter((_,i)=>i!==index)})}>Remove branch {index+1}</button></fieldset>)}
      <button disabled={node.branches.length>=4} onClick={()=>onChange({...node,branches:[...node.branches,{...node.branches[0],result:undefined}]})}>Add parallel branch</button>
    </>;
    case 'call': {
      const choices=programCallables(program),key=(node.module?node.module+'.':'')+node.function,callee=choices.find(c=>c.key===key)!.fn;
      return <>
        <label>Function<select aria-label="Called function" value={key} onChange={e=>{
          const next=choices.find(c=>c.key===e.target.value)!;
          onChange({id:node.id,op:'call',function:next.fn.name,...(next.module?{module:next.module}:{}),args:next.fn.parameters.map(p=>valueExpression(p.type))});
        }}>{choices.filter(c=>c.module||c.fn.name!==fn.name).map(c=><option key={c.key} value={c.key}>{c.key}</option>)}</select></label>
        {callee.parameters.map((p,i)=><div key={p.name}>{expr('Argument '+p.name,node.args[i],p.type,next=>onChange({...node,args:node.args.map((v,j)=>j===i?next:v)}))}</div>)}
        {callee.returns!=='void'&&<label>Store return value<select aria-label="Function result" value={node.result??''} onChange={e=>{
          const next={...node};if(e.target.value)next.result=e.target.value;else delete next.result;onChange(next);
        }}><option value="">Do not store</option>{locals.filter(v=>sameDataType(v.type,callee.returns)).map(v=><option key={v.name} value={v.name}>{v.name}</option>)}</select></label>}
      </>;
    }
    case 'return':return fn.returns==='void'?<p>Finish this function.</p>:expr('Return value',node.value!,fn.returns,value=>onChange({...node,value}));
  }
}

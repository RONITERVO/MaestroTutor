// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourCatalog} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityParameterType} from '../../../shared/capabilities';
import type {BehaviourProgram,Expression,ProgramFunction,ProgramNode,ValueType} from '../../core-sdk/room/programs';
import {CapabilityFields,initialCapabilityValue,type EditorObject} from './CapabilityFields';
import {ProgramValueEditor,defaultValue,expressionType,roomValueSources,valueType,type ValueSource} from './ProgramValueEditor';

export function ProgramBlockEditor({node,program,fn,objects,onChange}:{
  node:ProgramNode;program:BehaviourProgram;fn:ProgramFunction;objects:readonly EditorObject[];onChange:(node:ProgramNode)=>void;
}) {
  const locals=[...fn.parameters,...fn.locals.map(v=>({name:v.name,type:valueType(v.initial)}))];
  const states=(program.state??[]).map(v=>({name:v.name,type:valueType(v.initial)}));
  const sources:ValueSource[]=[...roomValueSources,...locals.map(v=>({...v,kind:'var' as const})),...states.map(v=>({...v,kind:'state' as const}))];
  const expr=(label:string,value:Expression,type:ValueType,change:(value:Expression)=>void)=><ProgramValueEditor label={label} value={value} type={type} sources={sources} onChange={change}/>;
  const variable=(label:string,current:string,type:ValueType,change:(value:string)=>void,choices=locals)=><label>{label}<select aria-label={label} value={current} onChange={e=>change(e.target.value)}>
    {!choices.some(v=>v.name===current&&v.type===type)&&<option value={current}>{current||'Choose a variable'}</option>}
    {choices.filter(v=>v.type===type).map(v=><option key={v.name} value={v.name}>{v.name}</option>)}
  </select></label>;
  switch(node.op) {
    case 'invoke': {
      const definition=capabilityDefinition(node.capability)!;
      return <div>
        <label>Action<select aria-label="Block action" value={node.capability} onChange={e=>{
          const next=capabilityDefinition(e.target.value)!;
          onChange({id:node.id,op:'invoke',capability:next.id,version:next.version,arguments:next.example??initialCapabilityValue(next.input,objects) as Record<string,unknown>,bindings:{}});
        }}>{behaviourCatalog.actions.map(action=><option key={action.id} value={action.id}>{action.label}</option>)}</select></label>
        <p className="room-workspace-intro">Changing the action replaces its inputs and result assignments. Editing a value keeps all other fields.</p>
        {definition.description&&<p>{definition.description}</p>}
        {Object.entries(definition.input.properties??{}).map(([key,schema])=>{
          const type=capabilityParameterType(node.capability,key),bound=node.bindings[key];
          const required=definition.input.required?.includes(key),present=Object.prototype.hasOwnProperty.call(node.arguments,key);
          return <div key={key}>
            {!required&&<label className="rule-checkbox"><input type="checkbox" aria-label={'Include '+key} checked={present} onChange={e=>{
              const args={...node.arguments},bindings={...node.bindings};
              if(e.target.checked)args[key]=initialCapabilityValue(schema,objects);else {delete args[key];delete bindings[key];}
              onChange({...node,arguments:args,bindings});
            }}/>Include {key}</label>}
            {(required||present)&&<>
              {type&&<label>{key} input<select aria-label={key+' input mode'} value={bound?'expression':'literal'} onChange={e=>{
                const bindings={...node.bindings},args={...node.arguments};
                if(e.target.value==='expression') {
                  bindings[key]={value:defaultValue(type)};
                  // A bound resource still needs a structurally valid literal. This
                  // placeholder grants no authority; native resolves the binding.
                  if(schema['x-resource']==='object'&&!args[key])args[key]='0'.repeat(32);
                }else delete bindings[key];
                onChange({...node,arguments:args,bindings});
              }}><option value="literal">Value</option><option value="expression">Variable or calculation</option></select></label>}
              {bound&&type?expr(key,bound,type,value=>onChange({...node,bindings:{...node.bindings,[key]:value}})):
                <CapabilityFields label={key} schema={schema} value={node.arguments[key]} objects={objects} onChange={value=>onChange({...node,arguments:{...node.arguments,[key]:value}})}/>}
            </>}
          </div>;
        })}
      </div>;
    }
    case 'if': return expr('Condition',node.test,'boolean',test=>onChange({...node,test}));
    case 'repeat': return expr('Repetitions',node.count,'number',count=>onChange({...node,count}));
    case 'sleep': return expr('Seconds',node.seconds,'number',seconds=>onChange({...node,seconds}));
    case 'forever': return <p>Repeats these blocks until stopped. Add a timer or event wait to yield between iterations.</p>;
    case 'set': case 'setState': {
      const choices=node.op==='setState'?states:locals,type=choices.find(v=>v.name===node.variable)?.type??expressionType(node.value,sources);
      return <>
        <label>Destination<select aria-label="Assignment destination" value={node.variable} onChange={e=>{
          const next=choices.find(v=>v.name===e.target.value)!;
          onChange({...node,variable:next.name,value:next.type===type?node.value:{value:defaultValue(next.type)}});
        }}>{choices.map(v=><option key={v.name} value={v.name}>{v.name}</option>)}</select></label>
        {expr('Assigned value',node.value,type,value=>onChange({...node,value}))}
      </>;
    }
    case 'switch': {
      const type=expressionType(node.value,sources);
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
      const selected=events.find(e=>e.name===node.event),type=selected?.type??'text';
      return <>
        <label>Event<select aria-label="Await event" value={node.event} onChange={e=>{
          const event=events.find(v=>v.name===e.target.value)!;
          onChange({...node,event:event.name,source:event.objectEvent?node.source:'',value:locals.find(v=>v.type===event.type&&v.name!==node.received)?.name??''});
        }}>{events.map(e=><option key={e.name} value={e.name}>{e.name}</option>)}</select></label>
        {selected?.objectEvent&&<label>Event object<select aria-label="Event object" value={node.source} onChange={e=>onChange({...node,source:e.target.value})}><option value="">Any object</option>{objects.map(o=><option key={o.id} value={o.id}>{o.name??o.id}</option>)}</select></label>}
        {expr('Timeout seconds',node.timeout,'number',timeout=>onChange({...node,timeout}))}
        <p className="room-workspace-intro">Zero waits until the event arrives or the run is stopped.</p>
        {variable('Event received',node.received,'boolean',received=>onChange({...node,received}))}
        {variable('Event value',node.value,type,value=>onChange({...node,value}))}
      </>;
    }
    case 'emitEvent': {
      const type=program.events?.find(e=>e.name===node.event)?.type??'number';
      return <>
        <label>Named event<select aria-label="Send named event" value={node.event} onChange={e=>{
          const next=program.events!.find(v=>v.name===e.target.value)!;
          onChange({...node,event:next.name,value:next.type===type?node.value:{value:defaultValue(next.type)}});
        }}>{program.events?.map(e=><option key={e.name} value={e.name}>{e.name}</option>)}</select></label>
        {expr('Event payload',node.value,type,value=>onChange({...node,value}))}
      </>;
    }
    case 'call': {
      const callee=program.functions.find(f=>f.name===node.function)!;
      return <>
        <label>Function<select aria-label="Called function" value={node.function} onChange={e=>{
          const next=program.functions.find(f=>f.name===e.target.value)!;
          onChange({id:node.id,op:'call',function:next.name,args:next.parameters.map(p=>({value:defaultValue(p.type)}))});
        }}>{program.functions.filter(f=>f.name!==fn.name).map(f=><option key={f.name} value={f.name}>{f.name}</option>)}</select></label>
        {callee.parameters.map((p,i)=><div key={p.name}>{expr('Argument '+p.name,node.args[i],p.type,next=>onChange({...node,args:node.args.map((v,j)=>j===i?next:v)}))}</div>)}
        {callee.returns!=='void'&&<label>Store return value<select aria-label="Function result" value={node.result??''} onChange={e=>{
          const next={...node};if(e.target.value)next.result=e.target.value;else delete next.result;onChange(next);
        }}><option value="">Do not store</option>{locals.filter(v=>v.type===callee.returns).map(v=><option key={v.name} value={v.name}>{v.name}</option>)}</select></label>}
      </>;
    }
    case 'return':return fn.returns==='void'?<p>Finish this function.</p>:expr('Return value',node.value!,fn.returns,value=>onChange({...node,value}));
  }
}

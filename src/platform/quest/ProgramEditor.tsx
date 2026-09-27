// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useEffect,useState} from 'react';
import {expressionLabel,parseProgram,type BehaviourProgram,type ProgramNode} from '../../core-sdk/room/programs';
import {type RuleRun} from '../../core-sdk/room/rules';
import {capabilityDefinition} from '../../../shared/capabilities';
const copy=<T,>(value:T):T=>JSON.parse(JSON.stringify(value));
const children=(node:ProgramNode):{label:string;body:ProgramNode[]}[]=>node.op==='if'?[{label:'Then',body:node.then},{label:'Otherwise',body:node.else}]:node.op==='repeat'||node.op==='forever'?[{label:node.op==='forever'?'Until stopped':'Repeat these',body:node.body}]:node.op==='switch'?[...node.cases.map(c=>({label:'Case '+JSON.stringify(c.value),body:c.body})),{label:'Default',body:node.default}]:[];
function label(n:ProgramNode):string {
 switch(n.op){case 'invoke':return capabilityDefinition(n.capability)?.label??n.capability;case 'if':return 'If '+expressionLabel(n.test);case 'repeat':return 'Repeat '+expressionLabel(n.count)+' times';case 'sleep':return 'Wait '+expressionLabel(n.seconds)+' seconds';case 'awaitEvent':return 'Wait for '+n.event;case 'emitEvent':return 'Send '+n.event+' with '+expressionLabel(n.value);case 'forever':return 'Repeat until stopped';case 'setState':return 'Set state.'+n.variable+' to '+expressionLabel(n.value);case 'set':return 'Set '+n.variable+' to '+expressionLabel(n.value);case 'call':return (n.result?n.result+' = ':'')+n.function+'('+n.args.map(expressionLabel).join(', ')+')';case 'return':return 'Return'+(n.value?' '+expressionLabel(n.value):'');case 'switch':return 'Choose case for '+expressionLabel(n.value);}
}
function findBlock(program:BehaviourProgram,id:string):{body:ProgramNode[];at:number}|null {
 const search=(body:ProgramNode[]):{body:ProgramNode[];at:number}|null=>{for(let at=0;at<body.length;at++){if(body[at].id===id)return {body,at};for(const c of children(body[at])){const found=search(c.body);if(found)return found;}}return null;};
 for(const fn of program.functions){const found=search(fn.body);if(found)return found;}return null;
}
export function ProgramEditor({source,onChange,run,onEditingChange,targets,eventsSupported=false,onSignal,disabled=false}:{source:string;onChange:(source:string)=>void;run?:RuleRun;onEditingChange:(value:boolean)=>void;targets:readonly {id:string;name:string}[];eventsSupported?:boolean;onSignal?:(name:string,value:number|boolean|string)=>void;disabled?:boolean}) {
 const parsed=parseProgram(source),program=parsed.program;
 const [signalValues,setSignalValues]=useState<Record<string,string>>({});
 const [editing,setEditing]=useState<{kind:'source'|'node'|'function';id:string;buffer:string}|null>(null),[error,setError]=useState('');
 useEffect(()=>{onEditingChange(editing!==null);return ()=>onEditingChange(false);},[editing!==null,onEditingChange]);
 const write=(value:BehaviourProgram)=>{const json=JSON.stringify(value),result=parseProgram(json);if(!result.program){setError(result.error!);return false;}onChange(json);setError('');return true;};
 const update=(fn:(value:BehaviourProgram)=>void)=>{if(!program)return;const next=copy(program);fn(next);write(next);};
 const saveEditor=()=>{
  if(!editing||!program)return;
  try {
   if(editing.kind==='source'){const result=parseProgram(editing.buffer);if(!result.program)throw new Error(result.error!);if(write(result.program))setEditing(null);return;}
   const value:unknown=JSON.parse(editing.buffer),next=copy(program);
   if(editing.kind==='node'){const found=findBlock(next,editing.id);if(!found)throw new Error('Block changed. Discard this editor draft.');if(!value||typeof value!=='object'||(value as {id:unknown}).id!==editing.id)throw new Error('Keep this block’s stable id.');found.body[found.at]=value as ProgramNode;}
   else {const index=next.functions.findIndex(f=>f.name===editing.id);if(index<0)throw new Error('Function changed. Discard this editor draft.');next.functions[index]=value as BehaviourProgram['functions'][number];}
   if(write(next))setEditing(null);
  }catch(e){setError(e instanceof Error?e.message:'Invalid program data');}
 };
 const add=(fn:string,op:'invoke'|'if'|'repeat'|'sleep'|'awaitEvent'|'forever')=>update(value=>{let i=1;while(findBlock(value,'block_'+i))i++;const id='block_'+i;
  const f=value.functions.find(f=>f.name===fn)!;
  if(['sleep','awaitEvent','forever'].includes(op)){value.version=3;value.state??=[];value.events??=[];}
  const local=(base:string,initial:boolean|string)=>{let name=base,c=1;while([...f.locals,...f.parameters].some(l=>l.name===name))name=base+(c++);f.locals.push({name,initial});return name;};
  const n:ProgramNode=op==='sleep'?{id,op,seconds:{value:1}}:op==='forever'?{id,op,body:f.body.splice(0)}:op==='awaitEvent'?{id,op,event:'maestro.speaking.enter',source:'',timeout:{value:0},received:local('received',false),value:local('eventValue','')}:op==='invoke'?{id,op,capability:'time.wait',version:1,arguments:{seconds:1},bindings:{}}:op==='if'?{id,op,test:{value:true},then:[],else:[]}:{id,op,count:{value:2},body:[]};const last=f.body[f.body.length-1];if(op!=='forever'&&last?.op==='forever')last.body.push(n);else f.body.push(n);
 });
 const blocks=(nodes:ProgramNode[]) => <ol className="rule-block-list">{nodes.map(n=><li key={n.id} className={`rule-action-block program-block-${n.op}${run?.nodeId===n.id?' rule-action-active':''}`} data-node-id={n.id}>
  <div className="rule-step-heading"><strong>{label(n)}</strong>{run?.nodeId===n.id&&<span>{run.status??'Running'}</span>}</div>
  {n.op==='invoke'&&<p className="room-workspace-intro">{n.capability==='time.wait'?'Pause':targets.find(target=>target.id===n.arguments.target)?.name??'Unavailable object'}{typeof n.arguments.seconds==='number'&&' · '+(n.arguments.seconds===0?'Full duration':n.arguments.seconds+' seconds')}</p>}
  <div className="room-workspace-actions"><button onClick={()=>{setEditing({kind:'node',id:n.id,buffer:JSON.stringify(n,null,2)});setError('');}} aria-label={`Edit block ${n.id}`}>Edit block</button><button aria-label={`Move block ${n.id} up`} onClick={()=>update(value=>{const found=findBlock(value,n.id)!;if(found.at>0)[found.body[found.at-1],found.body[found.at]]=[found.body[found.at],found.body[found.at-1]];})}>↑</button><button aria-label={`Move block ${n.id} down`} onClick={()=>update(value=>{const found=findBlock(value,n.id)!;if(found.at<found.body.length-1)[found.body[found.at+1],found.body[found.at]]=[found.body[found.at],found.body[found.at+1]];})}>↓</button><button aria-label={`Remove block ${n.id}`} onClick={()=>update(value=>{const found=findBlock(value,n.id)!;found.body.splice(found.at,1);})}>Remove</button></div>
  {children(n).map((c,i)=><div key={i} className="program-branch"><small>{c.label}</small>{c.body.length?blocks(c.body):<p className="room-workspace-intro">No actions</p>}</div>)}
 </li>)}</ol>;
 return <section className="program-editor" aria-label="Program editor">
  <p className="room-workspace-intro">Functions share one program with the agent. Edit blocks or the full JSON source, then apply your changes to the room. Loops and actions run locally.</p>
  {error&&<p role="alert" className="room-message room-message-warning">{error}</p>}
  {!program?<p role="alert">{parsed.error}</p>:<>
   {run&&<div className="program-watch" aria-label="Live program values"><strong>{run.functionName} · {run.status}</strong>{[...(run.state??[]).map(v=>({...v,name:'state.'+v.name})),...(run.locals??[])].map(v=><p key={v.name}><code>{v.name}</code> = {v.value} <small>({v.type})</small></p>)}</div>}
   {editing?<fieldset disabled={disabled}><legend>{editing.kind==='source'?'Full program source':editing.kind==='node'?'Edit block '+editing.id:'Edit function '+editing.id}</legend><label>Program JSON<textarea aria-label="Program JSON" spellCheck={false} rows={18} maxLength={24000} value={editing.buffer} onChange={e=>setEditing({...editing,buffer:e.target.value})}/></label><p className="room-workspace-intro">Update draft validates the complete program. Your changes do not run until applied and triggered.</p><div className="room-workspace-actions"><button onClick={saveEditor}>Update draft</button><button onClick={()=>{setEditing(null);setError('');}}>Discard editor draft</button></div></fieldset>:<fieldset disabled={disabled} className="room-edit-body">
    <div className="room-workspace-actions"><button onClick={()=>setEditing({kind:'source',id:'',buffer:JSON.stringify(program,null,2)})}>Edit full source</button><button disabled={program.functions.length>=16} onClick={()=>update(value=>{let i=1;while(value.functions.some(f=>f.name==='function_'+i))i++;value.functions.push({name:'function_'+i,returns:'void',parameters:[],locals:[],body:[]});})}>+ Function</button></div>
    <p className="room-workspace-intro">Starts at <code>{program.entry}</code> · {program.resources.length} declared {program.resources.length===1?'object':'objects'}</p>
    {program.version===3&&<div className="program-watch" aria-label="Program state and events"><strong>Event program</strong><p>Start enables this run. Stop or app pause cancels it. State stays between events until the run ends.</p>{program.state?.map(v=><p key={v.name}><code>state.{v.name}</code> starts at {JSON.stringify(v.initial)}</p>)}<button onClick={()=>update(v=>{let i=1;while(v.state?.some(s=>s.name==='state_'+i))i++;v.state!.push({name:'state_'+i,initial:0});})}>+ State variable</button><button onClick={()=>update(v=>{let i=1;while(v.events?.some(e=>e.name==='user.event_'+i))i++;v.events!.push({name:'user.event_'+i,type:'number'});})}>+ Named event</button>
     {program.events?.map(event=><label key={event.name}>{event.name} ({event.type})<input aria-label={'Value for '+event.name} value={signalValues[event.name]??(event.type==='number'?'0':event.type==='boolean'?'false':'')} onChange={e=>setSignalValues({...signalValues,[event.name]:e.target.value})}/><button disabled={!onSignal} onClick={()=>{const value=signalValues[event.name]??(event.type==='number'?'0':event.type==='boolean'?'false':'');if(event.type==='boolean'&&!['true','false'].includes(value)){setError('Use true or false.');return;}const payload=event.type==='number'?Number(value):event.type==='boolean'?value==='true':value;if(typeof payload==='number'&&(!Number.isFinite(payload)||Math.abs(payload)>1000000)){setError('Use a finite number between −1000000 and 1000000.');return;}onSignal?.(event.name,payload);}}>Send event</button></label>)}</div>}
    {program.functions.map(fn=><section key={fn.name} className="program-function" aria-label={'Function '+fn.name}><div className="program-function-heading"><h3>Define {fn.name}({fn.parameters.map(p=>p.name+': '+p.type).join(', ')})</h3><small>Returns {fn.returns}</small></div><div className="room-workspace-actions"><button onClick={()=>setEditing({kind:'function',id:fn.name,buffer:JSON.stringify(fn,null,2)})}>Edit function {fn.name}</button>{fn.name!==program.entry&&<button onClick={()=>update(value=>{value.functions=value.functions.filter(f=>f.name!==fn.name);})}>Remove function {fn.name}</button>}</div>{fn.locals.length>0&&<p>{fn.locals.map(v=>v.name+' = '+JSON.stringify(v.initial)).join(' · ')}</p>}{blocks(fn.body)}<div className="room-workspace-actions"><button onClick={()=>add(fn.name,'invoke')}>+ Wait in {fn.name}</button><button onClick={()=>add(fn.name,'if')}>+ If in {fn.name}</button><button onClick={()=>add(fn.name,'repeat')}>+ Repeat in {fn.name}</button>{eventsSupported&&<><button onClick={()=>add(fn.name,'sleep')}>+ Timer in {fn.name}</button><button onClick={()=>add(fn.name,'awaitEvent')}>+ Event wait in {fn.name}</button><button onClick={()=>add(fn.name,'forever')}>+ Forever in {fn.name}</button></>}</div></section>)}
   </fieldset>}
  </>}
 </section>;
}

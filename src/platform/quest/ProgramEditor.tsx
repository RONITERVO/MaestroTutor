import {ProgramDeclarationsEditor} from './ProgramDeclarationsEditor';
import {declarationDraft,editProgramDeclarations,type DeclarationDraft} from './programDeclarationEditing';
import {dataTypeLabel} from '../../../shared/programValues';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useEffect,useState} from 'react';
import {ProgramFunctionEditor} from './ProgramFunctionEditor';
import {editProgramFunction,functionDraft,type FunctionDraft} from './programFunctionEditing';
import {ProgramBlockEditor} from './ProgramBlockEditor';
import {defaultValue,valueType,valueExpression} from './ProgramValueEditor';
import {expressionLabel,parseProgram,type BehaviourProgram,type ProgramNode} from '../../core-sdk/room/programs';
import {type RuleRun} from '../../core-sdk/room/rules';
import {capabilityDefinition,capabilityParameterType,capabilityBindingFields,capabilityOutputType,literalCapabilityResources} from '../../../shared/capabilities';
const copy=<T,>(value:T):T=>JSON.parse(JSON.stringify(value));
const children=(node:ProgramNode):{label:string;body:ProgramNode[]}[]=>node.op==='if'?[{label:'Then',body:node.then},{label:'Otherwise',body:node.else}]:node.op==='repeat'||node.op==='forever'?[{label:node.op==='forever'?'Until stopped':'Repeat these',body:node.body}]:node.op==='switch'?[...node.cases.map(c=>({label:'Case '+JSON.stringify(c.value),body:c.body})),{label:'Default',body:node.default}]:[];
function label(n:ProgramNode):string {
 switch(n.op){case 'invoke':return (capabilityDefinition(n.capability)?.label??n.capability)+(n.results?' → '+Object.entries(n.results).map(([key,name])=>name+' ('+key+')').join(', '):'');case 'if':return 'If '+expressionLabel(n.test);case 'repeat':return 'Repeat '+expressionLabel(n.count)+' times';case 'sleep':return 'Wait '+expressionLabel(n.seconds)+' seconds';case 'awaitEvent':return 'Wait for '+n.event;case 'emitEvent':return 'Send '+n.event+' with '+expressionLabel(n.value);case 'forever':return 'Repeat until stopped';case 'setState':return 'Set state.'+n.variable+' to '+expressionLabel(n.value);case 'set':return 'Set '+n.variable+' to '+expressionLabel(n.value);case 'call':return (n.result?n.result+' = ':'')+n.function+'('+n.args.map(expressionLabel).join(', ')+')';case 'return':return 'Return'+(n.value?' '+expressionLabel(n.value):'');case 'switch':return 'Choose case for '+expressionLabel(n.value);}
}
function findBlock(program:BehaviourProgram,id:string):{body:ProgramNode[];at:number}|null {
 const search=(body:ProgramNode[]):{body:ProgramNode[];at:number}|null=>{for(let at=0;at<body.length;at++){if(body[at].id===id)return {body,at};for(const c of children(body[at])){const found=search(c.body);if(found)return found;}}return null;};
 for(const fn of program.functions){const found=search(fn.body);if(found)return found;}return null;
}
export function ProgramEditor({source,onChange,run,onEditingChange,targets,eventsSupported=false,eventFieldsSupported=false,eventSubscriptionsSupported=false,resultsSupported=false,structuredSupported=false,onSignal,disabled=false}:{source:string;onChange:(source:string)=>void;run?:RuleRun;onEditingChange:(value:boolean)=>void;targets:readonly {id:string;name:string}[];eventsSupported?:boolean;eventFieldsSupported?:boolean;eventSubscriptionsSupported?:boolean;resultsSupported?:boolean;structuredSupported?:boolean;onSignal?:(name:string,value:number|boolean|string)=>void;disabled?:boolean}) {
 const parsed=parseProgram(source),program=parsed.program;
 const [signalValues,setSignalValues]=useState<Record<string,string>>({});
 const [editing,setEditing]=useState<{kind:'source'|'node'|'function'|'visual'|'signature'|'newFunction'|'declarations';id:string;buffer:string;base?:string}|null>(null),[error,setError]=useState('');
 useEffect(()=>{onEditingChange(editing!==null);return ()=>onEditingChange(false);},[editing!==null,onEditingChange]);
 const write=(value:BehaviourProgram)=>{const json=JSON.stringify(value),result=parseProgram(json);if(!result.program){setError(result.error!);return false;}onChange(json);setError('');return true;};
 const update=(fn:(value:BehaviourProgram)=>void)=>{if(!program)return;const next=copy(program);fn(next);write(next);};
 const saveEditor=()=>{
  if(!editing)return;
  try {
   if(editing.kind==='source'){const result=parseProgram(editing.buffer);if(!result.program)throw new Error(result.error!);if(write(result.program))setEditing(null);return;}
   if(!program)return;
   if(editing.kind==='declarations'){
    if(editing.base!==source)throw new Error('Program changed while editing. Discard this editor draft and reopen declarations.');
    if(write(editProgramDeclarations(program,JSON.parse(editing.buffer) as DeclarationDraft)))setEditing(null);return;
   }
   if(editing.kind==='signature'||editing.kind==='newFunction'){
    if(editing.base!==source)throw new Error('Program changed while editing. Discard this editor draft and reopen the function.');
    if(write(editProgramFunction(program,editing.kind==='newFunction'?null:editing.id,JSON.parse(editing.buffer) as FunctionDraft)))setEditing(null);return;
   }
   const value:unknown=JSON.parse(editing.buffer),next=copy(program);
   if(editing.kind==='node'||editing.kind==='visual'){const found=findBlock(next,editing.id);if(!found)throw new Error('Block changed. Discard this editor draft.');if(!value||typeof value!=='object'||(value as {id:unknown}).id!==editing.id)throw new Error('Keep this block’s stable id.');found.body[found.at]=value as ProgramNode;
    if(editing.kind==='visual'){
     const collect=(nodes:ProgramNode[])=>{for(const node of nodes){if(node.op==='invoke'){
      const schema=capabilityBindingFields(node.capability,node.arguments);
      if(Object.keys(node.bindings).some(key=>schema?.[key]?.['x-resource']==='object')){next.version=3;next.state??=[];next.events??=[];}
      next.resources=[...new Set([...next.resources,...literalCapabilityResources(node.capability,node.arguments,node.bindings,next.version)])];}for(const branch of children(node))collect(branch.body);}};
     collect([value as ProgramNode]);
    }
   }
   else {const index=next.functions.findIndex(f=>f.name===editing.id);if(index<0)throw new Error('Function changed. Discard this editor draft.');next.functions[index]=value as BehaviourProgram['functions'][number];}
   if(write(next))setEditing(null);
  }catch(e){setError(e instanceof Error?e.message:'Invalid program data');}
 };
 type AddKind='invoke'|'if'|'repeat'|'sleep'|'awaitEvent'|'forever'|'switch'|'set'|'setState'|'emitEvent'|'call'|'return';
 const add=(fn:string,op:AddKind,parent?:string,branch=0)=>update(value=>{let i=1;while(findBlock(value,'block_'+i))i++;const id='block_'+i;
  const f=value.functions.find(f=>f.name===fn)!;
  if(['sleep','awaitEvent','forever','setState','emitEvent'].includes(op)){value.version=3;value.state??=[];value.events??=[];}
  const local=(base:string,initial:boolean|string)=>{let name=base,c=1;while([...f.locals,...f.parameters].some(l=>l.name===name))name=base+(c++);f.locals.push({name,initial});return name;};
  const destination=parent?children(findBlock(value,parent)!.body[findBlock(value,parent)!.at])[branch].body:f.body;
  let n:ProgramNode;
  if(op==='set'||op==='setState'){
   const vars=op==='setState'?value.state!:f.locals;const v=vars[0];n={id,op,variable:v.name,value:valueExpression(valueType(v.initial,v.type),v.initial)};
  }else if(op==='emitEvent'){const e=value.events![0];n={id,op,event:e.name,value:{value:defaultValue(e.type)}};}
  else if(op==='call'){const callee=value.functions.find(x=>x.name!==f.name)!;n={id,op,function:callee.name,args:callee.parameters.map(p=>valueExpression(p.type))};}
  else if(op==='return')n=f.returns==='void'?{id,op}:{id,op,value:valueExpression(f.returns)};
  else n=op==='sleep'?{id,op,seconds:{value:1}}:op==='forever'?{id,op,body:destination.splice(0)}:op==='awaitEvent'?{id,op,event:'maestro.speaking.enter',source:'',timeout:{value:0},received:local('received',false),value:local('eventValue','')}:op==='invoke'?{id,op,capability:'time.wait',version:1,arguments:{seconds:1},bindings:{}}:op==='if'?{id,op,test:{value:true},then:[],else:[]}:op==='switch'?{id,op,value:{value:0},cases:[{value:0,body:[]}],default:[]}:{id,op,count:{value:2},body:[]};
  const last=destination[destination.length-1];if(!parent&&op!=='forever'&&last?.op==='forever')last.body.push(n);else if(last?.op==='return')destination.splice(destination.length-1,0,n);else destination.push(n);
 });
 const addControls=(fn:BehaviourProgram['functions'][number],parent?:string,branch=0,branchLabel='')=>{
  const location=parent?parent+' '+branchLabel:fn.name;
  const choices:[AddKind,string,boolean][]=[
   ['invoke','Action',true],['if','If',true],['repeat','Repeat',true],['switch','Cases',true],
   ['set','Set variable',fn.locals.length>0],['call','Call function',Boolean(program&&program.functions.length>1)],['return','Return',true],
   ...(eventsSupported?([['sleep','Timer',true],['awaitEvent','Event wait',true],['forever','Forever',true],['setState','Set state',Boolean(program?.state?.length)],['emitEvent','Send event',Boolean(program?.events?.length)]] as [AddKind,string,boolean][]):[])
  ];
  return <details className="program-add-block"><summary aria-label={'Add block in '+location}>Add block to {parent?branchLabel:fn.name}</summary><div className="room-workspace-actions">{choices.map(([op,title,enabled])=><button key={op} disabled={!enabled} onClick={()=>add(fn.name,op,parent,branch)} aria-label={'+ '+title+' in '+location}>+ {title}</button>)}</div></details>;
 };
 const blocks=(nodes:ProgramNode[],fn:BehaviourProgram['functions'][number]) => <ol className="rule-block-list">{nodes.map(n=><li key={n.id} className={`rule-action-block program-block-${n.op}${run?.nodeId===n.id?' rule-action-active':''}`} data-node-id={n.id}>
  <div className="rule-step-heading"><strong>{label(n)}</strong>{run?.nodeId===n.id&&<span>{run.status??'Running'}</span>}</div>
  {n.op==='invoke'&&<p className="room-workspace-intro">{n.capability==='time.wait'?'Pause':n.bindings.target?expressionLabel(n.bindings.target):n.arguments.target?targets.find(target=>target.id===n.arguments.target)?.name??'Unavailable object':'Creates a new object'}{n.bindings.seconds?' · '+expressionLabel(n.bindings.seconds)+' seconds':typeof n.arguments.seconds==='number'&&' · '+(n.arguments.seconds===0?'Full duration':n.arguments.seconds+' seconds')}</p>}
  {n.op==='invoke'&&resultsSupported&&<details><summary>Variables and results</summary>
   {Object.keys(capabilityBindingFields(n.capability,n.arguments)).filter(key=>capabilityParameterType(n.capability,key,n.arguments)).map(key=><label key={'input_'+key}>{key} from<select aria-label={n.id+' argument '+key+' variable'} value={n.bindings[key]&&'var' in n.bindings[key]?n.bindings[key].var:''} onChange={e=>update(value=>{const block=findBlock(value,n.id)!.body[findBlock(value,n.id)!.at];if(block.op!=='invoke')return;if(e.target.value){value.version=3;value.state??=[];value.events??=[];block.bindings[key]={var:e.target.value};}else delete block.bindings[key];})}><option value="">Literal or expression</option>{[...fn.locals.map(local=>({name:local.name,type:valueType(local.initial,local.type)})),...fn.parameters].filter(local=>local.type===capabilityParameterType(n.capability,key,n.arguments)).map(local=><option key={local.name} value={local.name}>{local.name}</option>)}</select></label>)}
   {Object.keys(capabilityDefinition(n.capability)?.output?.properties??{}).map(key=><label key={'output_'+key}>Save {key}<select aria-label={n.id+' result '+key} value={n.results?.[key]??''} onChange={e=>update(value=>{const block=findBlock(value,n.id)!.body[findBlock(value,n.id)!.at];if(block.op!=='invoke')return;value.version=3;value.state??=[];value.events??=[];block.results??={};if(e.target.value)block.results[key]=e.target.value;else delete block.results[key];})}><option value="">Do not store in a variable</option>{fn.locals.filter(local=>(valueType(local.initial,local.type))===capabilityOutputType(n.capability,key)).map(local=><option key={local.name} value={local.name}>{local.name}</option>)}</select><button aria-label={n.id+' new variable for '+key} onClick={()=>update(value=>{value.version=3;value.state??=[];value.events??=[];const f=value.functions.find(f=>f.name===fn.name)!;let name=key,i=1;while([...f.locals,...f.parameters].some(x=>x.name===name))name=key+(i++);const type=capabilityOutputType(n.capability,key);f.locals.push({name,initial:type==='text'?'':type==='boolean'?false:0});const found=findBlock(value,n.id)!,block=found.body[found.at];if(block.op==='invoke')block.results={...block.results,[key]:name};})}>New result variable</button></label>)}
  </details>}
  <div className="room-workspace-actions"><button aria-label={'Edit values '+n.id} onClick={()=>{setEditing({kind:'visual',id:n.id,buffer:JSON.stringify(n)});setError('');}}>Edit values</button><button onClick={()=>{setEditing({kind:'node',id:n.id,buffer:JSON.stringify(n,null,2)});setError('');}} aria-label={`Edit block ${n.id}`}>Source</button><button aria-label={`Move block ${n.id} up`} onClick={()=>update(value=>{const found=findBlock(value,n.id)!;if(found.at>0)[found.body[found.at-1],found.body[found.at]]=[found.body[found.at],found.body[found.at-1]];})}>↑</button><button aria-label={`Move block ${n.id} down`} onClick={()=>update(value=>{const found=findBlock(value,n.id)!;if(found.at<found.body.length-1)[found.body[found.at+1],found.body[found.at]]=[found.body[found.at],found.body[found.at+1]];})}>↓</button><button aria-label={`Remove block ${n.id}`} onClick={()=>update(value=>{const found=findBlock(value,n.id)!;found.body.splice(found.at,1);})}>Remove</button></div>
  {children(n).map((c,i)=><div key={i} className="program-branch"><small>{c.label}</small>{c.body.length?blocks(c.body,fn):<p className="room-workspace-intro">No actions</p>}{addControls(fn,n.id,i,c.label)}</div>)}
 </li>)}</ol>;
 return <section className="program-editor" aria-label="Program editor">
  <p className="room-workspace-intro">Build with the same blocks as the agent. Add actions inside branches, edit values, then apply the draft. Source editing is optional; applying a draft does not start it.</p>
  {error&&<p role="alert" className="room-message room-message-warning">{error}</p>}
  {!program?<><p role="alert">{parsed.error}</p><p>The saved source is preserved. Repair it here, or delete this behaviour. Other behaviours remain usable.</p><fieldset disabled={disabled}>{editing?<><label>Program JSON<textarea aria-label="Program JSON" spellCheck={false} rows={18} maxLength={24000} value={editing.buffer} onChange={e=>setEditing({...editing,buffer:e.target.value})}/></label><div className="room-workspace-actions"><button onClick={saveEditor}>Update draft</button><button onClick={()=>{setEditing(null);setError('');}}>Discard editor draft</button></div></>:<button onClick={()=>setEditing({kind:'source',id:'',buffer:source})}>Repair source</button>}</fieldset></>:<>
   {run&&<div className="program-watch" aria-label="Live program values"><strong>{run.functionName} · {run.status}</strong>{[...(run.state??[]).map(v=>({...v,name:'state.'+v.name})),...(run.locals??[])].map(v=><p key={v.name}><code>{v.name}</code> = {v.value} <small>({v.type})</small></p>)}</div>}
   {editing?<fieldset disabled={disabled}><legend>{editing.kind==='source'?'Full program source':editing.kind==='declarations'?'State & signals':editing.kind==='newFunction'?'New function':editing.kind==='visual'?'Edit values':editing.kind==='node'?'Edit block '+editing.id:'Edit function '+editing.id}</legend>{editing.kind==='declarations'?<ProgramDeclarationsEditor value={JSON.parse(editing.buffer) as DeclarationDraft} structured={structuredSupported} onChange={value=>setEditing({...editing,buffer:JSON.stringify(value)})}/>:editing.kind==='signature'||editing.kind==='newFunction'?<ProgramFunctionEditor value={JSON.parse(editing.buffer) as FunctionDraft} structured={structuredSupported} entry={editing.kind==='signature'&&editing.id===program.entry} onChange={value=>setEditing({...editing,buffer:JSON.stringify(value)})}/>:editing.kind==='visual'?<ProgramBlockEditor eventFieldsSupported={eventFieldsSupported} eventSubscriptionsSupported={eventSubscriptionsSupported} node={JSON.parse(editing.buffer) as ProgramNode} program={program} fn={program.functions.find(fn=>findBlock({...program,functions:[fn]},editing.id))!} objects={targets} onChange={node=>setEditing({...editing,buffer:JSON.stringify(node)})}/>:<label>Program JSON<textarea aria-label="Program JSON" spellCheck={false} rows={18} maxLength={24000} value={editing.buffer} onChange={e=>setEditing({...editing,buffer:e.target.value})}/></label>}<p className="room-workspace-intro">Update draft validates the complete program. Your changes do not run until applied and triggered.</p><div className="room-workspace-actions"><button onClick={saveEditor}>Update draft</button><button onClick={()=>{setEditing(null);setError('');}}>Discard editor draft</button></div></fieldset>:<fieldset disabled={disabled} className="room-edit-body">
    <div className="room-workspace-actions"><button onClick={()=>setEditing({kind:'source',id:'',buffer:JSON.stringify(program,null,2)})}>Edit full source</button>{eventsSupported&&<button onClick={()=>{setError('');setEditing({kind:'declarations',id:'',base:source,buffer:JSON.stringify(declarationDraft(program))});}}>Edit state & signals</button>}<button disabled={program.functions.length>=16} onClick={()=>{let i=1;while(program.functions.some(f=>f.name==='function_'+i))i++;setError('');setEditing({kind:'newFunction',id:'',base:source,buffer:JSON.stringify({name:'function_'+i,returns:'void',parameters:[],locals:[]})});}}>+ Function</button></div>
    <p className="room-workspace-intro">Starts at <code>{program.entry}</code> · {program.resources.length} declared {program.resources.length===1?'object':'objects'}</p>
    {program.version===3&&<div className="program-watch" aria-label="Program state and events"><strong>Event program</strong><p>Start enables this run. Stop or app pause cancels it. State stays between events until the run ends.</p>{program.state?.map(v=><p key={v.name}><code>state.{v.name}</code> starts at {JSON.stringify(v.initial)}</p>)}
     {program.events?.map(event=><label key={event.name}>{event.name} ({event.type})<input aria-label={'Value for '+event.name} value={signalValues[event.name]??(event.type==='number'?'0':event.type==='boolean'?'false':'')} onChange={e=>setSignalValues({...signalValues,[event.name]:e.target.value})}/><button disabled={!onSignal} onClick={()=>{const value=signalValues[event.name]??(event.type==='number'?'0':event.type==='boolean'?'false':'');if(event.type==='boolean'&&!['true','false'].includes(value)){setError('Use true or false.');return;}const payload=event.type==='number'?Number(value):event.type==='boolean'?value==='true':value;if(typeof payload==='number'&&(!Number.isFinite(payload)||Math.abs(payload)>1000000)){setError('Use a finite number between −1000000 and 1000000.');return;}onSignal?.(event.name,payload);}}>Send event</button></label>)}</div>}
    {program.functions.map(fn=><section key={fn.name} className="program-function" aria-label={'Function '+fn.name}><div className="program-function-heading"><h3>Define {fn.name}({fn.parameters.map(p=>p.name+': '+dataTypeLabel(p.type)).join(', ')})</h3><small>Returns {dataTypeLabel(fn.returns)}</small></div><div className="room-workspace-actions"><button onClick={()=>{setError('');setEditing({kind:'signature',id:fn.name,base:source,buffer:JSON.stringify(functionDraft(fn))});}}>Edit function {fn.name}</button><button onClick={()=>setEditing({kind:'function',id:fn.name,buffer:JSON.stringify(fn,null,2)})}>Source for {fn.name}</button>{fn.name!==program.entry&&<button onClick={()=>update(value=>{value.functions=value.functions.filter(f=>f.name!==fn.name);})}>Remove function {fn.name}</button>}</div>{fn.locals.length>0&&<p>{fn.locals.map(v=>v.name+' = '+JSON.stringify(v.initial)).join(' · ')}</p>}{blocks(fn.body,fn)}{addControls(fn)}
<div className="room-workspace-actions">{(['number','text','boolean'] as const).map(type=><button key={type} disabled={fn.locals.length>=16} onClick={()=>update(value=>{const f=value.functions.find(v=>v.name===fn.name)!;let name=type+'_value',i=1;while([...f.locals,...f.parameters].some(v=>v.name===name))name=type+'_value'+i++;f.locals.push({name,initial:defaultValue(type)});})}>+ {type} variable in {fn.name}</button>)}</div></section>)}
   </fieldset>}
  </>}
 </section>;
}

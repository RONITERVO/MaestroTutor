// Development-only UI fixture. Simulated receipts; no provider or headset access.
import {createRoot} from 'react-dom/client';
import {QuestBookSurface} from '../../src/platform/quest/QuestBookSurface';
import {useMaestroStore,initialSettings} from '../../src/store';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {simpleProgramSteps} from '../../src/core-sdk/room/programs';
import {copyRecipe,parseRecipe} from '../../src/core-sdk/room/recipe';
import robot from './recipeRobot.json';
import nativeProgram from './programBookState.json';
import unavailableProgram from './unavailableProgramState.json';
import historyRecovery from './actionHistoryRecoveryStates.json';
import nativeExecutions from './executionStates.json';
import nativeEvents from './eventProgramStates.json';
import creationProgram from '../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-create.json';
import creationResult from './creationResult.json';
import visualProgram from '../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-visual.json';
import objectEditProgram from './objectEditProgram.json';
import objectEditResults from './objectEditResults.json';
import recipeCreationProgram from './recipeCreationProgram.json';
import recipeCreationResult from './recipeCreationResult.json';
import {capabilityDefinition,validateCapabilityArguments,capabilityResources} from '../../shared/capabilities';
import {behaviourCatalog} from '../../shared/behaviourCatalog';
import nativeRules from './ruleBookState.json';
import {validRuleView,type RuleView} from '../../src/core-sdk/room/rules';
import '../../src/app/index.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
const recipe=parseRecipe(robot);if(!recipe)throw new Error('Native recipe fixture is invalid');
const id='b'.repeat(32),white={r:1,g:1,b:1,a:1};
let state:RoomAgentState={version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'The robot was created from an editable native recipe.',canUndo:false,canRedo:false,physicsRunning:false,visible:true,selectedId:id,created:[],
 objects:[{id:'book',objectRevision:1,name:'My conversation book',kind:'Book',position:{x:0,y:1.1,z:.6},scale:1,color:white,animated:false},{id:'maestro',objectRevision:2,name:'Maestro',kind:'Maestro',position:{x:-.8,y:0,z:1.4},scale:1,color:white,animated:false},{id,objectRevision:3,name:'Practice robot',kind:'Assembly',position:{x:.4,y:.8,z:.8},scale:.4,color:white,animated:true}],inspection:{id,objectRevision:3,recipe}};
if(!validRuleView(nativeRules))throw new Error('Native rule observation fixture is invalid');
state.capabilities=['behaviourPrograms.v3'];state.rules=JSON.parse(JSON.stringify(nativeRules));state.workspaceView='objects';
const eventPrograms=new URLSearchParams(location.search).has('events');let signalCount=0;
if(eventPrograms)state=JSON.parse(JSON.stringify(nativeEvents.waiting));
const programs=new URLSearchParams(location.search).has('program');if(programs)state=JSON.parse(JSON.stringify(nativeProgram));
if(new URLSearchParams(location.search).has('execution')){state=JSON.parse(JSON.stringify(nativeExecutions.running));state.visible=true;state.execution={selected:null,running:[],outcomes:[]};}
const visualBlocks=new URLSearchParams(location.search).has('visualBlocks');
const objectEdits=new URLSearchParams(location.search).has('objectEdits');
const recipeCreation=new URLSearchParams(location.search).has('recipeCreation');
if(new URLSearchParams(location.search).has('creation')||recipeCreation||objectEdits||visualBlocks){
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 const program=visualBlocks?{...visualProgram,resources:[],functions:[{...visualProgram.functions[0],body:[]}]}:objectEdits?objectEditProgram:recipeCreation?recipeCreationProgram:creationProgram;
 const name=visualBlocks?'Speaking greeting':objectEdits?'Make a red ball':recipeCreation?'Create waving robot':'Create and push';
 state.rules!.selected!.program=JSON.stringify(program);state.rules!.selected!.name=name;
 state.rules!.sequences=state.rules!.sequences.map(x=>x.id===state.rules!.selected!.id?{...x,name,steps:program.functions[0].body.length,program:true}:x);
 state.rules!.running=[];state.rules!.outcomes=[];state.rules!.bindings=[];state.rules!.bindingCount=0;state.rules!.buttons=[];
 state.execution=JSON.parse(JSON.stringify(objectEdits?objectEditResults.painted:recipeCreation?recipeCreationResult:creationResult));state.capabilities=[...state.capabilities??[],'eventPrograms.v1','actionResults.v1','recipeCreation.v1','objectEdits.v1','execution.v1','executionReceipts.v1'];
}
if(new URLSearchParams(location.search).has('unavailablePrograms'))state=JSON.parse(JSON.stringify(unavailableProgram));
const recovering=new URLSearchParams(location.search).has('recovery');if(recovering)state=JSON.parse(JSON.stringify(historyRecovery.error));
state.capabilities=[...new Set([...state.capabilities??[],'catalog.v1'])];
if(new URLSearchParams(location.search).has('structured')){
 state.capabilities=[...state.capabilities,'structuredValues.v1'];
 Object.assign(window,{maestroWorkspaceRulesEvidence:(rules:unknown)=>{if(!validRuleView(rules))throw new Error('Invalid native program observation');state={...state,rules:JSON.parse(JSON.stringify(rules)),visible:true,workspaceView:'rules'};}});
}

const prop=simpleProgramSteps(nativeRules.selected.program)?.[0]?.propId;
if(prop)state.objects.push({id:prop,objectRevision:4,name:'Practice ball',kind:'Ball',position:{x:.3,y:.8,z:.8},scale:1,color:white,animated:false});
const undo:typeof recipe[]=[],redo:typeof recipe[]=[];
const ruleUndo:RuleView[]=[],ruleRedo:RuleView[]=[];
const copy=<T,>(value:T):T=>JSON.parse(JSON.stringify(value));
const uuid=()=>crypto.randomUUID().replace(/-/g,'');
createRoot(document.getElementById('root')!).render(<QuestBookSurface><div style={{padding:32}}>Your conversation stays here while the workshop is open.</div></QuestBookSurface>);
// Retain fixture requests so browser probes cannot miss a fast simulated acknowledgement.
const requestHistory:unknown[]=[];Object.assign(window,{maestroWorkspaceRequests:requestHistory});
setInterval(()=>{
 const bridge=window.maestroBook;if(!bridge)return;
 const request=bridge.roomSnapshot().request;
 if(request&&request.session===state.session&&request.sequence===state.ack+1){
  requestHistory.push(copy(request));if(requestHistory.length>32)requestHistory.shift();
  state={...state,ack:request.sequence,ok:true,status:'Fixture action completed'};
  for(const command of request.commands){
   if(command.action==='execution'&&command.execution){
    const input=command.execution;
    if(input.operation==='recover'&&recovering&&input.recoveryId===historyRecovery.error.execution.recovery.id){state.execution=copy(historyRecovery.success.execution);state.status=historyRecovery.success.status;}
    else if(input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeExecutions.running.execution.selected.call)){
     state.execution=copy(nativeExecutions.running.execution) as RoomAgentState['execution'];state.status='Replayed native running observation';
    }else if(input.operation==='cancel'&&input.runId===nativeExecutions.running.execution.selected.id){
     state.execution=copy(nativeExecutions.cancelled.execution) as RoomAgentState['execution'];state.status='Replayed native cancelled observation';
    }else {state.ok=false;state.status='This browser fixture only replays the recorded native call. It does not execute actions.';}
   }else if(command.action==='catalog'&&command.catalog){
    const query=command.catalog;
    if(query.operation!=='check'&&query.category&&query.category!=='actions'){state.ok=false;state.status='This older browser fixture supports action discovery only.';continue;}
    if(query.operation==='search'){
     const terms=query.query.toLowerCase().trim().split(/ +/).filter(Boolean);
     const matches=behaviourCatalog.actions.filter(x=>terms.every(term=>(x.id+' '+x.label+' '+x.requirements.join(' ')).toLowerCase().includes(term))).sort((a,b)=>a.id.localeCompare(b.id));
     const offset=Math.min(query.offset,Math.max(0,Math.floor((matches.length-1)/6)*6));
     state.catalog={operation:'search',query:query.query,offset,pageSize:6,total:matches.length,entries:matches.slice(offset,offset+6).map(({id,version,label})=>({id,version,label})),status:'Fixture catalog search'};
    }else if(query.operation==='inspect'){
     const definition=capabilityDefinition(query.capability);
     state.catalog={operation:'inspect',capability:query.capability,version:query.version,...(query.category?{category:'actions' as const}:{}),definition:definition?.version===query.version?definition:null,status:'Fixture definition'};
    }else{
     const call=query.call,valid=validateCapabilityArguments(call.id,call.version,call.arguments)===null;
     state.catalog={operation:'check',call,valid,available:false,occupied:false,resources:valid?capabilityResources(call.id,call.arguments):[],status:'Browser preview cannot verify live action availability. Check in Unity.'};
    }
   }else if(command.action==='workspace')state.visible=command.visible;
   else if(command.action==='inspect'){state.workspaceView='objects';state.inspection={id:command.target!,objectRevision:state.objects.find(x=>x.id===command.target)!.objectRevision!,recipe:command.target===id?recipe:null};}
   else if(command.action==='recipe'&&command.target===id&&parseRecipe(command.recipe)){
    undo.push(copyRecipe(recipe));redo.length=0;Object.assign(recipe,copyRecipe(command.recipe as typeof recipe));state.sceneRevision++;
    state.objects=state.objects.map(x=>x.id===id?{...x,objectRevision:state.sceneRevision}:x);state.inspection={id,objectRevision:state.sceneRevision,recipe};
   }else if(command.action==='play'||command.action==='stop')state.objects=state.objects.map(x=>x.id===command.target?{...x,animated:command.action==='play'}:x);
   else if(command.action==='undo'&&undo.length){redo.push(copyRecipe(recipe));Object.assign(recipe,undo.pop());state.sceneRevision++;state.objects=state.objects.map(x=>x.id===id?{...x,objectRevision:state.sceneRevision}:x);state.inspection={id,objectRevision:state.sceneRevision,recipe};}
   else if(command.action==='redo'&&redo.length){undo.push(copyRecipe(recipe));Object.assign(recipe,redo.pop());state.sceneRevision++;state.objects=state.objects.map(x=>x.id===id?{...x,objectRevision:state.sceneRevision}:x);state.inspection={id,objectRevision:state.sceneRevision,recipe};}
   else if(command.action==='rules'&&command.rule) {
    const rule=command.rule;let view=state.rules!;state.workspaceView='rules';state.inspection=null;
    if(!['inspect','stop'].includes(rule.action)&&rule.revision!==view.revision){state.ok=false;state.status='Behaviours changed';continue;}
    if(rule.action==='inspect')view.status='Behaviour inspected';
    if(rule.action==='signal'){
     if(eventPrograms&&rule.eventName==='user.wave'&&rule.value===1&&view.selected?.program===nativeEvents.waiting.rules.selected.program){
      view=copy(signalCount++===0?nativeEvents.moving.rules:nativeEvents.second.rules) as RuleView;state.status='Replayed native event result';
     }else{state.ok=false;state.status='This browser fixture only replays the recorded user.wave signal. It does not execute programs.';}
    }
    if(rule.action==='edit'){
     ruleUndo.push(copy(view));ruleRedo.length=0;view=copy(view);
     for(const edit of rule.edits??[]) {
      if(edit.kind==='save'&&edit.sequence){const sequence=copy(edit.sequence);sequence.id ||= uuid();view.selected=sequence;view.selectedError=null;view.sequences=view.sequences.filter(x=>x.id!==sequence.id);view.sequences.push({id:sequence.id,name:sequence.name,steps:JSON.parse(sequence.program).functions.reduce((n:number,f:{body:unknown[]})=>n+f.body.length,0),program:Boolean(sequence.program),repeat:sequence.repeat});}
      else if(edit.kind==='bind'&&edit.binding){const binding=copy(edit.binding);binding.id ||= uuid();view.bindings=view.bindings.filter(x=>x.id!==binding.id);view.bindings.push(binding);view.bindingCount=view.bindings.length;}
      else if(edit.kind==='unbind') {view.bindings=view.bindings.filter(x=>x.id!==edit.target);view.bindingCount=view.bindings.length;}
      else if(edit.kind==='button')view.buttons.push({id:uuid(),sequenceId:edit.target!,mount:edit.mount!,position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1}});
      else if(edit.kind==='unbutton')view.buttons=view.buttons.filter(x=>x.id!==edit.target);
      else {state.ok=false;state.status='This fixture does not simulate that edit';}
     }
     view.revision++;view.running=[];view.status='Fixture behaviour edit applied';
    } else if(rule.action==='undo'&&ruleUndo.length){const revision=view.revision+1;ruleRedo.push(copy(view));view=ruleUndo.pop()!;view.revision=revision;}
    else if(rule.action==='redo'&&ruleRedo.length){const revision=view.revision+1;ruleUndo.push(copy(view));view=ruleRedo.pop()!;view.revision=revision;}
    else if(rule.action==='play'&&view.selected?.program){state.ok=false;state.status='Programs execute in Unity. This browser fixture only replays recorded native observations.';}
    else if(rule.action==='stop'){if(eventPrograms)view=copy(nativeEvents.stopped.rules) as RuleView;else view.running=[];view.status='Fixture playback stopped';}
    view.canUndo=ruleUndo.length>0;view.canRedo=ruleRedo.length>0;state.rules=view;
   }
   else{state.ok=false;state.status='This development fixture only simulates recipe editing and playback.';}
  }
  state.canUndo=undo.length>0;state.canRedo=redo.length>0;
 }
 state={...state,revision:state.revision+1};bridge.roomState(JSON.parse(JSON.stringify(state)));
},200);

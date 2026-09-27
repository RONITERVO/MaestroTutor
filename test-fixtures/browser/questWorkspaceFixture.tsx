// Development-only UI fixture. Simulated receipts; no provider or headset access.
import {createRoot} from 'react-dom/client';
import {QuestBookSurface} from '../../src/platform/quest/QuestBookSurface';
import {useMaestroStore,initialSettings} from '../../src/store';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {copyRecipe,parseRecipe} from '../../src/core-sdk/room/recipe';
import robot from './recipeRobot.json';
import nativeProgram from './programBookState.json';
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
state.rules=JSON.parse(JSON.stringify(nativeRules));state.workspaceView='objects';
const programs=new URLSearchParams(location.search).has('program');if(programs)state=JSON.parse(JSON.stringify(nativeProgram));
const prop=nativeRules.selected?.steps[0]?.propId;
if(prop)state.objects.push({id:prop,objectRevision:4,name:'Practice ball',kind:'Ball',position:{x:.3,y:.8,z:.8},scale:1,color:white,animated:false});
const undo:typeof recipe[]=[],redo:typeof recipe[]=[];
const ruleUndo:RuleView[]=[],ruleRedo:RuleView[]=[];
const copy=<T,>(value:T):T=>JSON.parse(JSON.stringify(value));
const uuid=()=>crypto.randomUUID().replace(/-/g,'');
createRoot(document.getElementById('root')!).render(<QuestBookSurface><div style={{padding:32}}>Your conversation stays here while the workshop is open.</div></QuestBookSurface>);
setInterval(()=>{
 const bridge=window.maestroBook;if(!bridge)return;
 const request=bridge.roomSnapshot().request;
 if(request&&request.session===state.session&&request.sequence===state.ack+1){
  state={...state,ack:request.sequence,ok:true,status:'Fixture action completed'};
  for(const command of request.commands){
   if(command.action==='workspace')state.visible=command.visible;
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
    if(rule.action==='edit'){
     ruleUndo.push(copy(view));ruleRedo.length=0;view=copy(view);
     for(const edit of rule.edits??[]) {
      if(edit.kind==='save'&&edit.sequence){const sequence=copy(edit.sequence);sequence.id ||= uuid();for(const step of sequence.steps)step.id ||= uuid();view.selected=sequence;view.sequences=view.sequences.filter(x=>x.id!==sequence.id);view.sequences.push({id:sequence.id,name:sequence.name,steps:sequence.program?JSON.parse(sequence.program).functions.reduce((n:number,f:{body:unknown[]})=>n+f.body.length,0):sequence.steps.length,program:Boolean(sequence.program),repeat:sequence.repeat});}
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
    else if(rule.action==='play'&&view.selected){view.running=[{id:uuid(),sequenceId:view.selected.id,stepId:view.selected.steps[0].id,preparing:false}];view.status='Fixture playback started';}
    else if(rule.action==='stop'){view.running=[];view.status='Fixture playback stopped';}
    view.canUndo=ruleUndo.length>0;view.canRedo=ruleRedo.length>0;state.rules=view;
   }
   else{state.ok=false;state.status='This development fixture only simulates recipe editing and playback.';}
  }
  state.canUndo=undo.length>0;state.canRedo=redo.length>0;
 }
 state={...state,revision:state.revision+1};bridge.roomState(JSON.parse(JSON.stringify(state)));
},200);

// Development-only UI fixture. Simulated receipts; no provider or headset access.
import {createRoot} from 'react-dom/client';
import {QuestBookSurface} from '../../src/platform/quest/QuestBookSurface';
import {useMaestroStore,initialSettings} from '../../src/store';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {copyRecipe,parseRecipe} from '../../src/core-sdk/room/recipe';
import robot from './recipeRobot.json';
import '../../src/app/index.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
const recipe=parseRecipe(robot);if(!recipe)throw new Error('Native recipe fixture is invalid');
const id='b'.repeat(32),white={r:1,g:1,b:1,a:1};
let state:RoomAgentState={version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'The robot was created from an editable native recipe.',canUndo:false,canRedo:false,physicsRunning:false,visible:true,selectedId:id,created:[],
 objects:[{id:'book',objectRevision:1,name:'My conversation book',kind:'Book',position:{x:0,y:1.1,z:.6},scale:1,color:white,animated:false},{id:'maestro',objectRevision:2,name:'Maestro',kind:'Maestro',position:{x:-.8,y:0,z:1.4},scale:1,color:white,animated:false},{id,objectRevision:3,name:'Practice robot',kind:'Assembly',position:{x:.4,y:.8,z:.8},scale:.4,color:white,animated:true}],inspection:{id,objectRevision:3,recipe}};
const undo:typeof recipe[]=[],redo:typeof recipe[]=[];
createRoot(document.getElementById('root')!).render(<QuestBookSurface><div style={{padding:32}}>Your conversation stays here while the workshop is open.</div></QuestBookSurface>);
setInterval(()=>{
 const bridge=window.maestroBook;if(!bridge)return;
 const request=bridge.roomSnapshot().request;
 if(request&&request.session===state.session&&request.sequence===state.ack+1){
  state={...state,ack:request.sequence,ok:true,status:'Fixture action completed'};
  for(const command of request.commands){
   if(command.action==='workspace')state.visible=command.visible;
   else if(command.action==='inspect')state.inspection={id:command.target!,objectRevision:state.objects.find(x=>x.id===command.target)!.objectRevision!,recipe:command.target===id?recipe:null};
   else if(command.action==='recipe'&&command.target===id&&parseRecipe(command.recipe)){
    undo.push(copyRecipe(recipe));redo.length=0;Object.assign(recipe,copyRecipe(command.recipe as typeof recipe));state.sceneRevision++;
    state.objects=state.objects.map(x=>x.id===id?{...x,objectRevision:state.sceneRevision}:x);state.inspection={id,objectRevision:state.sceneRevision,recipe};
   }else if(command.action==='play'||command.action==='stop')state.objects=state.objects.map(x=>x.id===command.target?{...x,animated:command.action==='play'}:x);
   else if(command.action==='undo'&&undo.length){redo.push(copyRecipe(recipe));Object.assign(recipe,undo.pop());state.sceneRevision++;state.objects=state.objects.map(x=>x.id===id?{...x,objectRevision:state.sceneRevision}:x);state.inspection={id,objectRevision:state.sceneRevision,recipe};}
   else if(command.action==='redo'&&redo.length){undo.push(copyRecipe(recipe));Object.assign(recipe,redo.pop());state.sceneRevision++;state.objects=state.objects.map(x=>x.id===id?{...x,objectRevision:state.sceneRevision}:x);state.inspection={id,objectRevision:state.sceneRevision,recipe};}
   else{state.ok=false;state.status='This development fixture only simulates recipe editing and playback.';}
  }
  state.canUndo=undo.length>0;state.canRedo=redo.length>0;
 }
 state={...state,revision:state.revision+1};bridge.roomState(JSON.parse(JSON.stringify(state)));
},200);

import nativeChannelWait from './channelWaitState.json';
import nativeAnchorZone from './anchorZoneState.json';
import nativeRemembered from './rememberedProgramState.json';
import nativeRecipeEdit from './recipeAuthoring.json';
import nativeDrawing from './drawingAuthoring.json';
import nativeCopy from './objectCopy.json';
import nativeSurface from './surfacePlacement.json';
import nativeEnvironment from './roomEnvironment.json';
import repeatConversion from '../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-repeat-conversion.json';
import nativeSimulation from './physicsSimulation.json';
import type {DataValue} from '../../shared/programValues';
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
import nativeRemoval from './workspaceRemoval.json';
import nativeAuthoring from './animationAuthoring.json';
import nativeRecording from './recordingSessions.json';
import nativePosing from './posingSessions.json';
import nativeModelImport from './modelSelection.json';
import nativeImportReadback from './importReadback.json';
import nativeController from './controllerConfiguration.json';
import nativeModes from './controllerModes.json';
import nativeRecovery from './toolRecovery.json';
import nativePlacement from './worldPlacement.json';
import nativeWorldIdentity from './worldIdentity.json';
import nativeWorldGround from './worldGround.json';
import nativeEntityEnvironment from './entityEnvironment.json';
import nativeSpatial from './spatialSettings.json';
import nativeMotionBatch from './motionBatchImport.json';
import nativeAvatar from './avatarSelection.json';
import nativeEvents from './eventProgramStates.json';
import compositionProgram from '../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-build-structure.json';
import creationProgram from '../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-create.json';
import creationResult from './creationResult.json';
import visualProgram from '../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-visual.json';
import objectEditProgram from './objectEditProgram.json';
import objectEditResults from './objectEditResults.json';
import recipeCreationProgram from './recipeCreationProgram.json';
import recipeCreationResult from './recipeCreationResult.json';
import {capabilityDefinition,validateCapabilityArguments,capabilityResources} from '../../shared/capabilities';
import {behaviourCatalog,behaviourFact} from '../../shared/behaviourCatalog';
import {validCatalogView} from '../../shared/roomCatalog';
import {validExecutionView} from '../../shared/roomExecutions';
import nativeRules from './ruleBookState.json';
import {validRuleView,type RuleView} from '../../src/core-sdk/room/rules';
import '../../src/app/index.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
if(!validExecutionView(nativeRemoval.previewExecution)||!validExecutionView(nativeRemoval.removeExecution))throw new Error('Invalid native removal fixture');
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
const avatarSelection=new URLSearchParams(location.search).has('avatarSelection');
let avatarObservation=nativeAvatar.before;
if(avatarSelection){
 if(!validExecutionView(nativeAvatar.library)||!validExecutionView(nativeAvatar.selection))throw new Error('Invalid native avatar fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeAvatar.library)),selected:null,running:[],outcomes:[],nextRunId:nativeAvatar.library.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','avatarModels.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const controllerConfiguration=new URLSearchParams(location.search).has('controllerConfiguration');
let controllerObservation=nativeController.before;
if(controllerConfiguration){
 if(![nativeController.movement,nativeController.button].every(validExecutionView))throw new Error('Invalid native controller fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeController.movement)),selected:null,running:[],outcomes:[],nextRunId:nativeController.movement.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','controllerConfiguration.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const toolRecovery=new URLSearchParams(location.search).has('toolRecovery');
let recoveryObservation=nativeRecovery.before;
if(toolRecovery){
 if(!validExecutionView(nativeRecovery.receipt))throw new Error('Invalid native tool recovery fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeRecovery.receipt)),selected:null,running:[],outcomes:[],nextRunId:nativeRecovery.receipt.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','toolRecovery.v1','structuredValues.v1','factQueries.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const worldIdentity=new URLSearchParams(location.search).has('worldIdentity');
if(worldIdentity){
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','worldIdentity.v1','structuredValues.v1','factQueries.v1'];
}
const worldGround=new URLSearchParams(location.search).has('worldGround');
if(worldGround){
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','terrainTraversal.v1','structuredValues.v1','factQueries.v1'];
}
const environmentProfiles=new URLSearchParams(location.search).has('environmentProfiles');
if(environmentProfiles){
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','environmentProfiles.v1','structuredValues.v1','factQueries.v1'];
}
const worldPlacement=new URLSearchParams(location.search).has('worldPlacement');
let placementObservation=nativePlacement.before;
if(worldPlacement){
 if(!validExecutionView(nativePlacement.receipt))throw new Error('Invalid native world placement fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativePlacement.receipt)),selected:null,running:[],outcomes:[],nextRunId:nativePlacement.receipt.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','worldViewpoint.v1','structuredValues.v1','factQueries.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const controllerModes=new URLSearchParams(location.search).has('controllerModes');
const modeViews=[nativeModes.enable,nativeModes.virtualView,nativeModes.user,nativeModes.mixed];
let modeObservation=nativeModes.before;
if(controllerModes){
 if(!modeViews.every(validExecutionView))throw new Error('Invalid native controller mode fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeModes.enable)),selected:null,running:[],outcomes:[],nextRunId:nativeModes.enable.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','controllerModes.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const recipeAuthoring=new URLSearchParams(location.search).has('recipeAuthoring');
if(recipeAuthoring){
 if(!validExecutionView(nativeRecipeEdit.receipt))throw new Error('Invalid native recipe edit fixture');
 state.objects=[...state.objects.filter(o=>o.id==='book'||o.id==='maestro'),{id:nativeRecipeEdit.before.target,objectRevision:nativeRecipeEdit.before.revision,name:'Practice robot',kind:'Assembly',position:{x:.3,y:1.3,z:.65},scale:1,color:{r:.4,g:.5,b:.6,a:1},animated:true}];state.selectedId=nativeRecipeEdit.before.target;
 state.inspection={id:nativeRecipeEdit.before.target,objectRevision:nativeRecipeEdit.before.revision,recipe:parseRecipe(nativeRecipeEdit.beforeRecipe)};
 state.execution={...JSON.parse(JSON.stringify(nativeRecipeEdit.receipt)),selected:null,running:[],outcomes:[],nextRunId:nativeRecipeEdit.receipt.selected.id};
 state.capabilities=[...state.capabilities??[],'catalog.v1','catalogVocabulary.v1','recipeEdits.v1','structuredValues.v1','factQueries.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const drawingAuthoring=new URLSearchParams(location.search).has('drawingAuthoring');
let drawingObservation=nativeDrawing.before;
if(drawingAuthoring){
 if(![nativeDrawing.creation,nativeDrawing.edit].every(validExecutionView))throw new Error('Invalid native drawing fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';state.rules={...state.rules!,selected:null,running:[],outcomes:[]};
 state.execution={...JSON.parse(JSON.stringify(nativeDrawing.creation)),selected:null,running:[],outcomes:[],nextRunId:nativeDrawing.creation.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','drawingEdits.v1','structuredValues.v1','factQueries.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const objectCopy=new URLSearchParams(location.search).has('objectCopy');
if(objectCopy){
 if(!validExecutionView(nativeCopy.receipt))throw new Error('Invalid native object copy fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';state.rules={...state.rules!,selected:null,running:[],outcomes:[]};
 state.objects=[...state.objects.filter(o=>o.id==='book'||o.id==='maestro'),{id:nativeCopy.before.target,objectRevision:nativeCopy.before.revision,name:'Drawing source',kind:'Drawing',position:nativeCopy.before.position,scale:nativeCopy.before.scale,color:white,animated:true}];
 state.execution={...JSON.parse(JSON.stringify(nativeCopy.receipt)),selected:null,running:[],outcomes:[],nextRunId:nativeCopy.receipt.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','objectCopy.v1','structuredValues.v1','factQueries.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const surfacePlacement=new URLSearchParams(location.search).has('surfacePlacement');
if(surfacePlacement){
 if(!validExecutionView(nativeSurface.receipt))throw new Error('Invalid native surface placement fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';state.rules={...state.rules!,selected:null,running:[],outcomes:[]};
 state.objects=[...state.objects.filter(o=>o.id==='book'||o.id==='maestro'),{id:nativeSurface.request.call.arguments.target,objectRevision:nativeSurface.beforeRevision,name:'Placement block',kind:'Block',position:nativeSurface.before,scale:1,color:white,animated:false}];
 state.execution={...JSON.parse(JSON.stringify(nativeSurface.receipt)),selected:null,running:[],outcomes:[],nextRunId:nativeSurface.receipt.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','surfacePlacement.v1','structuredValues.v1','factQueries.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const roomEnvironment=new URLSearchParams(location.search).has('roomEnvironment');
const environmentViews=[nativeEnvironment.loadReceipt,nativeEnvironment.showReceipt,nativeEnvironment.hideReceipt];
let environmentObservation=nativeEnvironment.before;
if(roomEnvironment){
 if(!environmentViews.every(validExecutionView))throw new Error('Invalid native room environment fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';state.rules!.running=[];state.rules!.outcomes=[];
 state.execution={...JSON.parse(JSON.stringify(nativeEnvironment.loadReceipt)),selected:null,running:[],outcomes:[],nextRunId:nativeEnvironment.loadReceipt.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','roomEnvironment.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
 Object.assign(window,{maestroRoomEnvironmentLoaded:()=>{environmentObservation=nativeEnvironment.loaded;state={...state,revision:state.revision+1,status:'Captured platform completion; no headset scan'};}});
}
const physicsSimulation=new URLSearchParams(location.search).has('physicsSimulation');
const simulationViews=[nativeSimulation.start,nativeSimulation.pause];
let simulationObservation=nativeSimulation.before;
if(physicsSimulation){
 if(!simulationViews.every(validExecutionView))throw new Error('Invalid native physics simulation fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeSimulation.start)),selected:null,running:[],outcomes:[],nextRunId:nativeSimulation.start.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','physicsSimulation.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const spatialSettings=new URLSearchParams(location.search).has('spatialSettings');
const spatialViews=[nativeSpatial.physics,nativeSpatial.movement,nativeSpatial.walk];
let spatialFacts:Record<string,DataValue>={'object.physics.settings':nativeSpatial.beforePhysics,'avatar.movement.settings':nativeSpatial.beforeMovement,'avatar.walk.settings':nativeSpatial.beforeWalk};
if(spatialSettings){
 if(!spatialViews.every(validExecutionView))throw new Error('Invalid native spatial settings fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.objects.push({id:nativeSpatial.beforePhysics.target,objectRevision:nativeSpatial.beforePhysics.revision,name:'Native settings block',kind:'Block',position:{x:0,y:1,z:1},scale:1,color:white,animated:false});
 state.execution={...JSON.parse(JSON.stringify(nativeSpatial.physics)),selected:null,running:[],outcomes:[],nextRunId:nativeSpatial.physics.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','factQueries.v1','structuredValues.v1','spatialSettings.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
if(spatialSettings&&new URLSearchParams(location.search).has('repeatLoops')){
 const cycle=repeatConversion.functions[0];const selected=state.rules!.selected!;
 state.rules!.running=[];state.rules!.outcomes=[];state.rules!.queued=0;state.rules!.status='Ready';
 selected.name='Repeat greeting';selected.repeat=true;selected.program=JSON.stringify({version:2,entry:cycle.name,resources:repeatConversion.resources,functions:[cycle]});
 state.rules!.sequences=[{id:selected.id,name:selected.name,repeat:true,program:true,steps:cycle.body.length}];
}
const importReadback=new URLSearchParams(location.search).has('importReadback');
if(importReadback){
 if(!validExecutionView(nativeImportReadback.execution))throw new Error('Invalid native import readback fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';state.execution=JSON.parse(JSON.stringify(nativeImportReadback.execution));
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','factQueries.v1','modelImport.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const motionBatch=new URLSearchParams(location.search).has('motionBatch');
let batchObservation:typeof nativeMotionBatch.after=nativeMotionBatch.before;
if(motionBatch){
 if(![nativeMotionBatch.select,nativeMotionBatch.category,nativeMotionBatch.start].every(validExecutionView))throw new Error('Invalid native batch fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeMotionBatch.select)),selected:null,running:[],outcomes:[],nextRunId:nativeMotionBatch.select.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','factQueries.v1','motionBatchImport.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const modelSelection=new URLSearchParams(location.search).has('modelSelection');
let modelImportObservation:typeof nativeModelImport.after=nativeModelImport.before;
if(modelSelection){
 if(![nativeModelImport.select,nativeModelImport.accept].every(validExecutionView))throw new Error('Invalid native model import fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeModelImport.select)),selected:null,running:[],outcomes:[],nextRunId:nativeModelImport.select.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','modelImport.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const poseSessions=new URLSearchParams(location.search).has('poseSessions');
let poseObservation:typeof nativePosing.active=nativePosing.before;
if(poseSessions){
 if(![nativePosing.start,nativePosing.rotate,nativePosing.finish].every(validExecutionView))throw new Error('Invalid native posing fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativePosing.start)),selected:null,running:[],outcomes:[],nextRunId:nativePosing.start.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','animationPosing.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const recordingSessions=new URLSearchParams(location.search).has('recordingSessions');
let recordingObservation=nativeRecording.before;
if(recordingSessions){
 if(!validExecutionView(nativeRecording.start)||!validExecutionView(nativeRecording.finish))throw new Error('Invalid native recording fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeRecording.start)),selected:null,running:[],outcomes:[],nextRunId:nativeRecording.start.selected.id};
 state.capabilities=[...state.capabilities??[],'catalogVocabulary.v1','animationRecording.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const authoring=new URLSearchParams(location.search).has('animationAuthoring');
if(authoring){
 if(!validExecutionView(nativeAuthoring.execution))throw new Error('Invalid native authoring fixture');
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 state.execution={...JSON.parse(JSON.stringify(nativeAuthoring.execution)),selected:null,running:[],outcomes:[],nextRunId:nativeAuthoring.execution.selected.id};
 state.capabilities=[...state.capabilities??[],'animationAuthoring.v1','execution.v1','executionReceipts.v1','actionResults.v1'];
}
const disposal=new URLSearchParams(location.search).has('disposal');
if(disposal){state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';state.execution=JSON.parse(JSON.stringify(nativeRemoval.previewExecution));state.execution!.workspace!.nextRunId=nativeRemoval.removeExecution.workspace.selected.id;state.capabilities=[...state.capabilities??[],'workspaceRetention.v1','workspaceDisposal.v1','execution.v1','executionReceipts.v1','actionResults.v1'];}
const visualBlocks=new URLSearchParams(location.search).has('visualBlocks');
const objectEdits=new URLSearchParams(location.search).has('objectEdits');
const composition=new URLSearchParams(location.search).has('composition');
const recipeCreation=new URLSearchParams(location.search).has('recipeCreation');
if(new URLSearchParams(location.search).has('creation')||recipeCreation||objectEdits||visualBlocks||composition){
 state=JSON.parse(JSON.stringify(nativeProgram));state.visible=true;state.workspaceView='rules';
 const compositionDraft=structuredClone(compositionProgram);compositionDraft.functions[0].body[2].arguments!.source!.name='Untitled structure';
 const program=composition?compositionDraft:visualBlocks?{...visualProgram,resources:[],functions:[{...visualProgram.functions[0],body:[]}]}:objectEdits?objectEditProgram:recipeCreation?recipeCreationProgram:creationProgram;
 const name=composition?'Build and restore castle':visualBlocks?'Speaking greeting':objectEdits?'Make a red ball':recipeCreation?'Create waving robot':'Create and push';
 state.rules!.selected!.program=JSON.stringify(program);state.rules!.selected!.name=name;
 state.rules!.sequences=state.rules!.sequences.map(x=>x.id===state.rules!.selected!.id?{...x,name,steps:program.functions[0].body.length,program:true}:x);
 state.rules!.running=[];state.rules!.outcomes=[];state.rules!.bindings=[];state.rules!.bindingCount=0;state.rules!.buttons=[];
 state.execution=JSON.parse(JSON.stringify(objectEdits?objectEditResults.painted:recipeCreation?recipeCreationResult:creationResult));state.capabilities=[...state.capabilities??[],'eventPrograms.v1','actionResults.v1','recipeCreation.v1','objectEdits.v1','execution.v1','executionReceipts.v1'];
}
if(composition)state.capabilities=[...state.capabilities??[],'batchCreation.v1','structures.v1','structuredValues.v1','structuredResults.v1','structuredInputs.v1'];
if(new URLSearchParams(location.search).has('unavailablePrograms'))state=JSON.parse(JSON.stringify(unavailableProgram));
if(new URLSearchParams(location.search).has('memory')){state=JSON.parse(JSON.stringify(nativeRemembered.stopped));state.visible=true;state.workspaceView='rules';}
if(new URLSearchParams(location.search).has('channelWait')) state=structuredClone(new URLSearchParams(location.search).has('waiting')?nativeChannelWait.waiting:nativeChannelWait.saved) as unknown as RoomAgentState;
if(new URLSearchParams(location.search).has('anchorZone')) state=structuredClone(nativeAnchorZone.saved) as unknown as RoomAgentState;
const recovering=new URLSearchParams(location.search).has('recovery');if(recovering)state=JSON.parse(JSON.stringify(historyRecovery.error));
state.capabilities=[...new Set([...state.capabilities??[],'catalog.v1'])];
if(new URLSearchParams(location.search).has('modules'))state.capabilities.push('programModules.v1');
if(new URLSearchParams(location.search).has('structured')){
 state.capabilities=[...state.capabilities,'structuredValues.v1'];
 Object.assign(window,{maestroWorkspaceRulesEvidence:(rules:unknown)=>{if(!validRuleView(rules))throw new Error('Invalid native program observation');state={...state,rules:JSON.parse(JSON.stringify(rules)),visible:true,workspaceView:'rules'};}});
}

if(new URLSearchParams(location.search).has('parallel'))Object.assign(window,{maestroParallelEvidence:(evidence:RoomAgentState)=>{
 if(!validRuleView(evidence.rules))throw new Error('Invalid native parallel observation');
 state={...JSON.parse(JSON.stringify(evidence)),revision:state.revision+1,ack:state.ack,visible:true,workspaceView:'rules'};
}});

let moduleEvidence:Record<string,RoomAgentState>|null=null;
if(new URLSearchParams(location.search).has('moduleLibrary'))Object.assign(window,{maestroModuleLibraryEvidence:(evidence:Record<string,RoomAgentState>)=>{
 for(const key of ['published','search','inspected','running','removed'])if(!evidence[key]||!validRuleView(evidence[key].rules))throw new Error('Native module evidence is missing');
 if(!validCatalogView(evidence.search.catalog)||!validCatalogView(evidence.inspected.catalog))throw new Error('Invalid module catalog evidence');
 moduleEvidence=JSON.parse(JSON.stringify(evidence));state={...JSON.parse(JSON.stringify(evidence.published)),revision:state.revision+1,ack:0,visible:true,workspaceView:'rules'};
}});

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
    if(avatarSelection&&input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeAvatar.library.selected.call)){state.execution=copy(nativeAvatar.library) as RoomAgentState['execution'];state.status='Recorded native library metadata; browser acknowledgement is simulated';}
    else if(avatarSelection&&input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeAvatar.selection.selected.call)){state.execution=copy(nativeAvatar.selection) as RoomAgentState['execution'];avatarObservation=nativeAvatar.after;state.status='Recorded native model selection; browser acknowledgement is simulated';}
    else if(spatialSettings&&input.operation==='start'){
     const index=spatialViews.findIndex(view=>input.call?.id===view.selected.call.id&&input.call.version===view.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(view.selected.call.arguments).sort()));
     if(index<0){state.ok=false;state.status='Only captured native spatial settings can be replayed';}
     else{state.execution=copy(spatialViews[index]) as RoomAgentState['execution'];spatialFacts=index===0?{...spatialFacts,'object.physics.settings':nativeSpatial.afterPhysics}:index===1?{...spatialFacts,'avatar.movement.settings':nativeSpatial.afterMovement,'avatar.walk.settings':nativeSpatial.walkBeforeSave}:{...spatialFacts,'avatar.walk.settings':nativeSpatial.afterWalk};state.status='Captured native settings result; no headset or provider execution';}
    }
    else if(toolRecovery&&input.operation==='start'){
     const view=nativeRecovery.receipt;
     if(input.runId!==view.selected.id||input.call.id!==view.selected.call.id||input.call.version!==view.selected.call.version||JSON.stringify(Object.entries(input.call.arguments).sort())!==JSON.stringify(Object.entries(view.selected.call.arguments).sort())){state.ok=false;state.status='Only captured native recovery can be replayed';}
     else{state.execution=copy(view) as RoomAgentState['execution'];recoveryObservation=nativeRecovery.after;state.status='Captured native tool recovery; browser does not move a headset';}
    }
    else if(worldPlacement&&input.operation==='start'){
     const view=nativePlacement.receipt;
     if(input.runId!==view.selected.id||input.call.id!==view.selected.call.id||input.call.version!==view.selected.call.version||JSON.stringify(Object.entries(input.call.arguments).sort())!==JSON.stringify(Object.entries(view.selected.call.arguments).sort())){state.ok=false;state.status='Only captured native placement can be replayed';}
     else{state.execution=copy(view) as RoomAgentState['execution'];placementObservation=nativePlacement.after;state.status='Captured native world placement; browser does not move a headset';}
    }
    else if(controllerModes&&input.operation==='start'){
     const index=modeViews.findIndex(view=>input.call?.id===view.selected.call.id&&input.call.version===view.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(view.selected.call.arguments).sort()));
     if(index<0){state.ok=false;state.status='Only captured native mode transitions can be replayed';}
     else{state.execution=copy(modeViews[index]) as RoomAgentState['execution'];modeObservation=modeViews[index].selected.output;state.status='Captured native mode result; this browser does not move a headset';}
    }
    else if(recipeAuthoring&&input.operation==='start'){
     const view=nativeRecipeEdit.receipt;
     if(input.runId!==view.selected.id||input.call.id!==view.selected.call.id||input.call.version!==view.selected.call.version||JSON.stringify(Object.entries(input.call.arguments).sort())!==JSON.stringify(Object.entries(view.selected.call.arguments).sort())){state.ok=false;state.status='Only the captured native recipe edit can be replayed';}
     else{state.execution=copy(view) as RoomAgentState['execution'];state.objects=state.objects.map(o=>o.id===nativeRecipeEdit.after.target?{...o,objectRevision:nativeRecipeEdit.after.revision,animated:false}:o);state.inspection={...state.inspection!,id:nativeRecipeEdit.after.target,objectRevision:nativeRecipeEdit.after.revision,recipe:parseRecipe(nativeRecipeEdit.afterRecipe)};state.status='Captured native recipe edit; no headset execution';}
    }
    else if(drawingAuthoring&&input.operation==='start'){
     const view=[nativeDrawing.creation,nativeDrawing.edit].find(v=>input.runId===v.selected.id&&input.call?.id===v.selected.call.id&&input.call.version===v.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(v.selected.call.arguments).sort()));
     if(!view){state.ok=false;state.status='Only captured native drawing requests can be replayed';}
     else{state.execution=copy(view) as RoomAgentState['execution'];drawingObservation=view===nativeDrawing.creation?nativeDrawing.before:nativeDrawing.after;if(!state.objects.some(o=>o.id===nativeDrawing.before.target))state.objects.push({id:nativeDrawing.before.target,objectRevision:drawingObservation.revision,name:'Pencil arch',kind:'Drawing',position:{x:.3,y:1.3,z:.65},scale:1,color:{r:.2,g:.6,b:.9,a:1},animated:false});else state.objects=state.objects.map(o=>o.id===nativeDrawing.before.target?{...o,objectRevision:drawingObservation.revision}:o);state.status='Captured native drawing result; no headset execution';}
    }
    else if(objectCopy&&input.operation==='start'){
     const view=nativeCopy.receipt;
     if(input.runId!==view.selected.id||input.call?.id!==view.selected.call.id||input.call.version!==view.selected.call.version||JSON.stringify(Object.entries(input.call.arguments).sort())!==JSON.stringify(Object.entries(view.selected.call.arguments).sort())){state.ok=false;state.status='Only the captured native copy can be replayed';}
     else{state.execution=copy(view) as RoomAgentState['execution'];if(!state.objects.some(o=>o.id===nativeCopy.copied.target))state.objects.push({id:nativeCopy.copied.target,objectRevision:nativeCopy.copied.revision,name:'Copied drawing',kind:nativeCopy.copied.kind,position:copy(nativeCopy.copied.position),scale:nativeCopy.copied.scale,color:white,animated:true});state.status='Captured native object copy; no headset execution';}
    }
    else if(surfacePlacement&&input.operation==='start'){
     const view=nativeSurface.receipt;
     if(input.call?.id!==view.selected.call.id||JSON.stringify(Object.entries(input.call.arguments).sort())!==JSON.stringify(Object.entries(view.selected.call.arguments).sort())){state.ok=false;state.status='Only the captured native placement can be replayed';}
     else{state.execution=copy(view) as RoomAgentState['execution'];state.objects=state.objects.map(o=>o.id===view.selected.output.target?{...o,objectRevision:view.selected.output.revision,position:copy(view.selected.output.position)}:o);state.status='Captured native surface placement; no live headset raycast';}
    }
    else if(roomEnvironment&&input.operation==='start'){
     const index=environmentViews.findIndex(view=>input.call?.id===view.selected.call.id&&input.call.version===view.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(view.selected.call.arguments).sort()));
     if(index<0){state.ok=false;state.status='Only captured native room setup requests can be replayed';}
     else{state.execution=copy(environmentViews[index]) as RoomAgentState['execution'];environmentObservation=index===0?nativeEnvironment.loading:index===1?nativeEnvironment.showing:nativeEnvironment.hidden;state.physicsRunning=environmentObservation.surfaces.physicsRunning;state.status='Captured native room setup; no permission screen or headset execution';}
    }
    else if(physicsSimulation&&input.operation==='start'){
     const index=simulationViews.findIndex(view=>input.call?.id===view.selected.call.id&&input.call.version===view.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(view.selected.call.arguments).sort()));
     if(index<0){state.ok=false;state.status='Only captured native simulation transitions can be replayed';}
     else{state.execution=copy(simulationViews[index]) as RoomAgentState['execution'];simulationObservation=simulationViews[index].selected.output;state.status='Captured native physics result; this browser does not simulate a headset';}
    }
    else if(controllerConfiguration&&input.operation==='start'){
     const views=[nativeController.movement,nativeController.button];
     const index=views.findIndex(view=>input.call?.id===view.selected.call.id&&input.call.version===view.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(view.selected.call.arguments).sort()));
     if(index<0){state.ok=false;state.status='Only captured native controller edits can be replayed';}
     else{state.execution=copy(views[index]) as RoomAgentState['execution'];controllerObservation=[nativeController.afterMovement,nativeController.after][index];state.status='Captured native settings result; no headset controls changed in this browser';}
    }
    else if(motionBatch&&input.operation==='start'){
     const views=[nativeMotionBatch.select,nativeMotionBatch.category,nativeMotionBatch.start];
     const index=views.findIndex(view=>input.call?.id===view.selected.call.id&&input.call.version===view.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(view.selected.call.arguments).sort()));
     if(index<0){state.ok=false;state.status='Only the captured batch requests can be replayed';}
     else{state.execution=copy(views[index]) as RoomAgentState['execution'];batchObservation=[nativeMotionBatch.ready,nativeMotionBatch.tagged,nativeMotionBatch.after][index];state.status='Captured native batch result; this browser does not open the headset picker';}
    }
    else if(modelSelection&&input.operation==='start'){
     const index=[nativeModelImport.select,nativeModelImport.accept].findIndex(view=>input.call?.id===view.selected.call.id&&input.call.version===view.selected.call.version&&JSON.stringify(Object.entries(input.call.arguments).sort())===JSON.stringify(Object.entries(view.selected.call.arguments).sort()));
     if(index<0){state.ok=false;state.status='Only the captured native import requests can be replayed';}
     else{state.execution=copy([nativeModelImport.select,nativeModelImport.accept][index]) as RoomAgentState['execution'];modelImportObservation=[nativeModelImport.preview,nativeModelImport.after][index];state.status='Captured native model preview/import; no system picker is opened in this browser fixture';}
    }
    else if(poseSessions&&input.operation==='start'){
     const index=[nativePosing.start,nativePosing.rotate,nativePosing.finish].findIndex(view=>JSON.stringify(input.call)===JSON.stringify(view.selected.call));
     if(index<0){state.ok=false;state.status='Only the captured native pose requests can be replayed';}
     else{state.execution=copy([nativePosing.start,nativePosing.rotate,nativePosing.finish][index]) as RoomAgentState['execution'];poseObservation=[nativePosing.active,nativePosing.edited,nativePosing.idle][index];state.status='Captured native pose result; browser acknowledgement is simulated';}
    }
    else if(recordingSessions&&input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeRecording.start.selected.call)){state.execution=copy(nativeRecording.start) as RoomAgentState['execution'];recordingObservation=nativeRecording.active;state.status='Recorded native start; browser acknowledgement is simulated';}
    else if(recordingSessions&&input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeRecording.finish.selected.call)){state.execution=copy(nativeRecording.finish) as RoomAgentState['execution'];recordingObservation=nativeRecording.idle;state.status='Recorded native finish; browser acknowledgement is simulated';}
    else if(authoring&&input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeAuthoring.execution.selected.call)){state.execution=copy(nativeAuthoring.execution) as RoomAgentState['execution'];state.status='Recorded native authoring result; browser acknowledgement is simulated';}
    else if(disposal&&input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeRemoval.removeExecution.workspace.selected.call)){state.execution=copy(nativeRemoval.removeExecution) as RoomAgentState['execution'];state.status='Recorded native disposal result; no files are changed by this browser fixture';}
    else if(moduleEvidence&&input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(moduleEvidence.published.execution?.selected?.call)){
     state.execution=copy(moduleEvidence.published.execution);state.status='Recorded native publication; browser acknowledgement is simulated';
    }else if(input.operation==='recover'&&recovering&&input.recoveryId===historyRecovery.error.execution.recovery.id){state.execution=copy(historyRecovery.success.execution);state.status=historyRecovery.success.status;}
    else if(input.operation==='start'&&JSON.stringify(input.call)===JSON.stringify(nativeExecutions.running.execution.selected.call)){
     state.execution=copy(nativeExecutions.running.execution) as RoomAgentState['execution'];state.status='Replayed native running observation';
    }else if(input.operation==='cancel'&&input.runId===nativeExecutions.running.execution.selected.id){
     state.execution=copy(nativeExecutions.cancelled.execution) as RoomAgentState['execution'];state.status='Replayed native cancelled observation';
    }else {state.ok=false;state.status='This browser fixture only replays the recorded native call. It does not execute actions.';}
   }else if(command.action==='catalog'&&command.catalog){
    const query=command.catalog;
    if(moduleEvidence&&query.operation!=='check'&&query.category==='modules'){
     if(query.operation==='search')state.catalog=copy(moduleEvidence.search.catalog);
     else {const view=moduleEvidence.inspected.catalog;state.catalog=view?.operation==='inspect'&&query.capability===view.capability?copy(view):{operation:'inspect',category:'modules',capability:query.capability,version:query.version,definition:null,revision:1,ready:true,pending:false,status:'Not present in recorded evidence'};}
     continue;
    }
    if(spatialSettings&&query.operation!=='check'&&query.category==='facts'){
     const definitions=Object.keys(spatialFacts).filter(id=>query.operation!=='search'||id.includes(query.query??'')).map(id=>behaviourFact(id)!);
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:definitions.length,pageSize:6,entries:definitions.map(d=>({id:d.id,version:1,label:d.label})),status:'Found spatial settings'};
     else{const definition=behaviourFact(query.capability??'');const value=spatialFacts[query.capability??''];const available=!!value&&(query.capability!=='object.physics.settings'||query.arguments?.target===nativeSpatial.beforePhysics.target);state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition,arguments:query.arguments,available,value:available?copy(value):null,status:'Captured native spatial settings'};}
     continue;
    }
    if(toolRecovery&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('room.tools.recovery')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Book and tool recovery'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(recoveryObservation):null,status:'Captured native recovery state'};
     continue;
    }
    if(worldIdentity&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('world.identity')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'World and region identity'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(nativeWorldIdentity.identity):null,status:'Captured native authored world scope'};
     continue;
    }
    if(environmentProfiles&&query.operation!=='check'&&query.category==='facts'){
     const definitions=['object.environment','environment.profile','environment.profiles'].map(id=>behaviourFact(id)!);
     if(query.operation==='search'){
      const matches=definitions.filter(d=>(d.id+' '+d.label).toLowerCase().includes((query.query??'').toLowerCase()));
      state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:matches.length,pageSize:6,entries:matches.map(d=>({id:d.id,version:1,label:d.label})),status:'Native environment profiles'};
     }else{
      const definition=definitions.find(d=>d.id===query.capability)??null,args=query.arguments??definition?.example;
      const value=definition?.id==='object.environment'&&args?.target==='maestro'?copy(nativeEntityEnvironment.after):
       definition?.id==='environment.profile'&&args?.id===nativeEntityEnvironment.profile.id?copy(nativeEntityEnvironment.profile):
       definition?.id==='environment.profiles'&&args?.offset===0?copy(nativeEntityEnvironment.profiles):
       definition?.id==='environment.profiles'&&args?.offset===16?copy(nativeEntityEnvironment.emptyPage):null;
      state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,arguments:args,definition,available:value!==null,value,status:'Captured native per-object environment'};
     }
     continue;
    }
    if(worldGround&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('world.ground')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Accepted terrain support'};
     else {
      const args=query.arguments??definition.example;
      const match=JSON.stringify(args)===JSON.stringify(nativeWorldGround.arguments);
      const origin=JSON.stringify(args)===JSON.stringify(definition.example);
      state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,arguments:args,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id&&(match||origin),value:query.capability===definition.id?(match?copy(nativeWorldGround.supported):origin?copy(nativeWorldGround.missing):null):null,status:'Captured native terrain support; no movement'};
     }
     continue;
    }
    if(worldPlacement&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('world.viewpoint')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Your world location'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(placementObservation):null,status:'Captured native world placement state'};
     continue;
    }
    if(controllerModes&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('controller.mode')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found live control modes'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(modeObservation):null,status:'Captured native control modes'};
     continue;
    }
    if(recipeAuthoring&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('object.recipe')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Recipe summary'};
     else{const available=query.capability===definition.id&&query.arguments?.target===nativeRecipeEdit.before.target;state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,arguments:query.arguments,definition:query.capability===definition.id?definition:null,available,value:available?copy(state.execution?.selected?.phase==='completed'?nativeRecipeEdit.after:nativeRecipeEdit.before):null,status:'Captured native recipe'};}
     continue;
    }
    if(drawingAuthoring&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('object.drawing')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found pencil stroke'};
     else{const available=query.capability===definition.id&&query.arguments?.target===nativeDrawing.before.target&&state.objects.some(o=>o.id===nativeDrawing.before.target);state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,arguments:query.arguments,definition:query.capability===definition.id?definition:null,available,value:available?copy(drawingObservation):null,status:'Captured native stroke'};}
     continue;
    }
    if(objectCopy&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('object.definition')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found object definition'};
     else{const value=query.arguments?.target===nativeCopy.before.target?nativeCopy.before:state.objects.some(o=>o.id===nativeCopy.copied.target)&&query.arguments?.target===nativeCopy.copied.target?nativeCopy.copied:null;state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,arguments:query.arguments,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id&&!!value,value:query.capability===definition.id?copy(value):null,status:'Captured native definition'};}
     continue;
    }
    if(surfacePlacement&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('room.environment')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found room setup'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(nativeSurface.environment):null,status:'Captured native room setup'};
     continue;
    }
    if(roomEnvironment&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('room.environment')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found room setup'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(environmentObservation):null,status:'Captured native room setup'};
     continue;
    }
    if(physicsSimulation&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('physics.simulation')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found room physics state'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(simulationObservation):null,status:'Captured native physics state'};
     continue;
    }
    if(controllerConfiguration&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('controller.settings')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found controller preferences'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(controllerObservation):null,status:'Captured native controller preferences'};
     continue;
    }
    if(importReadback&&query.operation!=='check'&&query.category==='facts'){
     const definitions=['model.import.selection','model.import.motions'].map(id=>behaviourFact(id)!);
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:2,pageSize:6,entries:definitions.map(d=>({id:d.id,version:1,label:d.label})),status:'Found model-import facts'};
     else{
      const definition=definitions.find(d=>d.id===query.capability)??null;const args=definition?.id==='model.import.motions'?(query.arguments??definition.example):undefined;
      const page=nativeImportReadback.pages.find(p=>p.arguments.requestId===args?.requestId&&p.arguments.motionOffset===args.motionOffset);
      const value=definition?.id==='model.import.selection'?copy(nativeImportReadback.summary):page?copy(page.value):null;
      state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition,arguments:args,available:value!==null,value,status:'Captured native model-import readback'};
     }
     continue;
    }
    if(motionBatch&&query.operation!=='check'&&query.category==='facts'){
     const definitions=['motion.import.batch.status','motion.import.batch.file'].map(id=>behaviourFact(id)!);
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:2,pageSize:6,entries:definitions.map(d=>({id:d.id,version:1,label:d.label})),status:'Found batch facts'};
     else{
      const definition=definitions.find(d=>d.id===query.capability)??null;
      const args=definition?.id==='motion.import.batch.file'?(query.arguments??definition.example):undefined;
      const value=definition?.id==='motion.import.batch.status'?copy(batchObservation):args?.requestId===nativeMotionBatch.after.requestId&&args.motionOffset===0?(args.index===0?copy(nativeMotionBatch.file):args.index===1?copy(nativeMotionBatch.failedFile):null):null;
      state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition,arguments:args,available:value!==null,value,status:'Captured native animation import'};
     }
     continue;
    }
    if(modelSelection&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('model.import.selection')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found import state'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(modelImportObservation):null,status:'Captured native import session'};
     continue;
    }
    if(poseSessions&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('animation.posing')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found pose state'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(poseObservation):null,status:'Captured native pose session'};
     continue;
    }
    if(avatarSelection&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('avatar.model')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found model state'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(avatarObservation):null,status:'Recorded native model state'};
     continue;
    }
    else if(recordingSessions&&query.operation!=='check'&&query.category==='facts'){
     const definition=behaviourFact('animation.recording')!;
     if(query.operation==='search')state.catalog={operation:'search',category:'facts',query:query.query,offset:0,pageSize:6,total:1,entries:[{id:definition.id,version:1,label:definition.label}],status:'Recorded fact discovery'};
     else state.catalog={operation:'inspect',category:'facts',capability:query.capability,version:1,definition:query.capability===definition.id?definition:null,available:query.capability===definition.id,value:query.capability===definition.id?copy(recordingObservation):null,status:'Recorded native recorder state'};
     continue;
    }
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
   else if(recipeAuthoring&&command.action==='inspect'&&command.target===nativeRecipeEdit.before.target){state.workspaceView='objects';state.inspection={id:command.target,objectRevision:state.objects.find(o=>o.id===command.target)!.objectRevision!,partId:command.partId,recipe:parseRecipe(state.execution?.selected?.phase==='completed'?nativeRecipeEdit.afterRecipe:nativeRecipeEdit.beforeRecipe)};}
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

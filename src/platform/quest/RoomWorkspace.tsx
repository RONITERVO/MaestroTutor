import {ConstructionSelectionControls} from './ConstructionSelectionControls';
import {roomObjectLabel} from '../../../shared/roomSelection';
import type {CapabilityInvocation} from '../../../shared/capabilities';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useEffect,useState,useSyncExternalStore} from 'react';
import {RoomOwnershipDetails} from './RoomOwnershipDetails';
import type {RoomAgentState,RoomCommand} from '../../core-sdk/room/roomAgent';
import {copyRecipe,parseRecipe,rotateBy,defaultLatheProfile,defaultExtrusionProfile,type RoomRecipe,type Rotation} from '../../core-sdk/room/recipe';
import type {RoomAgentClient} from './roomAgentBridge';
import './roomWorkspace.css';
import {recipeEditCall} from '../../../shared/recipeEdits';
import {TemporaryRoomControls} from './TemporaryRoomControls';
import {RuleWorkspace} from './RuleWorkspace';
import {CapabilityBrowser,type CatalogInsert,type OpenCatalog} from './CapabilityBrowser';
type Draft={id:string;revision:number;recipe:RoomRecipe|null;source:RoomAgentState};
const identity={x:0,y:0,z:0,w:1};
const axes=['x','y','z'] as const;
// Hide binary32 serialization noise without changing the stored native coordinate.
const profileNumber=(n:number)=>{const short=Number(n.toPrecision(7));return Math.fround(n)===Math.fround(short)?short:n;};
const colours=[{name:'Teal',r:.18,g:.65,b:.63,a:1},{name:'Purple',r:.47,g:.24,b:.66,a:1},{name:'Gold',r:.9,g:.65,b:.2,a:1},{name:'Coral',r:.9,g:.36,b:.3,a:1},{name:'Paper',r:.94,g:.91,b:.82,a:1}];
const fromState=(state:RoomAgentState):Draft|null=>state.inspection?{id:state.inspection.id,revision:state.inspection.objectRevision,recipe:state.inspection.recipe?copyRecipe(state.inspection.recipe):null,source:state}:null;
function TurnControls({label,rotation,onChange}:{label:string;rotation:Rotation;onChange:(value:Rotation)=>void}) {
 return <fieldset className="room-axis-controls"><legend>{label}</legend>{axes.map(axis=><div key={axis}><span>{axis.toUpperCase()}</span><button type="button" aria-label={`${label} ${axis} minus 15 degrees`} onClick={()=>onChange(rotateBy(rotation,axis,-15))}>−15°</button><button type="button" aria-label={`${label} ${axis} plus 15 degrees`} onClick={()=>onChange(rotateBy(rotation,axis,15))}>+15°</button></div>)}<button type="button" onClick={()=>onChange({...identity})}>Reset rotation</button></fieldset>;
}
/** An optional projection of the native document, never an independent scene copy. */
export function RoomWorkspace({client}:{client:RoomAgentClient}) {
 const {state}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const [catalog,setCatalog]=useState<{insert?:CatalogInsert;initialCall?:CapabilityInvocation}|null>(null);
 useEffect(()=>{setCatalog(null);},[state?.session,state?.visible]);
 const openCatalog:OpenCatalog=(insert,initialCall)=>setCatalog({insert,initialCall});
 return <><div hidden={Boolean(catalog)||state?.workspaceView==='rules'}><ObjectsWorkspace client={client} onCatalog={openCatalog}/></div><div hidden={Boolean(catalog)||state?.workspaceView!=='rules'}><RuleWorkspace client={client} onCatalog={openCatalog}/></div>{catalog&&state?.visible&&<CapabilityBrowser key={state.session} client={client} onClose={()=>setCatalog(null)} onInsert={catalog.insert} initialCall={catalog.initialCall}/>}</>;
}
function ObjectsWorkspace({client,onCatalog}:{client:RoomAgentClient;onCatalog:OpenCatalog}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const [draft,setDraft]=useState<Draft|null>(null),[dirty,setDirty]=useState(false),[error,setError]=useState('');
 const [partId,setPartId]=useState(''),[tab,setTab]=useState<'parts'|'animation'>('parts'),[keyIndex,setKeyIndex]=useState(0);
 useEffect(()=>{if(!dirty&&state){setDraft(fromState(state));}},[state?.inspection?.id,state?.inspection?.objectRevision,dirty]);
 useEffect(()=>{setPartId('');setKeyIndex(0);setError('');},[draft?.id]);
 useEffect(()=>{if(!dirty&&state?.inspection?.partId)setPartId(state.inspection.partId);},[state?.inspection?.partId,dirty]);
 if(!state?.visible)return null;
 const item=state.objects.find(value=>value.id===draft?.id),recipe=draft?.recipe;
 const part=recipe?.parts.find(value=>value.id===partId)??recipe?.parts[0];
 const track=recipe?.tracks.find(value=>value.part===part?.id),key=track?.keys[Math.min(keyIndex,track.keys.length-1)];
 const stale=Boolean(draft&&(draft.source.session!==state.session||!item||item.objectRevision!==draft.revision)),blocked=pending||stale;
 const send=async(commands:RoomCommand[],expected?:RoomAgentState)=>{
  setError('');try {const result=await client.request(commands,expected);if(!result.ok)setError(result.status);return result;}
  catch(e){setError(e instanceof Error?e.message:'The room could not complete this action.');return null;}
 };
 const change=(edit:(value:RoomRecipe)=>void)=>{if(!draft?.recipe)return;const value=copyRecipe(draft.recipe);edit(value);setDraft({...draft,recipe:value});setDirty(true);setError('');};
 const select=async(id:string)=>{if(dirty){setError('Apply or discard your draft before choosing another object.');return;}await send([{action:'inspect',target:id}]);};
 const save=async()=>{
  if(!draft?.recipe||!parseRecipe(draft.recipe)){setError('This recipe needs valid sizes, joints and animation keys before it can be applied.');return;}
  const runId=state.execution?.nextRunId;
  if(!state.capabilities?.includes('recipeEdits.v1')||!runId){setError('This room cannot accept shared recipe edits yet. Your draft is kept.');return;}
  try {
   const call=recipeEditCall(draft.id,draft.revision,draft.source.inspection!.recipe!,draft.recipe);
   const result=await send([{action:'execution',execution:{operation:'start',call,runId}}],draft.source);
   if(result?.ok&&result.execution?.selected?.id===runId&&result.execution.selected.phase==='completed'){setDraft(fromState(result));setDirty(false);}
   else if(result?.ok)setError('The recipe edit has not completed. Your draft is kept.');
  } catch(e){setError(e instanceof Error?e.message:'The recipe edit could not be prepared.');}
 };
 const create=async()=>{if(dirty){setError('Apply or discard your draft first.');return;}const result=await send([{action:'create',reference:'robot',name:'Practice robot',kind:'boxRobot',scale:.4}]);if(result?.ok&&result.created[0])await select(result.created[0]);};
 return <div className="room-workspace" aria-label="Room workspace">
  <section className="room-workspace-page room-hierarchy" aria-label="Room objects and parts">
   <div className="room-workspace-heading"><div><span className="room-eyebrow">YOUR ROOM</span><h1>Workshop</h1></div><button disabled={pending} onClick={()=>{setDirty(false);void send([{action:'workspace',visible:false}]);}}>{dirty?'Discard & return':'Back to chat'}</button></div>
   <p className="room-workspace-intro">Explore what Maestro made. Your changes and conversation edit the same objects.</p>
   <div className="room-workspace-actions"><button disabled={pending||!state.capabilities?.includes('catalog.v1')} onClick={()=>onCatalog()}>Action catalog</button>{state.rules&&<button disabled={pending||dirty} onClick={()=>void send([{action:'rules',rule:{action:'inspect'}}])}>Behaviours</button>}<button disabled={pending||dirty} onClick={()=>void create()}>+ Box robot</button><button disabled={pending||dirty||!state.canUndo} onClick={()=>void send([{action:'undo'}])}>Undo</button><button disabled={pending||dirty||!state.canRedo} onClick={()=>void send([{action:'redo'}])}>Redo</button></div>
   <TemporaryRoomControls client={client} disabled={dirty}/>
   <RoomOwnershipDetails state={state}/>
   <ConstructionSelectionControls key={state.session} client={client} disabled={dirty} onCapture={call=>onCatalog(undefined,call)}/>
   <div className="room-object-list" aria-label="Objects">{state.objects.map(object=><button key={object.id} disabled={pending} aria-pressed={draft?.id===object.id} onClick={()=>void select(object.id)}><span>{object.kind==='Assembly'?'◇':object.kind==='Maestro'?'♙':object.kind==='Book'?'▤':'○'} {roomObjectLabel(object,state.objects)}</span><small>{object.kind}{object.animated?' · Playing':''}</small></button>)}</div>
   {recipe&&<div className="room-parts-list" aria-label="Parts"><h2>Parts & joints <small>{recipe.parts.length}</small></h2>{recipe.parts.map(node=><button key={node.id} disabled={pending} aria-pressed={part?.id===node.id} onClick={()=>{setPartId(node.id);setKeyIndex(0);void send([{action:'inspect',target:draft!.id,partId:node.id}]);}}><span>{node.id}</span><small>{node.parent?`↳ ${node.parent}`:'Root part'} · {node.shape}</small></button>)}</div>}
  </section>
  <section className="room-workspace-page room-inspector" aria-label="Object editor">
   <div className="room-workspace-heading"><div><span className="room-eyebrow">EDIT TOGETHER</span><h2>{item?.name??'Choose an object'}</h2></div>{item?.kind==='Assembly'&&<span className="room-status-pill">{item.animated?'Playing':'Stopped'}</span>}</div>
   <div role="status" className={error||stale?'room-message room-message-warning':'room-message'}>{error||(stale?'This object changed while you were editing. Your draft is kept; reload the latest version before applying.':pending?'Waiting for the room…':dirty?'Draft changes · Apply saves one edit and stops this object’s recipe animation':state.status)}</div>
   {draft&&<div className="room-workspace-actions"><button disabled={pending||!dirty||stale} onClick={()=>void save()}>Apply changes</button><button disabled={pending} onClick={()=>{setDirty(false);setDraft(fromState(state));setError('');}}> {stale?'Reload latest':'Discard draft'}</button></div>}
   {item&&!recipe&&<div className="room-simple-inspector"><p>{item.kind==='ImportedModel'?'This imported asset retains its original mesh. Its placement, size and tint can be edited here.':'Edit this object directly, or describe a change in chat.'}</p>
    <fieldset disabled={pending}><legend>Size · {item.scale.toFixed(2)}×</legend><button onClick={()=>void send([{action:'resize',target:item.id,scale:Number((item.scale-.1).toFixed(2))}])}>Smaller</button><button onClick={()=>void send([{action:'resize',target:item.id,scale:Number((item.scale+.1).toFixed(2))}])}>Larger</button></fieldset>
    {!['Book','Maestro'].includes(item.kind)&&<fieldset disabled={pending}><legend>Colour</legend>{colours.map(color=><button key={color.name} onClick={()=>void send([{action:'paint',target:item.id,color}])}>{color.name}</button>)}</fieldset>}
   </div>}
   {recipe&&part&&<>
    <div className="room-workspace-tabs" role="tablist" aria-label="Edit view"><button role="tab" aria-selected={tab==='parts'} onClick={()=>setTab('parts')}>Parts</button><button role="tab" aria-selected={tab==='animation'} onClick={()=>setTab('animation')}>Animation</button></div>
    <fieldset disabled={blocked} className="room-edit-body"><legend>{part.id}</legend>
    {tab==='parts'?<>
     <div className="room-shapes" aria-label="Part shape">{(['box','sphere','cylinder','lathe','extrude'] as const).map(shape=><button key={shape} disabled={shape==='lathe'&&!state.capabilities?.includes('latheGeometry.v1')||shape==='extrude'&&!state.capabilities?.includes('extrusionGeometry.v1')} aria-pressed={part.shape===shape} onClick={()=>{if(part.shape!==shape)change(value=>{const node=value.parts.find(node=>node.id===part.id)!;node.shape=shape;node.profile=shape==='lathe'?defaultLatheProfile():shape==='extrude'?defaultExtrusionProfile():[];node.segments=shape==='lathe'?24:0;});}}>{shape}</button>)}</div>
     {(part.shape==='lathe'||part.shape==='extrude')&&<fieldset className="room-lathe-profile" aria-label={part.shape==='lathe'?'Lathe profile':'Extrusion outline'}><legend>{part.shape==='lathe'?'Rotated profile':'Extruded outline'}</legend>
      <p>{part.shape==='lathe'?'Radius and height are scaled by this part’s dimensions.':'X and Y are scaled by this part’s dimensions; Z is thickness.'} Keep a simple counter-clockwise outline. Collision uses the object’s separately configured shapes.</p>
      <div className="room-lathe-preview"><svg role="img" aria-label={part.shape==='lathe'?'Lathe cross section':'Extrusion cross section'} viewBox={part.shape==='lathe'?'-5 -5 120 210':'-20 -20 240 240'} style={{width:part.shape==='lathe'?120:240,height:part.shape==='lathe'?210:240,background:'#eee'}}>{part.shape==='lathe'&&<path d="M 0 0 V 200" stroke="#777"/>}<polygon points={(part.profile??[]).map(p=>`${(p.x+(part.shape==='extrude'?.5:0))*200},${(0.5-p.y)*200}`).join(' ')} fill="#86caca" stroke="#174949" strokeWidth="1.5"/>{(part.profile??[]).map((p,i)=><text key={i} x={(p.x+(part.shape==='extrude'?.5:0))*200+3} y={(0.5-p.y)*200+3} fontSize={part.shape==='lathe'?8:12}>{i+1}</text>)}</svg>
      {part.shape==='lathe'&&<label>Angular segments<input aria-label="Lathe segments" type="number" min={8} max={48} step={1} value={part.segments??24} onChange={e=>change(value=>{value.parts.find(node=>node.id===part.id)!.segments=Number(e.target.value);})}/></label>}</div>
      {(part.profile??[]).map((point,index)=><div key={index} className="room-lathe-point">
       <span>Point {index+1}</span>{(['x','y'] as const).map(axis=>{const label=part.shape==='lathe'?(axis==='x'?'radius':'height'):axis;return <label key={axis}>{label}<input aria-label={`Profile point ${index+1} ${label}`} type="number" min={part.shape==='lathe'&&axis==='x'?0:-.5} max={.5} step={.01} value={profileNumber(point[axis])} onChange={e=>change(value=>{value.parts.find(node=>node.id===part.id)!.profile![index][axis]=Number(e.target.value);})}/></label>;})}
       <button aria-label={`Insert after point ${index+1}`} disabled={(part.profile?.length??0)>=(part.shape==='lathe'?16:32)} onClick={()=>change(value=>{const points=value.parts.find(node=>node.id===part.id)!.profile!,next=points[(index+1)%points.length];points.splice(index+1,0,{x:(point.x+next.x)/2,y:(point.y+next.y)/2});})}>Insert after</button>
       <button aria-label={`Remove point ${index+1}`} disabled={(part.profile?.length??0)<=3} onClick={()=>change(value=>{value.parts.find(node=>node.id===part.id)!.profile!.splice(index,1);})}>Remove</button>
      </div>)}
     </fieldset>}
     <fieldset className="room-axis-controls"><legend>Position · metres from {part.parent||'object origin'}</legend>{axes.map(axis=><div key={axis}><span>{axis.toUpperCase()} {part.position[axis].toFixed(2)}</span><button aria-label={`Position ${axis} minus`} onClick={()=>change(value=>{value.parts.find(node=>node.id===part.id)!.position[axis]-=.01;})}>−.01</button><button aria-label={`Position ${axis} plus`} onClick={()=>change(value=>{value.parts.find(node=>node.id===part.id)!.position[axis]+=.01;})}>+.01</button></div>)}</fieldset>
     <fieldset className="room-axis-controls"><legend>Dimensions · metres</legend>{axes.map(axis=><div key={axis}><span>{axis.toUpperCase()} {part.size[axis].toFixed(2)}</span><button aria-label={`Size ${axis} minus`} onClick={()=>change(value=>{const node=value.parts.find(node=>node.id===part.id)!;node.size[axis]=Math.max(.005,node.size[axis]-.01);})}>−.01</button><button aria-label={`Size ${axis} plus`} onClick={()=>change(value=>{value.parts.find(node=>node.id===part.id)!.size[axis]+=.01;})}>+.01</button></div>)}</fieldset>
     <TurnControls label="Rest pose" rotation={part.rotation} onChange={rotation=>change(value=>{value.parts.find(node=>node.id===part.id)!.rotation=rotation;})}/>
     <fieldset><legend>Part colour</legend>{colours.map(color=><button key={color.name} onClick={()=>change(value=>{const {name:_,...pigment}=color;value.parts.find(node=>node.id===part.id)!.color=pigment;})}>{color.name}</button>)}</fieldset>
    </>:<>
     <div className="room-workspace-actions"><button disabled={dirty||pending||!recipe.tracks.length} onClick={()=>void send([{action:'play',target:draft!.id}])}>Play from start</button><button disabled={dirty||pending} onClick={()=>void send([{action:'stop',target:draft!.id}])}>Stop</button></div>
     <div className="room-clip-settings"><span>{recipe.duration.toFixed(1)} seconds</span><button aria-pressed={recipe.loop} onClick={()=>change(value=>{value.loop=!value.loop;})}>Loop {recipe.loop?'on':'off'}</button></div>
     {track?<><p>Rotation keys for {part.id}</p><div className="room-timeline" aria-label="Animation keyframes">{track.keys.map((frame,index)=><button key={index} aria-pressed={keyIndex===index} onClick={()=>setKeyIndex(index)}>{frame.time.toFixed(2)}s</button>)}</div>
      {key&&<TurnControls label={`Key ${key.time.toFixed(2)}s`} rotation={key.rotation} onChange={rotation=>change(value=>{value.tracks.find(channel=>channel.part===part.id)!.keys[Math.min(keyIndex,track.keys.length-1)].rotation=rotation;})}/>}
      <div className="room-workspace-actions"><button disabled={track.keys.length>=16||keyIndex>=track.keys.length-1} onClick={()=>change(value=>{const keys=value.tracks.find(channel=>channel.part===part.id)!.keys;const a=keys[keyIndex],b=keys[keyIndex+1];keys.splice(keyIndex+1,0,{time:(a.time+b.time)/2,rotation:{...a.rotation}});setKeyIndex(keyIndex+1);})}>Add key after</button>
       <button disabled={keyIndex===0||keyIndex>=track.keys.length-1} onClick={()=>change(value=>{value.tracks.find(channel=>channel.part===part.id)!.keys.splice(keyIndex,1);setKeyIndex(keyIndex-1);})}>Remove key</button></div>
     </>:<><p>This part follows its parent. Add keys to animate its own joint.</p><button disabled={recipe.tracks.length>=17} onClick={()=>change(value=>{value.tracks.push({part:part.id,keys:[{time:0,rotation:{...identity}},{time:value.duration,rotation:{...identity}}]});setKeyIndex(0);})}>Add rotation track</button></>}
    </>}
    </fieldset>
   </>}
   {!draft&&<p className="room-workspace-empty">Select an object on the left. You can also ask Maestro to create a small waving robot.</p>}
  </section>
 </div>;
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readNativeObjectIdentity,assertResumedNativeIdentity,readNativeResumeReceipt,isNativeIdentityRead} from './native-probe-identity';
import {probeNativeSteering} from './probe-native-steering';
import {probeNativeSparsePrograms} from './probe-native-sparse-programs';
import {probeNativeMotion} from './probe-native-motion';
import {readNativeObjects} from './native-probe-read-set';
import {probeAvatarAttention} from './probe-avatar-attention';
import {probeGroundedTravel,probeGroundedFollowing} from './probe-grounded-travel';
import {prepareObjectWalk,probeObjectWalk} from './probe-object-walk';
import {prepareAgentModelGeometry,runAgentModelGeometryProof} from './probe-agent-model-geometry';
import {probeModelAssetBounds} from './probe-model-asset-bounds';
import {runAgentWindowProof} from './probe-agent-windows';
import {prepareAgentGeneratedImage,runAgentGeneratedImageProof} from './probe-agent-generated-image';
import {prepareAgentImportedImage,runAgentImportedImageProof} from './probe-agent-imported-image';
import {prepareAgentImportedAudio,runAgentImportedAudioProof} from './probe-agent-imported-audio';
import {prepareAgentLiquidContacts,runAgentLiquidContactsProof} from './probe-agent-liquid-contacts';
import {runAgentWorldWeatherProof} from './probe-agent-world-weather';
import {probeWorldWeather} from './probe-world-weather';
import {probeLiquidMedium} from './probe-liquid-medium';
import {probeWaterTraversal} from './probe-water-traversal';
import {probeTerrainWater} from './probe-terrain-water';
import {probeChatAudio} from './probe-chat-audio';
import {prepareAgentWaterTraversal,runAgentWaterTraversalProof} from './probe-agent-water-traversal';
import {prepareAgentLiquidMedium,runAgentLiquidMediumProof} from './probe-agent-liquid-medium';
import {runAgentWorldTimeProof} from './probe-agent-world-time';
import {runAgentLightingProof} from './probe-agent-lighting';
import {runAgentPresentationProof} from './probe-agent-presentation';
import {probeWorldPresentation} from './probe-world-presentation';
import {probeWorldLighting} from './probe-world-lighting';
import {probeObjectLights} from './probe-object-lights';
import {probeWorldTime} from './probe-world-time';
import {probeVisibilityLayers} from './probe-visibility-layers';
import {probeWorldRegions} from './probe-world-regions';
import {probeConstructionResources} from './probe-construction-resources';
import {runAgentSteeringProof,type ReceiptObserver} from './probe-agent-steering';
import {runAgentPhysicsProof} from './probe-agent-physics';
import {runAgentCompositeProof} from './probe-agent-composite';
import {runAgentAnimationProof} from './probe-agent-animation';
import {runAgentProgramProof} from './probe-agent-program';
import {assertCreatedParityBall,assertPaintedParityBall,assertSameRoomObjects} from './agent-provider-contract';
import {createHash} from 'node:crypto';
import type {LiveSendRealtimeInputParameters} from '@google/genai';
import {runHeadlessRoomLiveTurn,providerMediaHashes} from '../src/headless/roomLiveJourney';
import {ROOM_AGENT_RESPONSE_SCHEMA} from '../shared/prompts/room';
import {decodePcm16LeBase64} from '../src/core-sdk/media/pcmInput';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {isDeepStrictEqual} from 'node:util';
import {nativeProbeObjects} from './native-probe-objects';
import {objectSearchRequest,readObjectSearchPage} from '../shared/objectSearch';
import {roomRevisionBasis} from '../src/core-sdk/room/roomObjectContext';
import {HeadlessRoomTransport} from '../src/headless/roomTransport';
import {type RoomCommand,type RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {createHeadlessClient} from '../src/headless/client';
import {capabilityDefinition} from '../shared/capabilities';
import {constructionCaptureDraft} from '../shared/roomSelection';
import {applyCurrentInputSnapshots} from '../shared/currentCapabilityInputs';
import {insertProgramCapability} from '../src/core-sdk/room/programCapabilityEditing';
import {HeadlessRoomAgent,runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {selectHeadlessLanguage,runHeadlessChatTurn} from '../src/headless/chatJourney';
import {runHeadlessSuggestionAftersteps} from '../src/headless/suggestionJourney';
import {parseProgram} from '../src/core-sdk/room/programs';
import {completeProbeAction,checkedProbeReply,factReply,assertSamePlacement,type NativeProbeState} from './native-probe-contract';
const directory=process.argv[2];if(!directory)throw new Error('Supply the explicitly started native probe directory.');
const prompt=process.env.MAESTRO_ROOM_PROBE_PROMPT;
const providerScenario=process.env.MAESTRO_ROOM_PROBE_SCENARIO;
if(providerScenario && (!['ContextCreateEdit','LiveVisual','ObserverVisual','EventProgram','AvatarAnimation','CompositeModule','PhysicsLaunch','TaskSteering','WorldPresentation','WorldLighting','WorldTime','WorldWeather','LiquidMedium','WaterTraversal','LiquidContacts','ImportedAudio','ImportedImage','GeneratedImage','ModelGeometry','PassthroughWindow'].includes(providerScenario)||!prompt))throw new Error('Unknown or unconfigured provider scenario.');
const transport=await HeadlessRoomTransport.connect(directory,120000);
const observations:unknown[]=[];const readSets:unknown[]=[];
const probeOwner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8'));
const checkSparse=(state:RoomAgentState)=>{if(probeOwner.sparseObjectView&&state.objects.length)throw new Error('The sparse-view fixture leaked a complete object list');};
try{
 const lease=transport.lease();let initial=structuredClone(lease.state());checkSparse(initial);
 const execute=async<const C extends RoomCommand[]>(commands:C):Promise<NativeProbeState<C>>=>{
  if(probeOwner.identityOnly&&!commands.every(isNativeIdentityRead))throw new Error('IdentityOnly permits only explicit saved-object directory reads');
  const state=lease.state(),prepared=await transport.client.prepare(commands,state);
  if(!lease.valid())throw new Error('Native probe room changed while preparing an action');
  readSets.push({commands:structuredClone(commands),session:prepared.state.session,sceneRevision:prepared.state.sceneRevision,objects:structuredClone(prepared.objects)});
  let result=await lease.execute(commands,prepared.state.sceneRevision,prepared.objects);checkSparse(result);observations.push(structuredClone(result));
  const command=commands.length===1?commands[0]:undefined;
  const execution=command?.action==='execution'?command.execution:undefined;
  // Instant actions can still prepare dormant native inputs asynchronously.
  // Follow the accepted receipt; never replay the effect or wait away a timed action.
  if(result.ok&&execution?.operation==='start'&&capabilityDefinition(execution.call.id)?.duration==='instant'){
   const runId=result.execution?.selected?.id;if(!runId)throw new Error('Instant action has no native receipt');
   const deadline=Date.now()+15000;
   while(['preparing','running'].includes(result.execution!.selected!.phase)&&Date.now()<deadline){
    await new Promise(r=>setTimeout(r,100));const current=lease.state();
    result=await lease.execute([{action:'execution',execution:{operation:'inspect',runId}}],current.sceneRevision,roomRevisionBasis(current));checkSparse(result);observations.push(structuredClone(result));
    if(!result.ok||result.execution?.selected?.id!==runId)throw new Error('Instant-action receipt changed while waiting: '+result.status);
   }
   if(result.execution?.selected?.phase!=='completed')throw new Error('Instant native action did not complete: '+JSON.stringify(result.execution?.selected));
  }
  if(!result.ok){
   await writeFile(join(directory,'refused-command.json'),JSON.stringify({commands,expectedSceneRevision:state.sceneRevision,expectedObjects:prepared.objects,result},null,2));
   throw new Error('Native action refused: '+result.status);
  }return checkedProbeReply(commands,result);
 };
 const nativeObjects=nativeProbeObjects(execute);
 const initialSavedCount=await nativeObjects.count();
 const readPlacement=(target:string)=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target}}}]);
 let outcome:unknown;
 if(probeOwner.identityOnly){
  if(prompt||providerScenario||probeOwner.motionOnly||probeOwner.steeringOnly)throw new Error('The identity fixture cannot call a provider or another focused fixture');
  const nativeIdentity=await readNativeObjectIdentity(execute);let resume:ReturnType<typeof readNativeResumeReceipt>|undefined;
  try{resume=readNativeResumeReceipt(JSON.parse(await readFile(join(directory,'learner-resume.json'),'utf8')));}
  catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
  if(resume)assertResumedNativeIdentity(resume.nativeIdentity,nativeIdentity);
  outcome={identityOnly:true,resumedFrom:resume?.sourceRun,nativeIdentity,boundary:'Explicit saved-object identity across actual native startup. No provider, task replay, profile restoration or headset proof.'};
 }else if(probeOwner.steeringOnly){
  if(prompt||providerScenario||probeOwner.motionOnly)throw new Error('The steering fixture cannot call a paid provider or another focused fixture');
  await probeNativeSteering(execute,()=>transport.lease(),directory,!!probeOwner.sparseObjectView);outcome={steeringOnly:true};
 }else if(probeOwner.motionOnly){
  if(prompt||providerScenario)throw new Error('The motion fixture cannot call a provider');
  await probeNativeMotion(execute,()=>lease.state(),directory);outcome={motionOnly:true};
 }else if(prompt){
  const client=await createHeadlessClient(); // isolated evidence, never an existing user's profile
  // Observe responses from the real provider for this isolated synthetic test.
  // The original stream and request are passed through unchanged.
  const providerResponses:Array<{model:string;text:string}>=[];
  const providerInputs:Array<{stage:string;media:ReturnType<typeof providerMediaHashes>}>=[];
  const liveSends:Array<{pcm:ReturnType<typeof createHash>;audioBytes:number;frames:string[]}>=[];
  if(providerScenario==='LiveVisual'||providerScenario==='ObserverVisual'){
   const connect=client.ai.live.connect.bind(client.ai.live);
   client.ai.live.connect=async params=>{
    const session=await connect(params),send=session.sendRealtimeInput.bind(session);
    const observation={pcm:createHash('sha256'),audioBytes:0,frames:[] as string[]};liveSends.push(observation);
    session.sendRealtimeInput=(message:LiveSendRealtimeInputParameters)=>{
     send(message);
     if(message.audio?.data){const bytes=Buffer.from(message.audio.data,'base64');observation.pcm.update(bytes);observation.audioBytes+=bytes.length;}
     if(message.video?.data)observation.frames.push(createHash('sha256').update(Buffer.from(message.video.data,'base64')).digest('hex'));
    };
    return session;
   };
  }
  const stream=client.ai.models.generateContentStream.bind(client.ai.models);
  client.ai.models.generateContentStream=async request=>{
   providerInputs.push({stage:isDeepStrictEqual(request.config?.responseJsonSchema,ROOM_AGENT_RESPONSE_SCHEMA)?'planning':'other',media:providerMediaHashes(request.contents)});
   await writeFile(join(directory,'provider-inputs.json'),JSON.stringify(providerInputs,null,2));
   const result=await stream(request);
   return (async function*(){let text='';try{for await(const chunk of result){if(chunk.text)text+=chunk.text;yield chunk;}}
    finally{providerResponses.push({model:request.model,text});await writeFile(join(directory,'provider-responses.json'),JSON.stringify(providerResponses,null,2));}})();
  };
  let receiptObserver:ReceiptObserver|undefined;
  transport.setChatSource(()=>{const scope=client.state.settings.selectedLanguagePairId||'';return {scope,messages:client.state.chats[scope]||[],bookmark:client.state.settings.historyBookmarkMessageId};});
  const agent=new HeadlessRoomAgent(client,()=>{
   const native=transport.lease();
   return !receiptObserver?native:{...native,execute:async(...args)=>{
    const receipt=await native.execute(...args);await receiptObserver?.(args[0],receipt);return receipt;
   }};
  });client.roomAgent=agent;
  // The eight-turn presentation journey includes independent tutor, verifier,
  // planner and final-reply calls for each turn. Keep it bounded below the
  // native probe's 15-minute lifetime, without cutting it off at the old six-turn budget.
  const timeoutMs=['PhysicsLaunch','TaskSteering','WorldPresentation'].includes(providerScenario||'')?780000:providerScenario?600000:240000;
  const startedAt=Date.now();let deadlineExpiredAt:number|null=null;
  const timer=setTimeout(()=>{deadlineExpiredAt=Date.now();agent.tasks.stopAll();},timeoutMs);
  try{
   await selectHeadlessLanguage(client,{targetLanguageCode:'es-ES',nativeLanguageCode:'en-US'});
   const spoken=providerScenario==='LiveVisual'||providerScenario==='ObserverVisual';
   const contextTurn=await runHeadlessChatTurn(client,{text:['WorldPresentation','WorldLighting','WorldTime','WorldWeather','LiquidMedium','WaterTraversal','LiquidContacts','ImportedAudio','ImportedImage','GeneratedImage','ModelGeometry','PassthroughWindow'].includes(providerScenario||'')?"Hello! I am learning Spanish. I will try the room view controls next, but please do not change anything yet.":(['EventProgram','AvatarAnimation','CompositeModule','PhysicsLaunch','TaskSteering'].includes(providerScenario||''))?"For this test, my test object is a blue ball named ParityBall, half the diameter of the room's standard ball. Remember that; do not create anything yet.":spoken?"For this test, 'my test object' means one ball named ParityBall, exactly half the diameter of the room's standard ball. I will choose its colour in my next request. Remember that; do not make anything yet.":"For this test, 'my test object' means one small blue ball named ParityBall. Remember that for my next request; do not make anything yet.",useGoogleSearch:false});
   const contextAftersteps=await runHeadlessSuggestionAftersteps(client,{assistantMessageId:contextTurn.assistantMessage.id});
   if(contextAftersteps.toolRequest?.tool==='agent'||lease.state().sceneRevision!==initial.sceneRevision||agent.usage.length)throw new Error('Context-only chat unexpectedly started room work.');
   // The transport handshake can precede the authored-to-render-frame round trip.
   // Compare effects to a real native reading immediately before the first user
   // action, retaining startup evidence; do not weaken exact object assertions.
   const startup=initial;
   initial=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.environment',version:1}}]);
   await writeFile(join(directory,'provider-baseline.json'),JSON.stringify({startup,beforeRequest:initial},null,2));

   const presentationBaseline=providerScenario==='WorldPresentation'
    ?await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.environment',version:1}}])
    :initial;
   if(providerScenario==='WorldPresentation')await writeFile(join(directory,'presentation-baseline.json'),JSON.stringify({startup:initial,beforeRequest:presentationBaseline},null,2));
   // Sample the native frame after startup/context, before the user's first edit.
   // The boot handshake can precede the authored-to-render-frame round trip.
   const lightingBaseline=providerScenario==='WorldLighting'
    ?await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.lighting',version:1}}])
    :initial;
   if(providerScenario==='WorldLighting')await writeFile(join(directory,'lighting-baseline.json'),JSON.stringify({startup:initial,beforeRequest:lightingBaseline},null,2));
   const worldTimeBaseline=providerScenario==='WorldTime'?await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.time',version:1}}]):initial;
   if(providerScenario==='WorldTime')await writeFile(join(directory,'world-time-baseline.json'),JSON.stringify({startup:initial,beforeRequest:worldTimeBaseline},null,2));
   const mediumSetup=providerScenario==='LiquidMedium'?await prepareAgentLiquidMedium(execute):null;
   const geometrySetup=providerScenario==='ModelGeometry'?await prepareAgentModelGeometry(execute,directory):null;
   const imageSetup=providerScenario==='ImportedImage'?await prepareAgentImportedImage(execute,directory):null;
   const generatedSetup=providerScenario==='GeneratedImage'?await prepareAgentGeneratedImage(client,transport,execute,directory,contextTurn.assistantMessage.id):null;
   const audioSetup=providerScenario==='ImportedAudio'?await prepareAgentImportedAudio(execute,directory):null;
   const contactSetup=providerScenario==='LiquidContacts'?await prepareAgentLiquidContacts(execute):null;
   const waterSetup=providerScenario==='WaterTraversal'?await prepareAgentWaterTraversal(execute):null;
   const objectsBeforeRequest=await readNativeObjects(execute);
   await writeFile(join(directory,'provider-baseline-objects.json'),JSON.stringify(objectsBeforeRequest,null,2));
   let createdJourney;
   if(spoken){
    const fixture=JSON.parse(await readFile(process.env.MAESTRO_ROOM_PROBE_SPEECH!,'utf8'));
    if(fixture.sampleRate!==16000||typeof fixture.pcmBase64!=='string'||typeof fixture.expectedTranscript!=='string'||!fixture.expectedTranscript.trim())throw new Error('SpeechFixture needs 16 kHz pcmBase64 and a nonempty expectedTranscript.');
    createdJourney=await runHeadlessRoomLiveTurn(client,{mode:providerScenario==='ObserverVisual'?'observer':'conversation',
     pcm:decodePcm16LeBase64(fixture.pcmBase64),sampleRate:16000,expectedTranscript:fixture.expectedTranscript,pace:true,
     includeVisual:true,visualLabel:'REFERENCE',manualActivityBoundaries:true,timeoutMs:120000});
    const finalState=structuredClone(lease.state());
    await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify({scenario:providerScenario,phase:'validating',createdJourney,finalState},null,2));
    const input=createdJourney.inputHashes!,sent=liveSends[0];
    if(liveSends.length!==1||!sent||sent.audioBytes!==input.samples*2||sent.pcm.copy().digest('hex')!==input.pcm
      ||!isDeepStrictEqual(sent.frames,input.frames.map(frame=>frame.sha256)))throw new Error('Delegated media differs from the actual Live client sends.');
    const planner=providerInputs.filter(value=>value.stage==='planning').flatMap(value=>value.media);
    if(!planner.some(value=>value.mimeType==='audio/wav'&&value.sha256===input.audio)
      ||input.frames.some(frame=>!planner.some(value=>value.mimeType==='image/jpeg'&&value.sha256===frame.sha256)))throw new Error('Original Live audio/frames did not reach the real planner request.');
    assertCreatedParityBall(objectsBeforeRequest,await readNativeObjects(execute),'red',0.5);
    const scenarioEvidence={scenario:providerScenario,phase:'passed',semantics:{originalSentMedia:true,originalMediaInPlanner:true,contextNameAndSize:true,visualColour:true},
     createdJourney,finalState,liveInput:{pcm:input.pcm,audioBytes:sent.audioBytes,frames:sent.frames}};
    await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(scenarioEvidence,null,2));outcome=scenarioEvidence;
   }else {createdJourney=await runHeadlessRoomTurn(client,{text:prompt});outcome=createdJourney;}
   if(providerScenario==='ModelGeometry'&&geometrySetup){outcome={scenario:providerScenario,createdJourney,geometry:await runAgentModelGeometryProof({client,execute,directory,...geometrySetup})};}
   if(providerScenario==='PassthroughWindow'){outcome={scenario:providerScenario,createdJourney,window:await runAgentWindowProof({client,initialObjects:objectsBeforeRequest,execute,directory})};}
   if(providerScenario==='ImportedImage'||providerScenario==='GeneratedImage')await writeFile(join(directory,'provider-initial-turn.json'),JSON.stringify({scenario:providerScenario,createdJourney},null,2));
   if(providerScenario==='ImportedImage'&&imageSetup){outcome={scenario:providerScenario,createdJourney,image:await runAgentImportedImageProof({client,execute,directory,...imageSetup})};}
   if(providerScenario==='GeneratedImage'&&generatedSetup){outcome={scenario:providerScenario,createdJourney,image:await runAgentGeneratedImageProof({client,execute,directory,...generatedSetup})};}
   if(providerScenario==='ImportedAudio'&&audioSetup){outcome={scenario:providerScenario,createdJourney,audio:await runAgentImportedAudioProof({client,execute,directory,...audioSetup})};}
   if(providerScenario==='LiquidContacts'&&contactSetup){outcome={scenario:providerScenario,createdJourney,contacts:await runAgentLiquidContactsProof({client,execute,directory,...contactSetup})};}
   if(providerScenario==='WaterTraversal'&&waterSetup){outcome={scenario:providerScenario,createdJourney,water:await runAgentWaterTraversalProof({client,execute,directory,...waterSetup})};}
   if(providerScenario==='LiquidMedium'&&mediumSetup){outcome={scenario:providerScenario,createdJourney,medium:await runAgentLiquidMediumProof({client,execute,directory,...mediumSetup})};}
   if(providerScenario==='WorldWeather'){outcome={scenario:providerScenario,createdJourney,weather:await runAgentWorldWeatherProof({client,initialObjects:objectsBeforeRequest,execute,directory})};}
   if(providerScenario==='WorldTime'){outcome={scenario:providerScenario,createdJourney,time:await runAgentWorldTimeProof({client,initialObjects:objectsBeforeRequest,execute,directory})};}
   if(providerScenario==='WorldLighting'){
    outcome={scenario:providerScenario,createdJourney,lighting:await runAgentLightingProof({client,initialObjects:objectsBeforeRequest,execute,directory})};
   }
   if(providerScenario==='WorldPresentation'){
    outcome={scenario:providerScenario,createdJourney,presentation:await runAgentPresentationProof({client,initial:presentationBaseline,initialObjects:objectsBeforeRequest,execute,directory,read:()=>lease.state()})};
   }
   if(providerScenario==='EventProgram'){
    const createdObjects=await readNativeObjects(execute),before=structuredClone(lease.state()),ball=assertCreatedParityBall(objectsBeforeRequest,createdObjects,'blue',0.5);
    outcome={scenario:providerScenario,createdJourney,program:await runAgentProgramProof({client,before,target:ball.id,execute,directory})};
   }
   if(providerScenario==='AvatarAnimation'){
    const createdObjects=await readNativeObjects(execute),before=structuredClone(lease.state());assertCreatedParityBall(objectsBeforeRequest,createdObjects,'blue',0.5);
    outcome={scenario:providerScenario,createdJourney,animation:await runAgentAnimationProof({client,before,execute,directory,probeId:transport.id})};
   }
   if(providerScenario==='CompositeModule'){
    const createdObjects=await readNativeObjects(execute),before=structuredClone(lease.state());assertCreatedParityBall(objectsBeforeRequest,createdObjects,'blue',0.5);
    outcome={scenario:providerScenario,createdJourney,composite:await runAgentCompositeProof({client,before,execute,directory})};
   }
   if(providerScenario==='PhysicsLaunch'){
    const createdObjects=await readNativeObjects(execute),before=structuredClone(lease.state());
    const ball=assertCreatedParityBall(objectsBeforeRequest,createdObjects,'blue',0.5);
    await writeFile(join(directory,'provider-physics-create-objects.json'),JSON.stringify({before:objectsBeforeRequest,after:createdObjects},null,2));
    outcome={scenario:providerScenario,createdJourney,physics:await runAgentPhysicsProof({client,before,target:ball.id,execute,directory,read:()=>lease.state()})};
   }
   if(providerScenario==='TaskSteering'){
    const createdObjects=await readNativeObjects(execute),before=structuredClone(lease.state()),ball=assertCreatedParityBall(objectsBeforeRequest,createdObjects,'blue',0.5);
    outcome={scenario:providerScenario,createdJourney,steering:await runAgentSteeringProof({client,before,target:ball.id,directory,execute,providerMode:'live',
     lease:()=>transport.lease(),observe:observer=>{receiptObserver=observer;}})};
   }
   if(providerScenario==='ContextCreateEdit'){
    const createdState=structuredClone(lease.state());
    const createdObjects=await readNativeObjects(execute),ball=assertCreatedParityBall(objectsBeforeRequest,createdObjects);
    await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify({scenario:providerScenario,phase:'created',createdJourney,createdState},null,2));
    const editJourney=await runHeadlessRoomTurn(client,{text:'Ask the room agent to change only the existing ParityBall to red. Keep its name, size and position; do not create another object.'});
    const editedState=structuredClone(lease.state());
    const editedObjects=await readNativeObjects(execute);assertPaintedParityBall(createdObjects,editedObjects,ball.id);
    // These are the same native Undo/Redo handlers used by physical controls;
    // this is handler parity, not a claim that a controller button was pressed.
    const undone=await execute([{action:'undo'}]),undoneObjects=await readNativeObjects(execute);assertSameRoomObjects(createdObjects,undoneObjects);
    const redone=await execute([{action:'redo'}]),redoneObjects=await readNativeObjects(execute);assertSameRoomObjects(editedObjects,redoneObjects);
    const readbackJourney=await runHeadlessRoomTurn(client,{text:'Ask the room agent to inspect the current ParityBall in the live room and tell me its name, shape, colour and size. Read only; do not change anything.',requireActions:false});
    const readbackState=structuredClone(lease.state()),readbackObjects=await readNativeObjects(execute);assertSameRoomObjects(redoneObjects,readbackObjects);
    const visibleReply=readbackJourney.task?.message.translations?.map(value=>value.native).join(' ')||'';
    if(!visibleReply.includes('ParityBall')||!(/\bred\b/i.test(visibleReply)))throw new Error('Agent readback did not describe the current red ParityBall.');
    const scenarioEvidence={scenario:providerScenario,phase:'passed',semantics:{contextOnly:true,exactCreation:true,sameObjectEdit:true,unrelatedObjectsPreserved:true,undo:true,redo:true,agentReadback:true},
     createdJourney,editJourney,readbackJourney,createdState,editedState,undone,redone,readbackState};
    await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(scenarioEvidence,null,2));
    outcome=scenarioEvidence;
   }
   if(deadlineExpiredAt!==null)throw new Error('Provider scenario exceeded its bounded '+timeoutMs+' ms deadline.');
   const records=await agent.store.list();
   for(const record of records)for(const operation of record.operations)if(operation.receipt)observations.push(operation.receipt);
   await writeFile(join(directory,'agent-journal.json'),JSON.stringify(records,null,2));
   // Retrying the same claimed source must neither spend nor dispatch again.
   const record=records.find(record=>record.id===createdJourney.task?.id)!;const revision=lease.state().sceneRevision,usageCount=agent.usage.length;
   await agent.start(record.handoff.sourceAssistantId);
   if(lease.state().sceneRevision!==revision||agent.usage.length!==usageCount)throw new Error('Duplicate handoff was executed again.');
  }catch(error){await writeFile(join(directory,'agent-failure.json'),JSON.stringify({message:error instanceof Error?error.message:String(error),
   deadline:{timeoutMs,startedAt,expiredAt:deadlineExpiredAt,elapsedMs:Date.now()-startedAt},
   evidence:(error as {evidence?:unknown}).evidence},null,2));throw error;
  }finally{clearTimeout(timer);await agent.disconnect();await writeFile(join(directory,'agent-journal.json'),JSON.stringify(await agent.store.list(),null,2));await client.save();}

 }else{
   await probeNativeSteering(execute,()=>transport.lease(),directory,!!probeOwner.sparseObjectView);
   await probeNativeSparsePrograms(execute,directory);
  await probeNativeMotion(execute,()=>lease.state(),directory);
  await probeModelAssetBounds(execute,directory);
  const gait=await prepareObjectWalk(execute,directory);
  const catalog=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'object.create',version:1}}]);
  const definition=catalog.catalog?.definition as {example?:Record<string,unknown>}|undefined;
  if(!definition?.example)throw new Error('The native create capability has no example.');
  const created=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Native probe ball'}}}}]));
  const selected=created.execution?.selected;
  if(selected?.phase!=='completed'||typeof selected.output?.objectId!=='string')throw new Error('Creation did not produce a completed native receipt.');
  const target=selected.output.objectId;const original=await nativeObjects.observation(target);
  if(!original||await nativeObjects.count()!==initialSavedCount+1)throw new Error('The object was not created in the real room.');
  await execute([{action:'paint',target,color:{r:1,g:0,b:0,a:1}}]);
  if((await nativeObjects.observation(target)).color.r!==1)throw new Error('Native paint did not apply.');
  const viewSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Capture virtual room',offset:0}}]);
  const viewDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'room.view.capture',version:1}}]);
  const viewBefore=structuredClone(lease.state());
  const viewAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'room.view.capture',version:1,arguments:{}}}}]);
  const viewId=viewAfter.execution?.selected?.output?.captureId;
  if(typeof viewId!=='string'||viewAfter.execution?.selected?.phase!=='completed')throw new Error('Native virtual view capture did not complete');
  const viewImage=await lease.capture!(viewId);
  if(viewImage.capture.sha256!==viewAfter.execution.selected.output?.sha256||viewAfter.sceneRevision!==viewBefore.sceneRevision)throw new Error('Virtual view image differs from its receipt or mutated the room');
  await writeFile(join(directory,'virtual-room.jpg'),Buffer.from(viewImage.data,'base64'));
  await writeFile(join(directory,'view-capture.json'),JSON.stringify({boundary:'Real full-app Unity camera, shared action/receipt and separate image transport. No live provider or Quest frame-time proof.',before:viewBefore,search:viewSearch,definition:viewDefinition,after:viewAfter,payload:{version:1,revision:1,session:viewAfter.session,...viewImage},ack:transport.client.snapshot().captureAck},null,2));
  await execute([{action:'undo'}]);
  if(JSON.stringify((await nativeObjects.observation(target)).color)!==JSON.stringify(original.color))throw new Error('Native Undo did not restore the previous paint.');
  await execute([{action:'undo'}]);
  if(await nativeObjects.exists(target)||await nativeObjects.count()!==initialSavedCount)throw new Error('Native Undo did not remove the created object.');
  const diagnostic=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.modelBudget',version:1}}]);
  if(!diagnostic.catalog?.available)throw new Error('Native resource diagnostics are unavailable.');
  const modelUsage=factReply(diagnostic).value as {reserved:{models:number}};
  const modelReservations:unknown[]=[],modelReservationReads:unknown[]=[];
  for(let index=0;index<modelUsage.reserved.models;index++){
   const reply=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.modelReservation',version:1,arguments:{index}}}]);
   const reading=factReply(reply);modelReservationReads.push({index,...reading});
   // Indexed diagnostics are sequential observations; retirement can remove or shift a slot.
   if(!reading.available)continue;
   const value=reading.value as {reservationId:string;worldId:string;regionId:string;target:string;role:string;modelHash:string;state:string};
   if(!value||value.reservationId.length!==32||value.modelHash.length!==64||!['queued','loading','ready','retiring'].includes(value.state))throw new Error('Native model lease observation is missing or malformed.');
   if(value.role==='avatar'&&(value.target!=='maestro'||value.worldId.length!==32||value.regionId.length!==32))throw new Error('Native avatar model reservation lost its durable world/entity owner.');
   modelReservations.push(value);
  }
  await writeFile(join(directory,'model-reservations.json'),JSON.stringify({boundary:'Sequential native model reservation reads; slots may shift or disappear after the budget read. Not an atomic inventory, measured memory or headset performance.',budget:factReply(diagnostic).value,reads:modelReservationReads,leases:modelReservations},null,2));
  const imagesBefore=lease.state().sceneRevision;
  const imageBudget=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.imageBudget',version:1}}]);
  const imageUsage=factReply(imageBudget).value as {entries:number;owners:number;textureBytes:number};
  if(!imageBudget.catalog?.available||imageUsage.entries!==0||imageUsage.owners!==0||imageUsage.textureBytes!==0)throw new Error('Fresh native workspace must have an empty appearance texture cache.');
  const emptyImage=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.imageReservation',version:1,arguments:{index:0}}}]);
  if(emptyImage.catalog?.available!==false||lease.state().sceneRevision!==imagesBefore)throw new Error('Empty image observation must be unavailable and read-only.');
  await writeFile(join(directory,'image-reservations.json'),JSON.stringify({boundary:'Actual empty workspace image cache, not process memory or regional streaming.',budget:imageUsage,empty:factReply(emptyImage)},null,2));
  const audioBudget=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.audioBudget',version:1}}]);
  const audioUsage=factReply(audioBudget).value as {sources:number;owners:number;retiring:number;reservedPcmBytes:number;readyPcmBytes:number};
  if(!audioBudget.catalog?.available||audioUsage.sources!==0||audioUsage.owners!==0||audioUsage.retiring!==0||audioUsage.reservedPcmBytes!==0||audioUsage.readyPcmBytes!==0)throw new Error('Fresh workspace retained room audio resources.');
  const emptyAudio=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.audioReservation',version:1,arguments:{index:0}}}]);
  if(emptyAudio.catalog?.available!==false||lease.state().sceneRevision!==imagesBefore)throw new Error('Empty audio observation must be unavailable and read-only.');
  await writeFile(join(directory,'audio-reservations-empty.json'),JSON.stringify({boundary:'Actual empty shared room PCM ledger, not measured process memory or streaming.',budget:audioUsage,empty:factReply(emptyAudio)},null,2));
  const scanLayout=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.scan',version:1}}]);
  const scanValue=factReply(scanLayout).value as {available:boolean;stateId:string;count:number};
  if(scanValue.available||scanValue.stateId!==''||scanValue.count!==0)throw new Error('Desktop scan facts must not invent a physical room.');
  const scanDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.scan.surface',version:1}}]);
  if(!scanDefinition.catalog?.definition)throw new Error('Shared scanned surface schema is unavailable.');
  const scanInkDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'drawing.layer.edit',version:1}}]);
  if(!scanInkDefinition.catalog?.definition||!lease.state().capabilities?.includes('scanDrawingLayers.v1'))throw new Error('Shared scanned ink capability is unavailable.');
  const inkCount=await nativeObjects.count(),inkState=lease.state();const deniedInk=await lease.execute([{action:'execution',execution:{operation:'start',call:{id:'drawing.layer.edit',version:1,arguments:capabilityDefinition('drawing.layer.edit')!.example!}}}],inkState.sceneRevision,roomRevisionBasis(inkState));
  observations.push(structuredClone(deniedInk));
  if(deniedInk.ok||await nativeObjects.count()!==inkCount)throw new Error('Desktop ink placement must refuse an unavailable scan without creating a layer.');
  const repeat=Number(process.env.MAESTRO_ROOM_PROBE_REPEATS??1);if(!Number.isInteger(repeat)||repeat<1||repeat>32)throw new Error('Probe repeats must be 1–32.');
  for(let cycle=0;cycle<repeat;cycle++){
  const cases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/lathe-contract.json','utf8'));
  const cup=cases[0].recipe;
  // This checks live collision ownership; keep the cup inside the stationary viewer's frustum.
  const lathe=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:'Native profile cup',x:.2,y:1.4,z:1.5,scale:1,recipe:cup}}}}]));
  const cupId=lathe.execution?.selected?.output?.objectId;if(typeof cupId!=='string')throw new Error('Lathe creation did not return an object.');
  const inspected=await execute([{action:'inspect',target:cupId}]);
  if(inspected.inspection?.recipe?.parts[0].shape!=='lathe')throw new Error('The shared client cannot inspect the native lathe.');
  const changed=structuredClone(inspected.inspection.recipe.parts[0]);if(!changed.profile||changed.profile.length<2)throw new Error('Native lathe profile is missing');changed.profile[1].x=.48;changed.segments=32;
  await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.recipe.edit',version:1,arguments:{target:cupId,revision:inspected.inspection.objectRevision,parts:[changed],removeParts:[],tracks:[],removeTracks:[],duration:cup.duration,loop:false}}}}]));
  const revision=await nativeObjects.revision(cupId);
  const profile=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.recipe.profile',version:1,arguments:{target:cupId,revision,part:'Body',offset:0}}}]);
  const value=profile.catalog?.value as {segments?:number;points?:{x:number;y:number}[]}|undefined;
  if(!profile.catalog?.available||value?.segments!==32||Math.abs((value.points?.[1].x??0)-.48)>.00001)throw new Error('Native lathe readback differs from the edit.');
  await execute([{action:'undo'}]);const restored=await execute([{action:'inspect',target:cupId}]);
  if(restored.inspection?.recipe?.parts[0].segments!==24)throw new Error('Lathe Undo did not restore the profile.');
  const collisionCases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/collision-contract.json','utf8'));
  const collisionCatalog=await execute([{action:'catalog',catalog:{operation:'search',query:'Edit collision shapes',offset:0}}]);
  const collisionDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.collision.edit',version:1}}]);
  const collisionCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:cupId}}}]);
  const collided=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.collision.edit',version:1,arguments:{target:cupId,revision:restored.inspection!.objectRevision,collision:collisionCases[0].collision}}}}]));
  const collisionRevision=await nativeObjects.revision(cupId);
  const readCollisionOwnership=async()=>{
   const budgetReply=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.collisionResources',version:1}}]);
   const budget=factReply(budgetReply).value as {entries:number;colliders:number;meshColliders:number;sourceTriangles:number;activeColliders:number};
   if(!budget||!Number.isInteger(budget.entries)||budget.entries<0)throw new Error('Native authored collision resources are unavailable.');
   const owners:Array<{leaseId:string;worldId:string;regionId:string;target:string;role:string;kind:string;phase:string;colliders:number;sourceTriangles:number}>=[];
   for(let index=0;index<budget.entries;index++){
    const reply=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.collisionResource',version:1,arguments:{index}}}]);
    const owner=factReply(reply).value as typeof owners[number];if(owner)owners.push(owner);
   }
   return {budget,owners};
  };
  const collisionOwned=await readCollisionOwnership();
  const cupOwner=collisionOwned.owners.find(owner=>owner.target===cupId&&owner.kind==='compound'&&owner.phase==='ready');
  if(!cupOwner||cupOwner.colliders!==13||cupOwner.sourceTriangles<=0||cupOwner.worldId.length!==32||cupOwner.regionId.length!==32||cupOwner.leaseId.length!==32||cupOwner.role!=='collision')throw new Error('The actual native cup collision geometry lost its shared resource owner.');
  if(lease.state().sceneRevision!==collided.sceneRevision)throw new Error('Collision resource observations mutated the authored scene.');

  const summary=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:cupId}}}]);
  const summaryValue=summary.catalog?.value as {pieces?:number;shapes?:number;customActive?:boolean}|undefined;
  if(summaryValue?.pieces!==13||summaryValue.shapes!==2||summaryValue.customActive!==true)throw new Error('Native compound collision summary differs from the edit.');
  const wall=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision.shape',version:1,arguments:{target:cupId,revision:collisionRevision,index:1}}}]);
  if((wall.catalog?.value as {shape?:{id?:string;shape?:string}})?.shape?.shape!=='ring')throw new Error('Native collision shape readback is missing the hollow wall.');
  await execute([{action:'undo'}]);
  const collisionRestored=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:cupId}}}]);
  if((collisionRestored.catalog?.value as {shapes?:number})?.shapes!==0)throw new Error('Collision Undo did not restore the default proxy.');
  const collisionReleased=await readCollisionOwnership();
  if(collisionReleased.owners.some(owner=>owner.target===cupId))throw new Error('Collision Undo left the retired cup geometry retained after native destruction.');
  await writeFile(join(directory,'collision-resources.json'),JSON.stringify({boundary:'Shared catalog reads of actual native compound collision ownership and Undo cleanup. Source geometry counts, not measured memory, enforced admission or headset evidence.',owned:collisionOwned,released:collisionReleased},null,2));
  await writeFile(join(directory,'collision-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:restored,search:collisionCatalog,definition:collisionDefinition,current:collisionCurrent,after:collided,summary,wall},null,2));
  await execute([{action:'undo'}]);if(await nativeObjects.exists(cupId))throw new Error('Lathe Undo did not remove the created geometry.');
  const extrusionCases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/extrusion-contract.json','utf8'));
  const extrusionRecipe=extrusionCases[0].recipe;
  const extrusionCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:'Editable bracket',x:.2,y:1,z:.5,scale:1,recipe:extrusionRecipe}}}}]));
  const extrusionId=extrusionCreated.execution?.selected?.output?.objectId;if(typeof extrusionId!=='string')throw new Error('Extrusion creation did not return an object');
  const extrusionBefore=await execute([{action:'inspect',target:extrusionId}]);if(extrusionBefore.inspection?.recipe?.parts[0].shape!=='extrude')throw new Error('Native extrusion source is not inspectable');
  const extrusionPart=structuredClone(extrusionBefore.inspection.recipe.parts[0]);extrusionPart.profile![3].x=0;
  const extrusionAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.recipe.edit',version:1,arguments:{target:extrusionId,revision:extrusionBefore.inspection.objectRevision,parts:[extrusionPart],removeParts:[],tracks:[],removeTracks:[],duration:2,loop:false}}}}]));
  const extrusionRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.recipe.profile',version:1,arguments:{target:extrusionId,revision:await nativeObjects.revision(extrusionId),part:'Outline',offset:0}}}]);
  const extrusionValue=extrusionRead.catalog?.value as {segments:number;points:{x:number;y:number}[]};if(extrusionValue?.segments!==0||extrusionValue.points[3].x!==0)throw new Error('Extrusion profile readback differs from edit');
  const extrusionUndo=await execute([{action:'undo'}]);const extrusionRestored=await execute([{action:'inspect',target:extrusionId}]);if(Math.abs((extrusionRestored.inspection?.recipe?.parts[0].profile?.[3].x??0)+.1)>.00001)throw new Error('Extrusion Undo did not restore source');
  await writeFile(join(directory,'extrusion-authoring.json'),JSON.stringify({boundary:'Real full-app native recipe creation, editing, profile readback and Undo. Browser replays these exact acknowledgements; no headset performance proof.',before:extrusionBefore,after:extrusionAfter,read:extrusionRead,undo:extrusionUndo,restored:extrusionRestored},null,2));
  await execute([{action:'undo'}]);
  const sweepCases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/sweep-contract.json','utf8'));
  const sweepCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:'Editable handle',x:.2,y:1,z:.5,scale:1,recipe:sweepCases[0].recipe}}}}]));
  const sweepId=sweepCreated.execution?.selected?.output?.objectId;if(typeof sweepId!=='string')throw new Error('Sweep creation did not return an object');
  const sweepBefore=await execute([{action:'inspect',target:sweepId}]);if(sweepBefore.inspection?.recipe?.parts[0].shape!=='sweep')throw new Error('Native sweep source is not inspectable');
  const sweepPart=structuredClone(sweepBefore.inspection.recipe.parts[0]);sweepPart.path![2].z=.05;
  const sweepAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.recipe.edit',version:1,arguments:{target:sweepId,revision:sweepBefore.inspection.objectRevision,parts:[sweepPart],removeParts:[],tracks:[],removeTracks:[],duration:2,loop:false}}}}]));
  const sweepRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.recipe.path',version:1,arguments:{target:sweepId,revision:await nativeObjects.revision(sweepId),part:'Handle',offset:0}}}]);
  const sweepValue=sweepRead.catalog?.value as {count:number;points:{x:number;y:number;z:number}[]};if(sweepValue?.count!==6||Math.abs(sweepValue.points[2].z-.05)>.00001)throw new Error('Sweep path readback differs from edit');
  const sweepUndo=await execute([{action:'undo'}]);const sweepRestored=await execute([{action:'inspect',target:sweepId}]);if(sweepRestored.inspection?.recipe?.parts[0].path?.[2].z!==0)throw new Error('Sweep Undo did not restore source');
  await writeFile(join(directory,'sweep-authoring.json'),JSON.stringify({boundary:'Real full-app native recipe creation, editing, path readback and Undo. Browser replays these exact acknowledgements; no headset performance proof.',before:sweepBefore,after:sweepAfter,read:sweepRead,undo:sweepUndo,restored:sweepRestored},null,2));
  await execute([{action:'undo'}]);
  const patternCases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/pattern-contract.json','utf8'));
  const patternCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:'Pattern panel',x:.2,y:1,z:.5,scale:1,recipe:patternCases[1].recipe}}}}]));
  const patternId=patternCreated.execution?.selected?.output?.objectId;if(typeof patternId!=='string')throw new Error('Pattern creation failed');
  const patternBefore=await execute([{action:'inspect',target:patternId}]);if(!patternBefore.inspection?.recipe)throw new Error('Pattern source missing');
  const patternPart=structuredClone(patternBefore.inspection.recipe.parts[0]);patternPart.pattern={kind:'stripes',plane:'xz',columns:4,rows:8,secondary:'#eeddcc'};
  const patternAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.recipe.edit',version:1,arguments:{target:patternId,revision:patternBefore.inspection.objectRevision,parts:[patternPart],removeParts:[],tracks:[],removeTracks:[],duration:2,loop:patternBefore.inspection.recipe.loop}}}}]));
  const patternRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.recipe.part',version:1,arguments:{target:patternId,revision:await nativeObjects.revision(patternId),index:0}}}]);
  if(!isDeepStrictEqual((patternRead.catalog?.value as {part:{pattern:unknown}}).part.pattern,patternPart.pattern))throw new Error('Pattern readback differs from accepted edit');
  const patternUndo=await execute([{action:'undo'}]);const patternRestored=await execute([{action:'inspect',target:patternId}]);if(patternRestored.inspection?.recipe?.parts[0].pattern?.kind!=='checker')throw new Error('Pattern Undo failed');
  await writeFile(join(directory,'pattern-authoring.json'),JSON.stringify({boundary:'Real full-app native pattern creation, edit, fact readback and Undo; browser replays native acknowledgements. No headset/provider proof.',before:patternBefore,after:patternAfter,read:patternRead,undo:patternUndo,restored:patternRestored},null,2));
  await execute([{action:'undo'}]);
  const templateBefore=structuredClone(lease.state());
  const templateSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'object.create',offset:0}}]);
  const templateDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}}]);
  const templateSchema=templateDefinition.catalog?.definition as {input:{oneOf:{examples:Record<string,unknown>[]}[]}};
  const templateArgs=templateSchema.input.oneOf.find(branch=>branch.examples[0]?.kind==='template')?.examples[0];if(!templateArgs)throw new Error('Starter templates unavailable');
  const templateAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:templateArgs}}}]));
  const templateId=templateAfter.execution?.selected?.output?.objectId;if(typeof templateId!=='string')throw new Error('Template creation returned no object');
  const templateRead=await execute([{action:'inspect',target:templateId}]);
  if((await nativeObjects.observation(templateId)).name!=='Cup'||templateRead.inspection?.recipe?.parts.length!==2)throw new Error('Template geometry did not expand');
  const templateCollision=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.collision',version:1,arguments:{target:templateId}}}]);
  if((templateCollision.catalog?.value as {pieces?:number})?.pieces!==21)throw new Error('Template collision did not expand');
  const templatePhysics=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.physics.settings',version:1,arguments:{target:templateId}}}]);
  if((templatePhysics.catalog?.value as {mode?:string})?.mode!=='solid')throw new Error('Template physics did not expand');
  const templateUndo=await execute([{action:'undo'}]);if(await nativeObjects.exists(templateId))throw new Error('Single Undo did not remove the whole template');
  await writeFile(join(directory,'template-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:templateBefore,search:templateSearch,definition:templateDefinition,after:templateAfter,read:templateRead,collision:templateCollision,physics:templatePhysics,undo:templateUndo},null,2));
  const brickSource=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/brick.json','utf8'));
  const {createHash}=await import('node:crypto');const brickHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/brick.json')).digest('hex');
  const snowHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/snow-patch.json')).digest('hex');
  const fieldCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...templateArgs,templateHash:snowHash,name:'Editable snow'}}}}]));
  const fieldId=fieldCreated.execution?.selected?.output?.objectId;if(typeof fieldId!=='string')throw new Error('Height surface creation failed');
  const groundPosition=(await nativeObjects.observation(fieldId)).position;
  const groundRead=(position:{x:number;y:number;z:number})=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.ground',version:1,arguments:{position,radius:.05}}}]);
  const groundSupported=await groundRead(groundPosition),groundMissing=await groundRead({x:0,y:24,z:0});
  type GroundAvailability={found:boolean;availability:string;dependencyCount:number;dependencies:unknown[]};
  const supportedGround=groundSupported.catalog?.value as GroundAvailability,missingGround=groundMissing.catalog?.value as GroundAvailability;
  if(!groundSupported.catalog?.available||!supportedGround.found||supportedGround.availability!=='ready'||supportedGround.dependencyCount!==0||supportedGround.dependencies.length!==0)throw new Error('Accepted terrain is not ready through the shared ground fact');
  if(!groundMissing.catalog?.available||missingGround.found||missingGround.availability!=='ready'||missingGround.dependencyCount!==0)throw new Error('Known unsupported ground was confused with unavailable geometry');
  if(groundMissing.sceneRevision!==fieldCreated.sceneRevision||groundMissing.physics?.running)throw new Error('Passive ground inspection edited the world or started physics');
  await writeFile(join(directory,'world-ground.json'),JSON.stringify({boundary:'Real full-app native fact over the shared headless/book transport. Pending/retired/failed geometry is covered separately in PlayMode; no headset or provider proof.',position:groundPosition,supported:groundSupported,missing:groundMissing},null,2));
  const environmentRead=async()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.environment',version:1}}]);
  const environmentBefore=await environmentRead();
  const environmentBeforeValue=environmentBefore.catalog?.value as {stateId:string;realCollisions:boolean;authoredReady:boolean;scanReady:boolean};
  if(!environmentBeforeValue.realCollisions||!environmentBeforeValue.authoredReady)throw new Error('Physics environment lost its default or accepted authored ground');
  const virtualEnvironment=await execute([{action:'execution',execution:{operation:'start',call:{id:'physics.environment.set',version:1,arguments:{realCollisions:false,stateId:environmentBeforeValue.stateId}}}}]);
  const environmentVirtual=await environmentRead();
  const environmentVirtualValue=environmentVirtual.catalog?.value as {stateId:string;realCollisions:boolean;ready:boolean;scanReady:boolean};
  if(environmentVirtualValue.realCollisions||!environmentVirtualValue.ready||environmentVirtualValue.scanReady!==environmentBeforeValue.scanReady||virtualEnvironment.physics?.running)throw new Error('Collision policy changed scan readiness or started physics');
  const physicalEnvironment=await execute([{action:'execution',execution:{operation:'start',call:{id:'physics.environment.set',version:1,arguments:{realCollisions:true,stateId:environmentVirtualValue.stateId}}}}]);
  const environmentRestored=await environmentRead();
  if(!(environmentRestored.catalog?.value as {realCollisions:boolean}).realCollisions||physicalEnvironment.physics?.running)throw new Error('Restored physical collision policy did not remain paused');
  await writeFile(join(directory,'physics-environment.json'),JSON.stringify({boundary:'Full native shared transport and editable terrain readiness; actual collisions are tested in PlayMode. No provider or headset proof.',before:environmentBefore,disabled:virtualEnvironment,virtual:environmentVirtual,enabled:physicalEnvironment,restored:environmentRestored},null,2));

  await probeWorldPresentation(execute,directory);
  await probeWorldLighting(execute,directory);
  await probeObjectLights(execute,directory);
  await probeWorldTime(execute,directory);
  await probeWorldWeather(execute,directory);
  await probeLiquidMedium(execute,directory);
  await probeWaterTraversal(execute,directory);
  await probeTerrainWater(execute,directory);
 await probeChatAudio(transport,execute,directory);
  await probeVisibilityLayers(execute,directory);
  await probeWorldRegions(execute,directory);

  const profileSaved=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'environment.profile.save',version:1,arguments:{id:'',revision:0,name:'Virtual terrain actors',realCollisions:false,members:[]}}}}]));
  const profileId=profileSaved.execution?.selected?.output?.id;
  if(typeof profileId!=='string')throw new Error('Environment profile returned no stable ID');
  const profileRead=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'environment.profile',version:1,arguments:{id:profileId}}}]);
  const actorEnvironment=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.environment',version:1,arguments:{target:'maestro'}}}]);
  const unbound=await actorEnvironment(),profileInitial=await profileRead();
  const profileAssigned=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.environment.assign',version:1,arguments:{target:'maestro',revision:(unbound.catalog!.value as {revision:number}).revision,profileId,profileRevision:(profileInitial.catalog!.value as {revision:number}).revision}}}}]));
  const bound=await actorEnvironment(),profileBound=await profileRead();
  const boundValue=bound.catalog!.value as {profileId:string;state:{effectiveRealCollisions:boolean}};
  if(boundValue.profileId!==profileId||boundValue.state.effectiveRealCollisions)throw new Error('Per-actor profile did not exclude the real room');
  const members=(profileBound.catalog!.value as {members:string[]}).members;
  if(JSON.stringify(members)!==JSON.stringify(['maestro']))throw new Error('Profile membership did not preserve exact target IDs');
  const profileEdited=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'environment.profile.save',version:1,arguments:{id:profileId,revision:(profileBound.catalog!.value as {revision:number}).revision,name:'Downhill actors',realCollisions:false,members}}}}]));
  const profileList=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'environment.profiles',version:1,arguments:{offset:0}}}]);
  await execute([{action:'undo'}]);const profileUndo=await profileRead();
  if((profileUndo.catalog!.value as {name:string}).name!=='Virtual terrain actors')throw new Error('Profile Undo did not restore the definition');
  await execute([{action:'undo'}]);const bindingUndo=await actorEnvironment();
  if((bindingUndo.catalog!.value as {profileId:string}).profileId!=='')throw new Error('Assignment Undo did not restore inheritance');
  const unusedProfile=await profileRead();
  const profileRemoved=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'environment.profile.remove',version:1,arguments:{id:profileId,revision:(unusedProfile.catalog!.value as {revision:number}).revision}}}}]));
  await writeFile(join(directory,'entity-environment.json'),JSON.stringify({boundary:'Real native shared transport, persisted profile edits, binding facts and Undo. PlayMode separately tests physical motion; no headset or live provider proof.',profileSaved,unbound,profileInitial,profileAssigned,bound,profileBound,profileEdited,profileList,profileUndo,bindingUndo,profileRemoved},null,2));

  const appearanceStyle={tint:'#DDBBAA',patternMode:'inherit',imageHash:'',pattern:{kind:'solid',plane:'uv',secondary:'#FFFFFF',columns:1,rows:1},tiling:{x:1,y:1},offset:{x:0,y:0},renderMode:'blend',opacity:.4,cutoff:0,sidedness:'inherit',grain:-1,shading:-1};
  const appearanceSaved=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'appearance.save',version:1,arguments:{id:'',revision:0,name:'Shared warm glass',style:appearanceStyle,members:[]}}}}]));
  const appearanceId=appearanceSaved.execution?.selected?.output?.id;if(typeof appearanceId!=='string')throw new Error('Appearance returned no stable ID');
  const appearanceRead=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'appearance.definition',version:2,arguments:{id:appearanceId}}}]);
  const appearanceBindings=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.appearances',version:1,arguments:{target:fieldId,offset:0}}}]);
  const appearanceInitial=await appearanceRead();
  const appearanceBound=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.appearance.bind',version:1,arguments:{operation:'assign',target:fieldId,revision:await nativeObjects.revision(fieldId),appearanceRevision:(appearanceInitial.catalog!.value as {revision:number}).revision,binding:{version:1,appearanceId,kind:'root',partId:'',modelHash:'',materialIndex:-1,tint:''}}}}}]));
  const appearanceBeforePaint=await appearanceBindings();
  const appearancePainted=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.color.set',version:1,arguments:{target:fieldId,red:0,green:0,blue:1}}}}]));
  const appearanceAfterPaint=await appearanceBindings(),appearanceUnchanged=await appearanceRead();
  if((appearanceAfterPaint.catalog!.value as {bindings:{tint:string}[]}).bindings[0].tint!=='#0000FF'||(await nativeObjects.observation(fieldId)).color.b!==1)throw new Error('Object paint did not update the canonical local appearance');
  if(JSON.stringify(appearanceInitial.catalog!.value)!==JSON.stringify(appearanceUnchanged.catalog!.value))throw new Error('Object painting changed a shared appearance');
  const appearanceEdited=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'appearance.save',version:1,arguments:{id:appearanceId,revision:(appearanceUnchanged.catalog!.value as {revision:number}).revision,name:'Shared warm glass',style:{...appearanceStyle,opacity:.7},members:[fieldId]}}}}]));
  const appearanceChanged=await appearanceRead();if(Math.abs((appearanceChanged.catalog!.value as {surface:{opacity:number}}).surface.opacity-.7)>.00001)throw new Error('Shared appearance edit was not saved');
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  const appearanceRestored=await appearanceBindings();if((appearanceRestored.catalog!.value as {bindings:unknown[]}).bindings.length!==0)throw new Error('Appearance Undo did not restore unbound object');
  const appearanceUnused=await appearanceRead();
  const appearanceRemoved=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'appearance.remove',version:1,arguments:{id:appearanceId,revision:(appearanceUnused.catalog!.value as {revision:number}).revision}}}}]));
  await writeFile(join(directory,'appearance-authoring.json'),JSON.stringify({boundary:'Real native shared transport, persisted appearances, local paint, facts and Undo; no headset or live-provider proof.',appearanceSaved,appearanceInitial,appearanceBound,appearanceBeforePaint,appearancePainted,appearanceAfterPaint,appearanceUnchanged,appearanceEdited,appearanceChanged,appearanceRestored,appearanceRemoved},null,2));

  const portableResources=await probeConstructionResources(execute,directory);

  const fieldSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Sculpt a surface path',offset:0}}]);
  const fieldDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.field.sculpt',version:1}}]);
  const fieldBefore=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.field',version:1,arguments:{target:fieldId}}}]);
  const fieldRevision=(fieldBefore.catalog?.value as {revision:number}).revision;
  const fieldCall={id:'object.field.sculpt',version:1,arguments:{target:fieldId,revision:fieldRevision,mode:'lower',radius:.14,height:.04,path:[{x:-.3,z:0},{x:.3,z:0}]}};
  const fieldAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:fieldCall}}]));
  const fieldRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.field.samples',version:1,arguments:{target:fieldId,revision:await nativeObjects.revision(fieldId),offset:144}}}]);
  if(Math.abs((fieldRead.catalog?.value as {heights:number[]}).heights[0]-.04)>.000001)throw new Error('Accepted height differs from sculpted source');
  const fieldUndo=await execute([{action:'undo'}]);
  const fieldRestored=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.field.samples',version:1,arguments:{target:fieldId,revision:await nativeObjects.revision(fieldId),offset:144}}}]);
  if(Math.abs((fieldRestored.catalog?.value as {heights:number[]}).heights[0]-.08)>.000001)throw new Error('Surface Undo did not restore heights');
  await writeFile(join(directory,'height-field-authoring.json'),JSON.stringify({boundary:'Real shared-client/native surface creation, sculpt, readback and Undo. Physical collision is tested separately; no headset/provider proof.',created:fieldCreated,search:fieldSearch,definition:fieldDefinition,before:fieldBefore,call:fieldCall,after:fieldAfter,read:fieldRead,undo:fieldUndo,restored:fieldRestored},null,2));
  const receivingField=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...templateArgs,templateHash:snowHash,name:'Receiving snow',x:1.5}}}}]));
  const receivingId=receivingField.execution?.selected?.output?.objectId;if(typeof receivingId!=='string')throw new Error('Receiving height surface creation failed');
  const transferFieldRead=async(target:string)=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.field',version:1,arguments:{target}}}]);
  const transferBeforeSource=await transferFieldRead(fieldId),transferBeforeDestination=await transferFieldRead(receivingId);
  const transferSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Transfer surface volume',offset:0}}]);
  const transferDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.field.transfer',version:1}}]);
  const transferEndpoint=(target:string,state:RoomAgentState)=>({target,revision:(factReply(state).value as {revision:number}).revision,centre:{x:0,z:0},radius:.3});
  const transferCall={id:'object.field.transfer',version:1,arguments:{source:transferEndpoint(fieldId,transferBeforeSource),destination:transferEndpoint(receivingId,transferBeforeDestination),amountLitres:1}};
  const transferAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:transferCall}}]));
  const transferReadSource=await transferFieldRead(fieldId),transferReadDestination=await transferFieldRead(receivingId);
  const localVolume=(state:RoomAgentState)=>(factReply(state).value as {volumeLitres:number}).volumeLitres;
  const transferOutput=transferAfter.execution?.selected?.output as {removedLitres:number;addedLitres:number;roundingLitres:number};
  if(!transferOutput||Math.abs(localVolume(transferBeforeSource)-localVolume(transferReadSource)-transferOutput.removedLitres)>.000001||Math.abs(localVolume(transferReadDestination)-localVolume(transferBeforeDestination)-transferOutput.addedLitres)>.000001||Math.abs(transferOutput.roundingLitres)>Math.max(.000001,transferOutput.removedLitres*.000001))throw new Error('Surface transfer differs from actual saved volume');
  const transferUndo=await execute([{action:'undo'}]);const transferRestoredSource=await transferFieldRead(fieldId),transferRestoredDestination=await transferFieldRead(receivingId);
  if(localVolume(transferRestoredSource)!==localVolume(transferBeforeSource)||localVolume(transferRestoredDestination)!==localVolume(transferBeforeDestination))throw new Error('Surface transfer Undo did not restore both sides');
  await writeFile(join(directory,'field-transfer-authoring.json'),JSON.stringify({boundary:'Real full-app native shared-client transfer, actual volume readback and atomic Undo. Browser separately replays these observations; no headset/provider proof.',created:receivingField,beforeSource:transferBeforeSource,beforeDestination:transferBeforeDestination,search:transferSearch,definition:transferDefinition,call:transferCall,after:transferAfter,source:transferReadSource,destination:transferReadDestination,undo:transferUndo,restoredSource:transferRestoredSource,restoredDestination:transferRestoredDestination},null,2));
  const packingCreated=structuredClone(lease.state());
  const packingSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Pack surface material',offset:0}}]);
  const packingDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.material.pack',version:1}}]);
  const packingBefore=await transferFieldRead(fieldId);
  const packingCall={id:'object.material.pack',version:1,arguments:{source:transferEndpoint(fieldId,packingBefore),amountLitres:.5,name:'Packed snowball',position:{x:.2,y:1.4,z:.5},mass:.15}};
  const packingAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:packingCall}}]));
  const packingOutput=packingAfter.execution?.selected?.output as {objectId:string;amountLitres:number};if(!packingOutput?.objectId)throw new Error('Packing did not create a ball');
  const packingSource=await transferFieldRead(fieldId);
  const packingMaterial=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.material',version:1,arguments:{target:packingOutput.objectId}}}]);
  const packingStore=packingMaterial.catalog?.value as {configured:boolean;definition:{amountLitres:number;capacityLitres:number;material:string}};
  if(!packingStore.configured||packingStore.definition.amountLitres!==packingOutput.amountLitres||packingStore.definition.capacityLitres!==packingOutput.amountLitres||packingOutput.amountLitres>.5||Math.abs(localVolume(packingBefore)-localVolume(packingSource)-packingOutput.amountLitres)>1e-8)throw new Error('Packing disagrees with measured source loss');
  const packingRead=await execute([{action:'inspect',target:packingOutput.objectId}]);
  if(packingRead.inspection?.recipe?.parts[0].shape!=='sphere')throw new Error('Packed ball is not an editable ordinary recipe');
  const packingUndo=await execute([{action:'undo'}]);const packingRestored=await transferFieldRead(fieldId);
  if(await nativeObjects.exists(packingOutput.objectId)||localVolume(packingRestored)!==localVolume(packingBefore))throw new Error('Packing Undo did not restore both sides');
  await writeFile(join(directory,'material-packing-authoring.json'),JSON.stringify({boundary:'Real full-app native shared-client packing, material fact, ordinary geometry, measured source loss and atomic Undo. Browser separately replays these observations; no headset/provider proof.',created:packingCreated,before:packingBefore,search:packingSearch,definition:packingDefinition,call:packingCall,after:packingAfter,source:packingSource,material:packingMaterial,read:packingRead,undo:packingUndo,restored:packingRestored},null,2));
  // A carried store has one authoritative balance; surface/store transfers share the native kernel.
  const materialValue=(state:RoomAgentState)=>{if(state.catalog?.operation!=='inspect'||!('value' in state.catalog))throw new Error('Expected a native material fact');return state.catalog.value as {revision:number;configured?:boolean;definition:{amountLitres:number;capacityLitres:number;material:string;color:{r:number;g:number;b:number;a:number}}};};
  const materialRead=(id:string)=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.material',version:1,arguments:{target:id}}}]);
  const transferCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,shape:'block',name:'Carried material store',x:1,y:1,z:.7}}}}]));
  const storeId=transferCreated.execution?.selected?.output?.objectId;if(typeof storeId!=='string')throw new Error('Material carrier was not created');
  const materialSourceBefore=await transferFieldRead(fieldId);const sourceMaterial=materialValue(materialSourceBefore).definition;
  const materialConfigured=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.material.edit',version:1,arguments:{operation:'configure',target:storeId,revision:await nativeObjects.revision(storeId),definition:{capacityLitres:1,amountLitres:0,material:sourceMaterial.material,color:sourceMaterial.color}}}}}]));
  const materialStoreBefore=await materialRead(storeId);
  const materialSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Transfer carried or surface material',offset:0}}]);
  const materialDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.material.transfer',version:1}}]);
  const materialCall={id:'object.material.transfer',version:1,arguments:{source:{kind:'field',...transferEndpoint(fieldId,materialSourceBefore)},destination:{kind:'store',target:storeId,revision:materialValue(materialStoreBefore).revision},amountLitres:.2}};
  const materialAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:materialCall}}]));
  const materialSourceTaken=await transferFieldRead(fieldId),materialStoreTaken=await materialRead(storeId);
  const materialResult=materialAfter.execution?.selected?.output as {removedLitres:number;addedLitres:number;roundingLitres:number};
  if(materialAfter.execution?.selected?.phase!=='completed'||Math.abs(localVolume(materialSourceBefore)-localVolume(materialSourceTaken)-materialResult.removedLitres)>1e-8||materialValue(materialStoreTaken).definition.amountLitres!==materialResult.addedLitres)throw new Error('The carried transfer disagrees with its native balances');
  const materialReturnCall={id:'object.material.transfer',version:1,arguments:{source:{kind:'store',target:storeId,revision:materialValue(materialStoreTaken).revision},destination:{kind:'field',...transferEndpoint(fieldId,materialSourceTaken)},amountLitres:.1}};
  const materialReturned=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:materialReturnCall}}]));
  const materialSourceReturned=await transferFieldRead(fieldId),materialStoreReturned=await materialRead(storeId);
  if(localVolume(materialSourceReturned)<=localVolume(materialSourceTaken)||materialValue(materialStoreReturned).definition.amountLitres>=materialValue(materialStoreTaken).definition.amountLitres)throw new Error('Depositing did not update both native balances');
  const materialUndoReturn=await execute([{action:'undo'}]);const materialRestoredTakenSource=await transferFieldRead(fieldId),materialRestoredTakenStore=await materialRead(storeId);
  if(localVolume(materialRestoredTakenSource)!==localVolume(materialSourceTaken)||materialValue(materialRestoredTakenStore).definition.amountLitres!==materialValue(materialStoreTaken).definition.amountLitres)throw new Error('Deposit Undo did not restore both balances');
  const materialUndoTake=await execute([{action:'undo'}]);const materialRestoredSource=await transferFieldRead(fieldId),materialRestoredStore=await materialRead(storeId);
  if(localVolume(materialRestoredSource)!==localVolume(materialSourceBefore)||materialValue(materialRestoredStore).definition.amountLitres!==0)throw new Error('Take Undo did not restore both balances');
  await writeFile(join(directory,'material-transfer-authoring.json'),JSON.stringify({boundary:'Real full-app native shared-client surface/store transfer, actual balance readback, return deposit and atomic Undo. No physical shovel, headset or provider proof.',created:materialStoreBefore,configured:materialConfigured,sourceBefore:materialSourceBefore,storeBefore:materialStoreBefore,search:materialSearch,definition:materialDefinition,call:materialCall,after:materialAfter,sourceTaken:materialSourceTaken,storeTaken:materialStoreTaken,returnCall:materialReturnCall,returned:materialReturned,sourceReturned:materialSourceReturned,storeReturned:materialStoreReturned,undoReturn:materialUndoReturn,undoTake:materialUndoTake,restoredSource:materialRestoredSource,restoredStore:materialRestoredStore},null,2));
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await execute([{action:'undo'}]);
  await execute([{action:'undo'}]);
  const sculptHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/sculpt-brush.json')).digest('hex');
  const sculptCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...templateArgs,templateHash:sculptHash,name:'Editable sculpt brush'}}}}]));
  const sculptId=sculptCreated.execution?.selected?.output?.objectId;if(typeof sculptId!=='string')throw new Error('Sculpt brush creation failed');
  const sculptSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'sculpt tip',offset:0}}]);
  const sculptDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.sculptTip.edit',version:1}}]);
  const sculptBefore=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.sculptTip',version:1,arguments:{target:sculptId}}}]);
  const sculptSource=sculptBefore.catalog?.value as {revision:number;configured:boolean;definition:Record<string,unknown>};if(!sculptSource.configured)throw new Error('Default sculpt tip absent');
  const sculptCall={id:'object.sculptTip.edit',version:1,arguments:{operation:'configure',target:sculptId,revision:sculptSource.revision,definition:{...sculptSource.definition,mode:'level',radius:.1,height:.12}}};
  const sculptAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:sculptCall}}]));
  const sculptRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.sculptTip',version:1,arguments:{target:sculptId}}}]);
  const sculptSaved=(sculptRead.catalog?.value as {definition:{mode:string;height:number;radius:number}}).definition;if(sculptSaved.mode!=='level'||Math.abs(sculptSaved.height-.12)>.000001||Math.abs(sculptSaved.radius-.1)>.000001)throw new Error('Sculpt tip edit/readback differs');
  const sculptUndo=await execute([{action:'undo'}]);const sculptRestored=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.sculptTip',version:1,arguments:{target:sculptId}}}]);if((sculptRestored.catalog?.value as {definition:{mode:string}}).definition.mode!=='lower')throw new Error('Sculpt tip Undo failed');
  await execute([{action:'execution',execution:{operation:'start',call:{id:'field.tool.set',version:1,arguments:{mode:'lower',radius:.08,height:.03}}}}]);
  const sculptTool=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'field.tool',version:1}}]);if((sculptTool.catalog?.value as {mode:string}).mode!=='lower')throw new Error('Physical sculpt settings unavailable');
  const sculptIdle=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.field.capture',version:1}}]);if((sculptIdle.catalog?.value as {phase:string}).phase!=='idle')throw new Error('Choosing tool unexpectedly started a sculpt gesture');
  await execute([{action:'execution',execution:{operation:'start',call:{id:'field.tool.set',version:1,arguments:{mode:'off',radius:.08,height:.03}}}}]);
  await writeFile(join(directory,'physical-sculpt-authoring.json'),JSON.stringify({boundary:'Real full-app shared-client/native sculpt-tip edit, settings, facts and Undo. Physical gestures are separate PlayMode tests; no headset/provider proof.',created:sculptCreated,search:sculptSearch,definition:sculptDefinition,before:sculptBefore,after:sculptAfter,read:sculptRead,undo:sculptUndo,restored:sculptRestored,tool:sculptTool,idle:sculptIdle},null,2));
  await execute([{action:'undo'}]);
  const scoopHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/material-scoop.json')).digest('hex');
  const scoopCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...templateArgs,templateHash:scoopHash,name:'Editable material scoop'}}}}]));
  const scoopId=(scoopCreated.execution?.selected?.output as {objectId?:unknown}|undefined)?.objectId;if(typeof scoopId!=='string')throw new Error('Material scoop was not created');
  const physicalPackingSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'physical material packing tool',offset:0}}]);
  const physicalPackingDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'material.pack.tool.set',version:1}}]);
  const physicalPackingSettings={enabled:true,radius:.15,amountLitres:.1,mass:.2};
  const physicalPackingSet=await execute([{action:'execution',execution:{operation:'start',call:{id:'material.pack.tool.set',version:1,arguments:physicalPackingSettings}}}]);
  const physicalPackingRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'material.pack.tool',version:1}}]);
  const physicalPackingValues=factReply(physicalPackingRead).value as typeof physicalPackingSettings;
  if(!physicalPackingValues.enabled||Math.abs(physicalPackingValues.radius-.15)>.000001||Math.abs(physicalPackingValues.amountLitres-.1)>.000001||Math.abs(physicalPackingValues.mass-.2)>.000001)throw new Error('Physical packing settings differ from the shared accepted call');
  const physicalPackingIdle=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'material.pack.capture',version:1}}]);
  const physicalPackingCapture=factReply(physicalPackingIdle).value as {phase:string;lastSaved:{objectId:string}};
  if(physicalPackingCapture.phase!=='idle'||physicalPackingCapture.lastSaved.objectId!=='')throw new Error('Configuring physical packing unexpectedly created a ball or gesture');
  const physicalPackingDisabled=await execute([{action:'execution',execution:{operation:'start',call:{id:'material.pack.tool.set',version:1,arguments:{...physicalPackingSettings,enabled:false}}}}]);
  await writeFile(join(directory,'physical-packing-authoring.json'),JSON.stringify({boundary:'Actual native shared configuration and readback only. Physical touch/trigger gestures have separate PlayMode evidence; no headset/provider proof.',created:scoopCreated,search:physicalPackingSearch,definition:physicalPackingDefinition,call:{id:'material.pack.tool.set',version:1,arguments:physicalPackingSettings},set:physicalPackingSet,read:physicalPackingRead,idle:physicalPackingIdle,disabled:physicalPackingDisabled},null,2));
  const scoopSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'sculpt tip',offset:0}}]);
  const scoopDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.sculptTip.edit',version:1}}]);
  const readScoop=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.sculptTip',version:1,arguments:{target:scoopId}}}]);
  const scoopBefore=await readScoop();
  const materialScoopValueOf=(state:RoomAgentState)=>{if(state.catalog?.operation!=='inspect'||!('value' in state.catalog))throw new Error('Expected native scoop settings');return state.catalog.value as {revision:number;definition:{mode:string;amountLitres:number;height:number}};};
  const materialScoopValue=materialScoopValueOf(scoopBefore);
  if(materialScoopValue.definition.mode!=='scoop'||materialScoopValue.definition.amountLitres!==.25||materialScoopValue.definition.height!==0)throw new Error('Material scoop configuration differs from the template');
  const scoopCall={id:'object.sculptTip.edit',version:1,arguments:{operation:'configure',target:scoopId,revision:materialScoopValue.revision,definition:{...materialScoopValue.definition,amountLitres:.1,radius:.12}}};
  const scoopAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:scoopCall}}]));
  const scoopRead=await readScoop();if(materialScoopValueOf(scoopRead).definition.amountLitres!==.1)throw new Error('Native scoop amount edit failed');
  const scoopStore=await materialRead(scoopId);if(materialValue(scoopStore).definition.amountLitres!==0)throw new Error('Configuring a scoop created material');
  const scoopIdle=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.material.capture',version:1}}]);if(scoopIdle.catalog?.operation!=='inspect'||!('value' in scoopIdle.catalog)||(scoopIdle.catalog.value as {phase:string}).phase!=='idle')throw new Error('Configuring a scoop began a contact');
  const scoopUndo=await execute([{action:'undo'}]),scoopRestored=await readScoop();if(materialScoopValueOf(scoopRestored).definition.amountLitres!==.25)throw new Error('Scoop Undo did not restore its dose');
  await writeFile(join(directory,'material-scoop-authoring.json'),JSON.stringify({boundary:'Full native app template, shared sculpt-tip action, material/capture facts and Undo. Physical contact is separately verified in Unity PlayMode; no headset/provider proof.',created:scoopCreated,search:scoopSearch,definition:scoopDefinition,before:scoopBefore,call:scoopCall,after:scoopAfter,read:scoopRead,store:scoopStore,idle:scoopIdle,undo:scoopUndo,restored:scoopRestored},null,2));
  await execute([{action:'undo'}]);
  const liquidA=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...templateArgs,name:'Source cup'}}}}]));
  const liquidB=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...templateArgs,name:'Receiving cup',x:.8}}}}]));
  const liquidFrom=liquidA.execution?.selected?.output?.objectId,liquidTo=liquidB.execution?.selected?.output?.objectId;
  if(typeof liquidFrom!=='string'||typeof liquidTo!=='string')throw new Error('Container templates returned no object identities');
  const liquidRead=async(target:string)=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container',version:1,arguments:{target}}}]);
  const emptySource=await liquidRead(liquidFrom);const sourceValue=emptySource.catalog?.value as {configured:boolean;revision:number;definition:Record<string,unknown>};
  if(!sourceValue.configured||sourceValue.definition.amountMl!==0||sourceValue.definition.capacityMl!==500)throw new Error('Included cup did not expand its empty liquid store');
  await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.container.edit',version:1,arguments:{operation:'configure',target:liquidFrom,revision:sourceValue.revision,definition:{...sourceValue.definition,amountMl:250}}}}}]));
  const containerBefore=structuredClone(lease.state());
  const containerSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Transfer a measured amount of liquid',offset:0}}]);
  const containerDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'object.container.transfer',version:1}}]);
  const containerSource=await liquidRead(liquidFrom),containerDestination=await liquidRead(liquidTo);
  const containerArguments={source:{target:liquidFrom,revision:(containerSource.catalog?.value as {revision:number}).revision},destination:{target:liquidTo,revision:(containerDestination.catalog?.value as {revision:number}).revision},amountMl:125};
  const containerAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.container.transfer',version:1,arguments:containerArguments}}}]));
  if(containerAfter.execution?.selected?.output?.transferredMl!==125)throw new Error('Measured transfer receipt did not report its conserved amount');
  const containerFromAfter=await liquidRead(liquidFrom),containerToAfter=await liquidRead(liquidTo);
  for(const state of [containerFromAfter,containerToAfter])if((state.catalog?.value as {definition:{amountMl:number}}).definition.amountMl!==125)throw new Error('Measured transfer quantities differ from the real room');
  const pouringBefore=structuredClone(lease.state()),pouringSteps:{request:RoomCommand;response:unknown}[]=[];
  const pouringQuery=async<const C extends RoomCommand>(request:C)=>{const response=await execute([request]);pouringSteps.push({request,response});return response;};
  await pouringQuery({action:'catalog',catalog:{operation:'search',category:'facts',query:'Live container contents',offset:0}});
  await pouringQuery({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container.live',version:1}});
  const containerLive=await pouringQuery({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container.live',version:1,arguments:{target:liquidFrom}}});
  const liveValue=containerLive.catalog?.value as {phase:string;contents:{amountMl:number;savedAmountMl:number}};
  if(!containerLive.catalog?.available||liveValue.phase!=='idle'||liveValue.contents.amountMl!==125||liveValue.contents.savedAmountMl!==125)throw new Error('Native live liquid fact disagrees with accepted contents while physics is paused');
  const liquidPersistence=await pouringQuery({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.liquidPersistence',version:1,arguments:{}}});
  const persistence=liquidPersistence.catalog?.value as {phase:string;pending:number;capacity:number;temporary:boolean;error:string};
  if(!liquidPersistence.catalog?.available||persistence.phase!=='idle'||persistence.pending!==0||persistence.capacity!==2||persistence.temporary)throw new Error('Shared liquid durability fact is unavailable or inconsistent');
  await writeFile(join(directory,'liquid-persistence.json'),JSON.stringify({boundary:'Actual native shared fact through headless transport. Idle durability discovery; stalled-worker and failure semantics are covered by PlayMode.',persistence},null,2));
  await pouringQuery({action:'catalog',catalog:{operation:'search',category:'events',query:'A physical pour was saved',offset:0}});
  const pouredEvent=await pouringQuery({action:'catalog',catalog:{operation:'inspect',category:'events',capability:'object.container.poured',version:1}});
  if(pouredEvent.catalog?.operation!=='inspect'||!pouredEvent.catalog.definition)throw new Error('Native poured event is not discoverable');
  await writeFile(join(directory,'container-pouring-contract.json'),JSON.stringify({boundary:'Full native app exposes live quantities and typed event metadata. Physical flow is verified separately in real Unity PlayMode interaction tests; no scan or headset performance proof.',before:pouringBefore,steps:pouringSteps,live:containerLive,event:pouredEvent},null,2));
  const scoopingBefore=structuredClone(lease.state()),scoopingSteps:{request:RoomCommand;response:unknown}[]=[];
  const scoopingQuery=async<const C extends RoomCommand>(request:C)=>{const response=await execute([request]);scoopingSteps.push({request,response});return response;};
  await scoopingQuery({action:'catalog',catalog:{operation:'search',category:'facts',query:'Live liquid scooping',offset:0}});
  await scoopingQuery({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container.scooping',version:1}});
  const scoopLive=await scoopingQuery({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container.scooping',version:1,arguments:{target:liquidFrom}}});
  const scoopValue=scoopLive.catalog?.value as {phase:string;scoopedMl:number;drawnMl:number};
  if(!scoopLive.catalog?.available||scoopValue.phase!=='idle'||scoopValue.scoopedMl!==0||scoopValue.drawnMl!==0)throw new Error('Native scooping fact should be idle while physics is paused');
  await scoopingQuery({action:'catalog',catalog:{operation:'search',category:'events',query:'A liquid scoop was saved',offset:0}});
  const scoopedEvent=await scoopingQuery({action:'catalog',catalog:{operation:'inspect',category:'events',capability:'object.container.scooped',version:1}});
  if(!scoopedEvent.catalog?.definition)throw new Error('Native scooped event is not discoverable');
  await writeFile(join(directory,'container-scooping-contract.json'),JSON.stringify({boundary:'Full native app reads idle scooping state and typed event metadata. Physical flow is verified separately in real Unity PlayMode interaction tests; no scan or headset performance proof.',before:scoopingBefore,steps:scoopingSteps,live:scoopLive,event:scoopedEvent},null,2));
  const containerUndo=await execute([{action:'undo'}]);const containerRestoredSource=await liquidRead(liquidFrom),containerRestoredDestination=await liquidRead(liquidTo);
  if((containerRestoredSource.catalog?.value as {definition:{amountMl:number}}).definition.amountMl!==250||(containerRestoredDestination.catalog?.value as {definition:{amountMl:number}}).definition.amountMl!==0)throw new Error('One native Undo did not restore both liquid quantities');
  await writeFile(join(directory,'container-authoring.json'),JSON.stringify({boundary:'Real Unity saved quantities, shared calls, facts and atomic Undo. No physical pouring or headset performance proof.',before:containerBefore,search:containerSearch,definition:containerDefinition,source:containerSource,destination:containerDestination,after:containerAfter,fromAfter:containerFromAfter,toAfter:containerToAfter,undo:containerUndo,restoredSource:containerRestoredSource,restoredDestination:containerRestoredDestination},null,2));
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  const scoopTemplates=[];
  for(const [template,amount,capacity] of [['bucket',0,2000],['basin',32000,40000]] as const){
    const hash=createHash('sha256').update(await readFile(`unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/${template}.json`)).digest('hex');
    const before=structuredClone(lease.state()),beforeCount=await nativeObjects.count();
    const created=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:hash,name:'',x:.4,y:1,z:.6,scale:1}}}}]));
    const id=created.execution?.selected?.output?.objectId;if(typeof id!=='string')throw new Error('Missing scoop-template object identity');
    const contents=await liquidRead(id),definition=(contents.catalog?.value as {definition:{amountMl:number;capacityMl:number}}).definition;
    if(definition.amountMl!==amount||definition.capacityMl!==capacity)throw new Error('Scoop-template quantities differ from the source');
    const undone=await execute([{action:'undo'}]);if(await nativeObjects.exists(id)||await nativeObjects.count()!==beforeCount)throw new Error('Scoop-template Undo did not remove one complete object');
    scoopTemplates.push({template,hash,before,created,contents,undone});
  }
  await writeFile(join(directory,'scooping-templates.json'),JSON.stringify({boundary:'Ordinary shared creation, accepted quantities and one Undo; physical scooping is tested separately.',templates:scoopTemplates},null,2));
  const poolHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/shallow-pool.json')).digest('hex');
  const poolCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:poolHash,name:'Shallow pool',x:.4,y:1,z:.6,scale:1}}}}]));
  const poolId=poolCreated.execution?.selected?.output?.objectId;if(typeof poolId!=='string')throw new Error('Pool creation returned no object identity');
  const rectangleBefore=structuredClone(lease.state());
  const rectangleSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Configure or fill a liquid container',offset:0}}]);
  const rectangleDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'object.container.edit',version:1}}]);
  const rectangleCurrent=await liquidRead(poolId);
  const poolValue=rectangleCurrent.catalog?.value as {revision:number;configured:boolean;definition:Record<string,unknown>&{rectangle:{width:number;depth:number};amountMl:number}};
  if(!poolValue.configured||Math.abs(poolValue.definition.rectangle.width-1.18)>.0001||poolValue.definition.amountMl!==224209.44)throw new Error('Pool did not expose its actual rectangular saved cavity');
  const rectangleCall={id:'object.container.edit',version:1,arguments:{operation:'configure',target:poolId,revision:poolValue.revision,definition:{...poolValue.definition,rectangle:{width:1.16,depth:.78}}}};
  const rectangleAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:rectangleCall}}]));
  const rectangleRead=await liquidRead(poolId);const changedPool=(rectangleRead.catalog?.value as typeof poolValue).definition;
  if(Math.abs(changedPool.rectangle.width-1.16)>.0001||changedPool.amountMl!==poolValue.definition.amountMl)throw new Error('Shared rectangular edit changed contents or did not publish width');
  const rectangleUndo=await execute([{action:'undo'}]),rectangleRestored=await liquidRead(poolId);
  if(Math.abs((rectangleRestored.catalog?.value as typeof poolValue).definition.rectangle.width-1.18)>.0001)throw new Error('Pool rectangle Undo did not restore the previous shape');
  await writeFile(join(directory,'rectangular-container-authoring.json'),JSON.stringify({boundary:'Actual full native app template, current fact, exact shared edit/receipt and Undo. Physical dipping/pouring are verified separately in PlayMode; not headset acceptance.',before:rectangleBefore,created:poolCreated,search:rectangleSearch,definition:rectangleDefinition,current:rectangleCurrent,call:rectangleCall,after:rectangleAfter,read:rectangleRead,undo:rectangleUndo,restored:rectangleRestored},null,2));
  await execute([{action:'undo'}]);
  const chalkSource=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/chalkboard.json','utf8'));
  const chalkHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/chalkboard.json')).digest('hex');
  const chalkCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:chalkHash,name:'',x:.4,y:1,z:.6,scale:1}}}}]));
  const chalkId=chalkCreated.execution?.selected?.output?.objectId;if(typeof chalkId!=='string')throw new Error('Chalkboard template did not create an object');
  const chalkTool=await execute([{action:'execution',execution:{operation:'start',call:{id:'drawing.tool.set',version:1,arguments:{mode:'surface',red:1,green:1,blue:1,radius:.003}}}}]);
  const chalkToolRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'drawing.tool',version:1}}]);
  if((chalkToolRead.catalog?.value as {mode?:string})?.mode!=='surface')throw new Error('Shared pencil mode did not apply');
  const surfaceSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Edit a drawing surface',offset:0}}]);
  const surfaceDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.surface.edit',version:1}}]);
  const surfaceCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:chalkId}}}]);
  const surfaceValue=surfaceCurrent.catalog?.value as {revision:number;surfaces:{id:string;part:string}[]};
  if(surfaceValue?.surfaces[0]?.part!==chalkSource.definition.surfaces[0].part)throw new Error('Template drawing patch missing');
  const surfaceArgs={operation:'add',target:chalkId,revision:surfaceValue.revision,surface:'Front',stroke:'',red:1,green:1,blue:1,radius:.003,points:[{x:-.2,y:0,z:0},{x:0,y:.15,z:0},{x:.2,y:0,z:0}]};
  const surfaceAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.surface.edit',version:1,arguments:surfaceArgs}}}]));
  const stroke=surfaceAfter.execution?.selected?.output?.stroke,inkRevision=surfaceAfter.execution?.selected?.output?.revision;
  if(typeof stroke!=='string'||typeof inkRevision!=='number')throw new Error('Surface ink receipt missing');
  const inkRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surface.stroke',version:1,arguments:{target:chalkId,surface:'Front',stroke,revision:inkRevision,offset:0}}}]);
  const inkValue=inkRead.catalog?.value as {total?:number;points?:unknown[]};if(inkValue?.total!==3||JSON.stringify(inkValue.points)!==JSON.stringify(surfaceArgs.points))throw new Error('Native ink differs from shared authoring');
  const erased=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.surface.edit',version:1,arguments:{operation:'removeStroke',target:chalkId,surface:'Front',stroke,revision:inkRevision}}}}]));
  const erasedRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:chalkId}}}]);
  if((erasedRead.catalog?.value as {surfaces:{strokes:number}[]})?.surfaces[0].strokes!==0)throw new Error('Surface erasing did not remove ink');
  await execute([{action:'undo'}]);
  const surfaceUndo=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:chalkId}}}]);
  if((surfaceUndo.catalog?.value as {surfaces:{strokes:number}[]})?.surfaces[0].strokes!==1)throw new Error('Ink Undo did not restore stroke');
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await execute([{action:'execution',execution:{operation:'start',call:{id:'drawing.tool.set',version:1,arguments:{mode:'off',red:1,green:1,blue:1,radius:.003}}}}]);
  await writeFile(join(directory,'surface-authoring.json'),JSON.stringify({boundary:'Real Unity native states and shared transport; browser acknowledgements replayed separately. No headset or provider proof.',before:chalkCreated,tool:chalkTool,toolRead:chalkToolRead,search:surfaceSearch,definition:surfaceDefinition,current:surfaceCurrent,after:surfaceAfter,read:inkRead,erased,erasedRead,undo:surfaceUndo},null,2));
  const curvedCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Curved drawing sample'}}}}]));
  const curvedId=curvedCreated.execution?.selected?.output?.objectId;if(typeof curvedId!=='string')throw new Error('Curved drawing sample missing');
  const curvedSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Edit a drawing surface',offset:0}}]);
  const curvedActionDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.surface.edit',version:1}}]);
  const curvedCases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/curved-surfaces-contract.json','utf8'));
  const curvedDefinition=curvedCases.find((c:{name:string})=>c.name==='sphere').definition;
  const curvedCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:curvedId}}}]);
  const curvedConfigure=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.surface.edit',version:1,arguments:{operation:'configure',target:curvedId,revision:(curvedCurrent.catalog?.value as {revision:number}).revision,surface:'Front',definition:curvedDefinition}}}}]));
  const curvedRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surface',version:1,arguments:{target:curvedId,surface:'Front'}}}]);
  if((curvedRead.catalog?.value as {geometry:{shape:string}})?.geometry?.shape!=='sphere')throw new Error('Curved shape readback differs');
  const curvedInk=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.surface.edit',version:1,arguments:{operation:'add',target:curvedId,revision:(curvedRead.catalog?.value as {revision:number}).revision,surface:'Front',stroke:'',red:.05,green:.2,blue:.8,radius:.003,points:[{x:-.09,y:0,z:0},{x:0,y:.03,z:0},{x:.09,y:0,z:0}]}}}}]));
  const curvedStroke=curvedInk.execution?.selected?.output?.stroke,curvedRevision=curvedInk.execution?.selected?.output?.revision;
  if(typeof curvedStroke!=='string'||typeof curvedRevision!=='number')throw new Error('Curved ink receipt missing');
  const curvedPoints=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surface.stroke',version:1,arguments:{target:curvedId,surface:'Front',stroke:curvedStroke,revision:curvedRevision,offset:0}}}]);
  if((curvedPoints.catalog?.value as {total:number})?.total!==3)throw new Error('Curved ink source not preserved');
  const eraseSecond=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.surface.edit',version:1,arguments:{operation:'add',target:curvedId,revision:curvedRevision,surface:'Front',stroke:'',red:.8,green:.2,blue:.1,radius:.003,points:[{x:-.08,y:-.03,z:0},{x:.08,y:-.03,z:0}]}}}}]));
  const eraseId=eraseSecond.execution?.selected?.output?.stroke;if(typeof eraseId!=='string')throw new Error('Second erase sample missing');
  const eraseCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:curvedId}}}]);
  const eraseAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.surface.edit',version:1,arguments:{operation:'removeStrokes',target:curvedId,revision:(eraseCurrent.catalog?.value as {revision:number}).revision,surface:'Front',strokes:[curvedStroke,eraseId]}}}}]));
  const eraseRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:curvedId}}}]);
  if((eraseRead.catalog?.value as {surfaces:{strokes:number}[]})?.surfaces[0].strokes!==0)throw new Error('Atomic erasure left ink');
  await execute([{action:'undo'}]);const eraseUndone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:curvedId}}}]);
  if((eraseUndone.catalog?.value as {surfaces:{strokes:number}[]})?.surfaces[0].strokes!==2)throw new Error('Atomic erasure Undo did not restore both strokes');
  await writeFile(join(directory,'eraser-surface-authoring.json'),JSON.stringify({boundary:'Actual native atomic stroke-ID erasure and one Undo through the shared client. Physical held erasing has separate interaction tests.',before:eraseSecond,search:curvedSearch,definition:curvedActionDefinition,current:eraseCurrent,after:eraseAfter,read:eraseRead,undone:eraseUndone},null,2));
  await execute([{action:'undo'}]);
  await execute([{action:'undo'}]);
  const curvedUndone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.surfaces',version:1,arguments:{target:curvedId}}}]);
  if((curvedUndone.catalog?.value as {surfaces:{strokes:number}[]})?.surfaces[0].strokes!==0)throw new Error('Curved ink Undo failed');
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await writeFile(join(directory,'curved-surface-authoring.json'),JSON.stringify({boundary:'Actual Unity full-app surface configuration, editable arc ink, typed readback and Undo through the shared client. Browser replays exact receipts; physical interaction and rendering are separate native tests.',before:curvedCreated,search:curvedSearch,definition:curvedActionDefinition,current:curvedCurrent,after:curvedConfigure,read:curvedRead,ink:curvedInk,points:curvedPoints,undone:curvedUndone},null,2));
  const tipHash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/chalk.json')).digest('hex');
  const tipCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:tipHash,name:'',x:.4,y:1,z:.6,scale:1}}}}]));
  const tipId=tipCreated.execution?.selected?.output?.objectId;if(typeof tipId!=='string')throw new Error('Chalk template did not create a tool');
  const tipSearch=await execute([{action:'catalog',catalog:{operation:'search',query:"Configure an object's drawing tip",offset:0}}]);
  const tipDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.drawingTip.edit',version:1}}]);
  const tipCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.drawingTip',version:1,arguments:{target:tipId}}}]);
  const tipValue=tipCurrent.catalog?.value as {configured:boolean;revision:number;definition:{part:string;enabled:boolean;color:{r:number;g:number;b:number;a:number}}};
  if(!tipValue?.configured||tipValue.definition.part!=='Chalk')throw new Error('Chalk component missing');
  const tipArgs={operation:'configure',target:tipId,revision:tipValue.revision,definition:{...tipValue.definition,color:{r:.1,g:.5,b:.9,a:1}}};
  const tipEdited=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.drawingTip.edit',version:1,arguments:tipArgs}}}]));
  const tipRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.drawingTip',version:1,arguments:{target:tipId}}}]);
  const tipAfter=tipRead.catalog?.value as typeof tipValue;if(Math.abs(tipAfter?.definition.color.b-.9)>.00001)throw new Error('Shared drawing-tip ink edit failed');
  await execute([{action:'undo'}]);
  const tipUndo=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.drawingTip',version:1,arguments:{target:tipId}}}]);
  if((tipUndo.catalog?.value as typeof tipValue)?.definition.color.b!==1)throw new Error('Drawing-tip Undo failed');
  await execute([{action:'undo'}]);
  await writeFile(join(directory,'drawing-tip-authoring.json'),JSON.stringify({boundary:'Real Unity native configuration/readback/Undo; physical grip tested separately. No headset or provider proof.',before:tipCreated,search:tipSearch,definition:tipDefinition,current:tipCurrent,after:tipEdited,read:tipRead,undo:tipUndo},null,2));
  const drawingKit=[];
  for(const name of ['pencil','brush','eraser']){
   const hash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/'+name+'.json')).digest('hex');
   const created=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:hash,name:'',x:.4,y:1,z:.6,scale:1}}}}]));
   const id=created.execution?.selected?.output?.objectId;if(typeof id!=='string')throw new Error('Drawing-kit object missing');
   const current=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.drawingTip',version:1,arguments:{target:id}}}]);
   const tip=current.catalog?.value as {revision:number;definition:{mode:string}};if(tip?.definition.mode!==(name==='eraser'?'erase':'draw'))throw new Error('Drawing-kit mode differs');
   const search=await execute([{action:'catalog',catalog:{operation:'search',query:"Configure an object's drawing tip",offset:0}}]);
   const definition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.drawingTip.edit',version:1}}]);
   const edited=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.drawingTip.edit',version:1,arguments:{operation:'configure',target:id,revision:tip.revision,definition:{...tip.definition,mode:name==='eraser'?'draw':'erase'}}}}}]));
   const read=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.drawingTip',version:1,arguments:{target:id}}}]);
   if((read.catalog?.value as typeof tip)?.definition.mode==tip.definition.mode)throw new Error('Drawing-tool mode edit failed');
   await execute([{action:'undo'}]);const undone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.drawingTip',version:1,arguments:{target:id}}}]);
   if((undone.catalog?.value as typeof tip)?.definition.mode!==tip.definition.mode)throw new Error('Drawing-tool mode Undo failed');
   await execute([{action:'undo'}]);drawingKit.push({name,hash,before:created,current,search,definition,after:edited,read,undone});
  }
  await writeFile(join(directory,'drawing-kit.json'),JSON.stringify({boundary:'Actual native template creation, shared mode configuration/readback and Undo. Held contact and failed saves have separate native interaction tests.',examples:drawingKit},null,2));
  // Live joint readback needs observed native pieces; saved authoring also works offscreen.
  const layoutIds:string[]=[];let layoutBefore=templateUndo;
  for(let i=0;i<2;i++){
   layoutBefore=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:brickHash,name:brickSource.name+' '+(i+1),x:.4+i*.25,y:1.4,z:2,scale:1}}}}]));
   const id=layoutBefore.execution?.selected?.output?.objectId;if(typeof id!=='string')throw new Error('Layout member missing');layoutIds.push(id);
  }
  async function connectionBrowserInputs(targets:string[]){
   const directory:RoomAgentState[]=[],facts:RoomAgentState[]=[];let cursor='',world='',total=-1;const ids:string[]=[];
   for(let index=0;index<24;index++){
    const query=objectSearchRequest('','all',cursor),state=await execute([{action:'catalog',catalog:query}]),page=readObjectSearchPage(state.catalog,query);
    if(!page)throw new Error('Connection selection directory is unavailable');
    if(index===0){world=page.worldId;total=page.total;}
    if(page.worldId!==world||page.total!==total||page.offset!==ids.length||page.entries.some(entry=>ids.includes(entry.id)))throw new Error('Connection directory changed during paging');
    directory.push(state);ids.push(...page.entries.map(entry=>entry.id));cursor=page.next;if(!cursor)break;
   }
   if(cursor||ids.length!==total||!targets.every(id=>ids.includes(id)))throw new Error('Connection directory is missing an exact member');
   for(const target of targets)facts.push(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target}}}]));
   return {directory,facts};
  }
  const animationBrowser=await connectionBrowserInputs(['maestro']);
  const animationSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Edit pose or recorded motion',offset:0}}]);
  const animationDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'animation.author',version:1}}]);
  const animationCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.authored',version:1,arguments:{target:'maestro'}}}]);
  const animationRevision=(animationCurrent.catalog?.value as {revision:number}).revision;
  const animationFrame=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.frame',version:1,arguments:{target:'maestro',revision:animationRevision,index:-1}}}]);
  const frameSource=animationFrame.catalog?.value as {position:unknown;rotation:unknown;scale:number};
  const restJoints:{joint:string;rotation:{x:number;y:number;z:number;w:number}}[]=[];const jointReads:RoomAgentState[]=[];
  for(const joint of ['Hips','Spine','Chest','Neck','Head','LeftUpperArm','LeftLowerArm','LeftHand','RightUpperArm','RightLowerArm','RightHand','LeftUpperLeg','LeftLowerLeg','LeftFoot','RightUpperLeg','RightLowerLeg','RightFoot']){
   const read=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.joint',version:1,arguments:{target:'maestro',revision:animationRevision,index:-2,joint}}}]);jointReads.push(read);
   restJoints.push({joint,rotation:read.catalog?.value as {x:number;y:number;z:number;w:number}});
  }
  const nodJoints=structuredClone(restJoints),head=nodJoints.find(j=>j.joint==='Head')!.rotation;
  const sine=Math.sin(Math.PI/12),cosine=Math.cos(Math.PI/12);
  Object.assign(head,{x:head.x*cosine+head.w*sine,y:head.y*cosine+head.z*sine,z:head.z*cosine-head.y*sine,w:head.w*cosine-head.x*sine});
  const animationArgs={operation:'frames',target:'maestro',revision:animationRevision,replace:true,frames:[0,.25,.5].map((time,index)=>({time,position:frameSource.position,rotation:frameSource.rotation,scale:frameSource.scale,joints:index===1?nodJoints:restJoints}))};
  const animationStarted=await execute([{action:'execution',execution:{operation:'start',call:{id:'animation.author',version:1,arguments:animationArgs}}}]);
  const animationAfter=await completeProbeAction(execute,animationStarted);
  const animationRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.authored',version:1,arguments:{target:'maestro'}}}]);
  const animationSaved=animationRead.catalog?.value as {revision:number;frames:number;duration:number};
  if(animationSaved.frames!==3||animationSaved.duration!==.5||animationSaved.revision<=animationRevision)throw new Error('Authored animation metadata did not advance');
  const animationJoint=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.joint',version:1,arguments:{target:'maestro',revision:animationSaved.revision,index:1,joint:'Head'}}}]);
  const savedHead=animationJoint.catalog?.value as typeof head;
  if(Object.keys(head).some(k=>Math.abs(savedHead[k as keyof typeof head]-head[k as keyof typeof head])>1e-5))throw new Error('Authored canonical joint readback differs');
  const animationUndo=await execute([{action:'undo'}]);
  const animationUndone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.authored',version:1,arguments:{target:'maestro'}}}]);
  if((animationUndone.catalog?.value as {frames:number}).frames!==(animationCurrent.catalog?.value as {frames:number}).frames)throw new Error('Animation Undo did not restore source');
  await writeFile(join(directory,'animation-authoring.json'),JSON.stringify({boundary:'Actual native 17-joint authoring, completion, readback and Undo. Physical playback and imported retargeting have separate native tests; no provider or headset proof.',before:animationCurrent,search:animationSearch,definition:animationDefinition,current:animationCurrent,facts:[...animationBrowser.facts,animationCurrent],directory:animationBrowser.directory,rest:jointReads,started:animationStarted,after:animationAfter,read:animationRead,joint:animationJoint,undo:animationUndo,undone:animationUndone},null,2));
  // Live recorder lifecycle through the same public capability used by the book.
  // This offline leg records a stationary object; XRI movement is covered by native tests.
  const recordingFact=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.recording',version:1}}]);
  const recordingBefore=await recordingFact();
  const recordSession=(recordingBefore.catalog?.value as {sessionId:string;phase:string}).sessionId;
  if((recordingBefore.catalog?.value as {phase:string}).phase!=='idle')throw new Error('Recorder is not initially idle');
  const recordStart=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'animation.record',version:1,arguments:{operation:'start',sessionId:recordSession,target:'maestro',revision:(animationUndone.catalog?.value as {revision:number}).revision}}}}]));
  await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'time.wait',version:1,arguments:{seconds:.25}}}}]));
  const recordingActive=await recordingFact();
  if((recordingActive.catalog?.value as {phase:string}).phase!=='recording')throw new Error('Start receipt did not leave a live recorder');
  const recordingStarted=await execute([{action:'execution',execution:{operation:'start',call:{id:'animation.record',version:1,arguments:{operation:'finish',sessionId:recordSession,target:'maestro'}}}}]);
  const recordingAfter=await completeProbeAction(execute,recordingStarted);
  const recorded=recordingAfter.execution?.selected?.output as {phase:string;sessionId:string;frames:number;duration:number;revision:number};
  if(recorded?.phase!=='saved'||recorded.sessionId!==recordSession||recorded.frames<2||recorded.duration<.2)throw new Error('Recorder completion did not confirm the frozen take');
  const recordingIdle=await recordingFact(),idle=recordingIdle.catalog?.value as {phase:string;sessionId:string};
  if(idle.phase!=='idle'||idle.sessionId===recordSession)throw new Error('Saved recorder did not issue the next identity');
  const recordingRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.authored',version:1,arguments:{target:'maestro'}}}]);
  const recordedSaved=recordingRead.catalog?.value as {revision:number;frames:number;duration:number};
  if(recordedSaved.revision!==recorded.revision||recordedSaved.frames!==recorded.frames||Math.abs(recordedSaved.duration-recorded.duration)>1e-5)throw new Error('Recorder receipt differs from native authored data');
  await execute([{action:'undo'}]);
  const recordingUndone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.authored',version:1,arguments:{target:'maestro'}}}]);
  if((recordingUndone.catalog?.value as {frames:number}).frames!==(animationUndone.catalog?.value as {frames:number}).frames)throw new Error('Recorder Undo did not restore the previous animation');
  await writeFile(join(directory,'recording-completion.json'),JSON.stringify({boundary:'Real Unity stationary recording, queued finish, receipt, native readback and Undo. XRI movement covered separately; no provider or headset proof.',before:recordingBefore,start:recordStart,active:recordingActive,started:recordingStarted,after:recordingAfter,idle:recordingIdle,read:recordingRead,undone:recordingUndone},null,2));
  // Versioned live pose authoring shares physical controls and completes only after saving.
  type LivePose={sessionId:string;version:number;revision:number;phase:string;editing:boolean};
  const poseFact=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.posing',version:1}}]);
  const poseCall=(arguments_:Record<string,unknown>)=>execute([{action:'execution',execution:{operation:'start',call:{id:'animation.pose',version:1,arguments:{target:'maestro',...arguments_}}}}]);
  const poseBefore=await poseFact(),poseIdentity=(poseBefore.catalog?.value as LivePose).sessionId;
  if((poseBefore.catalog?.value as LivePose).phase!=='idle')throw new Error('Pose session is not initially idle');
  const poseStart=await completeProbeAction(execute,await poseCall({operation:'start',sessionId:poseIdentity,revision:(recordingUndone.catalog?.value as {revision:number}).revision}));
  const poseActive=await poseFact(),poseA=(poseActive.catalog?.value as LivePose);
  const poseRotate=await completeProbeAction(execute,await poseCall({operation:'rotate',sessionId:poseIdentity,version:poseA.version,joints:[{joint:'Head',rotation:head}]}));
  const poseEdited=await poseFact(),poseVersion=(poseEdited.catalog?.value as LivePose).version;
  const poseLiveJoint=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.pose.joint',version:1,arguments:{sessionId:poseIdentity,version:poseVersion,joint:'Head'}}}]);
  const poseSaving=await poseCall({operation:'save',sessionId:poseIdentity,version:poseVersion}),poseSaved=await completeProbeAction(execute,poseSaving);
  const poseSavedOutput=poseSaved.execution?.selected?.output as {version:number;revision:number;phase:string};
  const poseContinuing=await poseFact(),continuing=poseContinuing.catalog?.value as LivePose;
  if(poseSavedOutput?.phase!=='posing'||poseSavedOutput.version!==poseVersion||!continuing.editing||continuing.phase!=='posing')throw new Error('Saved pose did not preserve its version and live editor');
  const restHead=restJoints.find(j=>j.joint==='Head')!.rotation;
  const poseLater=await completeProbeAction(execute,await poseCall({operation:'rotate',sessionId:poseIdentity,version:continuing.version,joints:[{joint:'Head',rotation:restHead}]}));
  const poseLatest=await poseFact(),latestPose=poseLatest.catalog?.value as LivePose;
  const poseFinishing=await poseCall({operation:'finish',sessionId:poseIdentity,version:latestPose.version}),poseFinished=await completeProbeAction(execute,poseFinishing);
  const poseFinalOutput=poseFinished.execution?.selected?.output as {version:number;revision:number;phase:string};
  const poseIdle=await poseFact(),poseDone=poseIdle.catalog?.value as LivePose;
  if(poseFinalOutput?.phase!=='saved'||poseFinalOutput.version!==latestPose.version||poseFinalOutput.revision<=poseSavedOutput.revision||poseDone.phase!=='idle'||poseDone.editing||poseDone.sessionId===poseIdentity)throw new Error('Pose finish did not save the later version and end the session');
  const poseRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.joint',version:1,arguments:{target:'maestro',revision:poseFinalOutput.revision,index:-1,joint:'Head'}}}]);
  const closeJoint=(a:unknown,b:unknown)=>['x','y','z','w'].every(k=>Math.abs((a as Record<string,number>)[k]-(b as Record<string,number>)[k])<1e-5);
  if(!closeJoint(poseRead.catalog?.value,restHead))throw new Error('Final saved pose does not match the later live edit');
  await execute([{action:'undo'}]);
  const poseUndoSummary=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.authored',version:1,arguments:{target:'maestro'}}}]);
  const poseUndoJoint=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.joint',version:1,arguments:{target:'maestro',revision:(poseUndoSummary.catalog?.value as {revision:number}).revision,index:-1,joint:'Head'}}}]);
  if(!closeJoint(poseUndoJoint.catalog?.value,poseLiveJoint.catalog?.value))throw new Error('Pose Undo did not restore the earlier saved version');
  await execute([{action:'undo'}]);
  const poseUndone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.authored',version:1,arguments:{target:'maestro'}}}]);
  if((poseUndone.catalog?.value as {poseSaved:boolean}).poseSaved!==(recordingUndone.catalog?.value as {poseSaved:boolean}).poseSaved)throw new Error('Second pose Undo did not restore the original saved pose state');
  await writeFile(join(directory,'pose-completion.json'),JSON.stringify({boundary:'Real Unity versioned live posing, queued save and finish, saved joint readback and two Undo operations. No provider or headset execution.',before:poseBefore,start:poseStart,active:poseActive,rotate:poseRotate,edited:poseEdited,liveJoint:poseLiveJoint,saving:poseSaving,saved:poseSaved,continuing:poseContinuing,later:poseLater,latest:poseLatest,finishing:poseFinishing,finished:poseFinished,idle:poseIdle,read:poseRead,undoSummary:poseUndoSummary,undoJoint:poseUndoJoint,undone:poseUndone},null,2));
  // Pending physical edits are discoverable through the same sparse native catalog.
  const framePending=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'animation.edit.pending',version:1}}]);
  const frameState=framePending.catalog?.value as {sessionId:string;phase:string;target:string;frames:number};
  if(frameState?.phase!=='idle'||frameState.target!==''||frameState.frames!==0||!/^[a-f0-9]{32}$/.test(frameState.sessionId))throw new Error('Pending keyframe fact is not a valid idle session');
  if(!framePending.capabilities?.includes('animationEditResolution.v1'))throw new Error('Native keyframe resolution feature is missing');
  const frameDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'animation.edit.resolve',version:1}}]);
  if(frameDefinition.catalog?.definition?.duration!=='completion'||frameDefinition.catalog?.definition?.ownership!=='authoringSession')throw new Error('Native keyframe recovery contract differs from shared catalog');
  await writeFile(join(directory,'keyframe-discovery.json'),JSON.stringify({pending:framePending,definition:frameDefinition,boundary:'Real Unity sparse catalog discovery. Pending physical save, retry/discard and publication are exercised separately by PlayMode integration tests; no headset or provider execution.'},null,2));
  const hingeBrowser=await connectionBrowserInputs(layoutIds);
  const hingeSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Connect physical pieces',offset:0}}]);
  const hingeDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.connection.edit',version:1}}]);
  const hingeCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  const hingeValue=hingeCurrent.catalog?.value as {configured:boolean;revision:number;connected:string;definition:{enabled:boolean;kind:string;breakForce:number;breakTorque:number}};
  if(hingeValue?.configured)throw new Error('New piece unexpectedly has a hinge');
  const hingeFrames:Record<string,unknown>={};for(const side of ['owner','connected']){
   const read=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.frame',version:1,arguments:{target:layoutIds[0],side}}}]);
   const value=read.catalog?.value as {revision:number;frame:unknown};if(value.revision!==hingeValue.revision)throw new Error('Hinge frame revision changed');hingeFrames[side+'Frame']=value.frame;
  }
  const hingeDefaults=capabilityDefinition('object.connection.edit')!.example!.definition as {limits:Record<string,unknown>;drive:Record<string,unknown>};
  const hingeArgs={operation:'configure',target:layoutIds[0],connected:layoutIds[1],revision:hingeValue.revision,definition:{...hingeValue.definition,...hingeFrames,enabled:true,drive:{...hingeDefaults.drive,mode:'spring',target:20},limits:{...hingeDefaults.limits,enabled:true}}};
  const hingeStarted=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.connection.edit',version:1,arguments:hingeArgs}}}]);
  const hingeAfter=await completeProbeAction(execute,hingeStarted);
  const hingeRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  const hingeTuning=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.hinge',version:1,arguments:{target:layoutIds[0]}}}]);
  const hingeSaved=hingeRead.catalog?.value as typeof hingeValue;if(!hingeSaved?.configured||hingeSaved.connected!==layoutIds[1]||(hingeTuning.catalog?.value as {drive:{target:number}}).drive.target!==20)throw new Error('Hinge source did not persist');
  const hingeAligned=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.connection.edit',version:1,arguments:{operation:'align',target:layoutIds[0],connected:layoutIds[1],revision:hingeSaved.revision,angle:30}}}}]));
  const hingeState=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.state',version:1,arguments:{target:layoutIds[0]}}}]);
  if(Math.abs((hingeState.catalog?.value as {angle:number}).angle-30)>.05)throw new Error('Hinge alignment readback differs');
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  const hingeUndo=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  if((hingeUndo.catalog?.value as typeof hingeValue).configured)throw new Error('Hinge Undo did not remove component');
  await writeFile(join(directory,'hinge-authoring.json'),JSON.stringify({boundary:'Real Unity configuration, alignment, readback and Undo. PhysX tested separately; no headset or provider proof.',before:layoutBefore,search:hingeSearch,definition:hingeDefinition,current:hingeCurrent,facts:[...hingeBrowser.facts,hingeCurrent],directory:hingeBrowser.directory,started:hingeStarted,after:hingeAfter,read:hingeRead,tuning:hingeTuning,aligned:hingeAligned,state:hingeState,undo:hingeUndo},null,2));
  const connectionBefore=hingeUndo;
  const connectionPlacementsBefore=[];for(const target of layoutIds)connectionPlacementsBefore.push(await readPlacement(target));
  const connectionBrowser=await connectionBrowserInputs(layoutIds);
  const connectionSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Connect physical pieces',offset:0}}]);
  const connectionDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.connection.edit',version:1}}]);
  const connectionCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  const connectionArgs={operation:'attach',target:layoutIds[0],connected:layoutIds[1],revision:(connectionCurrent.catalog?.value as {revision:number}).revision,breakForce:45,breakTorque:3};
  const connectionStarted=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.connection.edit',version:1,arguments:connectionArgs}}}]);
  const connectionAfter=await completeProbeAction(execute,connectionStarted);
  const connectionRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  const connectionSaved=connectionRead.catalog?.value as typeof hingeValue;
  if(!connectionSaved?.configured||connectionSaved.definition.kind!=='fixed'||connectionSaved.definition.breakForce!==45||connectionSaved.definition.breakTorque!==3||connectionSaved.connected!==layoutIds[1])throw new Error('Fixed connection did not persist its exact members and break limits');
  const connectionPlacementsAfter=[];for(const target of layoutIds)connectionPlacementsAfter.push(await readPlacement(target));
  for(let i=0;i<layoutIds.length;i++)assertSamePlacement(connectionPlacementsBefore[i],connectionPlacementsAfter[i],'Fixed join moved its members');
  const connectionState=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.state',version:1,arguments:{target:layoutIds[0]}}}]);
  if((connectionState.catalog?.value as {active:boolean;broken:boolean}).active)throw new Error('Joining unexpectedly started physics');
  const connectionUndo=await execute([{action:'undo'}]);
  const connectionUndone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  if((connectionUndone.catalog?.value as typeof hingeValue).configured)throw new Error('Single Undo did not remove fixed connection');
  await writeFile(join(directory,'connection-authoring.json'),JSON.stringify({boundary:'Real Unity native attach/readback/Undo. PhysX break and typed event tested separately; no headset or provider proof.',before:connectionBefore,search:connectionSearch,definition:connectionDefinition,current:connectionCurrent,arguments:connectionArgs,facts:[...connectionBrowser.facts,connectionCurrent],directory:connectionBrowser.directory,started:connectionStarted,after:connectionAfter,placementsBefore:connectionPlacementsBefore,placementsAfter:connectionPlacementsAfter,read:connectionRead,state:connectionState,undo:connectionUndo,undone:connectionUndone},null,2));
  const buttonModule=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Programs/Modules/SpringButton.json','utf8'));
  const buttonArgs=buttonModule.program.functions.find((f:{name:string})=>f.name==='create').body[0].arguments;
  buttonArgs.position={x:.4,y:1.4,z:2}; // Observe the live slider without pinning unrelated creations.
  const buttonCreated=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:buttonArgs}}}]);
  const buttonIds=buttonCreated.execution?.selected?.output?.objectIds;if(!Array.isArray(buttonIds)||buttonIds.length!==2||!buttonIds.every(id=>typeof id==='string'))throw new Error('Spring button did not create two members');
  const sliderTarget=buttonIds[1] as string,sliderMount=buttonIds[0] as string;
  const sliderBefore=structuredClone(lease.state());
  const sliderBrowser=await connectionBrowserInputs([sliderTarget,sliderMount]);
  const sliderSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Connect physical pieces',offset:0}}]);
  const sliderDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.connection.edit',version:1}}]);
  const sliderCurrent=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:sliderTarget}}}]);
  const sliderConfig=structuredClone(buttonArgs.blueprint.connections[0].definition);sliderConfig.slide.target=.01;
  const sliderArguments={operation:'configure',target:sliderTarget,connected:sliderMount,revision:(sliderCurrent.catalog?.value as {revision:number}).revision,definition:sliderConfig};
  const sliderStarted=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.connection.edit',version:1,arguments:sliderArguments}}}]);
  const sliderAfter=await completeProbeAction(execute,sliderStarted);
  const sliderTuning=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.slider',version:1,arguments:{target:sliderTarget}}}]);
  const sliderSettings=sliderTuning.catalog?.value as {revision:number;slide:{target:number}};if(Math.abs(sliderSettings.slide.target-.01)>.000001)throw new Error('Slider spring target did not persist');
  const sliderAligned=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.connection.edit',version:1,arguments:{operation:'slide',target:sliderTarget,connected:sliderMount,revision:sliderSettings.revision,distance:.025}}}}]));
  const sliderRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.travel',version:1,arguments:{target:sliderTarget}}}]);
  if(typeof sliderRead.catalog?.value!=='number'||Math.abs(sliderRead.catalog.value-.025)>.0001)throw new Error('Slider distance readback differs');
  await execute([{action:'undo'}]);const sliderUndo=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.travel',version:1,arguments:{target:sliderTarget}}}]);
  if(typeof sliderUndo.catalog?.value!=='number'||Math.abs(sliderUndo.catalog.value)>.0001)throw new Error('Slider Undo did not restore its live travel');
  await execute([{action:'undo'}]);const buttonRemoved=await execute([{action:'undo'}]);if(await nativeObjects.any(buttonIds))throw new Error('Button construction was not one Undo');
  await writeFile(join(directory,'slider-authoring.json'),JSON.stringify({boundary:'Real Unity native slider configure/linear alignment/readback/Undo. Physical pressing and program response tested in PlayMode; no headset or provider proof.',before:sliderBefore,search:sliderSearch,definition:sliderDefinition,current:sliderCurrent,facts:[...sliderBrowser.facts,sliderCurrent],directory:sliderBrowser.directory,started:sliderStarted,after:sliderAfter,tuning:sliderTuning,aligned:sliderAligned,read:sliderRead,undo:sliderUndo,removed:buttonRemoved},null,2));
  const snapFrames={position:{x:0,y:.05,z:0},rotation:{x:0,y:0,z:0,w:1}};
  const snapEdited=[];
  for(const [i,target] of layoutIds.entries()){
   const point=i===0?'Bottom':'Top';
   const current=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.snapPoint',version:1,arguments:{target,point}}}]);
   const definition={name:point,family:'ProbeBrick',frame:{...snapFrames,position:{x:0,y:i===0?-.05:.05,z:0}}};
   snapEdited.push(await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.snapPoint.edit',version:1,arguments:{operation:'configure',target,revision:(current.catalog?.value as {revision:number}).revision,point,definition}}}}])));
  }
  const snapList=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.snapPoints',version:1,arguments:{target:layoutIds[0],offset:0}}}]);
  if(!(snapList.catalog?.value as {ids:string[]}).ids.includes('Bottom'))throw new Error('Saved snap-point ID missing');
  const snapDirectory:RoomAgentState[]=[];let snapCursor='',snapWorld='',snapTotal=-1;const snapIds:string[]=[];
  for(let pageIndex=0;pageIndex<24;pageIndex++){
   const query=objectSearchRequest('','all',snapCursor),state=await execute([{action:'catalog',catalog:query}]),page=readObjectSearchPage(state.catalog,query);
   if(!page)throw new Error('Snap selection directory is unavailable');
   if(pageIndex===0){snapWorld=page.worldId;snapTotal=page.total;}
   if(page.worldId!==snapWorld||page.total!==snapTotal||page.offset!==snapIds.length||page.entries.some(entry=>snapIds.includes(entry.id)))throw new Error('Snap selection directory changed during paging');
   snapDirectory.push(state);snapIds.push(...page.entries.map(entry=>entry.id));snapCursor=page.next;if(!snapCursor)break;
  }
  if(snapCursor||snapIds.length!==snapTotal||!layoutIds.every(id=>snapIds.includes(id)))throw new Error('Snap selection directory does not contain both exact members');
  const snapBefore=structuredClone(lease.state());
  const snapSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Snap a construction to a point',offset:0}}]);
  const snapDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.layout.snap',version:1}}]);
  const snapFacts=[];
  // Capture the same exact object-definition reads used by requestPrepared in
  // the book editor. Sparse periodic inventories do not supply revision guards.
  for(const target of layoutIds)snapFacts.push(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target}}}]));
  const snapPlacements=[];
  for(const target of layoutIds){const fact=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target}}}]);snapFacts.push(fact);snapPlacements.push(fact);}
  const snapArgs={mode:'join',members:[{target:layoutIds[0],revision:(snapPlacements[0].catalog?.value as {revision:number}).revision}],point:'Bottom',destination:{target:layoutIds[1],revision:(snapPlacements[1].catalog?.value as {revision:number}).revision,point:'Top'},turn:90,breakForce:45,breakTorque:3};
  const snapStarted=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.layout.snap',version:1,arguments:snapArgs}}}]);
  const snapAfter=await completeProbeAction(execute,snapStarted);
  if(snapAfter.execution?.selected?.output?.joined!==true)throw new Error('Snapping did not complete the requested fixed join');
  const snapRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  const snapConnection=snapRead.catalog?.value as {configured:boolean;connected:string;definition:{kind:string;breakForce:number}};
  if(!snapConnection.configured||snapConnection.connected!==layoutIds[1]||snapConnection.definition.kind!=='fixed'||snapConnection.definition.breakForce!==45)throw new Error('Snap join differs from its accepted definition');
  const snapMoved=await nativeObjects.observation(layoutIds[0]),snapTarget=await nativeObjects.observation(layoutIds[1]);
  if(Math.abs(snapMoved.position.x-snapTarget.position.x)>.0001||Math.abs(snapMoved.position.z-snapTarget.position.z)>.0001||Math.abs(snapMoved.position.y-snapTarget.position.y-.05*(snapMoved.scale+snapTarget.scale))>.0001)throw new Error('Snapped frames do not coincide');
  const snapUndo=await execute([{action:'undo'}]);
  const snapRestoredPlacements=[];for(const target of layoutIds)snapRestoredPlacements.push(await readPlacement(target));
  for(let i=0;i<layoutIds.length;i++)assertSamePlacement(snapPlacements[i],snapRestoredPlacements[i],'Snap Undo did not restore the complete live arrangement');
  const snapUndone=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:layoutIds[0]}}}]);
  if((snapUndone.catalog?.value as {configured:boolean}).configured)throw new Error('Snap Undo left its new join');
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await writeFile(join(directory,'snap-authoring.json'),JSON.stringify({boundary:'Real Unity root-local snap-point edits, exact construction snap/join, readback and atomic Undo. Headset snapping comfort and automatic physical previews are not covered.',before:snapBefore,search:snapSearch,definition:snapDefinition,facts:snapFacts,directory:snapDirectory,edited:snapEdited,list:snapList,started:snapStarted,after:snapAfter,read:snapRead,undo:snapUndo,restoredPlacements:snapRestoredPlacements,undone:snapUndone},null,2));
  const placements=layoutIds.map((target,i)=>({target,position:{x:.3+i*.25,y:1,z:.8},rotation:{x:0,y:0,z:0,w:1},scale:1}));
  const layoutSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Arrange or reset objects',offset:0}}]);
  const layoutDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.layout.apply',version:1}}]);
  const layoutAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.layout.apply',version:1,arguments:{placements}}}}]));
  if(layoutAfter.execution?.selected?.output?.count!==2)throw new Error('Layout did not complete both members');
  const layoutFacts=[];for(const p of placements){
   const fact=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target:p.target}}}]);
   const value=fact.catalog?.value as {position?:{x:number;y:number;z:number};scale?:number};
   if(!value?.position||Math.abs(value.position.x-p.position.x)>.00001||Math.abs(value.position.y-p.position.y)>.00001||Math.abs(value.position.z-p.position.z)>.00001||value.scale!==1)throw new Error('Layout live readback differs');layoutFacts.push(fact);
  }
  const layoutUndo=await execute([{action:'undo'}]);for(let i=0;i<2;i++)if(Math.abs((await nativeObjects.observation(layoutIds[i])).position.x-(.4+i*.25))>.00001)throw new Error('Single Undo did not restore both layout members');
  await writeFile(join(directory,'layout-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:layoutBefore,search:layoutSearch,definition:layoutDefinition,after:layoutAfter,facts:layoutFacts,undo:layoutUndo},null,2));
  const batchProgram=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-batch-create.json','utf8'));
  const batchArgs=batchProgram.functions[0].body[0].arguments,batchBefore=structuredClone(lease.state()),batchBeforeCount=await nativeObjects.count();
  const batchSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Create a structure',offset:0}}]);
  const batchDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.batch.create',version:1}}]);
  const batchAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:batchArgs}}}]);
  const batchIds=batchAfter.execution?.selected?.output?.objectIds,batchSlots=batchAfter.execution?.selected?.output?.slots;
  if(!Array.isArray(batchIds)||batchIds.length!==6||!batchIds.every(id=>typeof id==='string')||new Set(batchIds).size!==6||JSON.stringify(batchSlots)!==JSON.stringify(batchArgs.blueprint.pieces.map((p:{slot:string})=>p.slot)))throw new Error('Batch result lost piece identity/order');
  if(await nativeObjects.count()!==batchBeforeCount+6||!await nativeObjects.all(batchIds as string[]))throw new Error('Native structure pieces were not all created');
  const batchUndo=await execute([{action:'undo'}]);if(await nativeObjects.any(batchIds as string[])||await nativeObjects.count()!==batchBeforeCount)throw new Error('One Undo did not remove the whole batch');
  await writeFile(join(directory,'batch-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:batchBefore,search:batchSearch,definition:batchDefinition,after:batchAfter,undo:batchUndo},null,2));
  const structureBefore=structuredClone(lease.state()),structureMembers=[...layoutIds];
  if(structureMembers.length!==2)throw new Error('Two observed pieces are needed for the structure acceptance');
  const structureArgs={id:'',revision:0,source:{kind:'capture',name:'Native probe castle',positionTolerance:.05,rotationTolerance:15,scaleTolerance:.05,members:structureMembers.map((target,i)=>({slot:'piece_'+i,target}))}};
  const structureSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'structure.save',offset:0}}]);
  const structureDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'structure.save',version:1}}]);
  const structureAfter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'structure.save',version:1,arguments:structureArgs}}}]));
  const structureId=structureAfter.execution?.selected?.output?.structureId,structureRevision=structureAfter.execution?.selected?.output?.revision;
  if(typeof structureId!=='string'||typeof structureRevision!=='number')throw new Error('Native structure identity was not returned');
  const readStructure=async()=>{
   const observed=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'structure.state',version:1,arguments:{id:structureId}}}]);
   if((observed.catalog?.value as {available?:boolean})?.available!==true)throw new Error('Structure live observation is incomplete');
   return observed;
  };
  const structureInitial=await readStructure();if((structureInitial.catalog?.value as {displaced?:number})?.displaced!==0)throw new Error('Captured structure should match its live pieces');
  const member=await nativeObjects.observation(structureMembers[0]);
  await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.position.set',version:1,arguments:{target:member.id,x:member.position.x+.4,y:member.position.y,z:member.position.z}}}}]));
  const structureDisplaced=await readStructure();if((structureDisplaced.catalog?.value as {displaced?:number})?.displaced!==1)throw new Error('Live member movement was not observed');
  const structureReset=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'structure.reset',version:1,arguments:{id:structureId,revision:structureRevision,members:structureMembers}}}}]));
  const structureRestored=await readStructure();if((structureRestored.catalog?.value as {displaced?:number})?.displaced!==0)throw new Error('Structure reset did not restore its captured baseline');
  const structureUndo=await execute([{action:'undo'}]);if(Math.abs((await nativeObjects.observation(member.id)).position.x-member.position.x-.4)>.00001)throw new Error('Structure Undo lost the displaced pose');
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await writeFile(join(directory,'structure-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Not headset or provider proof.',before:structureBefore,search:structureSearch,definition:structureDefinition,after:structureAfter,initial:structureInitial,displaced:structureDisplaced,reset:structureReset,restored:structureRestored,undo:structureUndo},null,2));
  await execute([{action:'undo'}]);await execute([{action:'undo'}]); // Remove the two observed structure fixtures.
  const compositionProgram=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-build-structure.json','utf8'));
  // The watcher observes live disturbance, so every piece and its displaced pose stay in view.
  const compositionBody=compositionProgram.functions[0].body;
  compositionBody.find((step:{id:string})=>step.id==='create').arguments.position={x:.3,y:1.4,z:2};
  Object.assign(compositionBody.find((step:{id:string})=>step.id==='displace').arguments,{y:1.4,z:2});
  const compositionSource=JSON.stringify(compositionProgram);
  const compositionBefore=await execute([{action:'rules',rule:{action:'inspect'}}]),compositionBeforeIds=await nativeObjects.ids();
  const compositionSaved=await execute([{action:'rules',rule:{action:'edit',revision:compositionBefore.rules!.revision,edits:[{kind:'save',reference:'composition',sequence:{id:'',name:'Native build/reset probe',interruption:0,repeat:false,program:compositionSource}}]}}]);
  const compositionId=compositionSaved.rules?.sequences.find(s=>s.name==='Native build/reset probe')?.id;if(!compositionId)throw new Error('Composed program was not saved');
  let compositionAfter=await execute([{action:'rules',rule:{action:'play',revision:compositionSaved.rules!.revision,target:compositionId}}]);
  const compositionDeadline=Date.now()+15000;
  while(!compositionAfter.rules?.outcomes?.some(o=>o.sequenceId===compositionId)&&Date.now()<compositionDeadline){await new Promise(r=>setTimeout(r,100));compositionAfter=await execute([{action:'rules',rule:{action:'inspect',target:compositionId}}]);}
  const compositionOutcome=compositionAfter.rules?.outcomes?.find(o=>o.sequenceId===compositionId);
  if(compositionOutcome?.phase!=='completed'||await nativeObjects.count()!==compositionBeforeIds.length+6)throw new Error('Composed build/reset failed: '+JSON.stringify(compositionOutcome));
  const compositionList=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'structure.list',version:1,arguments:{offset:0}}}]);
  const compositionGroup=(compositionList.catalog?.value as {entries?:{id:string;name:string;count:number}[]})?.entries?.find(e=>e.name==='Castle'&&e.count===6);
  if(!compositionGroup)throw new Error('Composed structure was not saved');
  const compositionState=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'structure.state',version:1,arguments:{id:compositionGroup.id}}}]);
  if((compositionState.catalog?.value as {available?:boolean})?.available!==true||(compositionState.catalog?.value as {displaced?:number})?.displaced!==0)throw new Error('Composed reset did not recover its baseline');
  const watcher=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-structure-watch.json','utf8'));
  const moduleHash=watcher.imports[0].hash;
  const includedModule=await execute([{action:'catalog',catalog:{operation:'inspect',category:'modules',capability:moduleHash,version:1}}]);
  const moduleView=includedModule.catalog;
  if(moduleView?.operation!=='inspect'||moduleView.category!=='modules'||!moduleView.included||JSON.stringify(moduleView.definition)!==JSON.stringify(watcher.imports[0].module))throw new Error('Included native module differs from the shared fixture');
  const watchedRevision=(compositionState.catalog?.value as {revision:number}).revision;
  watcher.state.find((v:{name:string})=>v.name==='structureId').initial=compositionGroup.id;
  watcher.state.find((v:{name:string})=>v.name==='revision').initial=watchedRevision;
  const watcherSaved=await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'save',reference:'watcher',sequence:{id:'',name:'Native structure watch probe',interruption:0,repeat:false,program:JSON.stringify(watcher)}}]}}]);
  const watcherId=watcherSaved.rules?.sequences.find(s=>s.name==='Native structure watch probe')?.id;
  if(!watcherId||watcherSaved.rules!.running.some(r=>r.sequenceId===watcherId))throw new Error('Saving the watcher must not start it');
  await execute([{action:'rules',rule:{action:'play',revision:watcherSaved.rules!.revision,target:watcherId}}]);
  const waitForWatch=async(phase:string,cycles:number)=>{
   const deadline=Date.now()+15000;let last;
   do {
    last=await execute([{action:'rules',rule:{action:'inspect',target:watcherId}}]);
    const run=last.rules?.running.find(r=>r.sequenceId===watcherId);
    if(run?.state?.some(v=>v.name==='phase'&&v.value===phase)&&run.state.some(v=>v.name==='cycles'&&v.value===String(cycles)))return last;
    if(last.rules?.outcomes?.some(o=>o.sequenceId===watcherId))throw new Error('Watcher ended unexpectedly: '+JSON.stringify(last.rules.outcomes));
    await new Promise(r=>setTimeout(r,100));
   }while(Date.now()<deadline);
   throw new Error('Watcher did not reach '+phase+': '+JSON.stringify(last?.rules?.running));
  };
  const watcherArmed=await waitForWatch('waitingForDisturbance',0);
  const slot=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'structure.slot',version:1,arguments:{id:compositionGroup.id,index:0}}}]);
  const slotValue=slot.catalog?.value as {target:string;position:{x:number;y:number;z:number}};
  const watcherMembers=(await nativeObjects.ids()).filter(id=>!compositionBeforeIds.includes(id));
  if(!slotValue?.target||watcherMembers.length!==6||!watcherMembers.includes(slotValue.target))throw new Error('Watcher member identities unavailable');
  await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.position.set',version:1,arguments:{target:slotValue.target,x:slotValue.position.x+.3,y:slotValue.position.y,z:slotValue.position.z}}}}]));
  const watcherDisturbed=await waitForWatch('waitingForRebuild',1);
  await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'structure.reset',version:1,arguments:{id:compositionGroup.id,revision:watchedRevision,members:watcherMembers}}}}]));
  const watcherRebuilt=await waitForWatch('waitingForDisturbance',1);
  const watcherStopped=await execute([{action:'rules',rule:{action:'stop',target:watcherId}}]);
  if(watcherStopped.rules?.running.some(r=>r.sequenceId===watcherId))throw new Error('Watcher did not stop');
  await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'delete',target:watcherId}]}}]);
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await writeFile(join(directory,'program-structure-watch.json'),JSON.stringify({boundary:'Real Unity runtime and shared transport. Movement is an explicit edit here; ball physics is covered separately in PlayMode. No headset or provider proof.',source:watcher,module:includedModule,saved:watcherSaved,armed:watcherArmed,disturbed:watcherDisturbed,rebuilt:watcherRebuilt,stopped:watcherStopped},null,2));
  await execute([{action:'undo'}]);
  const compositionUndo=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'structure.state',version:1,arguments:{id:compositionGroup.id}}}]);
  if((compositionUndo.catalog?.value as {available?:boolean})?.available!==true||(compositionUndo.catalog?.value as {displaced?:number})?.displaced!==1)throw new Error('Composed reset Undo did not restore the displaced piece');
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'delete',target:compositionId}]}}]);
  await writeFile(join(directory,'program-composition.json'),JSON.stringify({boundary:'Real Unity runtime and shared transport; no headset or provider proof.',source:JSON.parse(compositionSource),saved:compositionSaved,after:compositionAfter,group:compositionGroup,state:compositionState,undo:compositionUndo,outcome:compositionOutcome},null,2));
  const leverSource=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-spring-lever.json','utf8'));
  const connectedArgs=leverSource.imports[0].module.program.functions.find((f:{name:string})=>f.name==='create').body[0].arguments;
  const connectedBefore=structuredClone(lease.state()),connectedBeforeCount=await nativeObjects.count();
  const connectedSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Create a structure',offset:0}}]);
  const connectedDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.batch.create',version:1}}]);
  const connectedAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:connectedArgs}}}]);
  const connectedIds=connectedAfter.execution?.selected?.output?.objectIds;
  if(!Array.isArray(connectedIds)||connectedIds.length!==2||!connectedIds.every(id=>typeof id==='string')||new Set(connectedIds).size!==2||await nativeObjects.count()!==connectedBeforeCount+2)throw new Error('Connected blueprint did not create exactly two distinct pieces');
  const connectedRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:connectedIds[1]}}}]);
  const connectedValue=connectedRead.catalog?.value as {configured:boolean;connected:string};
  if(!connectedValue.configured||connectedValue.connected!==connectedIds[0])throw new Error('Blueprint did not bind its hinge to its own mount');
  const connectedUndo=await execute([{action:'undo'}]);if(await nativeObjects.any(connectedIds as string[])||await nativeObjects.count()!==connectedBeforeCount)throw new Error('Connected blueprint was not one Undo');
  await writeFile(join(directory,'connected-blueprint-authoring.json'),JSON.stringify({boundary:'Real Unity native states; browser acknowledgements replayed separately. Physics tested in PlayMode; no headset or provider proof.',arguments:connectedArgs,before:connectedBefore,search:connectedSearch,definition:connectedDefinition,after:connectedAfter,read:connectedRead,undo:connectedUndo},null,2));
  // Included play-kit constructors use the same module lookup, program save/run,
  // native receipt and Undo path as a user's or the agent's own construction.
  const playKitEvidence=[];
  for(const [fixture,label,count] of [['small-fort','Small fort',16],['spinner','Passive spinner',2],['chess-white','Chess pieces white',16],['chess-black','Chess pieces black',16]] as const){
   const source=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-'+fixture+'.json','utf8'));
   const inspected=await execute([{action:'catalog',catalog:{operation:'inspect',category:'modules',capability:source.imports[0].hash,version:1}}]);
   if(!inspected.catalog?.included||!isDeepStrictEqual(inspected.catalog.definition,source.imports[0].module))throw new Error(label+' is not the exact included module');
   const before=await execute([{action:'rules',rule:{action:'inspect'}}]),beforeIds=await nativeObjects.ids();
   const saved=await execute([{action:'rules',rule:{action:'edit',revision:before.rules!.revision,edits:[{kind:'save',reference:'kit',sequence:{id:'',name:'Native '+label+' probe',interruption:0,repeat:false,program:JSON.stringify(source)}}]}}]);
   const sequenceId=saved.rules?.sequences.find(s=>s.name==='Native '+label+' probe')?.id;
   if(!sequenceId||await nativeObjects.count()!==beforeIds.length)throw new Error(label+' save unexpectedly built objects');
   let after=await execute([{action:'rules',rule:{action:'play',revision:saved.rules!.revision,target:sequenceId}}]);
   const deadline=Date.now()+15000;
   while(!after.rules?.outcomes?.some(o=>o.sequenceId===sequenceId)&&Date.now()<deadline){await new Promise(r=>setTimeout(r,100));after=await execute([{action:'rules',rule:{action:'inspect',target:sequenceId}}]);}
   const outcome=after.rules?.outcomes?.find(o=>o.sequenceId===sequenceId),members=(await nativeObjects.ids()).filter(id=>!beforeIds.includes(id));
   if(outcome?.phase!=='completed'||members.length!==count||after.physicsRunning!==before.physicsRunning)throw new Error(label+' did not complete with exactly its members: '+JSON.stringify(outcome));
   const undone=await execute([{action:'undo'}]);
   if(await nativeObjects.any(members)||await nativeObjects.count()!==beforeIds.length)throw new Error(label+' did not undo atomically');
   await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'delete',target:sequenceId}]}}]);
   playKitEvidence.push({label,hash:source.imports[0].hash,inspected,before,saved,after,undone,members});
  }
  await writeFile(join(directory,'default-play-kit.json'),JSON.stringify({boundary:'Actual native catalog, program, persistence and Undo journey. Physical play tested in PlayMode; Quest acceptance remains open.',examples:playKitEvidence},null,2));
  const leverHash=leverSource.imports[0].hash;
  const leverModule=await execute([{action:'catalog',catalog:{operation:'inspect',category:'modules',capability:leverHash,version:1}}]);
  if(!leverModule.catalog?.included||JSON.stringify(leverModule.catalog.definition)!==JSON.stringify(leverSource.imports[0].module))throw new Error('Included spring lever source differs from its pinned fixture');
  const leverBefore=await execute([{action:'rules',rule:{action:'inspect'}}]),leverBeforeIds=await nativeObjects.ids();
  const leverSaved=await execute([{action:'rules',rule:{action:'edit',revision:leverBefore.rules!.revision,edits:[{kind:'save',reference:'lever',sequence:{id:'',name:'Native spring lever probe',interruption:0,repeat:false,program:JSON.stringify(leverSource)}}]}}]);
  const leverId=leverSaved.rules?.sequences.find(s=>s.name==='Native spring lever probe')?.id;if(!leverId)throw new Error('Spring lever caller was not saved');
  let leverAfter=await execute([{action:'rules',rule:{action:'play',revision:leverSaved.rules!.revision,target:leverId}}]);
  const leverDeadline=Date.now()+15000;
  while(!leverAfter.rules?.outcomes?.some(o=>o.sequenceId===leverId)&&Date.now()<leverDeadline){await new Promise(r=>setTimeout(r,100));leverAfter=await execute([{action:'rules',rule:{action:'inspect',target:leverId}}]);}
  const leverOutcome=leverAfter.rules?.outcomes?.find(o=>o.sequenceId===leverId),leverMembers=(await nativeObjects.ids()).filter(id=>!leverBeforeIds.includes(id));
  if(leverOutcome?.phase!=='completed'||leverMembers.length!==2||leverMembers.some(id=>connectedIds.includes(id)))throw new Error('Spring lever module did not complete with fresh members: '+JSON.stringify(leverOutcome));
  const leverFacts=[];for(const id of leverMembers){const read=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:id}}}]);leverFacts.push(read);}
  const leverOwners=leverFacts.filter(f=>(f.catalog?.value as {configured:boolean}).configured);
  if(leverOwners.length!==1||!leverMembers.includes((leverOwners[0].catalog?.value as {connected:string}).connected))throw new Error('Program-created hinge does not connect the new members');
  const selectionBefore=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.selection',version:1}}]);
  if(!selectionBefore.constructionSelection||selectionBefore.constructionSelection.members.length)throw new Error('Native construction selection was not initially empty');
  const selectionFirst=await execute([{action:'execution',execution:{operation:'start',call:{id:'room.selection.set',version:1,arguments:{stateId:selectionBefore.constructionSelection.stateId,members:[leverMembers[0]],collecting:false}}}}]);
  const selectionBoth=await execute([{action:'execution',execution:{operation:'start',call:{id:'room.selection.set',version:1,arguments:{stateId:selectionFirst.constructionSelection!.stateId,members:leverMembers,collecting:false}}}}]);
  const selectionRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.selection',version:1}}]);
  if(!isDeepStrictEqual(selectionRead.catalog?.value,{stateId:selectionBoth.constructionSelection!.stateId,members:leverMembers,collecting:false}))throw new Error('Native fact and inline construction selection differ');
  const selectionLocated=await execute([{action:'inspect',target:leverMembers[0]}]);if(selectionLocated.selectedId!==leverMembers[0])throw new Error('Locate did not focus the requested construction member');
  const captureMembers=[],captureFacts=[];
  for(let i=0;i<leverMembers.length;i++){
   const fact=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:leverMembers[i]}}}]);
   captureFacts.push(fact);captureMembers.push({target:leverMembers[i],slot:'piece_'+(i+1),revision:(fact.catalog?.value as {revision:number}).revision});
  }
  const captureDraft=constructionCaptureDraft(selectionLocated.constructionSelection!);
  const captureArguments=applyCurrentInputSnapshots(capabilityDefinition(captureDraft.id)!.input,captureDraft.arguments,captureFacts.map(fact=>{if(!fact.catalog)throw new Error('Missing native member definition');return fact.catalog;}));
  if(!isDeepStrictEqual(captureArguments.members,captureMembers))throw new Error('Book selection draft differs from current native member facts');
  const captureSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Save construction',offset:0}}]);
  const captureDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'program.module.captureConstruction',version:1}}]);
  let captureAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'program.module.captureConstruction',version:1,arguments:captureArguments}}}]);
  const captureRun=captureAfter.execution?.selected?.id;if(!captureRun)throw new Error('Capture did not return a run identity');
  const captureDeadline=Date.now()+15000;
  while(captureAfter.execution?.selected?.phase!=='completed'&&Date.now()<captureDeadline){await new Promise(r=>setTimeout(r,100));captureAfter=await execute([{action:'execution',execution:{operation:'inspect',runId:captureRun}}]);}
  const capturedHash=captureAfter.execution?.selected?.output?.hash;if(typeof capturedHash!=='string')throw new Error('Capture did not publish a reusable module: '+JSON.stringify(captureAfter.execution?.selected));
  const capturedRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'modules',capability:capturedHash,version:1}}]);
  const capturedModule=capturedRead.catalog.definition;
  if(!capturedModule||leverMembers.some(id=>JSON.stringify(capturedModule).includes(id)))throw new Error('Captured source retained original object identities');
  const movementBefore=capturedRead;
  const movementShown=await execute([{action:'execution',execution:{operation:'start',call:{id:'room.selection.manipulate',version:1,arguments:{stateId:selectionBoth.constructionSelection!.stateId,members:leverMembers,visible:true}}}}]);
  if(!movementShown.constructionManipulation?.visible||movementShown.constructionManipulation.holding)throw new Error('Native construction handle was not shown idle');
  const movementRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.selection.manipulation',version:1}}]);
  if(!isDeepStrictEqual(movementRead.catalog?.value,movementShown.constructionManipulation))throw new Error('Move handle fact differs from the inline observation');
  const movementFacts=[];for(const target of leverMembers)movementFacts.push(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target}}}]));
  const movementOrigin=movementFacts[0].catalog?.value as {position:{x:number;y:number;z:number};rotation:{x:number;y:number;z:number;w:number};scale:number};
  const movementArguments={members:movementFacts.map(f=>{const v=f.catalog?.value as {target:string;revision:number};return {target:v.target,revision:v.revision};}),position:{x:movementOrigin.position.x+.2,y:movementOrigin.position.y+.1,z:movementOrigin.position.z+.1},rotation:{x:0,y:Math.SQRT1_2,z:0,w:Math.SQRT1_2},scale:1.2};
  const movementSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Move, turn or resize a construction',offset:0}}]);
  const movementDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.layout.transform',version:1}}]);
  const movementTransformed=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.layout.transform',version:1,arguments:movementArguments}}}]));
  if(movementTransformed.execution?.selected?.phase!=='completed'||(movementTransformed.execution.selected.output as {count:number}).count!==2)throw new Error('Native group transform did not complete');
  const movementAfterFacts=[];for(const target of leverMembers)movementAfterFacts.push(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target}}}]));
  for(let i=0;i<2;i++){const before=movementFacts[i].catalog?.value as typeof movementOrigin,after=movementAfterFacts[i].catalog?.value as typeof movementOrigin;if(Math.abs(after.scale-before.scale*1.2)>.0001)throw new Error('Group transform lost a member scale');}
  const movedOrigin=(movementAfterFacts[0].catalog?.value as typeof movementOrigin).position;if(Math.hypot(movedOrigin.x-movementArguments.position.x,movedOrigin.y-movementArguments.position.y,movedOrigin.z-movementArguments.position.z)>.0001)throw new Error('Group transform pivot was not placed');
  const movementUndo=await execute([{action:'undo'}]);
  for(let i=0;i<2;i++){const actual=await nativeObjects.observation(leverMembers[i]),expected=movementFacts[i].catalog?.value as typeof movementOrigin;if(Math.hypot(actual.position.x-expected.position.x,actual.position.y-expected.position.y,actual.position.z-expected.position.z)>.0001||Math.abs(actual.scale-expected.scale)>.0001)throw new Error('One group Undo did not restore both members');}
  const movementHidden=await execute([{action:'execution',execution:{operation:'start',call:{id:'room.selection.manipulate',version:1,arguments:{stateId:movementUndo.constructionSelection!.stateId,members:leverMembers,visible:false}}}}]);if(movementHidden.constructionManipulation?.visible)throw new Error('Move handle did not close');
  const currentCaptureSource=insertProgramCapability(JSON.stringify({version:2,entry:'main',resources:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]}),{id:'program.module.captureConstruction',version:1,arguments:{...captureArguments,members:captureMembers.map(m=>({...m,revision:1}))}},{kind:'current',fields:captureMembers.map((_,i)=>`members.${i}.revision`)});
  const currentCaptureSaved=await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'save',reference:'capture_current',sequence:{id:'',name:'Native current capture probe',interruption:0,repeat:false,program:JSON.stringify(currentCaptureSource)}}]}}]);
  const currentCaptureId=currentCaptureSaved.rules?.sequences.find(s=>s.name==='Native current capture probe')?.id;if(!currentCaptureId)throw new Error('Current-member capture caller was not saved');
  let currentCaptureAfter=await execute([{action:'rules',rule:{action:'play',revision:currentCaptureSaved.rules!.revision,target:currentCaptureId}}]);
  const currentCaptureDeadline=Date.now()+15000;
  while(!currentCaptureAfter.rules?.outcomes?.some(o=>o.sequenceId===currentCaptureId)&&Date.now()<currentCaptureDeadline){await new Promise(r=>setTimeout(r,100));currentCaptureAfter=await execute([{action:'rules',rule:{action:'inspect',target:currentCaptureId}}]);}
  const currentCaptureOutcome=currentCaptureAfter.rules?.outcomes?.find(o=>o.sequenceId===currentCaptureId);
  if(currentCaptureOutcome?.phase!=='completed')throw new Error('Visible current-member reads did not complete native capture: '+JSON.stringify(currentCaptureOutcome));
  await writeFile(join(directory,'current-members-program.json'),JSON.stringify({source:currentCaptureSource,saved:currentCaptureSaved,after:currentCaptureAfter,outcome:currentCaptureOutcome,hash:capturedHash,placeholderRevisions:captureMembers.map(()=>1),actualRevisions:captureMembers.map(m=>m.revision)},null,2));
  const leverUndo=await execute([{action:'undo'}]);if(leverUndo.constructionSelection?.members.length||leverUndo.constructionSelection?.stateId===selectionBoth.constructionSelection!.stateId)throw new Error('Deleting original members retained a stale selection');if(await nativeObjects.any(leverMembers))throw new Error('Program-created lever was not one Undo');
  const parsedCapture=parseProgram(JSON.stringify(capturedModule.program));
  const createNode=parsedCapture.program?.functions.find(f=>f.name==='create')?.body[0];
  if(createNode?.op!=='invoke'||createNode.capability!=='object.batch.create')throw new Error('Captured module does not contain its editable construction: '+parsedCapture.error);
  const rebuiltArgs=structuredClone(createNode.arguments);
  const rebuilt=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:rebuiltArgs}}}]);
  const rebuiltIds=rebuilt.execution?.selected?.output?.objectIds;
  if(!Array.isArray(rebuiltIds)||rebuiltIds.length!==2||rebuiltIds.some(id=>typeof id!=='string'||leverMembers.includes(id)))throw new Error('Captured construction did not survive removal of originals');
  const rebuiltHinges=[];for(const id of rebuiltIds){const read=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection',version:1,arguments:{target:id as string}}}]);rebuiltHinges.push(read);}
  const rebuiltOwner=rebuiltHinges.find(r=>(r.catalog?.value as {configured:boolean}).configured);
  if(!rebuiltOwner||!rebuiltIds.includes((rebuiltOwner.catalog?.value as {connected:string}).connected))throw new Error('Rebuilt hinge did not bind its fresh members');
  const rebuiltUndo=await execute([{action:'undo'}]);if(await nativeObjects.any(rebuiltIds))throw new Error('Captured construction did not keep one Undo');
  await writeFile(join(directory,'construction-capture.json'),JSON.stringify({boundary:'Real Unity shared transport and module library; no headset or provider proof.',arguments:captureArguments,facts:captureFacts,before:selectionLocated,search:captureSearch,definition:captureDefinition,captured:captureAfter,module:capturedRead,originalsRemoved:leverUndo,rebuilt,connections:rebuiltHinges,undo:rebuiltUndo},null,2));
  await writeFile(join(directory,'construction-movement.json'),JSON.stringify({boundary:'Real Unity native handle visibility/facts, group transform and one Undo. Physical grip is verified separately in PlayMode; no headset/provider proof.',before:movementBefore,shown:movementShown,read:movementRead,facts:movementFacts,search:movementSearch,definition:movementDefinition,arguments:movementArguments,transformed:movementTransformed,afterFacts:movementAfterFacts,undo:movementUndo,hidden:movementHidden},null,2));
  await writeFile(join(directory,'construction-selection.json'),JSON.stringify({boundary:'Real Unity room selection, fact, locate and capture states; browser replay is separate, no physical headset proof.',before:selectionBefore,first:selectionFirst,both:selectionBoth,read:selectionRead,located:selectionLocated,definition:captureDefinition,facts:captureFacts,captured:captureAfter,removed:leverUndo},null,2));
  let captureRemoved=await execute([{action:'execution',execution:{operation:'start',call:{id:'program.module.remove',version:1,arguments:{hash:capturedHash}}}}]);
  const removeDeadline=Date.now()+15000;while(captureRemoved.execution?.selected?.phase!=='completed'&&Date.now()<removeDeadline){await new Promise(r=>setTimeout(r,100));captureRemoved=await execute([{action:'execution',execution:{operation:'inspect',runId:captureRemoved.execution!.selected!.id}}]);}
  if(captureRemoved.execution?.selected?.phase!=='completed')throw new Error('Captured probe module was not cleaned up');
  await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'delete',target:leverId},{kind:'delete',target:currentCaptureId}]}}]);
  await writeFile(join(directory,'program-spring-lever.json'),JSON.stringify({boundary:'Real Unity runtime and shared transport. Physics tested in PlayMode; no headset or provider proof.',source:leverSource,module:leverModule,saved:leverSaved,after:leverAfter,facts:leverFacts,undo:leverUndo,outcome:leverOutcome},null,2));
  // Keep a real connected button through the running-physics checks. Its fixed
  // mount must remain an input without becoming another dynamic actor.
  const activeButtonCreated=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:buttonArgs}}}]);
  const activeButtonIds=activeButtonCreated.execution?.selected?.output?.objectIds;
  if(!Array.isArray(activeButtonIds)||activeButtonIds.length!==2||!activeButtonIds.every(id=>typeof id==='string'))throw new Error('Active spring button did not create two members');
  // Real full-app contact capture on the synthetic floor, with both inputs in view.
  // Keep their relative throw geometry and clearance from the walking fixtures.
  const catchHolderCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:'Catch socket',x:1.5,y:1,z:4,scale:1,recipe:{version:1,parts:[{id:'Socket',parent:null,shape:'box',position:{x:0,y:0,z:0},size:{x:.08,y:.08,z:.08},rotation:{x:0,y:0,z:0,w:1},color:{r:.2,g:.6,b:.6,a:1}}],tracks:[],duration:1,playing:false,loop:false}}}}}]));
  const catchHolder=catchHolderCreated.execution?.selected?.output?.objectId;if(typeof catchHolder!=='string')throw new Error('Catch holder creation failed');
  const catchCreated=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Catch ball',x:.8,y:1,z:4.3}}}}]));
  const catchTarget=catchCreated.execution?.selected?.output?.objectId;if(typeof catchTarget!=='string')throw new Error('Catch ball creation failed');
  const catchSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Catch a physical object',offset:0}}]);
  const catchDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.physics.catch',version:1}}]);
  const catchCall={id:'object.physics.catch',version:1,arguments:{target:catchTarget,holder:{kind:'recipePart',objectId:catchHolder,part:'Socket',revision:await nativeObjects.revision(catchHolder)},offset:{x:0,y:0,z:.3},timeout:5,holdSeconds:1,gripRadius:.05,maxSpeed:5}};
  const catchBlocked=await execute([{action:'catalog',catalog:{operation:'check',call:catchCall}}]);
  if(catchBlocked.catalog?.available!==false)throw new Error('Catching should require explicit running physics');
  const physicsBefore=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.simulation',version:1}}]);
  const physicsState=physicsBefore.catalog?.value as {stateId:string;ready:boolean};if(!physicsState?.ready)throw new Error('The explicitly synthetic probe floor is unavailable');
  await execute([{action:'execution',execution:{operation:'start',call:{id:'physics.simulation.set',version:1,arguments:{operation:'start',stateId:physicsState.stateId}}}}]);
  await probeAvatarAttention(execute,directory);
  const walkingTargets=await probeGroundedTravel(execute,directory);
  await probeGroundedFollowing(execute,directory,walkingTargets);
  await probeObjectWalk(execute,directory,gait);
  // Let ordinary gravity finish before preparing a direct throw. Physics autosave
  // advances object revisions too; a still-falling ball is not a stable fixture.
  // Observe over a full placement-capture interval; never retry a refused launch.
  const waitForCatchPlacement=async(simulating:boolean,wholeRoom=false)=>{
   const samples:unknown[]=[];const deadline=Date.now()+15000;let quietSince=Date.now();
   const sample=async()=>{
    const ids=wholeRoom?await nativeObjects.ids():[catchTarget],objects=[];
    for(const target of ids)objects.push(await nativeObjects.observation(target));
    const ball=objects.find(o=>o.id===catchTarget);if(!ball||ball.simulating!==simulating)throw new Error('The catch fixture has the wrong physics state');
    samples.push({ids,objects});return JSON.stringify(objects.map(o=>[o.id,o.objectRevision,o.position]));
   };
   let previous=await sample();
   while(Date.now()<deadline){
    await new Promise(r=>setTimeout(r,150));const current=await sample();
    if(current!==previous)quietSince=Date.now();previous=current;
    if(Date.now()-quietSince>=1250)return samples;
   }
   throw new Error('The catch fixture placement did not stabilize within the bounded observation window');
  };
  const catchSettling=await waitForCatchPlacement(true);
  const physicsInputs=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.physics.inputs',version:1,arguments:{target:catchTarget}}}]);
  const inputState=physicsInputs.catalog?.value as {state:string;ready:boolean;dependencyCount:number;dependencies:string[]};
  if(inputState?.state!=='ready'||!inputState.ready||inputState.dependencyCount<inputState.dependencies.length)throw new Error('The native physics input fact must describe the active body before catch');
  await writeFile(join(directory,'native-physics-inputs.json'),JSON.stringify(physicsInputs,null,2));
  const contactInputs=[];
  for(const [capability,args] of [['object.contact.inputs',{target:catchTarget}],['object.attachment.inputs',{target:catchTarget}],['object.recording.inputs',{target:catchTarget}],['avatar.authored.inputs',{}],['controller.contact.inputs',{hand:'left'}],['controller.contact.inputs',{hand:'right'}]] as const){
   const observed=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}]);
   const value=observed.catalog?.value as {state:string;ready:boolean;dependencyCount:number;dependencies:string[]};
   if(observed.catalog?.available!==true||value?.state!=='inactive'||value.ready||value.dependencyCount!==0||value.dependencies.length!==0)throw new Error('An untracked native contact must be inactive: '+capability);
   contactInputs.push(observed);
  }
  await writeFile(join(directory,'native-contact-inputs.json'),JSON.stringify({boundary:'Passive real-native catalog observations. No controller source or selected object is simulated by this driver; active contacts are tested in PlayMode.',observations:contactInputs},null,2));
  const connectedTarget=activeButtonIds[1] as string;
  let connectedInputs=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.physics.inputs',version:1,arguments:{target:connectedTarget}}}]);
  const connectedDeadline=Date.now()+10000;
  while(!(connectedInputs.catalog?.value as {ready:boolean})?.ready&&Date.now()<connectedDeadline){await new Promise(r=>setTimeout(r,100));connectedInputs=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.physics.inputs',version:1,arguments:{target:connectedTarget}}}]);}
  const connectedInputState=connectedInputs.catalog?.value as {state:string;ready:boolean;dependencyCount:number};
  if(connectedInputState?.state!=='ready'||!connectedInputState.ready||connectedInputState.dependencyCount<2||!(await nativeObjects.observation(connectedTarget)).simulating||(await nativeObjects.observation(activeButtonIds[0])).simulating)throw new Error('Connected button physics input ownership does not match its dynamic cap and fixed mount');
  const activeConnection=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.connection.state',version:1,arguments:{target:connectedTarget}}}]);
  if(!(activeConnection.catalog?.value as {active:boolean})?.active)throw new Error('The connected input fact admitted a cap without its active joint');
  await writeFile(join(directory,'connected-physics-inputs.json'),JSON.stringify({boundary:'Real Unity shared-client input and connection facts for a running slider on a fixed mount. Synthetic floor; pending-load and interruption coverage is in PlayMode, not this journey. No headset or provider proof.',created:activeButtonCreated,inputs:connectedInputs,connection:activeConnection},null,2));
  const catchReadyDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.physics.catch',version:1}}]);
  const catchReadyCheck=await execute([{action:'catalog',catalog:{operation:'check',call:catchCall}}]);if(catchReadyCheck.catalog?.available!==true)throw new Error('Native catch readiness differs from the active world');
  const catchWaiting=await execute([{action:'execution',execution:{operation:'start',call:catchCall}}]);const catchRun=catchWaiting.execution?.selected?.id;if(!catchRun||catchWaiting.execution?.selected?.phase!=='preparing')throw new Error('Native catch did not wait for physical contact');
  const catchFact=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.catch',version:1,arguments:{target:catchTarget}}}]);
  const catchAttempts=catchFact.catalog?.value as {attempts:{phase:string}[]};if(catchAttempts?.attempts[0]?.phase!=='waiting')throw new Error('Live catch fact lost the native wait');
  const catchLaunch=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.physics.launch',version:2,arguments:{target:catchTarget,destination:{kind:'anchor',anchor:catchCall.arguments.holder,offset:catchCall.arguments.offset},seconds:.4,maxSpeed:8}}}}]);
  if(catchLaunch.execution?.selected?.output?.phase!=='launched')throw new Error('The ball was not launched by the shared action');
  let catchAfter=await execute([{action:'execution',execution:{operation:'inspect',runId:catchRun}}]);const catchDeadline=Date.now()+12000;
  while(catchAfter.execution?.selected?.phase==='preparing'&&Date.now()<catchDeadline){await new Promise(r=>setTimeout(r,100));catchAfter=await execute([{action:'execution',execution:{operation:'inspect',runId:catchRun}}]);}
  if(catchAfter.execution?.selected?.phase!=='completed'||catchAfter.execution.selected.output?.caught!==true||catchAfter.execution.selected.output?.dropped!==true)throw new Error('Physical catch did not complete: '+JSON.stringify(catchAfter.execution?.selected));
  const catchReplay=await execute([{action:'execution',execution:{operation:'start',runId:catchRun,call:catchCall}}]);if(catchReplay.execution?.selected?.phase!=='completed'||catchReplay.rules?.running?.some(r=>r.id===catchRun))throw new Error('Catch receipt replay restarted a physical attempt');
  const physicsAfter=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.simulation',version:1}}]);
  const catchPaused=await execute([{action:'execution',execution:{operation:'start',call:{id:'physics.simulation.set',version:1,arguments:{operation:'pause',stateId:(physicsAfter.catalog!.value as {stateId:string}).stateId}}}}]);
  // Pausing freezes bodies; the periodic placement capture can still publish their last pose.
  // Observe the whole room for one capture interval before one-shot global Undo.
  const catchPausedSettling=await waitForCatchPlacement(false,true);
  await execute([{action:'undo'}]);if(await nativeObjects.exists(walkingTargets.obstacle))throw new Error('One Undo did not remove the walking obstacle');
  await execute([{action:'undo'}]);if(await nativeObjects.exists(walkingTargets.target))throw new Error('One Undo did not remove the walking fixture');
  await writeFile(join(directory,'physical-catching.json'),JSON.stringify({boundary:'Real full-app shared-client capture and drop with ordinary PhysX against a synthetic probe floor. Includes the real command result and duplicate receipt; no headset, scan, browser physics or provider proof.',search:catchSearch,definition:catchDefinition,readyDefinition:catchReadyDefinition,readyCheck:catchReadyCheck,call:catchCall,blocked:catchBlocked,settling:catchSettling,waiting:catchWaiting,fact:catchFact,launch:catchLaunch,after:catchAfter,replay:catchReplay,paused:catchPaused,pausedSettling:catchPausedSettling},null,2));
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);
  await execute([{action:'undo'}]);if(await nativeObjects.any(activeButtonIds))throw new Error('Active button did not retain single-Undo construction');
  outcome={groundedFollowing:{movingLeaderStatusDistanceAndReplayVerified:true},groundedTravel:{gradualMotionArrivalAndReplayVerified:true,solidObstacleDetourObserved:true},connectedPhysicsInputs:{runningSliderAndFixedMountVerified:true},portableResources,physicalCatching:{syntheticFloor:true,sharedLaunchCatchDropAndReplayVerified:true},constructionMovement:{sharedHandleTransformAndOneUndoVerified:true},constructionSelection:{sharedStateFactLocateCaptureAndPruneVerified:true},currentMembers:{visibleReadsAndIndexedGuardsVerified:true,program:currentCaptureId},constructionCapture:{hash:capturedHash,survivedOriginalRemovalAndInternalHingeAndUndoVerified:true},connectedBlueprint:{identitiesInternalHingeAndSingleUndoVerified:true},leverModule:{hash:leverHash,program:leverId,includedSourceAndNativeCreationVerified:true},drawingTip:{tipHash,configurationReadbackAndUndoVerified:true},surface:{chalkHash,stroke,toolAndInkReadEraseUndoVerified:true},watch:{moduleHash,program:watcherId,includedSourceAndNativeRearmVerified:true},composition:{program:compositionId,outcome:compositionOutcome,buildCaptureMoveResetAndUndoVerified:true},structures:{captureReceipt:structureAfter.execution?.selected,liveDisplacementResetAndUndoVerified:true},batch:{createReceipt:batchAfter.execution?.selected,identitiesAndSingleUndoVerified:true},layout:{applyReceipt:layoutAfter.execution?.selected,liveReadAndSingleUndoVerified:true},template:{hash:templateArgs.templateHash,createReceipt:templateAfter.execution?.selected,componentsAndSingleUndoVerified:true},createdId:target,createReceipt:selected,paintVerified:true,undoPaintVerified:true,undoCreateVerified:true,diagnostics:diagnostic.catalog.value,lathe:{createReceipt:lathe.execution?.selected,profile:value,editAndUndoVerified:true},collision:{summary:summaryValue,editAndUndoVerified:true},latheCycles:cycle+1};
  }
 }
 if(!prompt&&!probeOwner.identityOnly){
  const sound=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'audio.source.edit',version:1,arguments:{operation:'save',id:'',revision:0,definition:{name:'Robot greeting',kind:'tone',tone:{wave:'sine',frequency:440,endFrequency:660,seconds:.3,attack:.01,release:.04,seed:1}}}}}}]));
  const soundId=sound.execution?.selected?.output?.id;if(typeof soundId!=='string')throw new Error('Sound creation did not return its stable identity');
  const emitter=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.audioEmitter.edit',version:1,arguments:{operation:'configure',target:'book',revision:await nativeObjects.revision('book'),emitter:'probeSound',definition:{source:soundId,part:'',joint:'',position:{x:0,y:0,z:0},role:'effects',spatial:false,gain:0,distance:{minimum:.5,maximum:15}}}}}}]));
  const configured=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.audioEmitter',version:1,arguments:{target:'book',emitter:'probeSound'}}}]);
  if((configured.catalog?.value as {definition:{source:string}}).definition.source!==soundId)throw new Error('Sound attachment did not preserve its source identity');
  let played=await execute([{action:'execution',execution:{operation:'start',call:{id:'audio.play',version:1,arguments:{target:'book',emitter:'probeSound'}}}}]);
  const runId=played.execution?.selected?.id;if(typeof runId!=='string')throw new Error('Sound playback returned no run identity');
  const deadline=Date.now()+10000;
  while(played.execution?.selected?.phase==='preparing'&&Date.now()<deadline){await new Promise(r=>setTimeout(r,100));played=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);}
  if(played.execution?.selected?.phase!=='completed'||played.execution.selected.output?.source!==soundId||Number(played.execution.selected.output.seconds)<.299)throw new Error('Sound playback did not finish from consumed native samples: '+JSON.stringify(played.execution?.selected));
  let started=await execute([{action:'execution',execution:{operation:'start',call:{id:'audio.start',version:1,arguments:{target:'book',emitter:'probeSound',loop:true,lifetime:'room'}}}}]);
  const startRun=started.execution?.selected?.id;if(!startRun)throw new Error('Independent sound returned no run identity');
  const startDeadline=Date.now()+10000;
  while(!['completed','failed','cancelled'].includes(started.execution?.selected?.phase??'')&&Date.now()<startDeadline){await new Promise(r=>setTimeout(r,100));started=await execute([{action:'execution',execution:{operation:'inspect',runId:startRun}}]);}
  type SoundSample={revision:number;identity:{instance:string};playback:{phase:string;seconds:number;loop:boolean;gain:number;lifetime:string}};
  const startSample=started.execution?.selected?.output as SoundSample|undefined;
  if(started.execution?.selected?.phase!=='completed'||!startSample?.identity.instance||startSample.playback.lifetime!=='room'||startSample.playback.seconds<=0)throw new Error('Independent sound was not handed off after native consumption');
  const soundInstance=startSample.identity.instance;
  const ownershipRevision=lease.state().sceneRevision;
  const soundReservation=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.audioReservation',version:1,arguments:{index:0}}}]);
  const reservation=factReply(soundReservation).value as {reservationId:string;sourceId:string;state:string;owners:number;readyPcmBytes:number};
  if(reservation.sourceId!==soundId||reservation.state!=='ready'||reservation.owners!==1||reservation.readyPcmBytes<=0)throw new Error('Native playing sound lost its decoded source reservation');
  const soundOwnership=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.audioOwner',version:1,arguments:{reservationId:reservation.reservationId,index:0}}}]);
  const owner=factReply(soundOwnership).value as {instanceId:string;worldId:string;regionId:string;target:string;role:string;sourceId:string};
  if(owner.instanceId!==soundInstance||owner.worldId.length!==32||owner.regionId.length!==32||owner.target!=='book'||owner.role!=='audio'||owner.sourceId!==soundId||lease.state().sceneRevision!==ownershipRevision)throw new Error('Audio owner observation changed the room or lost native identity');
  const readSound=()=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.instance',version:1,arguments:{target:'book',instance:soundInstance}}}]);
  const activeSounds=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.instances',version:1}}]);
  if(!(activeSounds.catalog?.value as {entries:{instance:string}[]}).entries.some(v=>v.instance===soundInstance))throw new Error('Room sound discovery lost the handed-off instance');
  const beforePause=await readSound();
  const soundWatch={version:3,entry:'main',resources:['book'],state:[{name:'phase',initial:'waiting'}],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'received',initial:false},{name:'source',initial:''},{name:'phase',initial:''}],body:[
   {id:'watch',op:'awaitEvent',event:'audio.instance.changed',source:'',version:1,arguments:{target:'book',instance:soundInstance,after:(beforePause.catalog!.value as SoundSample).revision},bindings:{},timeout:{value:10},received:'received',value:'source',fields:{phase:'phase'}},
   {id:'remember',op:'setState',variable:'phase',value:{var:'phase'}},{id:'hold',op:'sleep',seconds:{value:20}}
  ]}]};
  const soundWatchSaved=await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'save',reference:'audio_watch',sequence:{id:'',name:'Native sound watch probe',interruption:0,repeat:false,program:JSON.stringify(soundWatch)}}]}}]);
  const soundWatchId=soundWatchSaved.rules?.sequences.find(s=>s.name==='Native sound watch probe')?.id;if(!soundWatchId)throw new Error('Sound event program was not saved');
  await execute([{action:'rules',rule:{action:'play',revision:soundWatchSaved.rules!.revision,target:soundWatchId}}]);
  const controlSound=async(operation:string)=>{
   const read=await readSound(),args={operation,target:'book',instance:soundInstance,revision:(read.catalog!.value as SoundSample).revision,...(operation==='gain'?{gain:0}: {})};
   const after=await execute([{action:'execution',execution:{operation:'start',call:{id:'audio.control',version:1,arguments:args}}}]);
   if(after.execution?.selected?.phase!=='completed'||(after.execution.selected.output as SoundSample).identity.instance!==soundInstance)throw new Error('Sound control failed or changed instance: '+operation);
   return after;
  };
  const soundPaused=await controlSound('pause');
  const soundMixBefore=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.mix',version:1}}]);
  const mixRevision=lease.state().sceneRevision;
  const soundMixed=await execute([{action:'execution',execution:{operation:'start',call:{id:'audio.mix.control',version:1,arguments:{operation:'set',stateId:(factReply(soundMixBefore).value as {stateId:string}).stateId,master:.6,effects:.4,media:.5,ambience:.1}}}}]);
  if(soundMixed.execution?.selected?.phase!=='completed')throw new Error('Shared audio mix did not complete');
  const soundMixAfter=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.mix',version:1}}]);
  const mixValue=factReply(soundMixAfter).value as {stateId:string;master:number;effects:number;media:number;ambience:number};
  if(Math.abs(mixValue.master-.6)>1e-6||Math.abs(mixValue.effects-.4)>1e-6||Math.abs(mixValue.media-.5)>1e-6||Math.abs(mixValue.ambience-.1)>1e-6||lease.state().sceneRevision!==mixRevision)throw new Error('Mix changed the room or lost its role levels');
  const pausedAfterMix=await readSound();
  if((pausedAfterMix.catalog!.value as SoundSample).revision!==(soundPaused.execution!.selected!.output as SoundSample).revision||(pausedAfterMix.catalog!.value as SoundSample).playback.phase!=='paused')throw new Error('World mix changed individual paused sound identity or revision');
  let soundObserved=lease.state();const soundEventDeadline=Date.now()+10000;
  while(Date.now()<soundEventDeadline){soundObserved=await execute([{action:'rules',rule:{action:'inspect',target:soundWatchId}}]);if(soundObserved.rules?.running.find(r=>r.sequenceId===soundWatchId)?.state?.some(v=>v.name==='phase'&&v.value==='paused'))break;await new Promise(r=>setTimeout(r,100));}
  if(!soundObserved.rules?.running.find(r=>r.sequenceId===soundWatchId)?.state?.some(v=>v.name==='phase'&&v.value==='paused'))throw new Error('Native event program did not receive the exact sound pause');
  const soundResumed=await controlSound('resume'),soundGain=await controlSound('gain');
  await new Promise(r=>setTimeout(r,450));const soundContinued=await readSound();
  if((soundContinued.catalog!.value as SoundSample).playback.seconds<=.3)throw new Error('Loop did not continue beyond its finite source duration');
  const soundMixReset=await execute([{action:'execution',execution:{operation:'start',call:{id:'audio.mix.control',version:1,arguments:{operation:'reset',stateId:mixValue.stateId}}}}]);
  const resetMix=soundMixReset.execution?.selected?.output;
  if(soundMixReset.execution?.selected?.phase!=='completed'||!resetMix||['master','effects','media','ambience'].some(key=>resetMix[key]!==1))throw new Error('World mix did not reset without a save');
  const soundStopped=await controlSound('stop'),soundTerminal=await readSound();
  if((soundTerminal.catalog!.value as SoundSample).playback.phase!=='stopped')throw new Error('Sound stop did not retain its terminal receipt');
  const releasedAudio=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.audioBudget',version:1}}]);
  const releasedUsage=factReply(releasedAudio).value as {sources:number;owners:number;reservedPcmBytes:number;readyPcmBytes:number};
  if(releasedUsage.sources!==0||releasedUsage.owners!==0||releasedUsage.reservedPcmBytes!==0||releasedUsage.readyPcmBytes!==0)throw new Error('Stopped sound retained PCM or admission');
  const staleAudioOwner=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.audioOwner',version:1,arguments:{reservationId:reservation.reservationId,index:0}}}]);
  if(staleAudioOwner.catalog?.available!==false)throw new Error('Released sound ownership remained available');
  await execute([{action:'rules',rule:{action:'stop',target:soundWatchId}}]);
  await execute([{action:'rules',rule:{action:'edit',revision:lease.state().rules!.revision,edits:[{kind:'delete',target:soundWatchId}]}}]);
  const removed=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.audioEmitter.edit',version:1,arguments:{operation:'remove',target:'book',revision:await nativeObjects.revision('book'),emitter:'probeSound'}}}}]));
  const sourceRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.source.definition',version:2,arguments:{id:soundId}}}]);
  const cleaned=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'audio.source.edit',version:1,arguments:{operation:'remove',id:soundId,revision:(sourceRead.catalog?.value as {revision:number}).revision}}}}]));
  await writeFile(join(directory,'world-audio.json'),JSON.stringify({boundary:'Shared headless client and real native source/edit/play, independent loop/control/discovery receipts and program event delivery; silent gain, no provider or headset audio proof.',sound,emitter,configured,played,started,soundReservation,soundOwnership,releasedAudio,staleAudioOwner,activeSounds,beforePause,soundWatch,soundWatchSaved,soundPaused,soundMixBefore,soundMixed,soundMixAfter,pausedAfterMix,soundMixReset,soundObserved,soundResumed,soundGain,soundContinued,soundStopped,soundTerminal,removed,cleaned},null,2));
  let liveSoundFixture:{endpoint:string}|null=null;
  try{liveSoundFixture=JSON.parse(await readFile(join(directory,'live-sound-fixture.json'),'utf8'));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
  if(liveSoundFixture){
   const callLive=async(id:string,args:Record<string,unknown>)=>{
    let state=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);const runId=state.execution?.selected?.id;if(!runId)throw new Error('Live audio action missing identity');
    const end=Date.now()+30000;while(!['completed','failed','cancelled'].includes(state.execution?.selected?.phase??'')&&Date.now()<end){await new Promise(r=>setTimeout(r,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);}
    if(state.execution?.selected?.phase!=='completed')throw new Error('Live audio action failed: '+JSON.stringify(state.execution?.selected));return state;
   };
   const liveFact=async(id:string,args?:Record<string,unknown>)=>execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,...(args?{arguments:args}:{})}}]);
   const saved=await callLive('audio.source.edit',{operation:'save',id:'',revision:0,definition:{name:'Headless radio',kind:'stream',stream:{endpoint:liveSoundFixture.endpoint}}});
   const sourceId=saved.execution!.selected!.output!.id as string;
   const source=await liveFact('audio.source.stream',{id:sourceId,offset:0});if((factReply(source).value as {endpoint:string}).endpoint!==liveSoundFixture.endpoint)throw new Error('Saved live endpoint differs');
   const silent=await liveFact('runtime.audioBudget');if((factReply(silent).value as {sources:number}).sources!==0)throw new Error('Saving a live source must remain passive');
   const emitter=await liveFact('object.audioEmitter',{target:'book',emitter:'radio'});
   const bound=await callLive('object.audioEmitter.edit',{operation:'configure',target:'book',revision:(factReply(emitter).value as {revision:number}).revision,emitter:'radio',definition:{source:sourceId,part:'',joint:'',position:{x:0,y:0,z:0},role:'media',spatial:true,gain:0,distance:{minimum:.2,maximum:10}}});
   const started=await callLive('audio.start',{target:'book',emitter:'radio',loop:false,lifetime:'room'});const sample=started.execution!.selected!.output as SoundSample,instance=sample.identity.instance;
   if(sample.playback.seconds<=0||sample.playback.lifetime!=='room')throw new Error('Live start was not confirmed by native PCM consumption');
   const control=async(operation:string)=>{const observed=await liveFact('audio.instance',{target:'book',instance});return callLive('audio.control',{operation,target:'book',instance,revision:(factReply(observed).value as SoundSample).revision});};
   const paused=await control('pause');await new Promise(r=>setTimeout(r,2500));const resumed=await control('resume');await new Promise(r=>setTimeout(r,300));
   const live=await liveFact('audio.live',{target:'book',instance});const counters=factReply(live).value as {skippedSeconds:number;receivedSeconds:number;bufferedSeconds:number};
   if(counters.skippedSeconds<1.5||counters.receivedSeconds<=counters.skippedSeconds||counters.bufferedSeconds>2)throw new Error('Live resume did not resynchronize with bounded, visible gaps');
   const stopped=await control('stop');const terminal=await liveFact('audio.live',{target:'book',instance});if((factReply(terminal).value as {sourcePhase:string}).sourcePhase!=='closed')throw new Error('Stopped live source is not closed');
   const released=await liveFact('runtime.audioBudget');if((factReply(released).value as {sources:number}).sources!==0)throw new Error('Stopped live source retained its connection');
   await writeFile(join(directory,'world-live-audio.json'),JSON.stringify({boundary:'Shared headless agent action/fact path, real native MP3 decoder and consumption with explicit offline HTTP fixture. No sockets, provider or headset proof.',saved,source,silent,bound,started,paused,resumed,live,stopped,terminal,released},null,2));
  }
 }
 if(!probeOwner.identityOnly){
 const gripBefore=lease.state();
 const gripSearch=await execute([{action:'catalog',catalog:{operation:'search',query:'Configure construction grip snapping',offset:0}}]);
 const gripDefinition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'room.selection.snapSettings',version:1}}]);
 const gripRead=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.selection.snapping',version:1}}]);
 const gripSettings=gripRead.catalog?.value as Record<string,unknown>;
 if(gripSettings.mode!=='off')throw new Error('Fresh room must start with grip snapping disabled');
 const gripArguments={...gripSettings,mode:'join',distance:.06,turnStep:90,breakForce:45,breakTorque:3};
 const gripAfter=await execute([{action:'execution',execution:{operation:'start',call:{id:'room.selection.snapSettings',version:1,arguments:gripArguments}}}]);
 if(gripAfter.execution?.selected?.phase!=='completed'||gripAfter.execution.selected.output?.mode!=='join'||gripAfter.execution.selected.output?.stateId===gripSettings.stateId)throw new Error('Grip snapping configuration did not advance its guard');
 const gripPreview=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.selection.snapPreview',version:1}}]);
 if((gripPreview.catalog?.value as {active:boolean}).active)throw new Error('Unheld handle reported a snap preview');
 const gripReset=await execute([{action:'execution',execution:{operation:'start',call:{id:'room.selection.snapSettings',version:1,arguments:{...gripAfter.execution.selected.output,mode:'off'}}}}]);
 if(gripReset.execution?.selected?.phase!=='completed')throw new Error('Could not return grip snapping to off');
 await writeFile(join(directory,'grip-snapping.json'),JSON.stringify({boundary:'Real Unity shared configuration and preview facts. Actual grip matching, save, cancellation and Undo are tested separately in PlayMode; no headset proof.',before:gripBefore,search:gripSearch,definition:gripDefinition,read:gripRead,arguments:gripArguments,after:gripAfter,preview:gripPreview,reset:gripReset},null,2));
 }
 const finalIdentity=await readNativeObjectIdentity(execute);await writeFile(join(directory,'native-object-identity.json'),JSON.stringify(finalIdentity,null,2));
 await writeFile(join(directory,'journey.json'),JSON.stringify({version:1,boundary:'Real Unity Editor app and shared room protocol; no Quest input, WebView, scan or Store proof',providerUsed:!!prompt,sparseObjectView:!!probeOwner.sparseObjectView,motionOnly:!!probeOwner.motionOnly,steeringOnly:!!probeOwner.steeringOnly,identityOnly:!!probeOwner.identityOnly,initial,observations,readSets,outcome},null,2));
 console.log(JSON.stringify({providerUsed:!!prompt,observations:observations.length,output:join(directory,'journey.json')}));
}catch(error){
 await writeFile(join(directory,'journey-failure.json'),JSON.stringify({error:error instanceof Error?error.stack:String(error),observations,readSets},null,2));
 transport.checkHealth();throw error;
}finally{await transport.close();}

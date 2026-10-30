// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Offline peer contracts. No browser, microphone, provider or native runtime.
import {afterAll,afterEach,beforeAll,describe,expect,it,vi} from 'vitest';
import {GoogleGenAI,Modality} from '@google/genai/web';
import {createHash} from 'node:crypto';
import {mkdtemp,readFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {dirname,join,resolve} from 'node:path';
import {firstLearnerLive,LIVE_REQUEST,LIVE_REPLY} from './first-learner-live.mjs';
import {LiveInputContext} from '../src/core-sdk/media/liveInputContext';
import {getLiveCostControlConfig} from '../shared/liveCostControls';
import {getLiveConversationThinkingConfig,getLiveRealtimeInputConfig} from '../src/core-sdk/media/liveModelCompatibility';
import {LIVE_INPUT_CONTEXT_INSTRUCTION,buildLiveInputTiming} from '../shared/prompts/liveinput';

const disk=vi.hoisted(()=>({beforeWrite:null}));
vi.mock('node:fs/promises',async importOriginal=>{
  const actual=await importOriginal();
  return {...actual,writeFile:async(...args)=>{await disk.beforeWrite?.(...args);return actual.writeFile(...args);}};
});
const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
const key='offline-live-peer-contract-not-a-real-key';
const socketUrl=`wss://generativelanguage.googleapis.com//ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent?key=${key}`;
const pcmA=Buffer.alloc(800,1),pcmB=Buffer.alloc(1000,2);
const audio=bytes=>({realtimeInput:{audio:{mimeType:'audio/pcm;rate=16000',data:bytes.toString('base64')}}});
const start={realtimeInput:{activityStart:{}}},end={realtimeInput:{activityEnd:{}}};
let directory,serializedSetup;

class Route {
  sent=[];closures=[];clientMessages=[];
  constructor(url=socketUrl){this.address=url;}
  url(){return this.address;}
  onMessage(handler){this.message=handler;}
  onClose(handler){this.closed=handler;}
  send(value){this.sent.push(JSON.parse(value));this.serverSend?.(value);}
  close(options){this.closures.push(options);return Promise.resolve();}
  receive(value){const raw=typeof value==='string'?value:JSON.stringify(value);this.clientMessages.push(JSON.parse(raw));this.pending=Promise.resolve(this.message(raw));return this.pending;}
  drain(){return this.pending||Promise.resolve();}
  disconnect(){this.closed?.(1000,'Normal offline completion');}
}
function offlineSdk(route){
  // Explicit browser SDK + inert WebSocket: no socket/network fallback exists.
  vi.stubGlobal('WebSocket',class {
    constructor(url){
      expect(url).toBe(socketUrl);
      route.serverSend=value=>queueMicrotask(()=>this.onmessage?.({data:new Blob([value])}));
      queueMicrotask(()=>this.onopen?.({}));
    }
    send(value){route.receive(value);}
    close(){route.disconnect();this.onclose?.({code:1000,reason:''});}
  });
  vi.stubGlobal('fetch',vi.fn(()=>{throw new Error('These SDK contracts must never use HTTP');}));
  return new GoogleGenAI({apiKey:key,httpOptions:{apiVersion:'v1alpha'}});
}
async function connect(ai,onmessage=()=>{}){
  const registry=JSON.parse(await readFile(new URL('../public/gemini-models.json',import.meta.url),'utf8'));
  const model=registry.audio.conversation;
  return ai.live.connect({model,config:{
    ...getLiveCostControlConfig(),responseModalities:[Modality.AUDIO],
    systemInstruction:'The English learner studies Spanish. We already made an apple and a tree.',
    inputAudioTranscription:{},outputAudioTranscription:{},
    realtimeInputConfig:getLiveRealtimeInputConfig(false,true),
    thinkingConfig:getLiveConversationThinkingConfig(model),
    speechConfig:{voiceConfig:{prebuiltVoiceConfig:{voiceName:'Kore'}}},
  },callbacks:{onmessage,onerror:error=>{throw error;}}});
}
async function peer(authorized=true,syntheticSpeech){
  const errors=[],live=await firstLearnerLive({fakeKey:key,errors,out:directory,syntheticSpeech});
  const grantPermissions=vi.fn(async()=>{});
  if(authorized)await live.arm({evaluate:async()=>({tracks:[]}),context:()=>({grantPermissions}),url:()=> 'http://127.0.0.1:5178/'});
  return {live,errors,grantPermissions};
}
async function open(live,setup=serializedSetup){
  const route=new Route();expect(live.handle(route,{handle:()=>false})).toBe(true);
  await route.receive(structuredClone(setup));return route;
}
async function complete(){
  const value=await peer(),route=await open(value.live);
  await route.receive(start);await route.receive(audio(pcmA));await route.receive(audio(pcmB));await route.receive(end);
  expect(value.errors).toEqual([]);return {...value,route};
}
function handoff(){
  let time=100;const context=new LiveInputContext(()=>time);
  context.recordAudio(pcmA.toString('base64'));time+=37;context.recordAudio(pcmB.toString('base64'));
  const media=context.finish();expect(media.complete).toBe(true);
  return {contents:[{role:'user',parts:[{text:LIVE_REQUEST},{text:LIVE_INPUT_CONTEXT_INSTRUCTION},
    {text:buildLiveInputTiming(media.packets)},{inlineData:{mimeType:'audio/wav',data:media.audio.data}}]}]};
}
function wavePart(body){return body.contents.at(-1).parts.find(part=>part.inlineData)?.inlineData;}
function mutateWave(body,change){const part=wavePart(body),bytes=Buffer.from(part.data,'base64');change(bytes);part.data=bytes.toString('base64');}
function mutateTiming(body,change){const part=body.contents.at(-1).parts.find(part=>part.text?.startsWith('Audio delivery timeline'));const prefix=part.text.indexOf(': ')+2;const rows=JSON.parse(part.text.slice(prefix));change(rows);part.text=part.text.slice(0,prefix)+JSON.stringify(rows);}
function speechFixture(rate=48000,samples=rate/10){
  const wav=Buffer.alloc(44+samples*2);wav.write('RIFF');wav.writeUInt32LE(wav.length-8,4);wav.write('WAVEfmt ',8);
  wav.writeUInt32LE(16,16);wav.writeUInt16LE(1,20);wav.writeUInt16LE(1,22);wav.writeUInt32LE(rate,24);wav.writeUInt32LE(rate*2,28);
  wav.writeUInt16LE(2,32);wav.writeUInt16LE(16,34);wav.write('data',36);wav.writeUInt32LE(samples*2,40);wav.fill(1,44);
  return {wav,sha256:sha(wav),description:'Deterministic PCM fixture; no recognition claim.'};
}

beforeAll(async()=>{
  directory=await mkdtemp(join(tmpdir(),'maestro-live-peer-'));
  const route=new Route();route.onMessage(value=>{serializedSetup=JSON.parse(value);});
  const session=await connect(offlineSdk(route));session.close();vi.unstubAllGlobals();
});
afterEach(()=>{disk.beforeWrite=null;vi.unstubAllGlobals();});
afterAll(async()=>{
  expect(dirname(resolve(directory))).toBe(resolve(tmpdir()));
  expect(directory.split(/[\\/]/).at(-1)).toMatch(/^maestro-live-peer-/);
  await rm(directory,{recursive:true,force:true});
});

describe('first-learner Live offline peer',()=>{
  it('accepts actual pinned SDK serialization and completes only after the manual PCM turn',async()=>{
    const {live,errors,grantPermissions}=await peer();
    expect(grantPermissions).toHaveBeenCalledWith(['microphone'],{origin:'http://127.0.0.1:5178'});
    const route=new Route(),messages=[];expect(live.handle(route,{handle:()=>false})).toBe(true);
    const session=await connect(offlineSdk(route),message=>messages.push(message));
    expect(route.clientMessages[0]).toEqual(serializedSetup);
    expect(Object.keys(serializedSetup.setup).sort()).toEqual([
      'contextWindowCompression','generationConfig','inputAudioTranscription','model',
      'outputAudioTranscription','realtimeInputConfig','systemInstruction',
    ]);
    session.sendRealtimeInput({activityStart:{}});
    session.sendRealtimeInput({audio:{mimeType:'audio/pcm;rate=16000',data:pcmA.toString('base64')}});
    session.sendRealtimeInput({audio:{mimeType:'audio/pcm;rate=16000',data:pcmB.toString('base64')}});
    await route.drain();
    expect(route.sent).toEqual([{setupComplete:{}}]);
    session.sendRealtimeInput({activityEnd:{}});
    await route.drain();await new Promise(done=>setImmediate(done));
    expect(messages.find(m=>m.serverContent?.inputTranscription)?.serverContent.inputTranscription.text).toBe(LIVE_REQUEST);
    expect(messages.find(m=>m.serverContent?.outputTranscription)?.serverContent.outputTranscription.text).toBe(LIVE_REPLY);
    expect(messages.at(-1).serverContent.turnComplete).toBe(true);
    expect(live.assertPlannerMedia(handoff())).toMatchObject({samples:900,packets:2});
    session.close();await live.settled();expect(errors).toEqual([]);
    expect(live.evidence().sessions[0]).toMatchObject({starts:1,ends:1,audioBytes:1800,packets:2,closed:true});
  });
  it('writes a canonical bounded synthetic microphone fixture with silence around the signal',async()=>{
    const {live}=await peer(),fixture=live.evidence().fixture,bytes=await readFile(fixture.file);
    expect(bytes.toString('ascii',0,4)).toBe('RIFF');expect(bytes.readUInt32LE(4)).toBe(bytes.length-8);
    expect(bytes.toString('ascii',8,16)).toBe('WAVEfmt ');expect(bytes.readUInt16LE(20)).toBe(1);
    expect(bytes.readUInt16LE(22)).toBe(1);expect(bytes.readUInt32LE(24)).toBe(48000);expect(bytes.readUInt16LE(34)).toBe(16);
    expect(bytes.toString('ascii',36,40)).toBe('data');expect(bytes.readUInt32LE(40)).toBe(48000*30*2);
    expect(bytes.subarray(44,44+48000*2).some(Boolean)).toBe(false);
    expect(bytes.subarray(44+48000*2,44+48000*9*2).some(Boolean)).toBe(true);
    expect(bytes.subarray(44+48000*9*2).some(Boolean)).toBe(false);
  });
  it.each([24000,48000])('pads verified %i Hz speech without modifying source samples',async rate=>{
    const source=speechFixture(rate),original=Buffer.from(source.wav),{live}=await peer(true,source);
    source.wav.fill(0);
    const fixture=live.evidence().fixture,bytes=await readFile(fixture.file);
    expect(fixture.source).toMatchObject({kind:'local-speech',sha256:sha(original),sampleRate:rate,samples:rate/10});
    expect(bytes.length).toBe(44+rate*30*2);expect(bytes.readUInt32LE(24)).toBe(rate);
    expect(bytes.subarray(44,44+rate*2).some(Boolean)).toBe(false);
    expect(bytes.subarray(44+rate*2,44+rate*2+original.length-44).equals(original.subarray(44))).toBe(true);
    expect(bytes.subarray(44+rate*2+original.length-44).some(Boolean)).toBe(false);
    expect((await readFile(live.launchArgs[1].split('=')[1].replace(/%noloop$/,''))).equals(bytes)).toBe(true);
  });
  it('accepts the maintained locally synthesized speech with its committed hash',async()=>{
    const metadata=JSON.parse(await readFile(new URL('../test-fixtures/audio/first-learner-apple-request.json',import.meta.url),'utf8'));
    const wav=await readFile(new URL('../test-fixtures/audio/first-learner-apple-request.wav',import.meta.url));
    expect(metadata.text).toBe(LIVE_REQUEST);
    const {live}=await peer(true,{wav,sha256:metadata.sha256,description:'Local SAPI speech; transcript still scripted.'});
    expect(live.evidence().fixture.source).toMatchObject({kind:'local-speech',sha256:metadata.sha256,sampleRate:48000,samples:275496});
  });
  it.each([
    ['hash mismatch',source=>{source.sha256='0'.repeat(64);}],
    ['wrong channels',source=>{source.wav.writeUInt16LE(2,22);source.sha256=sha(source.wav);}],
    ['wrong RIFF size',source=>{source.wav.writeUInt32LE(0,4);source.sha256=sha(source.wav);}],
    ['silent source',source=>{source.wav.fill(0,44);source.sha256=sha(source.wav);}],
    ['unsupported sample rate',()=>speechFixture(16000)],
    ['over twelve seconds',()=>speechFixture(24000,24000*12+1)],
  ])('rejects malformed speech: %s',async(_name,change)=>{
    let source=speechFixture();source=change(source)||source;
    await expect(peer(true,source)).rejects.toThrow();
  });
  it('awaits verified active-file silence before replying, preserving original fixture and actual sent PCM',async()=>{
    const {live,errors}=await peer(),route=await open(live),fixture=live.evidence().fixture;
    const original=await readFile(fixture.file),active=live.launchArgs[1].split('=')[1].replace(/%noloop$/,'');
    expect(active).not.toBe(fixture.file);expect((await readFile(active)).equals(original)).toBe(true);
    let releaseWrite,announceWrite;const writing=new Promise(done=>{announceWrite=done;}),hold=new Promise(done=>{releaseWrite=done;});
    disk.beforeWrite=async path=>{expect(path).toBe(active);announceWrite();await hold;};
    // Queue a complete turn without awaiting between packets: asynchronous disk
    // work must not reorder or lose the authentic input envelopes.
    route.receive(start);route.receive(audio(pcmA));route.receive(audio(pcmB));const ending=route.receive(end);
    await writing;
    expect(route.sent).toEqual([{setupComplete:{}}]);expect(live.evidence().sessions[0].turnComplete).toBe(false);
    expect(live.evidence().inputTransition).toMatchObject({state:'writing',capturedPcmSha256:sha(Buffer.concat([pcmA,pcmB]))});
    expect((await readFile(active)).equals(original)).toBe(true);
    releaseWrite();await ending;
    const silence=await readFile(active),evidence=live.evidence();
    expect(silence.subarray(0,44).equals(original.subarray(0,44))).toBe(true);expect(silence.subarray(44).some(Boolean)).toBe(false);
    expect((await readFile(fixture.file)).equals(original)).toBe(true);expect(sha(original)).toBe(fixture.sha256);
    expect(evidence.inputTransition).toMatchObject({state:'silence',originalFixtureSha256:sha(original),verifiedOriginalSha256:sha(original),verifiedActiveSha256:sha(silence)});
    expect(evidence.inputTransition.completedAt).toBeLessThanOrEqual(evidence.sessions[0].completedAt);
    expect(route.sent.at(-1)).toEqual({serverContent:{turnComplete:true}});
    expect(live.assertPlannerMedia(handoff()).pcmSha256).toBe(sha(Buffer.concat([pcmA,pcmB])));expect(errors).toEqual([]);
  });
  it('fails closed on an asynchronous silence-write error and ignores queued later input',async()=>{
    const {live,errors}=await peer(),route=await open(live),fixture=live.evidence().fixture,original=await readFile(fixture.file);
    disk.beforeWrite=async()=>{await Promise.resolve();throw new Error('Simulated active input write failure');};
    route.receive(start);route.receive(audio(pcmA));route.receive(audio(pcmB));route.receive(end);await route.receive(start);
    expect(errors).toHaveLength(1);expect(errors[0]).toContain('Simulated active input write failure');
    expect(route.sent).toEqual([{setupComplete:{}}]);expect(route.closures).toEqual([{code:1008,reason:'Offline Live contract rejected'}]);
    expect(live.evidence().sessions[0]).toMatchObject({starts:1,ends:1,turnComplete:false,pcmSha256:sha(Buffer.concat([pcmA,pcmB]))});
    expect(live.evidence().inputTransition.state).toBe('failed');expect((await readFile(fixture.file)).equals(original)).toBe(true);
  });
  it.each([
    ['before permission',null],['unknown setup field',s=>{s.setup.tools=[];}],
    ['automatic server VAD',s=>{s.setup.realtimeInputConfig.automaticActivityDetection.disabled=false;}],
    ['lost conversation',s=>{s.setup.systemInstruction.parts=[{text:'English and Spanish only'}];}],
    ['missing output transcription',s=>{delete s.setup.outputAudioTranscription;}],
  ])('rejects %s',async(_name,change)=>{
    const {live,errors}=await peer(change!==null),setup=structuredClone(serializedSetup);change?.(setup);
    const route=await open(live,setup);expect(errors.length).toBeGreaterThan(0);expect(route.closures.at(-1)?.code).toBe(1008);
    expect(route.sent).toEqual([]);
  });
  it.each([
    ['PCM before start',[audio(pcmA)]],['end before start',[end]],['duplicate start',[start,start]],
    ['duplicate end',[start,audio(pcmA),audio(pcmB),end,end]],
    ['PCM after end',[start,audio(pcmA),audio(pcmB),end,audio(pcmA)]],
    ['second turn',[start,audio(pcmA),audio(pcmB),end,start]],
    ['short utterance',[start,audio(pcmA),end]],['silent utterance',[start,audio(Buffer.alloc(1800)),end]],
    ['wrong sample rate',[start,{realtimeInput:{audio:{mimeType:'audio/pcm;rate=24000',data:pcmA.toString('base64')}}}]],
    ['unaligned PCM',[start,audio(Buffer.alloc(801))]],['oversized packet',[start,audio(Buffer.alloc(6402))]],
    ['unexpected client content',[{clientContent:{turns:[]}}]],['unknown realtime input',[{realtimeInput:{audioStreamEnd:true}}]],
    ['combined operations',[{realtimeInput:{activityStart:{},audio:{mimeType:'audio/pcm;rate=16000',data:pcmA.toString('base64')}}}]],
  ])('rejects %s',async(_name,inputs)=>{
    const {live,errors}=await peer(),route=await open(live);for(const input of inputs)await route.receive(input);
    expect(errors.length).toBeGreaterThan(0);expect(route.closures.at(-1)?.code).toBe(1008);
  });
  it('rejects duplicate setup and a second Live connection',async()=>{
    const {live,errors}=await peer(),route=await open(live);await route.receive(serializedSetup);
    expect(errors.length).toBeGreaterThan(0);const count=errors.length;await open(live);expect(errors.length).toBeGreaterThan(count);
  });
  it('forwards the real first TTS setup once and hands over subsequent messages',async()=>{
    const {live,errors}=await peer(false),route=new Route(),seen=[];
    const speech={handle:socket=>{socket.onMessage(value=>seen.push(JSON.parse(value)));socket.onClose(()=>{});return true;}};
    expect(live.handle(route,speech)).toBe(true);
    const setup={setup:{systemInstruction:{parts:[{text:'TTS peer validates this itself'}]}}};
    route.receive(setup);await route.receive(audio(pcmA));expect(seen).toEqual([setup,audio(pcmA)]);
    expect(live.evidence().sessions).toEqual([]);expect(errors).toEqual([]);
  });
  it('does not claim unrelated endpoints or credentials',async()=>{
    const {live}=await peer();for(const url of [socketUrl.replace(key,'wrong-key'),socketUrl.replace('wss:','ws:'),socketUrl.replace('googleapis.com','example.test'),socketUrl.replace('BidiGenerateContent','GenerateContent')]){
      const route=new Route(url);expect(live.handle(route,{handle:()=>{throw new Error('Must not route');}})).toBe(false);expect(route.message).toBeUndefined();
    }
  });
  it('waits for pending microphone acquisition even when all existing tracks have ended',async()=>{
    const {live}=await peer();
    const capture={attempts:[{state:'resolved'},{state:'pending'}],tracks:[{kind:'audio',readyState:'ended'}]};
    vi.stubGlobal('__firstLearnerCapture',()=>capture);
    const page={
      waitForFunction:async predicate=>{
        expect(predicate()).toBe(false);
        capture.attempts[1].state='resolved';
        capture.tracks.push({kind:'audio',readyState:'live'});
        expect(predicate()).toBe(false);
        capture.tracks[1].readyState='ended';
        expect(predicate()).toBe(true);
      },
      evaluate:async read=>read(),
    };
    expect(await live.assertReleased(page)).toBe(capture);
  });
  it.each([
    ['RIFF size',b=>b.writeUInt32LE(0,4)],['fmt chunk',b=>b.write('junk',12)],['fmt length',b=>b.writeUInt32LE(18,16)],
    ['floating-point format',b=>b.writeUInt16LE(3,20)],['byte rate',b=>b.writeUInt32LE(48000,28)],
    ['block alignment',b=>b.writeUInt16LE(4,32)],['data chunk',b=>b.write('JUNK',36)],
    ['data length',b=>b.writeUInt32LE(1,40)],['changed PCM',b=>{b[44]^=1;}],
  ])('rejects malformed delegated WAV: %s',async(_name,change)=>{
    const {live}=await complete(),body=handoff();mutateWave(body,change);expect(()=>live.assertPlannerMedia(body)).toThrow();
  });
  it.each([
    ['missing packet',rows=>rows.pop()],['offset gap',rows=>{rows[1].sampleOffset++;}],
    ['wrong length',rows=>{rows[0].samples++;}],['regressing time',rows=>{rows[1].atMs=-1;}],
    ['fractional time',rows=>{rows[1].atMs=.5;}],
  ])('rejects delegated timeline: %s',async(_name,change)=>{
    const {live}=await complete(),body=handoff();mutateTiming(body,change);expect(()=>live.assertPlannerMedia(body)).toThrow();
  });
  it.each([
    ['inline image',{inlineData:{mimeType:'image/jpeg',data:'/9j/2Q=='}}],
    ['file media',{fileData:{mimeType:'image/png',fileUri:'https://example.test/unsubmitted.png'}}],
    ['additional audio',{inlineData:{mimeType:'audio/pcm;rate=16000',data:pcmA.toString('base64')}}],
    ['invented frame label',{text:'Original Live frame 1; not actually submitted'}],
  ])('rejects unsubmitted microphone-only handoff media: %s',async(_name,part)=>{
    const {live}=await complete(),body=handoff();body.contents.at(-1).parts.push(part);expect(()=>live.assertPlannerMedia(body)).toThrow();
  });
});

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Offline SDK peer; production Live owns capture, turn completion and handoff.
// This peer replaces only remote SDK transport and the browser's microphone device.
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';

export const LIVE_REQUEST='Please remove only our apple tree. Keep our separate apple and everything else.';
export const LIVE_REPLY='Voy a pedir que quiten solo el árbol y conserven la manzana.\n[en] I will ask Maestro to remove only the tree and keep our apple.';
const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
function pcmWav(pcm,rate) {
  const wav=Buffer.alloc(44+pcm.length);wav.write('RIFF');wav.writeUInt32LE(wav.length-8,4);wav.write('WAVEfmt ',8);
  wav.writeUInt32LE(16,16);wav.writeUInt16LE(1,20);wav.writeUInt16LE(1,22);wav.writeUInt32LE(rate,24);wav.writeUInt32LE(rate*2,28);
  wav.writeUInt16LE(2,32);wav.writeUInt16LE(16,34);wav.write('data',36);wav.writeUInt32LE(pcm.length,40);pcm.copy(wav,44);
  return wav;
}
export async function firstLearnerLive({fakeKey,errors,out,syntheticSpeech}) {
  // Protocol tests may use harmonics; the native speech-gate journey supplies a
  // separately verified local spoken fixture. Neither supplies recognized text.
  let rate=48000,source={kind:'harmonic'};const seconds=30;
  let speechPcm;
  if(syntheticSpeech!==undefined) {
    assert(Buffer.isBuffer(syntheticSpeech.wav),'Synthetic speech must provide WAV bytes');
    const bytes=Buffer.from(syntheticSpeech.wav);
    assert(bytes.length>44&&bytes.length%2===0&&bytes.length<=44+48000*12*2,'Synthetic speech must be bounded PCM');
    assert.equal(sha(bytes),syntheticSpeech.sha256,'Synthetic speech source hash mismatch');
    rate=bytes.readUInt32LE(24);assert(rate===24000||rate===48000,'Synthetic speech must use 24 or 48 kHz');
    speechPcm=bytes.subarray(44);assert(speechPcm.length<=rate*12*2&&speechPcm.some(Boolean),'Synthetic speech must be nonempty and at most 12 seconds');
    assert.deepEqual(bytes,pcmWav(speechPcm,rate),'Synthetic speech requires canonical PCM16 mono WAV');
    assert(typeof syntheticSpeech.description==='string'&&syntheticSpeech.description.length>0&&syntheticSpeech.description.length<=2000);
    source={kind:'local-speech',sha256:syntheticSpeech.sha256,description:syntheticSpeech.description,sampleRate:rate,samples:speechPcm.length/2};
  }
  const pcm=Buffer.alloc(rate*seconds*2);
  if(speechPcm)speechPcm.copy(pcm,rate*2);
  else for(let i=rate;i<rate*9;i++) {
    const t=i/rate,envelope=.7+.3*Math.sin(2*Math.PI*3*t);
    const voiced=Math.sin(2*Math.PI*180*t)+.35*Math.sin(2*Math.PI*360*t)+.15*Math.sin(2*Math.PI*720*t);
    pcm.writeInt16LE(Math.round(6500*envelope*voiced),2*i);
  }
  const wav=pcmWav(pcm,rate),silence=pcmWav(Buffer.alloc(pcm.length),rate);
  const file=resolve(out,'synthetic-live-microphone.wav');await writeFile(file,wav);
  const activeFile=resolve(out,'synthetic-live-microphone-input.wav');await writeFile(activeFile,wav);
  const sessions=[],packets=[],socketKinds=[];let authorized=false,mediaComparisons=0,closed=false,inputTransition=null;
  const fixture={file,sha256:sha(wav),sampleRate:rate,samples:rate*seconds,source,description:'One-second silence, local synthetic signal, then silence; transcript is scripted, not recognized.'};
  // New capture sessions restart Chrome's device file. Preserve the original
  // fixture, but replace the active file with silence before completing turn 1.
  // This does not assume Chrome rereads it: an extra Live connection still fails.
  const launchArgs=['--use-fake-device-for-media-stream','--use-file-for-fake-audio-capture='+activeFile+'%noloop'];
  const observeCapture=async context=>{
    // Observe real browser getUserMedia/MediaStreamTracks; do not replace ports,
    // AudioContext, Worklet, stream contents, controller callbacks or stores.
    await context.addInitScript(()=>{
      if(globalThis!==globalThis.top||!navigator.mediaDevices)return;
      const streams=[],attempts=[],media=navigator.mediaDevices,original=media.getUserMedia;
      Object.defineProperty(globalThis,'__firstLearnerCapture',{value:()=>({attempts:attempts.map(x=>({...x})),tracks:streams.flatMap(stream=>stream.getTracks().map(track=>({kind:track.kind,readyState:track.readyState,enabled:track.enabled})))}),configurable:false});
      media.getUserMedia=async function(constraints) {
        const attempt={audio:!!constraints?.audio,video:!!constraints?.video,state:'pending'};attempts.push(attempt);
        try {const stream=await original.call(this,constraints);attempt.state='resolved';streams.push(stream);return stream;}
        catch(error){attempt.state='rejected';throw error;}
      };
    });
  };
  const arm=async page=>{
    assert(!authorized,'Live permission is single-use in this scenario');
    const before=await page.evaluate(()=>globalThis.__firstLearnerCapture());
    assert.equal(before.tracks.length,0,'No successful microphone acquisition is expected before explicit Live permission');
    await page.context().grantPermissions(['microphone'],{origin:new URL(page.url()).origin});authorized=true;
    // Caller must now click the actual visible Start Live button.
  };
  const handle=(ws,speech)=>{
    const url=new URL(ws.url());
    if(url.protocol!=='wss:'||url.hostname!=='generativelanguage.googleapis.com'||!/^\/{1,2}ws\/google\.ai\.generativelanguage\.v1(?:alpha|beta)\.GenerativeService\.BidiGenerateContent$/.test(url.pathname)||url.searchParams.get('key')!==fakeKey)return false;
    let first=true,entry,failed=false,forwardedHandler,queue=Promise.resolve();
    const fail=error=>{if(failed)return;failed=true;if(entry)entry.error=String(error);errors.push('Scripted Live boundary: '+String(error.stack||error));void Promise.resolve(ws.close({code:1008,reason:'Offline Live contract rejected'})).catch(closeError=>errors.push('Scripted Live close: '+String(closeError)));};
    const processMessage=async message=>{
        if(failed)return;
        if(forwardedHandler){await forwardedHandler(message);return;}
        const bytes=Buffer.isBuffer(message)?message:Buffer.from(message);assert(bytes.length<128000,'Oversized SDK message');
        const value=JSON.parse(bytes.toString());
        if(first) {
          first=false;assert.deepEqual(Object.keys(value),['setup']);
          if(value.setup.inputAudioTranscription===undefined) {
            // Forward the actual first setup, exactly once, to the unchanged TTS peer.
            // No fabricated client message and no network server are involved.
            socketKinds.push('tts');
            assert(speech.handle({url:()=>ws.url(),send:v=>ws.send(v),close:v=>ws.close(v),
              onMessage:fn=>{assert(!forwardedHandler);forwardedHandler=fn;},onClose:fn=>ws.onClose(fn)}));
            assert(forwardedHandler,'TTS peer did not register its message handler');await forwardedHandler(message);return;
          }
          socketKinds.push('live');assert(authorized,'Live socket opened before explicit synthetic-microphone permission');
          const allowedSetup=new Set(['model','generationConfig','systemInstruction','inputAudioTranscription','outputAudioTranscription','realtimeInputConfig','contextWindowCompression']);
          assert(Object.keys(value.setup).every(key=>allowedSetup.has(key)),'Unexpected Live SDK setup field');
          assert.equal(sessions.length,0,'This bounded scenario permits exactly one Live SDK connection');
          assert.deepEqual(value.setup.inputAudioTranscription,{});assert.deepEqual(value.setup.outputAudioTranscription,{});
          assert.deepEqual(value.setup.generationConfig?.responseModalities,['AUDIO']);
          assert.equal(value.setup.realtimeInputConfig?.automaticActivityDetection?.disabled,true,'Production local speech boundary must own activity markers');
          const instruction=value.setup.systemInstruction?.parts?.map(p=>p.text||'').join('');
          assert(instruction?.includes('Spanish')&&instruction.includes('English'));
          assert(instruction.includes('apple')&&instruction.includes('tree'),'Live setup lost the existing text conversation');
          entry={id:1,openedAt:Date.now(),setupSha256:sha(bytes),starts:0,ends:0,audioBytes:0,packets:0,turnComplete:false,closed:false};sessions.push(entry);
          ws.onClose((code,reason)=>{entry.closed=true;entry.closedAt=Date.now();entry.closeCode=code;closed=true;if(!entry.turnComplete)fail(new Error('Live closed before its one scripted turn'));void ws.close({code:code||1000,reason:reason||''});});
          ws.send(JSON.stringify({setupComplete:{}}));return;
        }
        assert(entry,'Unclassified SDK socket');assert.deepEqual(Object.keys(value),['realtimeInput']);
        const input=value.realtimeInput;assert.equal(Object.keys(input).length,1,'One explicit Live input operation per SDK envelope');
        if(input.activityStart) {entry.startedAt=Date.now();assert.equal(entry.starts++,0);assert.equal(entry.ends,0);assert.deepEqual(input.activityStart,{});return;}
        if(input.audio) {
          assert.equal(entry.starts,1);assert.equal(entry.ends,0);assert.equal(input.audio.mimeType,'audio/pcm;rate=16000');
          const bytes=Buffer.from(input.audio.data,'base64');assert(bytes.length>0&&bytes.length<=6400&&bytes.length%2===0);
          entry.audioBytes+=bytes.length;entry.packets++;assert(entry.audioBytes<=16000*2*20&&entry.packets<=2048);
          packets.push(bytes);return;
        }
        assert.deepEqual(input,{activityEnd:{}});assert.equal(entry.starts,1);assert.equal(entry.ends++,0);assert(entry.audioBytes>=1600);
        const inputPcm=Buffer.concat(packets);assert(inputPcm.some(v=>v!==0),'Live captured only silence');entry.pcmSha256=sha(inputPcm);
        entry.capturedAt=Date.now();
        inputTransition={file:activeFile,state:'writing',startedAt:Date.now(),originalFixtureSha256:fixture.sha256,
          previousSha256:fixture.sha256,silenceSha256:sha(silence),bytes:silence.length,sampleRate:rate,samples:rate*seconds,capturedPcmSha256:entry.pcmSha256};
        await writeFile(activeFile,silence);
        const activeHash=sha(await readFile(activeFile)),originalHash=sha(await readFile(file));
        assert.equal(activeHash,inputTransition.silenceSha256,'Active microphone file did not become the verified silence WAV');
        assert.equal(originalHash,fixture.sha256,'Original microphone fixture changed');
        Object.assign(inputTransition,{state:'silence',completedAt:Date.now(),verifiedActiveSha256:activeHash,verifiedOriginalSha256:originalHash});
        assert(!failed&&!entry.closed,'Live closed during microphone transition');
        const response=Buffer.alloc(4800);for(let i=0;i<2400;i++)response.writeInt16LE(Math.round(200*Math.sin(2*Math.PI*330*i/24000)),2*i);
        entry.outputSha256=sha(response);entry.turnComplete=true;entry.completedAt=Date.now();
        ws.send(JSON.stringify({serverContent:{inputTranscription:{text:LIVE_REQUEST},modelTurn:{role:'model',parts:[{inlineData:{mimeType:'audio/pcm;rate=24000',data:response.toString('base64')}}]},outputTranscription:{text:LIVE_REPLY}}}));
        ws.send(JSON.stringify({serverContent:{turnComplete:true}}));
    };
    ws.onMessage(message=>{
      queue=queue.then(()=>processMessage(message)).catch(error=>{if(inputTransition?.state==='writing'){inputTransition.state='failed';inputTransition.failedAt=Date.now();}fail(error);});
      return queue;
    });return true;
  };
  const assertPlannerMedia=body=>{
    assert.equal(sessions.length,1);assert(sessions[0].turnComplete);
    const current=body.contents?.at(-1);assert.equal(current?.role,'user');
    const parts=current.parts,waves=parts.filter(p=>p.inlineData?.mimeType==='audio/wav');assert.equal(waves.length,1);
    assert.equal(parts.filter(p=>p.inlineData).length,1,'Microphone-only delegation must contain only its captured WAV');
    assert(!parts.some(p=>p.fileData),'This Live turn supplied no current file attachment');
    const wav=Buffer.from(waves[0].inlineData.data,'base64'),expected=Buffer.concat(packets);
    assert(wav.length>=44,'Delegated audio needs a complete PCM WAV header');
    assert.equal(wav.toString('ascii',0,4),'RIFF');assert.equal(wav.readUInt32LE(4),wav.length-8);assert.equal(wav.toString('ascii',8,16),'WAVEfmt ');
    assert.equal(wav.readUInt32LE(16),16);assert.equal(wav.readUInt16LE(20),1);assert.equal(wav.readUInt16LE(22),1);assert.equal(wav.readUInt32LE(24),16000);
    assert.equal(wav.readUInt32LE(28),32000);assert.equal(wav.readUInt16LE(32),2);assert.equal(wav.readUInt16LE(34),16);assert.equal(wav.toString('ascii',36,40),'data');
    assert.equal(wav.length,44+expected.length);assert.equal(wav.readUInt32LE(40),expected.length);assert.deepEqual(wav.subarray(44),expected,'Delegation changed, dropped or invented sent Live PCM');
    const prefix='Audio delivery timeline (milliseconds and WAV sample offsets): ';
    const timing=parts.filter(p=>typeof p.text==='string'&&p.text.startsWith(prefix));assert.equal(timing.length,1);
    const rows=JSON.parse(timing[0].text.slice(prefix.length));assert.equal(rows.length,packets.length);let offset=0,time=0;
    rows.forEach((p,i)=>{assert(Number.isSafeInteger(p.atMs)&&p.atMs>=time);assert.equal(p.sampleOffset,offset);assert.equal(p.samples,packets[i].length/2);time=p.atMs;offset+=p.samples;});
    assert(parts.some(p=>p.text?.startsWith('Original input submitted by this client during the delegated Live connection follows.')));
    assert(!parts.some(p=>p.text?.startsWith('Original Live frame ')),'Microphone-only Live must not invent camera frames');
    mediaComparisons++;return {pcmSha256:sha(expected),samples:offset,packets:rows.length,wavSha256:sha(wav)};
  };
  const assertReleased=async page=>{
    await page.waitForFunction(()=>{const x=globalThis.__firstLearnerCapture();return x.tracks.length>0&&x.tracks.every(t=>t.readyState==='ended')&&x.attempts.every(a=>a.state!=='pending');},{},{timeout:15000});
    const evidence=await page.evaluate(()=>globalThis.__firstLearnerCapture());assert(evidence.tracks.every(t=>t.kind==='audio'&&t.readyState==='ended'));assert(evidence.attempts.every(a=>a.state!=='pending'));return evidence;
  };
  const settled=async()=>{
    const deadline=Date.now()+15000;while(sessions.some(s=>!s.closed)&&Date.now()<deadline)await sleep(50);
    assert.equal(sessions.length,1);assert(closed&&sessions.every(s=>s.starts===1&&s.ends===1&&s.turnComplete&&!s.error));assert(mediaComparisons>0);
  };
  return {launchArgs,observeCapture,arm,handle,assertPlannerMedia,assertReleased,settled,evidence:()=>({fixture,inputTransition,authorized,sessions,socketKinds,mediaComparisons,
    boundary:'Real browser PCM capture/worklet, actual Live UI/controller, scripted SDK server and transcript. Synthetic microphone signal and scripted transcript; local model initialization is exercised separately, not recognition quality. No physical microphone, camera, provider quality, native AEC or audible-output qualification.'})};
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Offline SDK peer for normal triggered TTS only. No server connection is opened.
// A synthetic tone follows the normal SDK callback path. Socket completion does
// not establish renderer drain, audible output or speech quality.
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile} from 'node:fs/promises';

export async function firstLearnerSpeech(fakeKey,errors) {
  const source=await readFile(new URL('../src/core-sdk/media/triggerAudioAsset.ts',import.meta.url),'utf8');
  const match=source.match(/export const TRIGGER_AUDIO_PCM_24K = "([A-Za-z0-9+/=]+)"/);
  assert(match,'The maintained prerecorded TTS trigger must be available');
  const trigger=Buffer.from(match[1],'base64');
  const allowed=new Set(),sessions=[];
  const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
  const allowText=text=>{
    let fenced=false;
    for(const line of text.split('\n')) {
      if(line.trim().startsWith('```')) {fenced=!fenced;continue;}
      const content=line.replace(/^\[[a-z]{2}(?:-[A-Z]{2})?\]\s*/, '').trim();
      if(!fenced&&content)allowed.add(content);
    }
  };
  const handle=ws=>{
    const url=new URL(ws.url());
    // The pinned SDK retains the base URL's trailing slash before /ws.
    if(url.protocol!=='wss:'||url.hostname!=='generativelanguage.googleapis.com'||
      !/^\/{1,2}ws\/google\.ai\.generativelanguage\.v1(?:alpha|beta)\.GenerativeService\.BidiGenerateContent$/.test(url.pathname)||
      url.searchParams.get('key')!==fakeKey)return false;
    const entry={id:sessions.length+1,path:url.pathname,setup:false,triggerBytes:0,outputSamples:0,turnComplete:false,closed:false,lines:[]};
    sessions.push(entry);
    const fail=error=>{entry.error=String(error.stack||error);errors.push('Scripted TTS boundary: '+entry.error);void ws.close({code:1008,reason:'Offline TTS contract rejected'});};
    if(sessions.length>6) {fail(new Error('First-learner TTS session budget exceeded'));return true;}
    ws.onMessage(message=>{
      try {
        const bytes=Buffer.isBuffer(message)?message:Buffer.from(message);assert(bytes.length<100000,'Oversized TTS message');
        const value=JSON.parse(bytes.toString());
        if(value.setup) {
          assert(!entry.setup,'Repeated TTS setup');assert.deepEqual(Object.keys(value),['setup']);
          assert.deepEqual(value.setup.generationConfig?.responseModalities,['AUDIO']);
          assert.equal(value.setup.inputAudioTranscription,undefined,'Microphone Live is outside this TTS fixture');
          const instruction=value.setup.systemInstruction?.parts?.map(part=>part.text||'').join('');
          assert(instruction?.startsWith('You are a professional Text-to-Speech engine.'));
          const marker='TEXT TO READ:\n',offset=instruction.indexOf(marker);assert(offset>=0);
          const lines=instruction.slice(offset+marker.length).split('\n\n').map(line=>{
            const match=line.match(/^\[([a-z]{2}(?:-[A-Z]{2})?)\] ([\s\S]+)$/);assert(match,'Expected language-labelled TTS line');
            assert(['en','es','en-US','es-ES'].includes(match[1]));assert(allowed.has(match[2]),'Speech must refer only to already-scripted tutor output: '+match[2]);
            return {language:match[1],text:match[2]};
          });
          assert(lines.length>0&&lines.length<=6);entry.lines=lines;entry.setup=true;entry.setupSha256=hash(bytes);
          ws.send(JSON.stringify({setupComplete:{}}));return;
        }
        assert(entry.setup,'TTS audio arrived before setup');assert.deepEqual(Object.keys(value),['realtimeInput']);
        assert.deepEqual(Object.keys(value.realtimeInput),['audio']);
        const audio=value.realtimeInput.audio;assert.equal(audio.mimeType,'audio/pcm;rate=24000');
        const pcm=Buffer.from(audio.data,'base64');assert(pcm.length>0&&pcm.length<=4800&&pcm.length%2===0);
        assert(entry.triggerBytes+pcm.length<=trigger.length,'Only the prerecorded TTS trigger is admitted');
        assert.deepEqual(pcm,trigger.subarray(entry.triggerBytes,entry.triggerBytes+pcm.length),'TTS must use its bundled trigger, not live microphone input');
        entry.triggerBytes+=pcm.length;
        if(!entry.turnComplete) {
          entry.triggerSha256=hash(pcm);
          const samples=6000*entry.lines.length,tone=Buffer.alloc(samples*2);
          for(let i=0;i<samples;i++)tone.writeInt16LE(Math.round(300*Math.sin(2*Math.PI*330*i/24000)),i*2);
          entry.outputSamples=samples;entry.outputSha256=hash(tone);entry.turnComplete=true;
          ws.send(JSON.stringify({serverContent:{modelTurn:{role:'model',parts:[{inlineData:{mimeType:'audio/pcm;rate=24000',data:tone.toString('base64')}}]},outputTranscription:{text:entry.lines.map(line=>`[${line.language}] ${line.text}`).join('\n\n')}}}));
          ws.send(JSON.stringify({serverContent:{turnComplete:true}}));
        }
      } catch(error) {fail(error);}
    });
    ws.onClose((code,reason)=>{entry.closed=true;entry.closeCode=code;if(!entry.turnComplete)fail(new Error('TTS closed before its scripted turn completed'));void ws.close({code:code||1000,reason:reason||''});});
    return true;
  };
  const settled=async()=>{
    const deadline=Date.now()+10000;
    while(sessions.some(session=>!session.closed)&&Date.now()<deadline)await new Promise(resolve=>setTimeout(resolve,50));
    assert(sessions.every(session=>session.setup&&session.turnComplete&&session.closed&&!session.error),'TTS protocol session did not finish cleanly');
  };
  return {allowText,handle,settled,evidence:()=>({boundary:'Scripted triggered-TTS SDK socket; prerecorded trigger and synthetic tone only. Socket completion does not establish renderer drain, audible output, microphone capture, speech quality, native audio or real provider behavior.',sessions})};
}

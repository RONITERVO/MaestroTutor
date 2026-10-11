// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validFactValue} from '../../../shared/behaviourFacts';
import {behaviourCatalog,behaviourFact} from '../../../shared/behaviourCatalog';
import {currentInputRequest} from '../../../shared/currentCapabilityInputs';

it('shares sound definitions and playback with both book authoring and agent programs',()=>{
 for(const id of ['audio.source.edit','object.audioEmitter.edit','audio.play','audio.start','audio.control']){
  const definition=capabilityDefinition(id)!;
  expect(definition).not.toBeNull();expect(validateCapabilityArguments(id,1,definition.example)).toBeNull();
 }
 expect(capabilityResources('audio.play',{target:'maestro',emitter:'beep'})).toEqual(['maestro']);
 expect(capabilityResources('audio.source.edit',capabilityDefinition('audio.source.edit')!.example!)).toEqual([]);
 expect(behaviourFact('audio.playback')?.features).toContain('worldAudio.v1');
 const emitter=capabilityDefinition('object.audioEmitter.edit')!;
 expect(currentInputRequest(emitter.input,emitter.example!)).toMatchObject({capability:'object.audioEmitter',arguments:{target:'maestro',emitter:'beep'}});
});

it('keeps continuous playback lifetime and exact instance controls explicit',()=>{
 const start=capabilityDefinition('audio.start')!,control=capabilityDefinition('audio.control')!;
 expect(capabilityResources('audio.start',start.example!)).toEqual(['maestro']);
 expect(currentInputRequest(control.input,control.example!)).toMatchObject({capability:'audio.instance',arguments:{target:'maestro',instance:'0'.repeat(32)}});
 const invalid={...start.example,lifetime:'unowned'};expect(validateCapabilityArguments('audio.start',1,invalid)).not.toBeNull();
 expect(validateCapabilityArguments('audio.control',1,{...control.example,operation:'gain',gain:1.1})).not.toBeNull();
 const event=behaviourCatalog.events.find(e=>e.id==='audio.instance.changed');
 expect(event?.features).toContain('worldAudio.v1');expect(event?.input).toBeDefined();
 expect(behaviourFact('audio.instance')?.features).toContain('worldAudio.v1');
});

it('accepts ordinary user sound names and rejects envelopes beyond the sound duration',()=>{
 const id='audio.source.edit',args=capabilityDefinition(id)!.example!;
 const definition=args.definition as {name:string;tone:{attack:number;release:number;seconds:number}};
 for(const name of ['Cafe beep','Happy puppy','Pencil click']){definition.name=name;expect(validateCapabilityArguments(id,1,args)).toBeNull();}
 definition.tone.attack=.2;definition.tone.release=.2;definition.tone.seconds=.25;
 expect(validateCapabilityArguments(id,1,args)).not.toBeNull();
});

it('rejects contradictory sound attachment distances and anchors before dispatch',()=>{
 const id='object.audioEmitter.edit',args=capabilityDefinition(id)!.example!;
 const definition=args.definition as {part:string;joint:string;distance:{minimum:number;maximum:number};position:{x:number;y:number;z:number}};
 definition.distance.maximum=.1;expect(validateCapabilityArguments(id,1,args)).not.toBeNull();definition.distance.maximum=15;
 definition.part='speaker';definition.joint='Head';expect(validateCapabilityArguments(id,1,args)).not.toBeNull();definition.joint='';
 definition.position={x:9,y:9,z:0};expect(validateCapabilityArguments(id,1,args)).not.toBeNull();
});

it('does not represent unimplemented network sources or microphone privilege as supported audio',()=>{
 const source=capabilityDefinition('audio.source.edit')!.example!;
 (source.definition as Record<string,unknown>).kind='live';expect(validateCapabilityArguments('audio.source.edit',1,source)).not.toBeNull();
 const emitter=capabilityDefinition('object.audioEmitter.edit')!.example!;
 (emitter.definition as Record<string,unknown>).role='conversation';expect(validateCapabilityArguments('object.audioEmitter.edit',1,emitter)).not.toBeNull();
});

it('shares explicit imported clip identities without URLs or implicit playback',()=>{
 const definition={name:'Water tap',kind:'clip',clip:{assetHash:'a'.repeat(64),seconds:1}};
 const call={operation:'save',id:'',revision:0,definition};
 expect(validateCapabilityArguments('audio.source.edit',1,call)).toBeNull();
 expect(validateCapabilityArguments('audio.source.edit',1,{...call,definition:{...definition,clip:{...definition.clip,assetHash:'https://example.invalid/water.wav'}}})).not.toBeNull();
 expect(validateCapabilityArguments('audio.source.edit',1,{...call,definition:{...definition,tone:{}}})).not.toBeNull();
 for(const operation of ['select','refresh'])expect(validateCapabilityArguments('audio.import',1,{operation})).toBeNull();
 expect(validateCapabilityArguments('audio.import',1,{operation:'accept',requestId:'a'.repeat(32),assetHash:'b'.repeat(64)})).toBeNull();
 expect(validateCapabilityArguments('audio.import',1,{operation:'accept',requestId:'a'.repeat(32)})).not.toBeNull();
 expect(behaviourFact('audio.source.definition')?.version).toBe(2);
 expect(behaviourFact('audio.library')?.features).toContain('audioClips.v1');
});


it('keeps imported-file discovery within the same bounded book and program values',()=>{
 const entry={id:'b'.repeat(64),name:'"'.repeat(63),seconds:30,sampleRate:96000,channels:2};
 const page={ready:true,error:'',total:32,next:2,entries:[entry,entry]};
 expect(validFactValue('audio.library',page)).toBe(true);
 const library={...page,next:1,entries:[entry]};
 const receipt={requestId:'a'.repeat(32),assetHash:'',sourceId:'',revision:0,temporary:false,library};
 expect(validateCapabilityOutput('audio.import',1,receipt)).toBeNull();
 expect(validateCapabilityOutput('audio.import',1,{...receipt,library:page})).not.toBeNull();
});

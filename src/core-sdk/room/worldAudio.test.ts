// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {currentInputRequest} from '../../../shared/currentCapabilityInputs';

it('shares sound definitions and playback with both book authoring and agent programs',()=>{
 for(const id of ['audio.source.edit','object.audioEmitter.edit','audio.play']){
  const definition=capabilityDefinition(id)!;
  expect(definition).not.toBeNull();expect(validateCapabilityArguments(id,1,definition.example)).toBeNull();
 }
 expect(capabilityResources('audio.play',{target:'maestro',emitter:'beep'})).toEqual(['maestro']);
 expect(capabilityResources('audio.source.edit',capabilityDefinition('audio.source.edit')!.example!)).toEqual([]);
 expect(behaviourFact('audio.playback')?.features).toContain('worldAudio.v1');
 const emitter=capabilityDefinition('object.audioEmitter.edit')!;
 expect(currentInputRequest(emitter.input,emitter.example!)).toMatchObject({capability:'object.audioEmitter',arguments:{target:'maestro',emitter:'beep'}});
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

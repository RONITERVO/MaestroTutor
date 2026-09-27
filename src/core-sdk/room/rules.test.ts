import {describe,it,expect} from 'vitest';
import {readFileSync} from 'node:fs';
import {validRuleRequest,validRuleView,validSequence,newRuleStep,ruleActions,ruleGestures,ruleEvents,ruleConditions,rulePolicies,ruleMounts} from './rules';
import nativeView from '../../../test-fixtures/browser/ruleBookState.json';
import {parseRoomCommands} from './roomAgent';
import {sequenceProgram,simpleProgramSteps} from './programs';
const id='a'.repeat(32),step='b'.repeat(32);
const sequence=()=>({id,name:'Wave',interruption:0,repeat:false,program:JSON.stringify(sequenceProgram([{...newRuleStep(1),id:step}]))});
describe('shared behaviour contract',()=>{
 it('reads the actual Unity-exported rule observation without dropping prop fields',()=>{expect(validRuleView(nativeView)).toBe(true);expect(simpleProgramSteps(nativeView.selected.program)![0].propId).toBeTruthy();expect(simpleProgramSteps(nativeView.selected.program)![0].propRotation!.w).toBe(1);});
 it('accepts one atomic edit with sequence, event and physical button',()=>{
  const rule={action:'edit',revision:4,edits:[{kind:'save',reference:'wave',sequence:{...sequence(),id:''}},{kind:'bind',binding:{id:'',sequenceId:'wave',sourceId:null,trigger:0,condition:0,cooldown:1,enabled:true,stopOnExit:true}},{kind:'button',target:'wave',mount:1}]};
  expect(validRuleRequest(rule)).toBe(true);expect(parseRoomCommands({commands:[{action:'rules',rule}]})).toHaveLength(1);
  expect(()=>parseRoomCommands({commands:[{action:'rules',rule},{action:'undo'}]})).toThrow();
  expect(validRuleRequest({...rule,revision:undefined})).toBe(false);
 });
 it('preserves props and stable identities and rejects invalid step edits',()=>{
  const data=sequence(),steps=simpleProgramSteps(data.program)!;
  const valid=()=>validSequence({...data,program:JSON.stringify(sequenceProgram(steps))});
  Object.assign(steps[0],{propId:'c'.repeat(32),propHand:1,propRelease:2,propOffset:{x:.1,y:0,z:0},propRotation:{x:0,y:0,z:0,w:1},propReleaseAt:.5});
  expect(valid()).toBe(true);steps.push({...steps[0]});expect(valid()).toBe(false);
  steps.pop();steps[0].seconds=-1;expect(valid()).toBe(false);
  steps[0]={...newRuleStep(8),id:step,targetId:'d'.repeat(32),seconds:0};expect(valid()).toBe(true);
 });
 it('keeps native and web action/event enum identities aligned',()=>{
  const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Runtime/Rules/RuleDocument.cs','utf8');
  for(const [name,values] of Object.entries({RuleActionKind:ruleActions,RuleGesture:ruleGestures,RuleEventKind:ruleEvents,RuleCondition:ruleConditions,RuleInterruption:rulePolicies,ButtonMount:ruleMounts})) {
   const match=source.match(new RegExp(`enum ${name} \{([^}]+)\}`));expect(match).not.toBeNull();expect(match![1].split(',').map(s=>s.trim().toLowerCase())).toEqual(values.map(s=>s.replace(/[ -]/g,'').toLowerCase()));
  }
 });
});


it('rejects inherited property names as behaviour edit kinds without throwing',()=>{
 for(const kind of ['__proto__','constructor','toString'])expect(validRuleRequest({action:'edit',revision:1,edits:[{kind,target:id}]})).toBe(false);
});

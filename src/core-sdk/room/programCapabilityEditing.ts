// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,type CapabilityInvocation} from '../../../shared/capabilities';
import {currentInputLocations,currentInputFields,currentInputFieldPath,currentInputRequest} from '../../../shared/currentCapabilityInputs';
import {defaultDataValue} from '../../../shared/programValues';
import {parseProgram,type BehaviourProgram,type Expression,type ProgramNode} from './programs';
import {visitProgramNodes} from './programTraversal';
export type ProgramCapabilityInputs={kind:'snapshot'}|{kind:'current';fields:string[]};
/** Produce ordinary, editable program nodes. No hidden executor or retry policy. */
export function insertProgramCapability(source:string,call:CapabilityInvocation,inputs:ProgramCapabilityInputs={kind:'snapshot'},functionName?:string):BehaviourProgram {
 if(inputs.kind!=='snapshot'&&inputs.kind!=='current')throw new Error('Choose snapshot or current program inputs.');
 const parsed=parseProgram(source);if(!parsed.program)throw new Error(parsed.error??'Invalid draft.');
 const invalid=validateCapabilityArguments(call.id,call.version,call.arguments);if(invalid)throw new Error(invalid);
 const definition=capabilityDefinition(call.id)!;if(definition.domain==='workspace')throw new Error('Workspace maintenance cannot run as a room behaviour.');
 const program=parsed.program,entry=program.functions.find(fn=>fn.name===(functionName??program.entry));
 if(!entry)throw new Error('Choose an existing function for this action.');
 const ids=new Set<string>();for(const fn of program.functions)visitProgramNodes(fn.body,node=>ids.add(node.id));
 const fresh=(prefix:string,used:Set<string>)=>{let i=1;while(used.has(prefix+i))i++;const value=prefix+i;used.add(value);return value;};
 const invoke:Extract<ProgramNode,{op:'invoke'}>={id:fresh('action_',ids),op:'invoke',capability:call.id,version:call.version,arguments:structuredClone(call.arguments),bindings:{}};
 const nodes:ProgramNode[]=[];
 if(inputs.kind==='current'){
  const locations=currentInputLocations(definition.input,call.arguments),fields=currentInputFields(definition.input,call.arguments);
  if(!locations.length)throw new Error('This action has no current-input mapping.');
  if(!Array.isArray(inputs.fields)||new Set(inputs.fields).size!==inputs.fields.length||inputs.fields.some(path=>!fields.some(f=>f.path===path))||fields.some(f=>f.guard&&!inputs.fields.includes(f.path)))throw new Error('Choose known current fields and keep all concurrency guards current.');
  for(const location of locations){
   const chosen=Object.keys(location.mapping.fields).filter(key=>inputs.fields.includes(currentInputFieldPath(location,key)));if(!chosen.length)continue;
   const query=currentInputRequest(location.schema,location.value);if(query.operation!=='inspect')throw new Error('Expected a fact query.');
   const fact=behaviourFact(query.capability)!;
   const local=fresh('current_',new Set([...entry.locals,...entry.parameters].map(value=>value.name)));
   program.version=3;program.dataVersion=1;program.state??=[];program.events??=[];
   entry.locals.push({name:local,type:fact.type,initial:defaultDataValue(fact.type)});
   const read:Expression=query.arguments?{fact:fact.id,version:query.version,arguments:structuredClone(query.arguments),bindings:{}}:{fact:fact.id};
   nodes.push({id:fresh('read_current_',ids),op:'set',variable:local,value:read});
   for(const key of chosen){let value:Expression={var:local};for(const field of location.mapping.fields[key])value={op:'field',args:[value,{value:field}]};invoke.bindings[currentInputFieldPath(location,key)]=value;}
  }
 }
 nodes.push(invoke);entry.body.unshift(...nodes);
 program.resources=[...new Set([...program.resources,...capabilityResources(call.id,call.arguments)])];
 const result=parseProgram(JSON.stringify(program));if(!result.program)throw new Error(result.error??'The action exceeds this program’s limits.');return result.program;
}

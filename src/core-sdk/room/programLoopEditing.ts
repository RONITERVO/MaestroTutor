// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {parseProgram,type BehaviourProgram} from './programs';
import {visitProgramNodes} from './programTraversal';
export const repeatingProgramEditError='This behaviour still uses sequence Repeat. Convert it to a loop or turn Repeat off before adding event-program features. Your draft is unchanged.';
/** An explicit authoring operation. Preserve each cycle's function scope and returns. */
export function loopProgram(source:string,delaySeconds:number):BehaviourProgram {
 const parsed=parseProgram(source);if(!parsed.program)throw new Error(parsed.error??'Invalid draft.');
 if(parsed.program.version!==2)throw new Error('This program already supports loops. Edit its Repeat until stopped block directly.');
 if(!Number.isFinite(delaySeconds)||delaySeconds<.1||delaySeconds>3600)throw new Error('Choose a delay from 0.1 to 3600 seconds between cycles.');
 const program=parsed.program,originalEntry=program.entry;
 const names=new Set(program.functions.map(fn=>fn.name)),ids=new Set<string>();
 for(const fn of program.functions)visitProgramNodes(fn.body,node=>ids.add(node.id));
 const fresh=(prefix:string,used:Set<string>)=>{let i=1;while(used.has(prefix+i))i++;const name=prefix+i;used.add(name);return name;};
 const entry=fresh('repeat_entry_',names);
 program.version=3;program.state=[];program.events=[];program.entry=entry;
 program.functions.push({name:entry,returns:'void',parameters:[],locals:[],body:[{id:fresh('repeat_loop_',ids),op:'forever',body:[
  {id:fresh('repeat_cycle_',ids),op:'call',function:originalEntry,args:[]},
  {id:fresh('repeat_delay_',ids),op:'sleep',seconds:{value:delaySeconds}},
 ]}]});
 const checked=parseProgram(JSON.stringify(program));if(!checked.program)throw new Error(checked.error??'The loop exceeds this program’s limits.');return checked.program;
}
/** Every authoring surface must retain the sequence flag until explicitly changed. */
export function sequenceProgramEditError(repeat:boolean,source:string):string|null {
 const parsed=parseProgram(source);if(!parsed.program)return parsed.error??'Invalid program.';
 return repeat&&parsed.program.version===3?repeatingProgramEditError:null;
}

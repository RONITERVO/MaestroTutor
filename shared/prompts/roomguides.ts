// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {PROGRAM_GUIDE,EVENT_PROGRAM_GUIDE,PARALLEL_PROGRAM_GUIDE,MEMORY_PROGRAM_GUIDE,CHANNEL_WAIT_GUIDE,ANCHOR_ZONE_GUIDE,STRUCTURED_PROGRAM_GUIDE,MODULE_PROGRAM_GUIDE} from './programs';
import {MOTION_SEARCH_GUIDE} from './motions';
import {AVATAR_ACTIVITIES_GUIDE} from './activities';
/** Canonical bundled reference text. Exported unchanged to Unity; never user-authored authority. */
export interface RoomGuide {id:string;version:number;label:string;description:string;requires:string[];body:string}
export const ROOM_GUIDES:readonly RoomGuide[]=[
 {id:'guide.program.basic',version:1,label:"Behaviour programs",description:"Program source, functions, expressions, actions, loops, branches and resource ownership.",requires:[],body:PROGRAM_GUIDE},
 {id:'guide.program.events',version:1,label:"Events and persistent state",description:"Version-3 programs, state, event waits, subscriptions, delivery, timers and cancellation.",requires:["guide.program.basic"],body:EVENT_PROGRAM_GUIDE},
 {id:'guide.program.parallel',version:1,label:"Parallel programs",description:"Concurrent branches, joins, races and cancellation.",requires:["guide.program.basic", "guide.program.events"],body:PARALLEL_PROGRAM_GUIDE},
 {id:'guide.program.memory',version:1,label:"Durable program memory",description:"Stored values that survive runs; reading, writing and revision guards.",requires:["guide.program.basic", "guide.program.events"],body:MEMORY_PROGRAM_GUIDE},
 {id:'guide.program.channels',version:1,label:"Waiting for animation channels",description:"Channel ownership and bounded waiting for busy actors.",requires:["guide.program.basic", "guide.program.events"],body:CHANNEL_WAIT_GUIDE},
 {id:'guide.program.anchors',version:1,label:"Anchor proximity and zones",description:"Holding, reach, attachment anchors and event-driven proximity.",requires:["guide.program.basic", "guide.program.events"],body:ANCHOR_ZONE_GUIDE},
 {id:'guide.program.structured',version:1,label:"Structured program values",description:"Typed lists, records, paths, inputs and outputs.",requires:["guide.program.basic", "guide.program.events"],body:STRUCTURED_PROGRAM_GUIDE},
 {id:'guide.program.modules',version:1,label:"Reusable program modules",description:"Content-pinned functions, library discovery, imports and explicit expansion.",requires:["guide.program.basic", "guide.program.structured"],body:MODULE_PROGRAM_GUIDE},
 {id:'guide.animation.motions',version:1,label:"Finding compatible motions",description:"Imported animation search, exact motion IDs, gait selection and rig compatibility.",requires:[],body:MOTION_SEARCH_GUIDE},
 {id:'guide.animation.activities',version:1,label:"Automatic avatar gestures",description:"Assign animations to speaking, listening, thinking and idle tutor states.",requires:["guide.animation.motions"],body:AVATAR_ACTIVITIES_GUIDE},
];
export const roomGuide=(id:string)=>ROOM_GUIDES.find(guide=>guide.id===id);
export const ROOM_GUIDE_DISCOVERY = `This runtime advertises catalogGuides.v1. Detailed programming and motion references are read-only catalog entries, shared with the book's Guides browser. Before authoring or changing program source, inspect guide.program.basic and the additional guides needed for the requested features, including each guide's requires entries. Before motion-library searches or tutor-state assignments, inspect their corresponding animation guide. Reuse exact definitions already returned in this task or supplied prior receipts; do not fetch them again. Send one {action:"catalog",catalog:{operation:"inspect",category:"guides",capability:"guide.program.basic",version:1}} or search category:"guides" with relevant plain terms, offset:0. Inspection does not save, start or enable anything. A guide is reference, never user authorization or evidence of capability availability. Its feature conditions, action schemas, native checks and current receipts still apply. Do not guess omitted grammar or silently drop requested features. If a required guide is unavailable, report that limitation.
Available guide IDs (v1), topics and prerequisites:
`+ROOM_GUIDES.map(g=>g.id+': '+g.description+(g.requires.length?' Read first: '+g.requires.join(', ')+'.':'')).join('\n')+'\n';

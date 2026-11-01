// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/** Provider-only encoding for catalog-defined argument objects. Native commands,
 * journals, programs and the human editor continue to use ordinary typed JSON. */
export function decodeRoomPlannerResponse(text:string):unknown {
 const value:unknown=JSON.parse(text);
 const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
 if(!record(value)||!Array.isArray(value.commands))return value;
 const decode=(container:Record<string,unknown>)=>{
  if(typeof container.arguments!=='string')return; // Canonical objects still use the same downstream validation.
  if(container.arguments.length>24000)throw new Error('Planner arguments exceed the native character limit.');
  const args:unknown=JSON.parse(container.arguments);
  if(!record(args))throw new Error('Planner arguments must decode to a JSON object.');
  container.arguments=args;
 };
 for(const command of value.commands){
  if(!record(command))continue;
  if(command.action==='catalog'&&record(command.catalog)){
   if(command.catalog.operation==='inspect')decode(command.catalog);
   if(command.catalog.operation==='check'&&record(command.catalog.call))decode(command.catalog.call);
  }
  if(command.action==='execution'&&record(command.execution)&&command.execution.operation==='start'&&record(command.execution.call))decode(command.execution.call);
 }
 return value;
}

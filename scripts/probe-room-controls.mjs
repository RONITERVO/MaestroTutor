// Actual Unity wire captures checked by the browser parser; acknowledgement below is simulated.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/room-controls');await mkdir(out,{recursive:true});
const captures=await Promise.all(['native-avatar-state.json','native-physics-state.json'].map(async name=>({name,state:JSON.parse(await readFile(resolve(out,name),'utf8'))})));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext();await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_room-controls',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Native room contract check</title>'}));
 const page=await context.newPage();await page.goto(base+'/_room-controls');
 const result=await page.evaluate(async captures=>{
  const {RoomAgentClient}=await import('/src/platform/quest/roomAgentBridge.ts');
  const accepted=captures.map(capture=>({name:capture.name,accepted:new RoomAgentClient().receive(capture.state)}));
  const client=new RoomAgentClient(),state=captures[0].state;
  if(!client.receive(state))throw new Error('Native avatar wire rejected');
  const pending=client.request([{action:'avatarMotion',target:'maestro',operation:'stop'}]);
  const request=client.snapshot().request;
  client.receive({...state,revision:state.revision+1,ack:request.sequence,status:'Simulated stop acknowledgement',avatar:{...state.avatar,active:false,mode:'stopped',status:'Stopped'}});
  const receipt=await pending;
  const old=new RoomAgentClient();const legacy={...state};delete legacy.capabilities;old.receive(legacy);let rejected=false;
  try{await old.request([{action:'physicsRun',operation:'start'}]);}catch{rejected=true;}
  return {accepted,request,receipt:{ok:receipt.ok,status:receipt.status,mode:receipt.avatar.mode},legacyRejected:rejected,noLegacyDispatch:old.snapshot().request===null};
 },captures);
 assert(result.accepted.every(capture=>capture.accepted));assert.equal(result.request.version,2);assert.equal(result.request.conditions[0].id,'maestro');
 assert.equal(result.receipt.mode,'stopped');assert(result.legacyRejected&&result.noLegacyDispatch);
 await writeFile(resolve(out,'browser-wire-check.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify({accepted:result.accepted,legacyRejected:result.legacyRejected,acknowledgement:'simulated'}));
} finally {await browser.close();}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {afterEach,expect,it,vi} from 'vitest';
import native from '../../../test-fixtures/browser/roomCapture.json';
import {validRoomCaptureImage,validRoomCaptureMetadata,sameRoomCapture,type RoomCaptureImage} from '../../../shared/roomViewCapture';
import {RoomAgentClient} from './roomAgentClient';
import {runRoomActionTask,type RoomAgentState} from './roomAgent';
import {validateArchivedRoomTask} from '../backup/archive';
const image={capture:native.capture,data:native.data} as RoomCaptureImage;
const base:RoomAgentState={version:1,session:native.session,revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',created:[],objects:[],canUndo:false,canRedo:false,physicsRunning:false,capture:image.capture};
afterEach(()=>vi.useRealTimers());
it('accepts the exact native JPEG identity and rejects different pixels, dimensions and poses',()=>{
 expect(validRoomCaptureImage(image)).toBe(true);expect(validRoomCaptureMetadata(image.capture)).toBe(true);
 expect(validRoomCaptureImage({...image,data:image.data.slice(0,-12)+'AAAAAAAAAAAA'})).toBe(false);
 expect(validRoomCaptureImage({...image,capture:{...image.capture,sha256:'f'.repeat(64)}})).toBe(false);
 expect(validRoomCaptureImage({...image,capture:{...image.capture,width:1024}})).toBe(false);
 expect(validRoomCaptureMetadata({...image.capture,position:{x:NaN,y:0,z:0}})).toBe(false);
 expect(sameRoomCapture(image.capture,{...image.capture,position:{...image.capture.position,x:1}})).toBe(false);
});
it('keeps pixels separate, waits for the matching channel and clears them on session changes',async()=>{
 const client=new RoomAgentClient();expect(client.receive(base)).toBe(true);const lease=client.lease()!;const waiting=lease.capture!(image.capture.captureId);
 expect(client.receiveCapture({...native,session:'b'.repeat(32)})).toBe(false);
 expect(client.receiveCapture({...native,capture:{...native.capture,position:{x:4,y:1,z:0}}})).toBe(false);
 expect(client.receiveCapture(native)).toBe(true);expect(await waiting).toEqual(image);
 expect(client.snapshot()).toMatchObject({captureAck:image.capture.captureId});expect(JSON.stringify(client.getSnapshot())).not.toContain(image.data);
 const copy=client.captureImage(image.capture.captureId)!;copy.capture.position.x=999;expect(client.captureImage(image.capture.captureId)).toEqual(image);
 const stale=lease.capture!('b'.repeat(32));const rejected=expect(stale).rejects.toThrow('interrupted');client.cancel();await rejected;
 expect(client.captureImage(image.capture.captureId)).toBeNull();expect(client.snapshot()).not.toHaveProperty('captureAck');expect(client.receiveCapture(native)).toBe(false);
});
it('times out a missing image and observes explicit Stop while waiting',async()=>{
 vi.useFakeTimers();const client=new RoomAgentClient();client.receive(base);const lease=client.lease()!;
 const missing=expect(lease.capture!(image.capture.captureId)).rejects.toThrow('unavailable');await vi.advanceTimersByTimeAsync(5000);await missing;
 client.receive({...base,revision:2});const signal=new AbortController();const cancelled=expect(client.lease()!.capture!(image.capture.captureId,signal.signal)).rejects.toThrow('interrupted');signal.abort();await cancelled;
});
it('sends only this task\'s captured pixels through the existing provider and persists them before the next plan',async()=>{
 const runId='b'.repeat(32);const call={id:'room.view.capture',version:1,arguments:{}};
 const initial={...base,capabilities:['roomViewCapture.v1','catalog.v1','execution.v1','executionReceipts.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:runId,storageError:null}};
 const outputs=[JSON.stringify({commands:[{action:'execution',execution:{operation:'start',call}}]}),'{"commands":[]}'];
 const generate=vi.fn(async()=>{const text=outputs.shift()!;return(async function*(){yield{text,candidates:[{content:{role:'model',parts:[{text}]}}]};})();});const ai={models:{generateContent:vi.fn(),generateContentStream:generate},live:{connect:vi.fn(),music:{connect:vi.fn()}}};
 const saved:string[]=[];const receipt={...initial,ack:1,execution:{...initial.execution,selected:{id:runId,capability:call.id,version:1,resources:[],phase:'completed' as const,status:'Completed',call,output:{...image.capture}},outcomes:[]}};
 const execute=vi.fn(async (..._args:any[])=>receipt),capture=vi.fn(async()=>image);
 const result=await runRoomActionTask({model:'gemini-3.8-flash',prompt:'Look at the virtual objects.',history:[]},{aiClient:ai},{state:()=>initial,valid:()=>true,execute,capture},()=>{}, {onReceipt:()=>{saved.push('receipt');},onSnapshot:()=>{saved.push('image');}});
 expect(saved).toEqual(['receipt','image']);expect(result.snapshots).toEqual([image]);expect(capture).toHaveBeenCalledWith(image.capture.captureId,undefined);
 const requests=generate.mock.calls as unknown as [any][];
 expect(JSON.stringify(requests[0][0])).not.toContain(image.data);
 expect(JSON.stringify(requests[1][0])).toContain(image.data);
 expect(requests[1][0].contents.flatMap((c:any)=>c.parts).filter((p:any)=>p.inlineData)).toEqual([{inlineData:{mimeType:'image/jpeg',data:image.data}}]);
 expect(execute.mock.calls[0][0][0].execution.runId).toBe(runId);
});
it('validates stored image bytes during backup import instead of trusting a caption or hash',()=>{
 const record={version:1,id:'task',handoff:{version:1,id:'task',conversationId:'pair',sourceUserId:'user',sourceAssistantId:'assistant',nativeSession:'a',accessScope:'byok',input:{prompt:'Look',model:'m',systemInstruction:'s',nativeLanguageCode:'en',history:[]}},phase:'completed',note:'Done',startedAt:1,updatedAt:2,operations:[],snapshots:[image]};
 expect(validateArchivedRoomTask({version:1,hidden:false,record}).record.snapshots).toEqual([image]);
 expect(()=>validateArchivedRoomTask({version:1,hidden:false,record:{...record,snapshots:[{...image,data:'bad'}]}})).toThrow();
});

it.each(['stop','access','mismatch','unavailable'] as const)('never uploads pixels after %s interrupts capture',async mode=>{
 const runId='c'.repeat(32),call={id:'room.view.capture',version:1,arguments:{}};
 const initial={...base,capabilities:['roomViewCapture.v1','execution.v1','executionReceipts.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:runId,storageError:null}};
 const text=JSON.stringify({commands:[{action:'execution',execution:{operation:'start',call}}]});
 const generate=vi.fn(async()=>(async function*(){yield{text};})());const ai={models:{generateContentStream:generate}} as any;
 const receipt={...initial,execution:{...initial.execution,selected:{id:runId,capability:call.id,version:1,resources:[],phase:'completed' as const,status:'Completed',call,output:{...image.capture}},outcomes:[]}};
 const controller=new AbortController();let current=true;const saved=vi.fn(),persist=vi.fn();
 const capture=async()=>{if(mode==='stop')controller.abort();if(mode==='access')current=false;if(mode==='unavailable')throw new Error('Image unavailable');return mode==='mismatch'?{...image,capture:{...image.capture,sceneRevision:image.capture.sceneRevision+1}}:image;};
 await expect(runRoomActionTask({model:'capture-test',prompt:'Inspect the virtual room.',history:[]},{aiClient:ai},{state:()=>initial,valid:()=>true,execute:async()=>receipt,capture},()=>{}, {signal:controller.signal,isCurrent:()=>current,onReceipt:saved,onSnapshot:persist})).rejects.toThrow(mode==='unavailable'?'unavailable':mode==='mismatch'?'match':'interrupted');
 expect(saved).toHaveBeenCalledOnce();expect(persist).not.toHaveBeenCalled();expect(generate).toHaveBeenCalledOnce();
 expect(JSON.stringify(generate.mock.calls)).not.toContain(image.data);
});

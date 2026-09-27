import {readFileSync} from 'node:fs';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {afterEach,describe,expect,it,vi} from 'vitest';
import {RoomAgentClient, registerRoomAgent, currentRoomAgentLease} from './roomAgentBridge';
const state=(more={})=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',created:[],objects:[],canUndo:false,canRedo:false,physicsRunning:false,...more});
const commands=[{action:'create' as const,reference:'robot',name:'Robot',kind:'boxRobot' as const}];
afterEach(()=>vi.useRealTimers());
describe('native room agent client',()=>{
 it('waits for an actual native receipt and preserves the planned revision',async()=>{
  const client=new RoomAgentClient();expect(client.lease()).toBeNull();client.receive(state());const lease=client.lease()!;
  client.receive(state({revision:2,sceneRevision:5}));const pending=lease.execute(commands,4);
  expect(client.snapshot().request).toMatchObject({sequence:1,sceneRevision:4});
  client.receive(state({revision:3,sceneRevision:5,ack:1,ok:false,status:'The room changed'}));
  expect((await pending).ok).toBe(false);expect(client.snapshot().request).toBeNull();
  expect(client.receive(state({revision:2,ack:1}))).toBe(false);
 });
 it('invalidates work on suspension and never replays an old session',async()=>{
  const client=new RoomAgentClient();client.receive(state());const lease=client.lease()!;
  const pending=lease.execute(commands,4);const rejected=expect(pending).rejects.toThrow('interrupted');client.cancel();await rejected;
  expect(lease.valid()).toBe(false);expect(client.snapshot().request).toBeNull();
  expect(client.receive(state({revision:20,ack:1}))).toBe(false);
  client.receive(state({session:'b'.repeat(32)}));expect(lease.valid()).toBe(false);
  const next=client.lease()!.execute(commands,4);expect(client.snapshot().request?.sequence).toBe(1);
  client.receive(state({session:'b'.repeat(32),revision:2,ack:1}));await next;
 });
 it('expires unacknowledged requests without retrying and rejects malformed state',async()=>{
  vi.useFakeTimers();const client=new RoomAgentClient();expect(client.receive(state({objects:[{id:'forged'}]}))).toBe(false);
  client.receive(state());const lease=client.lease()!;const pending=lease.execute(commands,4);const rejected=expect(pending).rejects.toThrow('interrupted');
  await vi.advanceTimersByTimeAsync(15001);await rejected;expect(client.snapshot().request).toBeNull();expect(lease.valid()).toBe(false);
 });
});


describe('request-owned room cancellation',()=>{
 it('does not enqueue or reset the room for an already aborted request',async()=>{
  const client=new RoomAgentClient();client.receive(state());const before=client.snapshot();
  const controller=new AbortController();controller.abort();
  await expect(client.lease()!.execute(commands,4,[],controller.signal)).rejects.toMatchObject({name:'AbortError'});
  expect(client.snapshot()).toEqual(before);expect(client.lease()).not.toBeNull();
 });
 it('cancels only its own pending request and rotates the native handshake',async()=>{
  const client=new RoomAgentClient();client.receive(state());const before=client.snapshot().clientId;
  const controller=new AbortController();const pending=client.lease()!.execute(commands,4,[],controller.signal);
  const rejected=expect(pending).rejects.toMatchObject({name:'AbortError'});controller.abort();await rejected;
  expect(client.snapshot().clientId).not.toBe(before);expect(client.snapshot().request).toBeNull();
  expect(client.receive(state({revision:2,ack:1}))).toBe(false);
 });
 it('detaches a completed request so its late abort cannot cancel a manual edit',async()=>{
  const client=new RoomAgentClient();client.receive(state());const controller=new AbortController();
  const first=client.lease()!.execute(commands,4,[],controller.signal);
  client.receive(state({revision:2,ack:1}));await first;
  const manual=client.request(commands);const pending=client.snapshot();controller.abort();
  expect(client.snapshot()).toEqual(pending);
  client.receive(state({revision:3,ack:2}));expect((await manual).ack).toBe(2);
 });
 it('does not let an old signal cancel work in a replacement native session',async()=>{
  const client=new RoomAgentClient();client.receive(state());const controller=new AbortController();
  const old=client.lease()!.execute(commands,4,[],controller.signal);const rejected=expect(old).rejects.toThrow();
  client.receive(state({session:'b'.repeat(32)}));await rejected;
  const next=client.request(commands);const pending=client.snapshot();controller.abort();expect(client.snapshot()).toEqual(pending);
  client.receive(state({session:'b'.repeat(32),revision:2,ack:1}));await next;
 });
});

it('allows task-control observation while an action awaits acknowledgement without allowing a second dispatch', async () => {
  const client = new RoomAgentClient(), unregister = registerRoomAgent(client); client.receive(state());
  const pending = client.request(commands); expect(client.lease()).toBeNull();
  const observation = currentRoomAgentLease()!; expect(observation.state().sceneRevision).toBe(4);
  await expect(observation.execute(commands, 4)).rejects.toThrow('busy');
  expect(client.snapshot().request?.sequence).toBe(1);
  client.receive(state({ revision: 2, ack: 1 })); await pending; unregister();
});

const controls={capabilities:['physicsSettings.v1','avatarSettings.v1','physicsRun.v1','avatarMotion.v1'],
 physics:{ready:true,running:false,status:'Aligned'},avatar:{active:false,mode:'stopped',status:'Ready',canLook:true,canFollow:false,lookReason:'',followReason:'Start physics',distance:1.3,speed:.65}};
it('requires native capability and preserves object conditions for shared controls',async()=>{
 const client=new RoomAgentClient();client.receive(state());
 expect(()=>client.lease()!.execute([{action:'physicsRun',operation:'start'}],4)).toThrow('does not support');
 expect(client.snapshot().request).toBeNull();
 const maestro={id:'maestro',name:'Maestro',kind:'Maestro',objectRevision:5,position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false,
  physics:{mode:'fixed',mass:.5,shape:'automatic'},movement:{distance:1.3,speed:.65},held:false,simulating:false};
 expect(client.receive(state({...controls,revision:2,objects:[maestro]}))).toBe(true);
 const promise=client.request([{action:'avatarMotion',target:'maestro',operation:'follow'}]);
 expect(client.snapshot().request).toMatchObject({version:2,sequence:1,conditions:[{id:'maestro',revision:5}]});
 client.receive(state({...controls,revision:3,ack:1,objects:[maestro],ok:false,status:'Start room physics first'}));
 expect((await promise).ok).toBe(false);
 expect(client.receive(state({...controls,revision:4,physics:{...controls.physics,running:true}}))).toBe(false);
 expect(client.receive(state({...controls,revision:4,avatar:{...controls.avatar,active:true}}))).toBe(false);
 expect(client.receive(state({...controls,revision:4,objects:[{...maestro,physics:{...maestro.physics,mass:100}}]}))).toBe(false);
 expect(client.receive(state({...controls,revision:4,capabilities:['physicsRun.v1','physicsRun.v1']}))).toBe(false);
});

it('accepts the actual Unity motion page and keeps search receipts tied to native acknowledgements',async()=>{
 const native=JSON.parse(readFileSync('test-fixtures/browser/motionSearchState.json','utf8'));
 const client=new RoomAgentClient();expect(client.receive(native)).toBe(true);
 const command={action:'motions' as const,target:'maestro',motionQuery:native.motions.query};
 const pending=client.request([command]);expect(client.snapshot().request?.commands).toEqual([command]);
 expect(client.snapshot().request?.conditions).toEqual([{id:'maestro',revision:native.objects.find((o:any)=>o.id==='maestro').objectRevision}]);
 expect(client.receive({...native,revision:2,ack:1})).toBe(true);expect((await pending).motions?.entries[0].id).toBe(native.motions.entries[0].id);
 expect(client.receive({...native,revision:3,ack:1,motions:{...native.motions,total:999}})).toBe(false);
 expect(client.receive({...native,revision:3,ack:1,motions:{...native.motions,ready:false}})).toBe(false);
 expect(client.getSnapshot().state?.sceneRevision).toBe(native.sceneRevision);
});

it('accepts the actual native walk observation and protects the selected avatar revision',async()=>{
 const native=JSON.parse(readFileSync('test-fixtures/browser/avatarWalkState.json','utf8'));
 const client=new RoomAgentClient();expect(client.receive(native)).toBe(true);
 expect(client.getSnapshot().state?.walk).toMatchObject({source:'library',available:true,name:'Walking'});
 const command={action:'avatarWalk' as const,target:'maestro',motionId:''};const pending=client.request([command]);
 expect(client.snapshot().request?.conditions).toEqual([{id:'maestro',revision:native.objects.find((o:any)=>o.id==='maestro').objectRevision}]);
 const cleared={...native.walk,source:'included',motionId:'',name:'Included walk'};
 expect(client.receive({...native,revision:native.revision+1,ack:1,walk:cleared})).toBe(true);expect((await pending).walk?.source).toBe('included');
 expect(client.receive({...native,revision:native.revision+2,ack:1,walk:{...native.walk,clipIndex:0}})).toBe(false);
});

it('accepts Unity activity observations and carries their independent revision without guessing one',async()=>{
 const native=JSON.parse(readFileSync('test-fixtures/browser/avatarActivityState.json','utf8'));
 const client=new RoomAgentClient();expect(client.receive(native)).toBe(true);
 expect(client.getSnapshot().state?.activityProfile?.roles[3].choices[0]).toMatchObject({name:'Greeting',available:true,weight:2});
 const command={action:'avatarActivities' as const,activities:{modelHash:native.activityProfile.modelHash,revision:native.activityProfile.revision,operation:'undo' as const}};
 const pending=client.request([command]);expect(client.snapshot().request?.commands).toEqual([command]);expect(client.snapshot().request?.conditions).toEqual([]);
 expect(client.receive({...native,revision:native.revision+1,ack:1,ok:false,status:'Tutor-state assignments changed',activityProfile:{...native.activityProfile,revision:3}})).toBe(true);
 expect((await pending).ok).toBe(false);expect(client.snapshot().request).toBeNull();
 for(const change of [{revision:undefined},{revision:0},{roles:[]},{roles:native.activityProfile.roles.map((r:any)=>({...r,choices:r.choices.map((c:any)=>({...c,weight:1.5}))}))}])expect(client.receive({...native,revision:native.revision+2,activityProfile:{...native.activityProfile,...change}})).toBe(false);
});

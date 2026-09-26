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

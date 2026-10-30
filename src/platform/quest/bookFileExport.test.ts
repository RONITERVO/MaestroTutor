// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {afterEach,expect,it,vi} from 'vitest';
import {createBookFileExport,EXPORT_CHUNK_BYTES} from './bookFileExport';
import {nativeFileWriter} from '../browser/fileWriter';
const id='a'.repeat(32), id2='b'.repeat(32);
const brokers:ReturnType<typeof createBookFileExport>[]=[];
function create(){let count=0;const b=createBookFileExport(window,()=>count++===0?id:id2);brokers.push(b);return b;}
function ack(b:ReturnType<typeof createBookFileExport>,bytes=0,extra:Record<string,unknown>={}){
 const request=b.poll()!;expect(request).toBeTruthy();expect(b.receive({version:1,id:request.id,sequence:request.sequence,ok:true,bytes,...extra})).toBe(true);
}
afterEach(()=>{brokers.forEach(b=>b.dispose());brokers.length=0;vi.useRealTimers();});
it('keeps the original browser path until a native poll and releases only its own registration',()=>{
 expect(nativeFileWriter('a.json','application/json')).toBeUndefined();const a=create();a.poll();const b=create();b.poll();a.dispose();
 const pending=nativeFileWriter('b.json','application/json')!;expect(b.poll()?.operation).toBe('open');ack(b);return pending.then(w=>{b.dispose();expect(nativeFileWriter('c.json','application/json')).toBeUndefined();expect(w.location?.()).toBeUndefined();});
});
it('streams bounded UTF-8 chunks with backpressure and publishes location only after finish',async()=>{
 const b=create();b.poll();const pending=nativeFileWriter('chat.ndjson','application/x-ndjson')!;ack(b);const w=await pending;
 const text='雪'.repeat(8191)+'🎵'+'ä'.repeat(24000);const writing=w.write(text);let bytes=0;const chunks:Uint8Array[]=[];
 while(b.poll()?.operation==='chunk'){
  const req=b.poll()!;if(req.operation!=='chunk')throw new Error('chunk');expect(req.offset).toBe(bytes);
  const decoded=Uint8Array.from(atob(req.data),c=>c.charCodeAt(0));expect(decoded.length).toBeLessThanOrEqual(EXPORT_CHUNK_BYTES);chunks.push(decoded);bytes+=decoded.length;
  expect(b.poll()).toEqual(req);ack(b,bytes);await new Promise<void>(resolve=>queueMicrotask(resolve));
 }
 await writing;expect(new TextDecoder().decode(Uint8Array.from(chunks.flatMap(c=>Array.from(c))))).toBe(text);
 expect(w.location?.()).toBeUndefined();const closing=w.close();expect(b.poll()?.operation).toBe('finish');expect(w.location?.()).toBeUndefined();ack(b,bytes,{location:'Downloads/Maestro/chat.ndjson'});await closing;expect(w.location?.()).toBe('Downloads/Maestro/chat.ndjson');
});
it('rejects paths, unsupported types and concurrent writers before dispatch',async()=>{
 const b=create();b.poll();for(const name of ['../backup.json','x\\a.json','a.exe','x\n.json'])await expect(nativeFileWriter(name,'application/json')).rejects.toThrow();
 await expect(nativeFileWriter('a.json','image/png')).rejects.toThrow();const first=nativeFileWriter('a.json','application/json')!;
 await expect(nativeFileWriter('b.json','application/json')).rejects.toThrow('Finish');ack(b);const w=await first;const abort=w.abort!();ack(b);await abort;
});
it('ignores stale or malformed acknowledgements and reports failure without a success receipt',async()=>{
 const b=create();b.poll();const opening=nativeFileWriter('a.json','application/json')!;
 expect(b.receive({version:1,id:id2,sequence:0,ok:true,bytes:0})).toBe(false);expect(b.receive({version:2,id,sequence:0,ok:true,bytes:0})).toBe(false);ack(b);const w=await opening;
 const failure=expect(w.write('hello')).rejects.toThrow('Disk full');ack(b,0,{ok:false,error:'Disk full'});await failure;expect(w.location?.()).toBeUndefined();await w.abort!();expect(b.poll()).toBeNull();
});
it('interrupts writes on suspension, ignores old receipts and can export after resume',async()=>{
 const b=create();b.poll();const opening=nativeFileWriter('a.json','application/json')!;ack(b);const w=await opening;
 const writing=expect(w.write('a')).rejects.toThrow('interrupted');const old=b.poll()!;b.lifecycle(true);await writing;expect(b.poll()).toBeNull();
 b.lifecycle(false);const second=nativeFileWriter('b.json','application/json')!;expect(b.receive({...old,ok:true,bytes:1})).toBe(false);ack(b);await second;
});
it('times out uncertain writes and does not claim a finished file',async()=>{
 vi.useFakeTimers();const b=create();b.poll();const opening=nativeFileWriter('a.json','application/json')!;ack(b);const w=await opening;
 const failure=expect(w.close()).rejects.toThrow('could not be confirmed');await vi.advanceTimersByTimeAsync(30000);await failure;expect(w.location?.()).toBeUndefined();expect(b.poll()).toBeNull();
});
it('aborts an inconsistent byte receipt and rejects a finish without its location',async()=>{
 const b=create();b.poll();const opening=nativeFileWriter('a.json','application/json')!;ack(b);const w=await opening;
 const failure=expect(w.write('abc')).rejects.toThrow('byte count');ack(b,2);await failure;const abort=w.abort!();ack(b,2);await abort;
 const second=nativeFileWriter('b.json','application/json')!;ack(b);const w2=await second;const finish=expect(w2.close()).rejects.toThrow('receipt');ack(b);await finish;expect(w2.location?.()).toBeUndefined();
});

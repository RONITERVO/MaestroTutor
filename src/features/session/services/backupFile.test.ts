// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {afterEach,beforeEach,expect,it,vi} from 'vitest';
const platform=vi.hoisted(()=>({native:vi.fn(),write:vi.fn(),append:vi.fn(),rename:vi.fn(),uri:vi.fn(),remove:vi.fn(),share:vi.fn()}));
vi.mock('@capacitor/core',()=>({Capacitor:{isNativePlatform:platform.native}}));
vi.mock('@capacitor/filesystem',()=>({Filesystem:{writeFile:platform.write,appendFile:platform.append,rename:platform.rename,getUri:platform.uri,deleteFile:platform.remove},Directory:{Documents:'DOCUMENTS',Cache:'CACHE'},Encoding:{UTF8:'utf8'}}));
vi.mock('@capacitor/share',()=>({Share:{share:platform.share}}));
import {saveBackupFile} from './backupFile';
const write=async(w:{write:(s:string)=>Promise<void>})=>{await w.write('one\n');await w.write('two\n');};
beforeEach(()=>{platform.native.mockReturnValue(true);platform.write.mockResolvedValue({uri:'file:///pending'});platform.append.mockResolvedValue(undefined);platform.rename.mockResolvedValue(undefined);platform.uri.mockResolvedValue({uri:'file:///saved'});platform.remove.mockResolvedValue(undefined);platform.share.mockResolvedValue({});});
afterEach(()=>{vi.resetAllMocks();vi.unstubAllGlobals();});
it('confirms a required Documents backup after rename without opening a share sheet',async()=>{
 const order:string[]=[];platform.rename.mockImplementation(async()=>{order.push('rename');});platform.uri.mockImplementation(async()=>{order.push('uri');return {uri:'file:///saved'};});
 const result=await saveBackupFile('backup.ndjson','Backup',write,true);expect(result.status).toBe('saved');expect(order).toEqual(['rename','uri']);expect(platform.share).not.toHaveBeenCalled();
 const first=platform.write.mock.calls[0][0],rename=platform.rename.mock.calls[0][0];expect(first.directory).toBe('DOCUMENTS');expect(first.path).toMatch(/backup-.*\.ndjson\.pending$/);expect(rename.from).toBe(first.path);expect(rename.to+'.pending').toBe(rename.from);
 await saveBackupFile('backup.ndjson','Backup',write,true);expect(platform.write.mock.calls[1][0].path).not.toBe(first.path);
});
it.each(['write','append','rename'])('aborts required backup on %s failure without a cache fallback',async fault=>{
 platform[fault as 'write'|'append'|'rename'].mockRejectedValue(new Error('Disk failure'));
 await expect(saveBackupFile('backup.ndjson','Backup',write,true)).rejects.toThrow('Disk failure');expect(platform.share).not.toHaveBeenCalled();expect(platform.remove).toHaveBeenCalledOnce();
 expect(platform.write.mock.calls.every(([v])=>v.directory==='DOCUMENTS')).toBe(true);
});
it('does not mistake a completed Documents backup for a failure when sharing is cancelled',async()=>{
 platform.share.mockRejectedValue(new DOMException('Share cancelled','AbortError'));
 const result=await saveBackupFile('backup.ndjson','Backup',write);expect(result).toMatchObject({status:'saved',sharingFailed:true});expect(platform.remove).not.toHaveBeenCalled();
});
it('labels a manual cache share separately and never treats it as a durable backup',async()=>{
 platform.write.mockRejectedValueOnce(new Error('Documents unavailable'));
 expect(await saveBackupFile('backup.ndjson','Backup',write)).toEqual({status:'shared'});expect(platform.write.mock.calls.map(([v])=>v.directory)).toEqual(['DOCUMENTS','CACHE']);expect(platform.share).toHaveBeenCalledOnce();
});
it('waits for the browser close and aborts when close fails',async()=>{
 platform.native.mockReturnValue(false);let rejectClose!:(e:Error)=>void;const close=vi.fn(()=>new Promise<void>((_yes,no)=>{rejectClose=no;})),abort=vi.fn();
 vi.stubGlobal('window',{showSaveFilePicker:async()=>({createWritable:async()=>({write:async()=>{},close,abort})})});
 let completed=false;const pending=saveBackupFile('backup.ndjson','Backup',write).then(()=>{completed=true;});const failed=expect(pending).rejects.toThrow('Close failed');
 await vi.waitFor(()=>expect(close).toHaveBeenCalledOnce());expect(completed).toBe(false);rejectClose(new Error('Close failed'));await failed;expect(abort).toHaveBeenCalledOnce();
});

it('preserves the desktop stream receiver when the archive encoder detaches write',async()=>{
 platform.native.mockReturnValue(false);const chunks:string[]=[];
 const stream={async write(this:unknown,text:string){expect(this).toBe(stream);chunks.push(text);},async close(this:unknown){expect(this).toBe(stream);}};
 vi.stubGlobal('window',{showSaveFilePicker:async()=>({createWritable:async()=>stream})});
 const result=await saveBackupFile('backup.ndjson','Backup',async writer=>{const detached=writer.write;await detached('task archive');});
 expect(chunks).toEqual(['task archive']);expect(result.status).toBe('saved');
});

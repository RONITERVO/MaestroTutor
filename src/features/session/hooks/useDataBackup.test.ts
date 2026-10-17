// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
const db = vi.hoisted(() => ({ stage:vi.fn(),commit:vi.fn(),reset:vi.fn(),save: vi.fn(), iterate: vi.fn(), profile: vi.fn(), avatar: vi.fn(), tasks: vi.fn() }));
vi.mock('../../chat', () => ({ safeSaveChatHistoryDB: db.save, getAllChatMetasDB: async () => ({}),
  getChatMetaDB: vi.fn(), getChatHistoryDB: vi.fn(), iterateChatHistoriesDB: db.iterate,
  resetRoomAgentTasks: db.reset }));
vi.mock('..', () => ({ getGlobalProfileDB: db.profile }));
vi.mock('../../../core/db/assets', () => ({ getMaestroProfileImageDB: db.avatar }));
vi.mock('../services/backupArchive', () => ({ stageBackup: db.stage, commitBackupStage: db.commit, discardBackupStage: vi.fn(), hasStagedBackupPair: vi.fn(), writeTaskBackup: db.tasks }));
import { createBookFileExport } from '../../../platform/quest/bookFileExport';
import { useDataBackup } from './useDataBackup';
import { initialSettings, useMaestroStore } from '../../../store';
import { BackupDecoder, type BackupEntry } from '../../../core-sdk/backup/archive';
beforeEach(()=>{db.save.mockResolvedValue(true);});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.resetAllMocks(); });

it('exports an empty selected chat with profile and avatar as a restorable archive', async () => {
  useMaestroStore.setState({ messages: [], settings: { ...initialSettings, selectedLanguagePairId: 'empty-pair' } });
  const avatar = { dataUrl: 'data:image/png;base64,AQID', mimeType: 'image/png' };
  db.profile.mockResolvedValue({ text: 'My learning preferences' }); db.avatar.mockResolvedValue(avatar);
  db.iterate.mockImplementation(async visit => { await visit('empty-pair', []); }); db.tasks.mockResolvedValue(0);
  const lines: string[] = [], close = vi.fn(), abort = vi.fn();
  vi.stubGlobal('showSaveFilePicker', async () => ({ createWritable: async () => ({
    write: async (line: string) => { lines.push(line); }, close, abort,
  }) }));
  const alert = vi.fn(); vi.stubGlobal('alert', alert);
  const h = renderHook(() => useDataBackup({ t: key => key }));
  await act(async () => { await h.result.current.handleSaveAllChats(); });
  expect(db.save).toHaveBeenCalledWith('empty-pair', []); expect(close).toHaveBeenCalledOnce();
  expect(abort).not.toHaveBeenCalled(); expect(alert).not.toHaveBeenCalled();
  const decoder = new BackupDecoder(), restored: BackupEntry[] = [];
  for (const line of lines) restored.push(...await decoder.push(line));
  restored.push(...decoder.finish());
  expect(restored).toContainEqual({ kind: 'chat', id: 'empty-pair', value: [] });
  expect(restored).toContainEqual({ kind: 'asset', id: 'maestroProfileImage', value: avatar });
  expect(restored).toContainEqual({ kind: 'profile', id: 'singleton', value: 'My learning preferences' });
});

it('exports the original Save All archive through the Quest chunk protocol and decodes it again', async () => {
  useMaestroStore.setState({messages:[],settings:{...initialSettings,selectedLanguagePairId:'quest-pair'}});
  const profile='Suomi 🎵 日本語 '.repeat(6000);
  db.profile.mockResolvedValue({text:profile});db.avatar.mockResolvedValue(null);
  db.iterate.mockImplementation(async visit=>{await visit('quest-pair',[]);});db.tasks.mockResolvedValue(0);
  vi.stubGlobal('alert',vi.fn());vi.stubGlobal('showSaveFilePicker',undefined);
  const bridge=createBookFileExport(window,()=>'c'.repeat(32));bridge.poll();const chunks:Uint8Array[]=[];let bytes=0,finished=false;
  const h=renderHook(()=>useDataBackup({t:(key,args)=>key==='sessionControls.backupSaved'?'Saved: '+args?.location:key}));
  try {
    await act(async()=>{
      let settled=false;const saving=h.result.current.handleSaveAllChats().finally(()=>{settled=true;});
      for(let attempt=0;!settled&&attempt<500;attempt++){
        await new Promise<void>(resolve=>setTimeout(resolve,0));const req=bridge.poll();if(!req)continue;
        expect(h.result.current.exportStatus).toBe('');
        if(req.operation==='chunk'){expect(req.offset).toBe(bytes);const data=Uint8Array.from(atob(req.data),c=>c.charCodeAt(0));chunks.push(data);bytes+=data.length;}
        if(req.operation==='finish')finished=true;
        bridge.receive({version:1,id:req.id,sequence:req.sequence,ok:true,bytes,...(finished?{location:'Downloads/Maestro/backup.ndjson'}:{})});
      }
      expect(settled).toBe(true);await saving;
    });
    expect(finished).toBe(true);expect(h.result.current.exportStatus).toBe('Saved: Downloads/Maestro/backup.ndjson');expect(alert).not.toHaveBeenCalled();
    const decoder=new BackupDecoder(),entries:BackupEntry[]=[];const utf8=new TextDecoder();
    let buffered='';for(const chunk of chunks){buffered+=utf8.decode(chunk,{stream:true});let newline:number;while((newline=buffered.indexOf('\n'))>=0){entries.push(...await decoder.push(buffered.slice(0,newline)));buffered=buffered.slice(newline+1);}}
    buffered+=utf8.decode();expect(buffered).toBe('');entries.push(...decoder.finish());
    expect(entries).toContainEqual({kind:'profile',id:'singleton',value:profile});
    expect(entries).toContainEqual({kind:'chat',id:'quest-pair',value:[]});
  } finally {bridge.dispose();}
});


it.each(['cancel','write','close','database','avatar'])('does not replace live data when the required backup fails at %s',async fault=>{
 useMaestroStore.setState({messages:[],settings:{...initialSettings,selectedLanguagePairId:'pair'}});
 db.profile.mockResolvedValue(null);db.avatar.mockResolvedValue(null);db.iterate.mockImplementation(async visit=>visit('pair',[]));db.tasks.mockResolvedValue(0);
 if(fault==='database')db.save.mockResolvedValue(false);if(fault==='avatar')db.avatar.mockRejectedValue(new Error('Unreadable avatar'));
 const abort=vi.fn();vi.stubGlobal('showSaveFilePicker',async()=>{if(fault==='cancel')throw new DOMException('Cancelled','AbortError');return {createWritable:async()=>({write:async()=>{if(fault==='write')throw new Error('Disk full');},close:async()=>{if(fault==='close')throw new Error('Close failed');},abort})};});
 vi.stubGlobal('alert',vi.fn());const hook=renderHook(()=>useDataBackup({t:key=>key}));
 await act(async()=>{expect(await hook.result.current.handleLoadAllChats(new File(['fixture'],'backup.ndjson'))).toBe(false);});
 expect(db.stage).not.toHaveBeenCalled();expect(db.commit).not.toHaveBeenCalled();expect(db.reset).not.toHaveBeenCalled();
 expect(hook.result.current.exportStatus).toBe('sessionControls.backupRequired');
 if(fault==='write'||fault==='close')expect(abort).toHaveBeenCalledOnce();
});
it('exports a profile with no conversations as a complete restorable archive',async()=>{
 useMaestroStore.setState({messages:[],settings:{...initialSettings,selectedLanguagePairId:null}});
 db.profile.mockResolvedValue({text:'Profile before first conversation'});db.avatar.mockResolvedValue(null);db.iterate.mockResolvedValue(undefined);db.tasks.mockResolvedValue(0);
 const lines:string[]=[];vi.stubGlobal('showSaveFilePicker',async()=>({createWritable:async()=>({write:async(s:string)=>{lines.push(s);},close:async()=>{}})}));vi.stubGlobal('alert',vi.fn());
 const hook=renderHook(()=>useDataBackup({t:key=>key}));await act(async()=>{expect(await hook.result.current.handleSaveAllChats({auto:true})).toEqual({status:'saved',location:undefined});});
 const decoder=new BackupDecoder(),entries:BackupEntry[]=[];for(const line of lines)entries.push(...await decoder.push(line));entries.push(...decoder.finish());
 expect(decoder.chats).toBe(0);expect(entries).toContainEqual({kind:'profile',id:'singleton',value:'Profile before first conversation'});expect(alert).not.toHaveBeenCalled();
});

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it,vi} from 'vitest';
import {inspectChatOffer,type ChatOfferFactReader} from './probe-chat-offers';

type Kind='image'|'audio';
const hash='d'.repeat(64),name='Known fixture';
function reader(kind:Kind,mode:'normal'|'paging'|'metadata'|'guard'|'pending'='normal'){
 const key=kind==='image'?'imageHash':'assetHash',prefix=kind+'.chat.';let generation='a',changed=false;
 const row=(id:string,target=false)=>({id,revision:1,[key]:id,name:target?name:'Other '+id[0],messageId:target?'source-'+generation:'other',origin:'generated',kibibytes:1});
 const values=()=>generation==='a'?[row('1'.repeat(64)),row('2'.repeat(64)),row('3'.repeat(64)),row(hash,true)]:[row(hash,true),row('4'.repeat(64))];
 const fact=vi.fn<ChatOfferFactReader>(async(id,args)=>{
  if(!changed&&mode==='pending'){changed=true;return {available:true,value:{offerSet:'',total:0,next:-1,entries:[]}};}
  if(!changed&&((mode==='paging'&&id===prefix+'choices'&&args.offset===1)||(mode==='metadata'&&id===prefix+'metadata'&&args.field==='messageId')||(mode==='guard'&&id===prefix+'item'))){generation='b';changed=true;}
  const offerSet=generation.repeat(32),all=values();
  if(id===prefix+'item')return {available:true,value:{offerSet}};
  if(id===prefix+'metadata'){
   expect(args[key]).toBe(hash);const source=all.find(v=>v.id===hash)!;
   return {available:true,value:{offerSet,[key]:hash,field:args.field,value:source[args.field as 'name'|'messageId'|'origin']}};
  }
  expect([prefix+'choices',prefix+'library']).toContain(id);const offset=Number(args.offset),length=offset===1&&generation==='a'?2:1,entries=all.slice(offset,offset+length);
  return {available:true,value:{offerSet,total:all.length,next:offset+entries.length<all.length?offset+entries.length:-1,entries:entries.map(v=>id===prefix+'library'?v:{id:v.id,revision:v.revision,[key]:v.id,name:v.name,kibibytes:v.kibibytes})}};
 });
 return fact;
}
it.each(['image','audio'] as const)('finds a later %s fixture through variable next cursors and checks exact metadata/current identity',async kind=>{
 const fact=reader(kind),result=await inspectChatOffer(fact,kind,{hash,name});
 expect(result.pages.map(p=>p.next)).toEqual([1,3,-1]);expect(result.metadata).toEqual({name,messageId:'source-a',origin:'generated'});expect(result.restarts).toBe(0);
 expect(fact.mock.calls.filter(([id])=>id.endsWith('.choices')).map(([,args])=>args.offset)).toEqual([0,1,3]);
 expect(fact.mock.calls.find(([id])=>id.endsWith('.library'))?.[1]).toEqual({offset:3});
 expect(fact.mock.calls.at(-1)?.[0]).toBe(kind+'.chat.item');
});
it.each(['paging','metadata','guard'] as const)('restarts from zero when the source set changes during %s',async mode=>{
 const fact=reader('image',mode),result=await inspectChatOffer(fact,'image',{hash,name});
 expect(result.offerSet).toBe('b'.repeat(32));expect(result.restarts).toBe(1);expect(result.metadata.messageId).toBe('source-b');expect(result.legacy.messageId).toBe('source-b');
 expect(result.pages).toHaveLength(1);expect(fact.mock.calls.filter(([id])=>id==='image.chat.choices').map(([,args])=>args.offset)).toContain(0);
 expect(fact.mock.calls.filter(([id,args])=>id==='image.chat.choices'&&args.offset===0)).toHaveLength(2);
});
it('waits for an initially unadvertised source without inventing an offer identity',async()=>{
 const fact=reader('audio','pending'),result=await inspectChatOffer(fact,'audio',{hash,name});expect(result.offerSet).toBe('a'.repeat(32));expect(result.restarts).toBe(0);
 expect(fact.mock.calls.filter(([id,args])=>id==='audio.chat.choices'&&args.offset===0)).toHaveLength(2);
});
it.each(['self-loop','empty-nonterminal','duplicate','metadata-hash','metadata-value'] as const)('rejects inconsistent %s evidence instead of selecting it',async mode=>{
 const base=reader('image');const fact:ChatOfferFactReader=async(id,args,version)=>{
  const result=await base(id,args,version);const value=result.value as Record<string,unknown>;
  if(id==='image.chat.choices'&&args.offset===0){if(mode==='self-loop')value.next=0;if(mode==='empty-nonterminal')value.entries=[];}
  if(id==='image.chat.choices'&&args.offset===1&&mode==='duplicate'){
   const entry=(value.entries as Record<string,unknown>[])[0];entry.id=entry.imageHash='1'.repeat(64);
  }
  if(id==='image.chat.metadata'){if(mode==='metadata-hash')value.imageHash='f'.repeat(64);if(mode==='metadata-value')value.value='A different source';}
  return result;
 };
 await expect(inspectChatOffer(fact,'image',{hash,name})).rejects.toThrow();
});
it('does not reuse metadata after an unavailable read and rechecks the exact current guard',async()=>{
 const base=reader('audio');let failed=false;
 const fact:ChatOfferFactReader=async(id,args,version)=>{if(!failed&&id==='audio.chat.metadata'&&args.field==='origin'){failed=true;return {available:false};}return base(id,args,version);};
 const result=await inspectChatOffer(fact,'audio',{hash,name});expect(result.restarts).toBe(1);expect(result.metadata).toEqual({name,messageId:'source-a',origin:'generated'});
 expect(base.mock.calls.filter(([id])=>id==='audio.chat.item')).toHaveLength(1);
});
it('fails within its deadline when the source never advertises the fixture',async()=>{
 const fact=vi.fn<ChatOfferFactReader>(async()=>({available:true,value:{offerSet:'',total:0,next:-1,entries:[]}}));
 await expect(inspectChatOffer(fact,'image',{hash,name},Date.now()+20)).rejects.toThrow('bounded deadline');expect(fact).toHaveBeenCalledOnce();
});

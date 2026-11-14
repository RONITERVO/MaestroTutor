// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';

export type ChatOfferFactReader=(id:string,args:Record<string,unknown>,version?:number)=>Promise<{available:boolean;value?:unknown}>;
const object=(value:unknown,label:string)=>{assert.ok(value&&typeof value==='object'&&!Array.isArray(value),label);return value as Record<string,unknown>;};
const pause=()=>new Promise(resolve=>setTimeout(resolve,100));

/** Offline journey evidence only. Discover through ordinary exact-choice facts,
 * then compare metadata with the known short fixture's legacy row. A changed
 * offer set restarts the whole read; no identity is joined across generations. */
export async function inspectChatOffer(read:ChatOfferFactReader,kind:'image'|'audio',expected:{hash:string;name:string},deadline=Date.now()+45000){
 const key=kind==='image'?'imageHash':'assetHash',prefix=kind+'.chat.';
 let restarts=0;
 for(let attempt=0;attempt<256&&Date.now()<deadline;attempt++){
  let offset=0,set='',total=-1,changed=false;const pages:Record<string,unknown>[]=[],seen=new Set<string>();
  while(offset<8&&Date.now()<deadline){
   const response=await read(prefix+'choices',{offset});if(!response.available){changed=true;break;}
   const page=object(response.value,'Expected a chat choice page');assert.equal(typeof page.offerSet,'string');
   if(page.offerSet===''&&page.total===0&&page.next===-1&&Array.isArray(page.entries)&&page.entries.length===0)break;
   assert.match(page.offerSet as string,/^[a-f0-9]{32}$/);
   if(set&&page.offerSet!==set){changed=true;break;}set=page.offerSet as string;
   assert.ok(Number.isInteger(page.total)&&Number(page.total)>=0&&Number(page.total)<=8,'Invalid chat offer total');
   if(total>=0)assert.equal(page.total,total,'Chat offer total changed without a new offerSet');total=Number(page.total);
   assert.ok(Array.isArray(page.entries)&&page.entries.length<=2,'Invalid chat choice entries');const entries=page.entries.map(v=>object(v,'Invalid chat choice'));
   assert.ok(offset+entries.length<=total&&((entries.length>0)||offset>=total),'Incomplete chat choice page');
   assert.equal(page.next,offset+entries.length<total?offset+entries.length:-1,'Chat next must advance by the actual entry count');
   for(const entry of entries){assert.equal(typeof entry[key],'string');assert.match(entry[key] as string,/^[a-f0-9]{64}$/);assert.equal(entry.id,entry[key]);assert.equal(entry.revision,1);assert.equal(typeof entry.name,'string');assert.ok(!seen.has(entry[key] as string),'Duplicate chat identity across pages');seen.add(entry[key] as string);}
   pages.push(page);const index=entries.findIndex(value=>value[key]===expected.hash);
   if(index>=0){
    const entry=entries[index];assert.equal(entry.name,expected.name,'The offered fixture name changed');
    const legacyReply=await read(prefix+'library',{offset:offset+index});if(!legacyReply.available){changed=true;break;}
    const legacy=object(legacyReply.value,'Expected the short fixture legacy page');if(legacy.offerSet!==set){changed=true;break;}
    assert.equal(legacy.total,total);assert.ok(Array.isArray(legacy.entries)&&legacy.entries.length>0);const original=object(legacy.entries[0],'Expected the exact legacy fixture row');
    assert.equal(original[key],expected.hash);assert.equal(original.name,expected.name);assert.equal(original.id,entry.id);assert.equal(original.revision,entry.revision);assert.equal(original.kibibytes,entry.kibibytes);
    const metadata:Record<string,string>={};
    for(const field of ['name','messageId','origin']){
     const reply=await read(prefix+'metadata',{[key]:expected.hash,field});if(!reply.available){changed=true;break;}
     const value=object(reply.value,'Expected exact chat metadata');if(value.offerSet!==set){changed=true;break;}
     assert.equal(value[key],expected.hash);assert.equal(value.field,field);assert.equal(typeof value.value,'string');assert.equal(value.value,original[field],'Chat metadata differs from the exact offered fixture');metadata[field]=value.value as string;
    }
    if(changed)break;
    const guardReply=await read(prefix+'item',{[key]:expected.hash});if(!guardReply.available){changed=true;break;}
    const guard=object(guardReply.value,'Expected current chat offer identity');if(guard.offerSet!==set){changed=true;break;}
    return {offerSet:set,entry,metadata,legacy:original,pages,restarts,current:guard};
   }
   if(page.next===-1)break;offset=Number(page.next);
  }
  if(changed)restarts++;await pause();
 }
 assert.fail('The synthetic '+kind+' fixture did not become a coherent current chat offer before the bounded deadline');
}

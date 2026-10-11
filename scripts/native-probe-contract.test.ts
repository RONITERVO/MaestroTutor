// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect,expectTypeOf} from 'vitest';
import {checkedProbeReply,factReply,placementReply,assertSamePlacement} from './native-probe-contract';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import type {CatalogView} from '../shared/roomCatalog';
import {behaviourFact} from '../shared/behaviourCatalog';
import {capabilityDefinition} from '../shared/capabilities';
const target='a'.repeat(32),other='b'.repeat(32);
const state=(catalog?:CatalogView):RoomAgentState=>({version:1,session:'1'.repeat(32),revision:1,sceneRevision:1,ack:1,ok:true,status:'Ready',created:[],canUndo:false,canRedo:false,physicsRunning:false,objects:[],catalog});
const placement=()=>state({operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target},available:true,definition:behaviourFact('object.placement')!,value:{target,revision:1,position:{x:1,y:2,z:3},rotation:{x:0,y:0,z:0,w:1},scale:1,temporary:false},status:'Ready'});
const requested=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target}}}] as const;

describe('native probe request/reply contracts',()=>{
 it('narrows a matching fact while preserving the exact native state',()=>{
  const original=placement(),reply=checkedProbeReply(requested,original);
  expect(reply).toBe(original);expectTypeOf(reply.catalog.category).toEqualTypeOf<'facts'>();expectTypeOf(reply.catalog.available).toEqualTypeOf<boolean>();
  expect(reply.catalog.value).toEqual(factReply(original).value);
 });
 it('accepts unavailable facts without manufacturing a value',()=>{
  const original=placement(),fact=factReply(original);fact.available=false;fact.value=null;
  expect(checkedProbeReply(requested,original).catalog.value).toBeNull();expect(()=>placementReply(original)).toThrow(/available placement/);
 });
 it.each(['operation','category','capability','version','arguments'] as const)('rejects a reply to a different %s',field=>{
  const original=placement(),fact=factReply(original);
  if(field==='operation')original.catalog={operation:'search',query:'object.placement',offset:0,pageSize:6,total:0,entries:[],status:'Ready'};
  if(field==='category')original.catalog={operation:'inspect',capability:'object.placement',category:'events',version:1,definition:null,status:'Ready'};
  if(field==='capability')fact.capability='object.position';
  if(field==='version')fact.version=2;
  if(field==='arguments')fact.arguments={target:other};
  expect(()=>checkedProbeReply(requested,original)).toThrow(/Native/);
 });
 it('requires a catalog reply and checks search page identity',()=>{
  expect(()=>checkedProbeReply(requested,state())).toThrow(/operation/);
  const request=[{action:'catalog',catalog:{operation:'search',query:'create',offset:0}}] as const;
  const original=state({operation:'search',category:'actions',query:'create',offset:0,pageSize:6,total:0,entries:[],status:'Ready'});
  expect(checkedProbeReply(request,original).catalog.entries).toEqual([]);
  for(const change of [{query:'other'},{offset:6},{category:'facts' as const}])expect(()=>checkedProbeReply(request,state({...original.catalog as Extract<CatalogView,{operation:'search'}>,...change}))).toThrow(/requested page/);
 });
 it('treats omitted and explicit action categories as the same contract',()=>{
  const original=state({operation:'inspect',capability:'object.create',version:1,definition:capabilityDefinition('object.create')!,status:'Ready'});
  const reply=checkedProbeReply([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'object.create',version:1}}],original);
  expect(reply.catalog.definition?.id).toBe('object.create');
  expectTypeOf(reply.catalog.definition).toEqualTypeOf<ReturnType<typeof capabilityDefinition>|null>();
 });
 it('matches the complete action being checked, including arguments',()=>{
  const call={id:'object.create',version:1,arguments:{name:'Original'}};
  const original=state({operation:'check',call,valid:false,available:false,occupied:false,resources:[],status:'Invalid arguments'});
  expect(checkedProbeReply([{action:'catalog',catalog:{operation:'check',call}}],original).catalog.valid).toBe(false);
  expect(()=>checkedProbeReply([{action:'catalog',catalog:{operation:'check',call:{...call,arguments:{name:'Different'}}}}],original)).toThrow(/different action/);
 });
 it('preserves non-catalog acknowledgements without inventing an inspection',()=>{
  const original=state();expect(checkedProbeReply([{action:'undo'}],original)).toBe(original);expect(()=>factReply(original)).toThrow(/fact inspection/);
 });
});

describe('native complete-placement evidence',()=>{
 it('rejects absent rotations even when both snapshots lack them',()=>{
  const a=placement(),fact=factReply(a);fact.value={target,position:{x:1,y:2,z:3},scale:1};
  expect(()=>assertSamePlacement(a,structuredClone(a),'Undo')).toThrow(/placement/);
 });
 it('detects a rotation-only change with unchanged position and scale',()=>{
  const before=placement(),after=placement(),v=placementReply(after);factReply(after).value={...v,rotation:{x:0,y:Math.SQRT1_2,z:0,w:Math.SQRT1_2}};
  expect(()=>assertSamePlacement(before,after,'Undo')).toThrow(/rotation/);
 });
 it.each(['target','position','scale'] as const)('detects a changed %s',field=>{
  const before=placement(),after=placement(),v=placementReply(after);
  if(field==='target'){factReply(after).arguments={target:other};v.target=other;}
  if(field==='position')v.position.x+=.01;
  if(field==='scale')v.scale=2;
  factReply(after).value=v;expect(()=>assertSamePlacement(before,after,'Join')).toThrow(/changed/);
 });
 it('allows equivalent quaternion signs and bounded float round-off',()=>{
  const before=placement(),after=placement(),v=placementReply(after);v.position.x+=.000001;v.rotation.w=-1;factReply(after).value=v;
  expect(()=>assertSamePlacement(before,after,'Undo')).not.toThrow();
 });
 it('rejects mismatched identities, non-finite values and invalid rotations',()=>{
  for(const change of [{target:other},{scale:NaN},{position:{x:Infinity,y:0,z:0}},{rotation:{x:0,y:0,z:0,w:2}}]){
   const s=placement();factReply(s).value={...placementReply(s),...change};expect(()=>placementReply(s)).toThrow();
  }
 });
});

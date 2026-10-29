// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {afterEach,describe,expect,it,vi} from 'vitest';
import {awaitIncludedMotionPackage,readMotionMetadata} from './probe-motion-maintenance';

const entry={motionId:'a'.repeat(32),entryRevision:'b'.repeat(64),payloadHash:'c'.repeat(64),exists:true,tagCount:3,favourite:true,archived:false,removed:false,downloaded:true,payloadPresent:true};
const name={available:true,value:{entryRevision:entry.entryRevision,name:'  <Exact name>  '}};
const page=(tags:string[],next:number,revision=entry.entryRevision)=>({available:true,value:{entryRevision:revision,total:3,next,tags}});
afterEach(()=>vi.useRealTimers());
describe('native included-package readiness',()=>{
 const pack=(busy:boolean)=>({available:true,busy,package:{manifestHash:'f'.repeat(64)},counts:{motions:178,catalogued:busy?0:178,removed:0}});
 it('records the initial unavailable package before refusing instead of bootstrapping a substitute',async()=>{
  const value={...pack(false),available:false},fact=vi.fn().mockResolvedValue({available:true,value}),observe=vi.fn().mockResolvedValue(undefined);
  await expect(awaitIncludedMotionPackage({fact},observe)).rejects.toThrow(/native included animation package is unavailable/);
  expect(observe).toHaveBeenCalledWith(value);expect(fact).toHaveBeenCalledTimes(1);
 });
 it('only polls the real package until its first-use copy finishes',async()=>{
  vi.useFakeTimers();const fact=vi.fn().mockResolvedValueOnce({available:true,value:pack(true)}).mockResolvedValueOnce({available:true,value:pack(false)}),observe=vi.fn().mockResolvedValue(undefined);
  const result=awaitIncludedMotionPackage({fact},observe);await vi.advanceTimersByTimeAsync(250);
  expect(await result).toEqual(pack(false));expect(fact.mock.calls).toEqual([['motion.pack.included'],['motion.pack.included']]);expect(observe).toHaveBeenCalledTimes(2);
 });
 it('stops polling at a fixed deadline without invoking an install or retry',async()=>{
  vi.useFakeTimers();const fact=vi.fn().mockResolvedValue({available:true,value:pack(true)}),observe=vi.fn().mockResolvedValue(undefined);
  const result=expect(awaitIncludedMotionPackage({fact},observe,500)).rejects.toThrow(/read-only readiness deadline/);
  await vi.advanceTimersByTimeAsync(500);await result;expect(fact).toHaveBeenCalledTimes(3);expect(observe).toHaveBeenCalledTimes(3);
 });
 it('rejects a package identity change while its copy is pending',async()=>{
  vi.useFakeTimers();const fact=vi.fn().mockResolvedValueOnce({available:true,value:pack(true)}).mockResolvedValueOnce({available:true,value:{...pack(false),package:{manifestHash:'e'.repeat(64)}}}),observe=vi.fn().mockResolvedValue(undefined);
  const result=expect(awaitIncludedMotionPackage({fact},observe)).rejects.toThrow(/identity changed/);await vi.advanceTimersByTimeAsync(250);await result;expect(observe).toHaveBeenCalledTimes(2);
 });
});
describe('native maintenance journey read coherence',()=>{
 it('reads bounded exact tag pages without changing spaces, commas or escaping',async()=>{
  const fact=vi.fn().mockResolvedValueOnce(name).mockResolvedValueOnce(page(['  Calm  ','one, two'],2)).mockResolvedValueOnce(page(['back\\slash "tag"'],-1));
  expect(await readMotionMetadata({fact},entry)).toEqual({name:'  <Exact name>  ',tags:['  Calm  ','one, two','back\\slash "tag"'],favourite:true});
  expect(fact.mock.calls).toEqual([['motion.library.name',{motionId:entry.motionId}],['motion.library.tags',{motionId:entry.motionId,entryRevision:entry.entryRevision,offset:0}],['motion.library.tags',{motionId:entry.motionId,entryRevision:entry.entryRevision,offset:2}]]);
 });
 it('refuses metadata from a newer revision before replacing existing tags',async()=>{
  const fact=vi.fn().mockResolvedValueOnce(name).mockResolvedValueOnce(page(['first','second'],2)).mockResolvedValueOnce(page(['newer'],-1,'d'.repeat(64)));
  await expect(readMotionMetadata({fact},entry)).rejects.toThrow();expect(fact).toHaveBeenCalledTimes(3);
 });
 it('does not retry an unavailable page or silently save a partial tag list',async()=>{
  const fact=vi.fn().mockResolvedValueOnce(name).mockResolvedValueOnce(page(['first','second'],2)).mockResolvedValueOnce({available:false});
  await expect(readMotionMetadata({fact},entry)).rejects.toThrow(/changed while reading/);expect(fact).toHaveBeenCalledTimes(3);
 });
 it.each([0,1,3])('refuses a repeated, overlapping or skipped page offset %s',async next=>{
  const fact=vi.fn().mockResolvedValueOnce(name).mockResolvedValueOnce(page(['first','second'],next));
  await expect(readMotionMetadata({fact},entry)).rejects.toThrow();expect(fact).toHaveBeenCalledTimes(2);
 });
 it('rejects out-of-budget metadata before issuing native reads',async()=>{
  const fact=vi.fn();await expect(readMotionMetadata({fact},{...entry,tagCount:17})).rejects.toThrow(/tag count/);expect(fact).not.toHaveBeenCalled();
 });
});

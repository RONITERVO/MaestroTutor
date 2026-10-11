// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {bookProviderStage,readBookProviderResponse,assertBookProviderPipeline} from './book-provider-evidence';
const chunk={candidates:[{content:{parts:[{thought:true,text:'private thought'},{text:'result'}]}}],modelVersion:'test',usageMetadata:{totalTokenCount:5}};
describe('real book provider evidence',()=>{
 it('reads direct SSE and managed chunks without counting thoughts as visible output',()=>{
  const direct=readBookProviderResponse('data: '+JSON.stringify(chunk)+'\n\n',false,'chat',200);
  const managed=readBookProviderResponse(JSON.stringify({type:'chunk',chunk})+'\n'+JSON.stringify({type:'final'})+'\n',true,'chat',200);
  expect(direct).toEqual(managed);expect(direct).toMatchObject({text:'result',tokens:5,chunks:1});
  expect(bookProviderStage({config:{responseJsonSchema:{properties:{commands:{}}}}})).toBe('planner');
  expect(bookProviderStage({generationConfig:{responseSchema:{properties:{suggestions:{}}}}})).toBe('verifier');
 });
 it('rejects truncated/error managed streams and missing stage/usage/verified-handoff evidence',()=>{
  expect(()=>readBookProviderResponse(JSON.stringify({type:'chunk',chunk}),true,'chat',200)).toThrow(/final/);
  expect(()=>readBookProviderResponse('{"type":"error"}',true,'chat',200)).toThrow(/error/);
  const response=readBookProviderResponse('data: '+JSON.stringify(chunk)+'\n\n',false,'chat',200);
  const rows=[response,response,{...response,stage:'planner' as const},{...response,stage:'verifier' as const,text:'{"toolRequest":{"tool":"agent"}}'}];
  expect(()=>assertBookProviderPipeline(rows)).not.toThrow();
  expect(()=>assertBookProviderPipeline(rows.map(r=>({...r,tokens:0})))).toThrow();
  expect(()=>assertBookProviderPipeline(rows.map(r=>({...r,stage:'chat'})))).toThrow();
  expect(()=>assertBookProviderPipeline(rows.map(r=>r.stage==='verifier'?{...r,text:'{"toolRequest":null}'}:r))).toThrow();
 });
});

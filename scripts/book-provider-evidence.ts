// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
export interface BookProviderResponse { stage: 'planner'|'verifier'|'chat'; status: number; text: string; tokens: number; chunks: number; model: string }
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
export function bookProviderStage(body:unknown):BookProviderResponse['stage'] {
 const request=record(body)?body:{},config=record(request.config)?request.config:record(request.generationConfig)?request.generationConfig:{};
 const schema=config.responseJsonSchema??config.responseSchema,properties=record(schema)&&record(schema.properties)?schema.properties:{};
 return properties.commands?'planner':properties.suggestions?'verifier':'chat';
}
/** Observe real wire responses without modifying what the browser receives. */
export function readBookProviderResponse(body:string,managed:boolean,stage:BookProviderResponse['stage'],status:number):BookProviderResponse {
 const rows:unknown[]=managed?body.split(/\r?\n/).filter(line=>line.trim()).map(line=>JSON.parse(line))
  :body.split(/\r?\n\r?\n/).filter(block=>block.trim()).map(block=>JSON.parse(block.split(/\r?\n/).filter(line=>line.startsWith('data:')).map(line=>line.slice(5).trimStart()).join('\n')));
 let text='',tokens=0,chunks=0,model='',final=!managed;
 for(const row of rows){
  assert.ok(record(row),'Provider response row is invalid');
  if(managed){assert.notEqual(row.type,'error','Managed provider emitted an error');if(row.type==='final'){final=true;continue;}assert.equal(row.type,'chunk');}
  const chunk=managed?row.chunk:row;assert.ok(record(chunk));chunks++;
  if(record(chunk.usageMetadata))tokens=Math.max(tokens,Number(chunk.usageMetadata.totalTokenCount)||0);
  if(typeof chunk.modelVersion==='string')model=chunk.modelVersion;
  if(Array.isArray(chunk.candidates))for(const candidate of chunk.candidates){
   if(!record(candidate)||!record(candidate.content)||!Array.isArray(candidate.content.parts))continue;
   for(const part of candidate.content.parts)if(record(part)&&typeof part.text==='string'&&part.thought!==true)text+=part.text;
  }else if(typeof chunk.text==='string')text+=chunk.text;
 }
 assert.ok(final,'Managed stream has no final accounting event');
 return {stage,status,text,tokens,chunks,model};
}
export function assertBookProviderPipeline(responses:BookProviderResponse[]) {
 assert.ok(responses.length>=4,'Missing real tutor/verifier/planner/reply requests');
 assert.ok(responses.every(r=>r.status===200&&r.tokens>0&&r.chunks>0&&r.model&&r.text.trim()),'Missing real provider usage or output');
 assert.ok(responses.filter(r=>r.stage==='chat').length>=2,'Missing tutor or final reply stream');
 assert.ok(responses.some(r=>r.stage==='planner'),'No real planner');
 assert.ok(responses.some(r=>r.stage==='verifier'&&JSON.parse(r.text).toolRequest?.tool==='agent'),'Verifier did not request the agent');
}

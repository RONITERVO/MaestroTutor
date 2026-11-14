// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,lstat} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {assertActionInterruptionResult} from './action-interruption-contract';
const root=resolve(process.argv[2]||'');
const read=async(name:string)=>{
 const path=join(root,name),info=await lstat(path);assert(info.isFile()&&!info.isSymbolicLink()&&info.size<=4*1024*1024);
 return JSON.parse(await readFile(path,'utf8'));
};
const result=await read('action-interruption-result.json');
assert.deepEqual(result.source,await read('action-interruption-source.json'));
assert.deepEqual(result.nativeProcess,await read('restart.json'));
assert.deepEqual(result.clientProcess,await read('client-restart.json'));
assert.deepEqual(result.source.ready,await read('ready.json'));assert.deepEqual(result.ready,await read('restart/ready.json'));
const boundary=await read('action-interruption-native.json'),q=result.source.request;
assert.equal(boundary.id,q.id);assert.equal(boundary.clientId,q.clientId);assert.equal(boundary.state.session,q.nativeSession);
assert.equal(boundary.state.execution.selected.id,q.runId);assert.equal(boundary.state.execution.selected.phase,'running');
assert.deepEqual(boundary.state.execution.selected.call,result.source.original.operations[3].receipt.execution.selected.call);
for(const name of ['terminal.json','action-interruption-error.json']){
 await assert.rejects(lstat(join(root,name)),(error:NodeJS.ErrnoException)=>error.code==='ENOENT');
}
const summary=assertActionInterruptionResult(result,root,await readFile(join(root,'restart-request.json'),'utf8'));
console.log(JSON.stringify({status:'passed',...summary}));

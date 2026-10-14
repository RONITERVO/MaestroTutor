// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync,readdirSync} from 'node:fs';
import {resolve,relative} from 'node:path';
import {createHash} from 'node:crypto';
import {it,expect} from 'vitest';
const digest=(bytes:string|Buffer)=>createHash('sha256').update(bytes).digest('hex');
const text=(path:string)=>readFileSync(path,'utf8').replace(/\r\n/g,'\n');
const files=(directory:string):string[]=>readdirSync(directory,{withFileTypes:true}).flatMap(entry=>entry.isDirectory()?files(resolve(directory,entry.name)):entry.name.endsWith('.cs')?[resolve(directory,entry.name)]:[]);
it.each(['NLayer','NVorbis'])('keeps the pinned %s runtime source, explicit patch list and shipped license in sync',name=>{
 const root=resolve('unity/MaestroQuest/Assets/ThirdParty',name),manifest=JSON.parse(readFileSync(resolve(root,'UPSTREAM.json'),'utf8')) as {commit:string;sourceSha256:Record<string,string>;shippedSourceSha256:Record<string,string>;patches:Record<string,string>};
 expect(manifest.commit).toMatch(/^[a-f0-9]{40}$/);
 const actual=Object.fromEntries(files(root).map(file=>[relative(root,file).replace(/\\/g,'/'),digest(text(file))]));
 expect(actual).toEqual(manifest.shippedSourceSha256);
 expect(Object.keys(manifest.patches).every(file=>file in actual)).toBe(true);
 expect(Object.keys(actual).every(file=>file in manifest.sourceSha256||file in manifest.patches)).toBe(true);
 expect(text(resolve('public/licenses',name.toLowerCase()+'.txt'))).toBe(text(resolve(root,'LICENSE.txt')));
});
it('keeps synthetic compressed fixtures identifiable without a runtime encoder or provider',()=>{
 const root=resolve('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/CompressedAudio');
 const provenance=JSON.parse(readFileSync(resolve(root,'PROVENANCE.json'),'utf8')) as {license:string;fixtures:{file:string;sha256:string}[];references:{file:string;sha256:string}[]};
 expect(provenance.license).toBe('CC0-1.0');expect(provenance.fixtures).toHaveLength(6);
 expect(provenance.references).toHaveLength(3);
 for(const fixture of [...provenance.fixtures,...provenance.references])expect(digest(readFileSync(resolve(root,fixture.file)))).toBe(fixture.sha256);
});

it.each([['NLayer','FLUENDO-NOTICE.txt','nlayer-fluendo.txt'],['NVorbis','XIPH-NOTICE.txt','nvorbis-xiph.txt']])('ships the additional %s attribution', (name,source,shipped)=>{
 expect(text(resolve('public/licenses',shipped))).toBe(text(resolve('unity/MaestroQuest/Assets/ThirdParty',name,source)));
});

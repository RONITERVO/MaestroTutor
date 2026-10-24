// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,stat,readdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
const root=new URL('../unity/MaestroQuest/Assets/',import.meta.url);
const manifest=JSON.parse(await readFile(new URL('Maestro/Resources/Avatars/IncludedMaestro.json',root),'utf8'));
const provenance=JSON.parse(await readFile(new URL('Maestro/Resources/Avatars/IncludedMaestro.provenance.json',root),'utf8'));
assert.deepEqual(Object.keys(manifest).sort(),['version','sha256','name','bytes','attribution','walkClipIndex'].sort());
assert.equal(manifest.version,1);
assert.match(manifest.sha256,/^[a-f0-9]{64}$/);
for (const [key,maximum] of [['name',100],['attribution',2048]]) {
 assert.equal(typeof manifest[key],'string');assert.ok(manifest[key].length>0&&manifest[key].length<=maximum&&!/[\p{Cc}<>]/u.test(manifest[key]));
}
assert.ok(Number.isInteger(manifest.bytes)&&manifest.bytes>=28&&manifest.bytes<=64*1024*1024);
const path=new URL('StreamingAssets/MaestroContent/IncludedMaestro.glb',root);
assert.equal((await stat(path)).size,manifest.bytes);
const bytes=await readFile(path);
assert.equal(createHash('sha256').update(bytes).digest('hex'),manifest.sha256);
assert.equal(provenance.sha256,manifest.sha256);assert.equal(provenance.bytes,manifest.bytes);
assert.equal(bytes.readUInt32LE(0),0x46546c67);assert.equal(bytes.readUInt32LE(4),2);assert.equal(bytes.readUInt32LE(8),bytes.length);
const jsonLength=bytes.readUInt32LE(12);assert.ok(jsonLength>0&&jsonLength%4===0&&jsonLength<=4*1024*1024&&28+jsonLength<=bytes.length);
assert.equal(bytes.readUInt32LE(16),0x4e4f534a);assert.equal(bytes.readUInt32LE(24+jsonLength),0x004e4942);assert.equal(28+jsonLength+bytes.readUInt32LE(20+jsonLength),bytes.length);
const gltf=JSON.parse(bytes.subarray(20,20+jsonLength).toString('utf8'));
assert.equal(gltf.asset.version,'2.0');assert.ok(gltf.skins?.length>0);
assert.equal(gltf.buffers?.length,1);assert.equal(gltf.buffers[0].uri,undefined);
assert.ok((gltf.images??[]).every(image=>Number.isInteger(image.bufferView)&&image.uri===undefined));
assert.ok(Number.isInteger(manifest.walkClipIndex)&&manifest.walkClipIndex>=-1&&manifest.walkClipIndex<=31&&manifest.walkClipIndex<(gltf.animations?.length??0));
console.log(`Included Maestro integrity verified: ${bytes.length} bytes, ${gltf.skins[0].joints.length} joints, ${gltf.animations?.length??0} embedded clips. Unity separately verifies the humanoid, walk and load budgets.`);

const motionManifestBytes=await readFile(new URL('Maestro/Resources/Avatars/IncludedMotions.json',root));
assert.ok(motionManifestBytes.length<=4*1024*1024);
const motions=JSON.parse(motionManifestBytes.toString('utf8'));
const motionProvenance=JSON.parse(await readFile(new URL('Maestro/Resources/Avatars/IncludedMotions.provenance.json',root),'utf8'));
assert.deepEqual(Object.keys(motions).sort(),['version','packId','revision','name','avatarHash','rigHash','catalogue','activities'].sort());
assert.equal(motions.version,2);assert.match(motions.packId,/^[a-z][a-z0-9.-]{0,79}$/);assert.ok(Number.isInteger(motions.revision)&&motions.revision>=1&&motions.revision<=1000000);
assert.equal(motions.avatarHash,manifest.sha256);assert.match(motions.rigHash,/^[a-f0-9]{64}$/);
assert.equal(motionProvenance.manifestSha256,createHash('sha256').update(motionManifestBytes).digest('hex'));assert.equal(motionProvenance.avatarSha256,manifest.sha256);
assert.equal(motionProvenance.packId,motions.packId);assert.equal(motionProvenance.revision,motions.revision);
const catalogue=motions.catalogue;assert.equal(catalogue.version,2);assert.ok(catalogue.entries.length>0&&catalogue.entries.length<=1024&&catalogue.sources.length<=1024);
const sources=new Map(catalogue.sources.map(source=>[source.hash,source]));assert.equal(sources.size,catalogue.sources.length);
for(const source of sources.values()){assert.match(source.hash,/^[a-f0-9]{64}$/);assert.equal(typeof source.name,'string');assert.ok(source.name.length<=100);assert.equal(typeof source.attribution,'string');assert.ok(source.attribution.length<=65536);}
const ids=new Set(),hashes=new Set();let motionBytes=0;
const float=(value)=>Math.fround(value);
const nodes=(doc)=>doc.nodes.map(node=>({name:node.name,children:node.children??[],...(node.matrix?{matrix:node.matrix.map(float)}:{translation:(node.translation??[0,0,0]).map(float),rotation:(node.rotation??[0,0,0,1]).map(float),scale:(node.scale??[1,1,1]).map(float)}),...(node.weights?{weights:node.weights.map(float)}:{})}));
const accessor=(doc,data,index)=>{const a=doc.accessors[index],v=doc.bufferViews[a.bufferView],width=({SCALAR:1,VEC3:3,VEC4:4,MAT4:16}[a.type]??0)*4;assert.equal(a.componentType,5126);assert.ok(width>0);const start=28+data.readUInt32LE(12)+(v.byteOffset??0)+(a.byteOffset??0),stride=v.byteStride??width;assert.ok(start+Math.max(0,a.count-1)*stride+width<=data.length);return Buffer.concat(Array.from({length:a.count},(_,i)=>data.subarray(start+i*stride,start+i*stride+width)));};
for(const entry of catalogue.entries){
 assert.match(entry.id,/^[a-f0-9]{32}$/);assert.ok(!ids.has(entry.id));ids.add(entry.id);assert.match(entry.hash,/^[a-f0-9]{64}$/);assert.ok(!hashes.has(entry.hash));hashes.add(entry.hash);assert.equal(entry.rigHash,motions.rigHash);
 assert.equal(entry.archived,false);assert.equal(entry.removed,false);assert.equal(entry.favourite,false);assert.ok(entry.bytes>=28&&entry.bytes<=8*1024*1024);assert.ok(entry.duration>0&&entry.duration<=3600&&entry.curveValues>0&&entry.curveValues<=800000);
 assert.ok(entry.name.trim().length>0&&entry.name.length<=100);assert.ok(entry.tags.length<=16&&entry.tags.every(tag=>tag.length>0&&tag.length<=32));assert.ok(entry.origins.length>0&&entry.origins.every(origin=>sources.has(origin.sourceHash)&&Number.isInteger(origin.clipIndex)&&origin.clipIndex>=0&&origin.clipIndex<32));
 const data=await readFile(new URL(`StreamingAssets/MaestroContent/Motions/${entry.hash}.motion`,root));assert.equal(data.length,entry.bytes);assert.equal(createHash('sha256').update(data).digest('hex'),entry.hash);motionBytes+=data.length;
 assert.equal(data.readUInt32LE(0),0x46546c67);assert.equal(data.readUInt32LE(4),2);assert.equal(data.readUInt32LE(8),data.length);const jsonSize=data.readUInt32LE(12);assert.equal(data.readUInt32LE(16),0x4e4f534a);assert.equal(data.readUInt32LE(24+jsonSize),0x004e4942);assert.equal(28+jsonSize+data.readUInt32LE(20+jsonSize),data.length);
 const doc=JSON.parse(data.subarray(20,20+jsonSize).toString('utf8'));assert.equal(doc.extras.maestroMotion.version,1);assert.equal(doc.extras.maestroMotion.axis,'Z');assert.equal(doc.animations.length,1);assert.equal(doc.buffers.length,1);assert.equal(doc.buffers[0].uri,undefined);assert.equal(doc.images,undefined);assert.equal(doc.materials,undefined);
 assert.deepEqual(nodes(doc),nodes(gltf));assert.equal(doc.skins.length,gltf.skins.length);
 for(let i=0;i<doc.skins.length;i++){assert.deepEqual(doc.skins[i].joints,gltf.skins[i].joints);assert.deepEqual(accessor(doc,data,doc.skins[i].inverseBindMatrices),accessor(gltf,bytes,gltf.skins[i].inverseBindMatrices));}
}
const packaged=(await readdir(new URL('StreamingAssets/MaestroContent/Motions/',root))).filter(name=>!name.endsWith('.meta')).sort();assert.deepEqual(packaged,[...hashes].map(hash=>`${hash}.motion`).sort());
assert.ok(motionBytes<=128*1024*1024);assert.equal(motionProvenance.uniqueMotions,ids.size);assert.equal(motionProvenance.motionBytes,motionBytes);assert.equal(motionProvenance.sourceFileCount,sources.size);
console.log(`Included motions integrity verified: ${ids.size} immutable clips, ${motionBytes} bytes, exact default-avatar hierarchy and inverse binds. Unity separately verifies playback and package installation.`);

assert.ok(Array.isArray(motions.activities)&&motions.activities.length<=4);
const roles=new Set();
for(const role of motions.activities){
 assert.deepEqual(Object.keys(role).sort(),['role','choices'].sort());assert.ok(Number.isInteger(role.role)&&role.role>=0&&role.role<=3&&!roles.has(role.role));roles.add(role.role);
 assert.ok(Array.isArray(role.choices)&&role.choices.length>=1&&role.choices.length<=4);const chosen=new Set();
 for(const choice of role.choices){
  assert.deepEqual(Object.keys(choice).sort(),['motionId','weight','speed','cooldown','loop'].sort());
  assert.ok(ids.has(choice.motionId)&&!chosen.has(choice.motionId));chosen.add(choice.motionId);assert.ok(catalogue.entries.find(x=>x.id===choice.motionId).duration>=.1);
  assert.ok(Number.isInteger(choice.weight)&&choice.weight>=1&&choice.weight<=10);assert.ok(Number.isFinite(choice.speed)&&choice.speed>=.25&&choice.speed<=2);
  assert.ok(Number.isFinite(choice.cooldown)&&choice.cooldown>=0&&choice.cooldown<=60);assert.equal(typeof choice.loop,'boolean');
 }
}
assert.deepEqual([...roles].sort(),[0,1,2,3]);
console.log('Included tutor-state defaults verified: exact compatible IDs for four editable starting states.');

const templateRoot=new URL('Maestro/Resources/Creation/Templates/',root);
const templateFiles=(await readdir(templateRoot)).filter(name=>name.endsWith('.json')).sort();
const shared=JSON.parse(await readFile(new URL('../shared/generated/behaviourCatalog.json',import.meta.url),'utf8'));
const templateKind=shared.actions.find(action=>action.id==='object.create').input.oneOf.find(branch=>branch.examples[0].kind==='template');
const choice=templateKind.properties.templateHash,templateHashes=[];
for(const file of templateFiles){
 const bytes=await readFile(new URL(file,templateRoot)),entry=JSON.parse(bytes.toString('utf8')),hash=createHash('sha256').update(bytes).digest('hex');
 assert.equal(bytes.includes(13),false,'Template bytes must use LF');assert.equal(entry.format,'maestro-creation-template');assert.equal(entry.version,1);assert.equal(entry.id+'.json',file);
 assert.equal(choice['x-enum-labels'][hash],entry.name);assert.equal(choice['x-enum-images'][hash],`quest/templates/${hash}.png`);
 const preview=await readFile(new URL(`../public/quest/templates/${hash}.png`,import.meta.url));assert.equal(preview.subarray(0,8).toString('hex'),'89504e470d0a1a0a');assert.equal(preview.readUInt32BE(16),512);assert.equal(preview.readUInt32BE(20),512);
 templateHashes.push(hash);
}
assert.deepEqual([...choice.enum].sort(),templateHashes.sort());
assert.deepEqual((await readdir(new URL('../public/quest/templates/',import.meta.url))).sort(),templateHashes.map(hash=>hash+'.png').sort());
console.log(`Starter templates verified: ${templateFiles.length} exact bundled definitions and Unity previews. Native tests cover component validity and physics.`);

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,stat} from 'node:fs/promises';
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

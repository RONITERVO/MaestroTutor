// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFile, readdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
const root=new URL('../',import.meta.url);
const manifest=JSON.parse(await readFile(new URL('shared/generated/behaviourCatalog.json',root),'utf8'));
// Keep this scope identical to QuestBehaviourCatalog.Export. Adding a native
// helper must invalidate old provenance without anyone editing another list.
const nativePrefix='unity/MaestroQuest/Assets/Maestro/';
async function discover(directory,accept) {
 const paths=[];
 for(const entry of await readdir(new URL(nativePrefix+directory+'/',root),{withFileTypes:true})) {
  const path=directory+'/'+entry.name;
  if(entry.isDirectory())paths.push(...await discover(path,accept));
  else if(entry.isFile()&&accept(entry.name))paths.push(path);
 }
 return paths;
}
const sources=[
 ...await discover('Runtime',name=>/\.(cs|shader|cginc|hlsl|asmdef|asmref)$/i.test(name)),
 'Resources/MaestroRoomAudio.mixer',
 ...await discover('Resources/Creation/Templates',name=>/\.json$/i.test(name)),
 ...await discover('Resources/Programs/Modules',name=>/\.json$/i.test(name)),
].sort().map(path=>nativePrefix+path);
if(manifest.version!==1||JSON.stringify(manifest.sources?.map(source=>source.path))!==JSON.stringify(sources))throw new Error('Invalid behaviour catalog provenance.');
for(const source of manifest.sources) {
 const content=(await readFile(new URL(source.path,root),'utf8')).replace(/^\uFEFF/,'').replace(/\r\n/g,'\n');
 if(createHash('sha256').update(content).digest('hex')!==source.sha256)
  throw new Error(`Native catalog changed: ${source.path}. Regenerate with unity/Tools/Verify-Quest.ps1 -UpdateBehaviourCatalog and review the manifest.`);
}
console.log(`Behaviour catalog source check passed (${fileURLToPath(root)}). Native export equality is checked by Verify-Quest.ps1.`);

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
const root=new URL('../',import.meta.url);
const manifest=JSON.parse(await readFile(new URL('shared/generated/behaviourCatalog.json',root),'utf8'));
const sources=['Runtime/Programs/BehaviourCatalog.cs','Runtime/Programs/CapabilityArguments.cs', 'Runtime/Programs/BehaviourProgram.cs','Runtime/Rules/RuleDocument.cs','Runtime/Creation/RoomRecipe.cs','Runtime/Creation/RecipeTemplates.cs'].map(path=>'unity/MaestroQuest/Assets/Maestro/'+path);
if(manifest.version!==1||JSON.stringify(manifest.sources?.map(source=>source.path))!==JSON.stringify(sources))throw new Error('Invalid behaviour catalog provenance.');
for(const source of manifest.sources) {
 const content=(await readFile(new URL(source.path,root),'utf8')).replace(/\r\n/g,'\n');
 if(createHash('sha256').update(content).digest('hex')!==source.sha256)
  throw new Error(`Native catalog changed: ${source.path}. Regenerate with unity/Tools/Verify-Quest.ps1 -UpdateBehaviourCatalog and review the manifest.`);
}
console.log(`Behaviour catalog source check passed (${fileURLToPath(root)}). Native export equality is checked by Verify-Quest.ps1.`);

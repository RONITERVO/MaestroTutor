// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync,writeFileSync} from 'node:fs';
import {ROOM_GUIDES} from '../shared/prompts/roomguides';
const path=new URL('../unity/MaestroQuest/Assets/Maestro/Resources/RoomGuides.json',import.meta.url);
const source=JSON.stringify({version:1,guides:ROOM_GUIDES},null,2)+'\n';
if(process.argv.includes('--write'))writeFileSync(path,source);
else if(readFileSync(path,'utf8').replace(/^\uFEFF/,'').replace(/\r\n/g,'\n')!==source)throw new Error('Bundled room guides differ from shared/prompts. Run node --import tsx scripts/generate-room-guides.ts --write and review the native resource.');
console.log('Bundled room guides match the shared references ('+ROOM_GUIDES.length+').');

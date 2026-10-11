// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { roomControlFields } from './prompts/roomcontrols';
export const roomCommandFields = {
  ...roomControlFields, execution:['execution'], catalog:['catalog'], avatarActivities:['activities'], motions:['target','motionQuery'],
  create:['reference','name','kind','atPosition','position','scale','color','recipe'], move:['target','position'], resize:['target','scale'],
  paint:['target','color'], recipe:['target','recipe'], delete:['target'], undo:[], redo:[], inspect:['target','partId'], workspace:['visible'], play:['target'], stop:['target'], rules:['rule'],
} as const;

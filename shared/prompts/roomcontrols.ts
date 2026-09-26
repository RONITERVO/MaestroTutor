// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export const roomControlFields = {
  physicsSettings:['target','physics'], avatarSettings:['target','movement'], physicsRun:['operation'], avatarMotion:['target','operation'],
} as const;
export const roomControlProperties = {
  operation:{type:'string',enum:['start','pause','look','follow','stop']},
  physics:{type:'object',properties:{mode:{type:'string',enum:['fixed','solid','bouncy']},shape:{type:'string',enum:['automatic','box','sphere']},mass:{type:'number',minimum:.05,maximum:20}},required:['mode','shape','mass'],additionalProperties:false},
  movement:{type:'object',properties:{distance:{type:'number',minimum:.8,maximum:2.5},speed:{type:'number',minimum:.2,maximum:1.2}},required:['distance','speed'],additionalProperties:false},
};

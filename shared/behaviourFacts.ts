// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourFact,type BehaviourValueType} from './behaviourCatalog';
import {validateCapabilityValue,schemaField} from './capabilities';
import {checkedDataValue,sameDataType} from './programValues';
export function validateFactArguments(id:string,version:number,args:unknown):string|null {
 const definition=behaviourFact(id);if(!definition||definition.version!==version)return 'Unknown fact version';
 return definition.input?validateCapabilityValue(args,definition.input):args===undefined?null:'This fact has no arguments';
}
export function factArgumentType(id:string,path:string):BehaviourValueType|null {
 const field=schemaField(behaviourFact(id)?.input,path);if(field?.['x-static'])return null;
 const type=field?.type;return type==='string'?'text':type==='integer'?'number':type==='number'||type==='boolean'?type:null;
}
export function validFactValue(id:string,value:unknown):boolean {
 const fact=behaviourFact(id);if(!fact)return false;
 try {return sameDataType(checkedDataValue(value,fact.type),fact.type);}catch{return false;}
}

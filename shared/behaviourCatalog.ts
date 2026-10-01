// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import manifest from './generated/behaviourCatalog.json';
import type {DataType} from './programValues';
import type {CapabilitySchema} from './capabilities';
export type BehaviourValueType='number'|'boolean'|'text';
/** Generated native vocabulary, not a claim that a target or device can run it. */
export const behaviourCatalog=manifest;
export const behaviourFactTypes:Readonly<Record<string,DataType>>=Object.freeze(Object.fromEntries(
 manifest.facts.map(fact=>[fact.id,fact.type as DataType])));
export interface BehaviourFactDefinition {domain?:'room'|'workspace';id:string;version:number;type:DataType;label:string;description:string;input?:CapabilitySchema;example?:Record<string,unknown>;features?:string[]}
const facts=new Map((structuredClone(manifest.facts) as unknown as BehaviourFactDefinition[]).map(fact=>[fact.id,fact]));
export function behaviourFact(id:string):BehaviourFactDefinition|null {const fact=facts.get(id);return fact?structuredClone(fact):null;}

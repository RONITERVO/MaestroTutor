// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import manifest from './generated/behaviourCatalog.json';
export type BehaviourValueType='number'|'boolean'|'text';
/** Generated native vocabulary, not a claim that a target or device can run it. */
export const behaviourCatalog=manifest;
export const behaviourFactTypes:Readonly<Record<string,BehaviourValueType>>=Object.freeze(Object.fromEntries(
 manifest.facts.map(fact=>[fact.id,fact.type as BehaviourValueType])));
export interface BehaviourFactDefinition {id:string;version:number;type:BehaviourValueType;label:string;description:string}
const facts=new Map((structuredClone(manifest.facts) as unknown as BehaviourFactDefinition[]).map(fact=>[fact.id,fact]));
export function behaviourFact(id:string):BehaviourFactDefinition|null {const fact=facts.get(id);return fact?structuredClone(fact):null;}

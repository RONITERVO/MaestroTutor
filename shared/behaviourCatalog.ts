// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import manifest from './generated/behaviourCatalog.json';
export type BehaviourValueType='number'|'boolean'|'text';
/** Generated native vocabulary, not a claim that a target or device can run it. */
export const behaviourCatalog=manifest;
export const behaviourFactTypes:Readonly<Record<string,BehaviourValueType>>=Object.freeze(Object.fromEntries(
 manifest.facts.map(fact=>[fact.id,fact.type as BehaviourValueType])));
export const behaviourFactGuide=manifest.facts.map(fact=>`${fact.id} (${fact.type})`).join(', ');

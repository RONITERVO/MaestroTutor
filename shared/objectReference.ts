// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validFactValue} from './behaviourFacts';
import {validCatalogView,type CatalogRequest,type CatalogView} from './roomCatalog';
export type ObjectReferenceSnapshot={id:string;name:string;kind:string;revision:number};
export const objectReferenceRequest=(target:string):CatalogRequest=>({operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target}});
/** A display snapshot only. Actions still read their own guarded current inputs. */
export function readObjectReference(view:CatalogView|null|undefined,target:string):ObjectReferenceSnapshot|null {
 if(!validCatalogView(view)||view.operation!=='inspect'||view.category!=='facts'||view.capability!=='object.definition'||view.version!==1||view.arguments?.target!==target||!view.available||!validFactValue('object.definition',view.value))return null;
 const value=view.value as Record<string,unknown>;
 if(value.target!==target||typeof value.name!=='string'||typeof value.kind!=='string'||!Number.isInteger(value.revision)||Number(value.revision)<1||Number(value.revision)>2147483647)return null;
 return {id:target,name:value.name,kind:value.kind,revision:value.revision as number};
}

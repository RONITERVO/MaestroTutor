// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {parseProgram} from './programs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityFeatures,validateCapabilityArguments} from '../../../shared/capabilities';
import {validTerrainReservoirs} from '../../../shared/terrainReservoir';
import {validPrototypeResources} from '../../../shared/creationResources';
const request=()=>structuredClone(capabilityDefinition('object.container.edit')!.example!) as {definition:Record<string,unknown>};
it('exposes edge spilling through the existing container action with explicit native support',()=>{
 const args=request();Object.assign(args.definition,{terrainFloor:true,spillAtTerrainEdge:true});
 expect(validateCapabilityArguments('object.container.edit',1,args)).toBeNull();
 expect(capabilityFeatures('object.container.edit',args)).toContain('terrainEdgeSpill.v1');
 args.definition.terrainFloor=false;expect(validateCapabilityArguments('object.container.edit',1,args)).not.toBeNull();
 delete args.definition.terrainFloor;expect(validateCapabilityArguments('object.container.edit',1,args)).not.toBeNull();
});
it('keeps old closed containers valid without silently enabling spilling',()=>{
 const args=request();delete args.definition.spillAtTerrainEdge;
 expect(validateCapabilityArguments('object.container.edit',1,args)).toBeNull();
 expect(capabilityFeatures('object.container.edit',args)).not.toContain('terrainEdgeSpill.v1');
 args.definition.terrainFloor=true;expect(validateCapabilityArguments('object.container.edit',1,args)).toBeNull();
 args.definition.spillAtTerrainEdge='true';expect(validateCapabilityArguments('object.container.edit',1,args)).not.toBeNull();
});
it('applies the same canonical bed checks to an open terrain reservoir',()=>{
 const frame={position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1}};
 const bed={frame,width:2,depth:1,cells:4,heights:Array<number>(25).fill(.125)};
 const liquid={version:4,frame,rectangle:{width:2,depth:1},height:.5,capacityMl:750000,amountMl:500000};
 expect(validTerrainReservoirs({mode:'fixed'},[bed],[liquid])).toBe(true);
 expect(validTerrainReservoirs({mode:'solid'},[bed],[liquid])).toBe(false);
 expect(validTerrainReservoirs({mode:'fixed'},[],[liquid])).toBe(false);
 expect(validTerrainReservoirs({mode:'fixed'},[bed],[{...liquid,capacityMl:750001}])).toBe(false);
 expect(validTerrainReservoirs({mode:'fixed'},[bed],[{...liquid,amountMl:750001}])).toBe(false);
});
it.each([9,10])('preserves shared environment resources in current reusable prototype version %i',version=>{
 expect(validPrototypeResources({version,geometry:{kind:'block'},environmentProfile:'a'.repeat(32)})).toBe(true);
 expect(validPrototypeResources({version,geometry:{kind:'block'},visibilityLayer:'b'.repeat(32)})).toBe(true);
});

it('keeps the original typed container program valid while new authoring uses the complete observation',()=>{
 expect(parseProgram(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/current-container-program.json','utf8')).error).toBeNull();
 expect(JSON.stringify(behaviourFact('object.container')!.type)).not.toContain('spillAtTerrainEdge');
 expect(JSON.stringify(behaviourFact('object.container.configuration')!.type)).toContain('spillAtTerrainEdge');
});

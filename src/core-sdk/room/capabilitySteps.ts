// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourCatalog} from '../../../shared/behaviourCatalog';
import {argumentValue,type CapabilityInvocation} from '../../../shared/capabilities';
import {type RuleStep} from './ruleSteps';
import {type Vec3,type Rotation} from './recipe';
const ids:readonly string[]=behaviourCatalog.adapters.ruleStep.actionIds;
const adapters=behaviourCatalog.adapters.ruleStep.invocations;
const clone=<T>(v:T):T=>JSON.parse(JSON.stringify(v));
function setPath(args:Record<string,unknown>,path:string,value:unknown){const parts=path.split('.');for(const key of parts.slice(0,-1)){if(!args[key])args[key]={};args=args[key] as Record<string,unknown>;}args[parts[parts.length-1]]=clone(value);}
const gestures=['greeting','pointing','listening','speaking','idle','walk'];
/** Private adapter for existing physical/simple controls. Saved programs use named calls. */
export function stepInvocation(step:RuleStep):CapabilityInvocation {
 const id=ids[step.action];if(!id)throw new Error('Unknown capability');
 const args:Record<string,unknown>={};
 if(![2,12,13].includes(step.action))args.target=step.targetId;
 if(![3,10,11,12,13,14,15,16,17].includes(step.action))args.seconds=step.seconds;
 if(step.action===12)Object.assign(args,step.creation??{shape:'ball',name:'',x:.3,y:1.3,z:.65,scale:1,red:1,green:1,blue:1});
 if(step.action===13){const c=step.creation;Object.assign(args,{name:c?.name??'',x:c?.x??.3,y:c?.y??1.3,z:c?.z??.65,scale:c?.scale??1,recipe:step.creationRecipe?JSON.parse(JSON.stringify(step.creationRecipe)):null});}
 if(step.action===14)Object.assign(args,step.editPosition??{x:0,y:0,z:0});
 if(step.action===15)args.scale=step.editScale??1;
 if(step.action===16)Object.assign(args,step.editColor??{red:1,green:1,blue:1});
 if(step.action===10){args.x=step.impulse?.x??0;args.y=step.impulse?.y??0;args.z=step.impulse?.z??0;}
 if(step.action===1||step.action===9)args.gesture=gestures[step.gesture]??'invalid';
 if([0,6,7,8].includes(step.action))args.loop=step.loop;
 if(step.action===6){args.modelHash=step.clipModelHash??'';args.clipIndex=step.clipIndex??0;}
 if(step.action===7)args.motionId=step.motionId??'';
 if(step.propId)args.prop={objectId:step.propId,avatarHash:step.propAvatarHash??'',hand:['left','right'][step.propHand??1],
  release:['return','drop','throw'][step.propRelease??0],releaseAt:step.propReleaseAt??1,
  offset:step.propOffset??{x:0,y:0,z:0},rotation:step.propRotation??{x:0,y:0,z:0,w:1}};
 const adapter=adapters.find(value=>value.id===id);if(!adapter)return {id,version:1,arguments:args};
 const result:Record<string,unknown>={};for(const [key,path] of Object.entries(adapter.fields))if(args[key]!==undefined)setPath(result,path,args[key]);
 for(const [path,value] of Object.entries(adapter.selectors))setPath(result,path,value);
 return {id:adapter.capability,version:1,arguments:result};
}
/** Decode a detached editor view. Callers validate the public schema and domain before applying.
 * Draft numeric values remain editable even when outside their permitted bounds.
 */
export function invocationStep(call:CapabilityInvocation,id:string):RuleStep {
 const adapter=adapters.find(value=>value.capability===call.id&&Object.entries(value.selectors).every(([path,expected])=>argumentValue(call.arguments,path)===expected));
 const action=ids.indexOf(adapter?.id??call.id);if(action<0||action>17||call.version!==1)throw new Error('Unknown capability or unsupported capability version');
 const a:Record<string,unknown>=adapter?Object.fromEntries(Object.entries(adapter.fields).map(([key,path])=>[key,argumentValue(call.arguments,path)]).filter(([,value])=>value!==undefined)):{...call.arguments};
 const p=a.prop as Record<string,unknown>|undefined;
 return {id,action,targetId:(a.target??'maestro') as string,seconds:(a.seconds??0) as number,
  gesture:a.gesture===undefined?0:gestures.indexOf(a.gesture as string),loop:(a.loop??false) as boolean,
  clipModelHash:(a.modelHash??'') as string,clipIndex:(a.clipIndex??0) as number,motionId:(a.motionId??'') as string,
  ...(action===14?{editPosition:{x:a.x as number,y:a.y as number,z:a.z as number}}:{}),
  ...(action===15?{editScale:a.scale as number}:{}),
  ...(action===16?{editColor:{red:a.red as number,green:a.green as number,blue:a.blue as number}}:{}),
  ...(action===13?{creation:{shape:'ball',name:a.name as string,x:a.x as number,y:a.y as number,z:a.z as number,scale:a.scale as number,red:1,green:1,blue:1},creationRecipe:JSON.parse(JSON.stringify(a.recipe)) as RuleStep['creationRecipe']}:{}),
  ...(action===12?{creation:a as RuleStep['creation']}:{}),
  ...(action===10?{impulse:{x:a.x as number,y:a.y as number,z:a.z as number}}:{}),
  ...(p?{propId:p.objectId as string,propAvatarHash:p.avatarHash as string,propHand:['left','right'].indexOf(p.hand as string),
   propRelease:['return','drop','throw'].indexOf(p.release as string),propReleaseAt:p.releaseAt as number,
   propOffset:p.offset as Vec3,propRotation:p.rotation as Rotation}:{})};
}

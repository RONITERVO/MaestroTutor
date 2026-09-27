// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourCatalog} from '../../../shared/behaviourCatalog';
import {type CapabilityInvocation} from '../../../shared/capabilities';
import {type RuleStep} from './ruleSteps';
import {type Vec3,type Rotation} from './recipe';
const ids:readonly string[]=behaviourCatalog.adapters.ruleStep.actionIds;
const gestures=['greeting','pointing','listening','speaking','idle','walk'];
/** Private adapter for existing physical/simple controls. Saved programs use named calls. */
export function stepInvocation(step:RuleStep):CapabilityInvocation {
 const id=ids[step.action];if(!id)throw new Error('Unknown capability');
 const args:Record<string,unknown>={};
 if(step.action!==2)args.target=step.targetId;
 if(step.action!==3)args.seconds=step.seconds;
 if(step.action===1)args.gesture=gestures[step.gesture]??'invalid';
 if([0,6,7,8].includes(step.action))args.loop=step.loop;
 if(step.action===6){args.modelHash=step.clipModelHash??'';args.clipIndex=step.clipIndex??0;}
 if(step.action===7)args.motionId=step.motionId??'';
 if(step.propId)args.prop={objectId:step.propId,avatarHash:step.propAvatarHash??'',hand:['left','right'][step.propHand??1],
  release:['return','drop','throw'][step.propRelease??0],releaseAt:step.propReleaseAt??1,
  offset:step.propOffset??{x:0,y:0,z:0},rotation:step.propRotation??{x:0,y:0,z:0,w:1}};
 return {id,version:1,arguments:args};
}
/** Decode a detached editor view. Callers validate the public schema and domain before applying.
 * Draft numeric values remain editable even when outside their permitted bounds.
 */
export function invocationStep(call:CapabilityInvocation,id:string):RuleStep {
 const action=ids.indexOf(call.id);if(action<0||call.version!==1)throw new Error('Unknown capability or unsupported capability version');
 const a=call.arguments,p=a.prop as Record<string,unknown>|undefined;
 return {id,action,targetId:(a.target??'maestro') as string,seconds:(a.seconds??0) as number,
  gesture:a.gesture===undefined?0:gestures.indexOf(a.gesture as string),loop:(a.loop??false) as boolean,
  clipModelHash:(a.modelHash??'') as string,clipIndex:(a.clipIndex??0) as number,motionId:(a.motionId??'') as string,
  ...(p?{propId:p.objectId as string,propAvatarHash:p.avatarHash as string,propHand:['left','right'].indexOf(p.hand as string),
   propRelease:['return','drop','throw'].indexOf(p.release as string),propReleaseAt:p.releaseAt as number,
   propOffset:p.offset as Vec3,propRotation:p.rotation as Rotation}:{})};
}

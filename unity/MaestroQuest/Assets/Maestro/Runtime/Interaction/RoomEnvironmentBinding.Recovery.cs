// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class RoomEnvironmentBinding
    {
        internal bool RestorationHeld {get;private set;}
        internal string RestorationReason {get;private set;}="";
        internal string RestorationState {get;private set;}=Guid.NewGuid().ToString("N");
        readonly RoomEnvironmentClearance clearance=new();
        bool retryRequired;
        internal static bool RestorationReady(RoomItem target)=>!target||!target.TryGetComponent<RoomEnvironmentBinding>(out var binding)||!binding.RestorationHeld;
        internal void BeginRestoration(bool previousHold)
        {
            retryRequired=previousHold;SetRestoration(true,"Preparing restored collision geometry");
            item?.GetComponent<RigidRoomItem>()?.Teleported();
        }
        void SetRestoration(bool held,string reason)
        {
            RestorationHeld=held;RestorationReason=Imports.ImportObservation.Text(reason);RestorationState=Guid.NewGuid().ToString("N");
        }
        internal bool CanRetry(out string error)
        {
            error="The restored object's environment is unavailable";
            if(!isActiveAndEnabled||!item||!item.isActiveAndEnabled||!world)return false;
            if(!world.EnvironmentActive){error=world.RuntimeHoldReason??"Return to the active room with accepted collision inputs before checking this object";return false;}
            if(item.Grab.isSelected){error="Release the object before checking its restored collisions";return false;}
            var rigid=item.GetComponent<RigidRoomItem>();
            if(rigid&&!rigid.GeometryReady){error="Object collision geometry is still loading; retry after it is ready";return false;}
            if(rigid&&rigid.AnimationOwned){error="Stop the object's animation or carried action before checking its collisions";return false;}
            error=null;return true;
        }
        // Publication checks enabled, kinematic shapes. Inactive PhysX shapes
        // cannot prove penetration. A previously failed hold needs fresh intent.
        internal void FinishRestoration()=>CheckRestoration(false);
        internal void RetryRestoration()=>CheckRestoration(true);
        void CheckRestoration(bool explicitIntent)
        {
            if(!RestorationHeld)return; // An already-satisfied retry preserves motion.
            var rigid=item?item.GetComponent<RigidRoomItem>():null;
            if(!CanRetry(out var error)||!clearance.Check(world,item,RealCollisions,world.IncludesRealRoom(item)||rigid&&rigid.Dynamic,out error)){
                retryRequired=true;SetRestoration(true,error);
            }else if(retryRequired&&!explicitIntent){
                SetRestoration(true,"Restored geometry is ready; check this object or start physics to resume from rest");
            }else{
                retryRequired=false;SetRestoration(false,"");
            }
            if(RestorationHeld)item?.GetComponent<RecipeObject>()?.Stop();
            // Discard both current and prepared momentum before releasing a hold.
            rigid?.Teleported();
        }
    }
}

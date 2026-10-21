// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    internal sealed class RoomAquaticClearance:IDisposable {
        readonly RoomEditor editor;readonly RoomItem item;readonly string target,habitat;
        readonly Func<bool> owned;RoomSpatialInputs inputs;readonly RoomBoxMotor motor=new();
        readonly Func<Collider,bool> obstacle;
        Bounds local;Vector3 scale;
        internal bool Pending=>inputs.Pending;
        internal int Generation {get;private set;}
        internal RoomAquaticClearance(RoomEditor editor,RoomItem item,string habitat,Func<bool> owned){
            this.editor=editor;this.item=item;this.habitat=habitat;this.owned=owned;target=editor.Identity(item);
            inputs=NewInputs();
            obstacle=c=>RoomWalkingObstacle.Blocks(c,item.transform,target);
        }
        RoomSpatialInputs NewInputs()=>new(editor,target,owned,false,purpose:"Swimming",followPhysicsPlacements:true);
        internal bool Body(out Bounds value,out string error){
            value=default;error="Swimming needs current accepted root colliders";
            if(!editor.MovementBody(target,out local))return false;
            scale=item.transform.lossyScale;value=local;
            var size=Vector3.Scale(local.size,scale);
            if(!RoomRecipe.Finite(size)||size.x<=0||size.y<=0||size.z<=0||size.magnitude>5){error="The swimming collision envelope must fit within five metres";return false;}
            error=null;return true;
        }
        internal bool Prepare(Vector3 goal,out string error){
            // A completed input set may become stale during ordinary water/room
            // updates. Acquire the new world and discard the old route; each
            // body/step still needs fresh containment and collision proof.
            // Failed or cancelled acquisition is never revived this way.
            if(inputs.SourceChanged){inputs.Dispose();inputs=NewInputs();Generation++;}
            if(!Body(out _,out error))return false;
            // Include every orientation and the entire bounded search corridor.
            var far=new Vector3(Mathf.Max(Mathf.Abs(local.min.x),Mathf.Abs(local.max.x)),Mathf.Max(Mathf.Abs(local.min.y),Mathf.Abs(local.max.y)),Mathf.Max(Mathf.Abs(local.min.z),Mathf.Abs(local.max.z)));
            float radius=Vector3.Scale(far,scale).magnitude;
            var bounds=new Bounds(item.transform.position,Vector3.one*(2*radius));bounds.Encapsulate(new Bounds(goal,Vector3.one*(2*radius)));bounds.Expand(RoomAquaticRoute.SearchMargin*2);
            var targets=editor.SpatialMovementTargets(editor.SpatialInputRoomBounds(bounds),null)
                .Concat(editor.Liquids.NavigationCavities(bounds)).Append(habitat).Where(x=>x!=target).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(!targets.SequenceEqual(inputs.Targets))Generation++;
            return inputs.Require(targets,(string[] ids,out string issue)=>editor.SpatialMovementReady(ids,null,out issue),out error);
        }
        internal bool Clear(Pose from,Pose to,out string error){
            error="The aquatic world or physics is unavailable";
            var world=editor?editor.PhysicsWorld:null;
            if(!owned()||!world||!world.SimulationActive||!editor.Frame.Valid||!editor.Liquids)return false;
            error="Aquatic movement would leave the authored world bounds";
            if(!RoomViewpoint.ValidPosition(editor.Frame.PointToRoom(from.position))||!RoomViewpoint.ValidPosition(editor.Frame.PointToRoom(to.position)))return false;
            var body=new RoomSweptBody(local,scale,from.position,from.rotation,to.position,to.rotation);
            var admission=editor.Liquids.CheckHabitatPath(item,habitat,body.From,body.To,body.Extents);
            if(!admission.Availability.Ready||!admission.Contained){error=admission.Reason??"The selected habitat is unavailable";return false;}
            if(world.IncludesRealRoom(item)){
                error="The swimming body leaves the accepted physical room";
                if(!world.EnvironmentReady(item))return false;
                for(int end=0;end<2;end++)for(int i=0;i<8;i++){
                    var p=(end==0?body.From:body.To)+Vector3.Scale(body.Extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    if(!world.ContainsSimulation(p,item))return false;
                }
            }
            error="The swimming body would pass too close to the user";
            if(!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy||!RoomRecipe.Finite(editor.Viewer.position)||
                CatchGeometry.SegmentDistance(editor.Viewer.position,body.From,body.To)<body.Extents.magnitude+.35f)return false;
            int mask=world.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),item);
            error="The swimming body or its turn is blocked by solid geometry (or the bounded query is full)";
            if(!motor.Clear(body,mask,obstacle))return false;
            error=null;return true;
        }
        public void Dispose(){inputs.Dispose();motor.Release();}
    }
}

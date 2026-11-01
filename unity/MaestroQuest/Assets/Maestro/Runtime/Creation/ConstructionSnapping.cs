// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using UnityEngine;
namespace Maestro.Quest.Creation {
    [Serializable] public sealed class ConstructionSnapSettings {
        public string stateId=Guid.NewGuid().ToString("N"),mode="off";
        public float distance=.08f,turnStep=90,breakForce=45,breakTorque=3;
        internal bool Valid=>(mode is "off" or "place" or "join")&&float.IsFinite(distance)&&distance>=.01f&&distance<=.25f&&float.IsFinite(turnStep)&&turnStep>=0&&turnStep<=180&&float.IsFinite(breakForce)&&breakForce>=0&&breakForce<=10000&&float.IsFinite(breakTorque)&&breakTorque>=0&&breakTorque<=10000;
        public ConstructionSnapSettings Copy()=>(ConstructionSnapSettings)MemberwiseClone();
    }
    public sealed partial class RoomEditor {
        ConstructionSnapSettings constructionSnap=new();
        public ConstructionSnapSettings ObserveConstructionSnapping()=>constructionSnap.Copy();
        internal bool CanConfigureConstructionSnapping(ConstructionSnapSettings next,out string error){
            error="Read current snapping settings before editing them";
            if(next==null||!next.Valid||next.stateId!=constructionSnap.stateId)return false;
            if(RuntimeGate.Held||Ownership.Suspended||WriteGate.Frozen){error="Room interaction is paused";return false;}
            if(GetComponent<ConstructionManipulator>()?.Holding==true){error="Release the construction handle before changing snapping";return false;}
            error=null;return true;
        }
        internal bool ConfigureConstructionSnapping(ConstructionSnapSettings next,out string error){
            if(!CanConfigureConstructionSnapping(next,out error))return false;
            constructionSnap=next.Copy();constructionSnap.stateId=Guid.NewGuid().ToString("N");
            SetStatus(next.mode=="off"?"Construction snapping off":"Construction snapping: "+next.mode+" — grip the construction handle near compatible points");return true;
        }
        internal bool SnapDestinationReady(string id,int revision){
            var item=Find(id);
            return item&&ObjectRevision(id)==revision&&item.isActiveAndEnabled&&item.Grab&&item.Grab.enabled&&!item.Grab.isSelected&&item.GetComponent<RigidRoomItem>()?.GeometryReady!=false&&GetComponent<AnimationWorkshop>()?.ControlsTarget(id)!=true;
        }
    }
    /// <summary>Pure bounded point matching. Frames coincide; no collision or occupancy inference.</summary>
    internal static class ConstructionSnapMath {
        internal static bool Match(ObjectPlacement moving,RoomSnapPoint from,RoomObjectData destination,RoomSnapPoint to,float radius,float step,out float distance,out float turn){
            distance=float.PositiveInfinity;turn=0;if(from.family!=to.family)return false;
            var a=moving.position+moving.rotation*(from.frame.position*moving.scale);
            var b=destination.position+destination.rotation*(to.frame.position*destination.scale);
            distance=(a-b).sqrMagnitude;if(distance>radius*radius)return false;
            var sourceRotation=moving.rotation*from.frame.rotation;var targetRotation=destination.rotation*to.frame.rotation;
            var delta=Quaternion.Inverse(targetRotation)*sourceRotation;
            // Preserve twist about the mating frame, correcting at most 30 degrees of tilt.
            if(Vector3.Angle(delta*Vector3.up,Vector3.up)>30)return false;
            var forward=delta*Vector3.forward;if(forward.x*forward.x+forward.z*forward.z<.0001f)return false;
            turn=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            if(step>0)turn=Mathf.Round(turn/step)*step;
            turn=Mathf.DeltaAngle(0,turn);
            return true;
        }
    }
    /// <summary>Grip-local cache. Saved components are copied once, not deserialized per frame.</summary>
    internal sealed class ConstructionSnapPreview {
        sealed class Point {public int index,revision;public RoomObjectData data;public RoomSnapPoint point;public RoomItem item;public BehaviourCatalog.Claim[] claims;}
        readonly RoomEditor editor;readonly TransformMember[] members;readonly RoomLayout before;readonly ConstructionSnapSettings settings;
        Point[] sources,targets;Point chosenSource,chosenTarget;string marking;
        RoomLayout pivotBefore;readonly RoomGroupTransform projection=new();
        internal RoomSnapPlacement Request {get;private set;}
        internal RoomLayout Layout {get;private set;}
        internal string Marking=>Request==null?null:marking;
        internal Vector3 Marker=>chosenTarget.data.position+chosenTarget.data.rotation*(chosenTarget.point.frame.position*chosenTarget.data.scale);
        internal ConstructionSnapPreview(RoomEditor editor,TransformMember[] members,RoomLayout before,ConstructionSnapSettings settings){this.editor=editor;this.members=members;this.before=before;this.settings=settings;Refresh();}
        internal void Refresh(){
            var objects=editor.Snapshot().objects;
            bool joinRoom=objects.Sum(o=>o.connections?.Length??0)<RoomConnection.MaximumRoomConnections;
            sources=objects.Where(o=>members.Any(m=>m.target==o.id)&&(settings.mode!="join"||joinRoom&&o.physics!=ItemPhysics.Fixed&&(o.connections?.Length??0)==0)).SelectMany(o=>(o.snapPoints??Array.Empty<RoomSnapPoint>()).Select(p=>new Point{data=o,point=p,index=Array.FindIndex(members,m=>m.target==o.id)})).OrderBy(p=>p.data.id,StringComparer.Ordinal).ThenBy(p=>p.point.id,StringComparer.Ordinal).ToArray();
            targets=objects.Where(o=>!o.IsBuiltIn&&!members.Any(m=>m.target==o.id)&&o.recipe?.playing!=true).SelectMany(o=>(o.snapPoints??Array.Empty<RoomSnapPoint>()).Select(p=>new Point{data=o,point=p,revision=editor.ObjectRevision(o.id),item=editor.Find(o.id),claims=new[]{new BehaviourCatalog.Claim(o.id,"wholeTarget")}})).OrderBy(p=>p.data.id,StringComparer.Ordinal).ThenBy(p=>p.point.id,StringComparer.Ordinal).ToArray();
        }
        internal bool Update(RoomLayout free,float scale){
            Point bestSource=null,bestTarget=null;float nearest=float.PositiveInfinity,bestTurn=0;
            if(settings.mode!="off"&&members.Length<=15)foreach(var target in targets){
                if(!editor.SnapDestinationReady(target.data.id,target.revision))continue;
                target.data.position=target.item.transform.localPosition;target.data.rotation=target.item.transform.localRotation;target.data.scale=target.item.transform.localScale.x;
                foreach(var source in sources){
                    if(!ConstructionSnapMath.Match(free.placements[source.index],source.point,target.data,target.point,settings.distance,settings.turnStep,out float distance,out float turn)||distance>=nearest)continue;
                    if(!editor.Ownership.CanAcquire("construction-snap-destination",RoomActorRole.Control,target.claims,out _))continue;
                    bestSource=source;bestTarget=target;nearest=distance;bestTurn=turn;
                }
            }
            if(bestSource==null){Request=null;chosenSource=chosenTarget=null;return false;}
            if(Request==null||bestSource!=chosenSource||bestTarget!=chosenTarget){
                chosenSource=bestSource;chosenTarget=bestTarget;marking=(settings.mode=="join"?"Release to join":"Release to snap")+"\n"+bestSource.point.name+" → "+bestTarget.point.name;
                var ordered=new[]{members[bestSource.index]}.Concat(members.Where((m,i)=>i!=bestSource.index)).ToArray();
                pivotBefore=new RoomLayout{placements=ordered.Select(m=>before.placements.First(p=>p.target==m.target)).ToArray()};
                Layout=new RoomLayout{placements=ordered.Select(m=>new ObjectPlacement{target=m.target}).ToArray()};
                Request=new RoomSnapPlacement{members=ordered,point=bestSource.point.id,destination=new SnapDestination{target=bestTarget.data.id,revision=bestTarget.revision,point=bestTarget.point.id},mode=settings.mode,breakForce=settings.breakForce,breakTorque=settings.breakTorque};
            }
            Request.turn=bestTurn;Request.scale=scale;
            Request.Projection(pivotBefore,bestSource.point,bestTarget.data,bestTarget.point,projection);
            if(!projection.Project(pivotBefore,Layout,out _)){Request=null;return false;}
            return true;
        }
    }
}

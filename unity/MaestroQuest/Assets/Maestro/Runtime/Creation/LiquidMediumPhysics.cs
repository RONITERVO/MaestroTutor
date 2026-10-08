// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class LiquidPouring {
        sealed class MediumBody {
            internal string Id;internal RoomItem Item;internal RigidRoomItem Rigid;internal Rigidbody Body;
            internal readonly MediumDisplacement Displacement=new();internal uint Revision=uint.MaxValue;
            internal float Submerged,Volume;internal Vector3 Lift;internal bool Applied;internal string Reason="Physics has not sampled this object";
        }
        readonly RoomEnvironmentQueries mediumEnvironment=new();
        readonly LiquidMediumGeometry[] media=new LiquidMediumGeometry[RoomContainer.MaximumPerRoom];int mediaCount;
        readonly SortedDictionary<string,MediumBody> mediumBodies=new(StringComparer.Ordinal);
        internal void SynchronizeMedia(RoomDocument document){
            mediumBodies.Clear();
            foreach(var data in document.objects){var item=editor.Find(data.id);if(!item||data.IsBuiltIn)continue;var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();if(rigid&&body)mediumBodies[data.id]=new(){Id=data.id,Item=item,Rigid=rigid,Body=body};}
        }
        internal void CaptureMedia(bool requireReadyGeometry=true){
            mediaCount=0;var gravity=Physics.gravity;if(!float.IsFinite(gravity.sqrMagnitude)||gravity.sqrMagnitude<.01f)return;var up=-gravity.normalized;
            foreach(var v in vessels.Values){if(mediaCount==media.Length)break;if(!v.Item||!v.Item.isActiveAndEnabled||!v.Rigid||requireReadyGeometry&&!v.Rigid.GeometryReady||v.Live.amountMl<=0)continue;media[mediaCount++]=new(v.Id,v.Item,v.Live,up,v.Body);}
        }
        internal bool SampleMedium(Vector3 point,RoomItem participant,string ignore,out LiquidMediumGeometry chosen,out float depth,out bool ready){
            using var environment=mediumEnvironment.Begin(world);
            return SampleMedium(point,participant,ignore,in environment,out chosen,out depth,out ready);
        }
        bool SampleMedium(Vector3 point,RoomItem participant,string ignore,in RoomEnvironmentQueries.Batch environment,out LiquidMediumGeometry chosen,out float depth,out bool ready){
            chosen=default;depth=0;ready=false;bool found=false;
            for(int i=0;i<mediaCount;i++){var m=media[i];if(m.Id==ignore||!m.Sample(point,out var d)||found&&m.Volume>=chosen.Volume)continue;chosen=m;depth=d;found=true;}
            // Nested authored cavities select the smallest containing medium, then
            // stable ID order. Never add overlapping densities or skip an unavailable
            // inner medium to borrow readiness from another vessel.
            if(found)ready=chosen.Item.GetComponent<RigidRoomItem>().GeometryReady&&!blocked&&environment.CanSimulate(point,participant,chosen.Item)&&!editor.RuntimeGate.Held&&!editor.Ownership.Suspended&&!editor.WriteGate.Frozen;
            return found;
        }
        void FixedUpdate()=>TickMedium(Time.fixedDeltaTime);
        internal void TickMedium(float seconds){
            if(!editor||publishing||!float.IsFinite(seconds)||seconds<=0)return;
            foreach(var b in mediumBodies.Values){b.Applied=false;b.Lift=Vector3.zero;b.Submerged=0;b.Reason="Physics or object ownership is unavailable";}
            if(!world||!world.Running||blocked||editor.RuntimeGate.Held||editor.Ownership.Suspended||editor.WriteGate.Frozen)return;
            CaptureMedia(false);if(mediaCount==0)return;
            using var environment=mediumEnvironment.Begin(world);
            foreach(var b in mediumBodies.Values){
                if(!b.Item||!b.Rigid||!b.Rigid.TryReadMotion(out bool available,out _,out _)||!available)continue;
                if(b.Revision!=b.Rigid.MotionRevision){b.Displacement.Capture(b.Item);b.Revision=b.Rigid.MotionRevision;}
                var shape=b.Displacement;if(shape.Count==0){b.Reason=shape.Reason;continue;}
                float scale=Mathf.Abs(b.Item.transform.lossyScale.x),cell=shape.CellVolume*scale*scale*scale;b.Volume=cell*shape.Count;
                Vector3 buoyancy=Vector3.zero,centre=Vector3.zero,drag=Vector3.zero;float weight=0,angular=0;int wet=0;
                for(int i=0;i<shape.Count;i++){
                    var p=b.Item.transform.TransformPoint(shape.Points[i]);if(!SampleMedium(p,b.Item,b.Id,in environment,out var m,out _,out bool ready)||!ready)continue;
                    wet++;var fluid=RoomFluid.Effective(m.Contents.fluid);float displacedMass=cell*fluid.densityKgM3;buoyancy-=Physics.gravity*displacedMass;centre+=p*displacedMass;weight+=displacedMass;
                    var mediumBody=m.Body;var flow=mediumBody&&!mediumBody.isKinematic?mediumBody.GetPointVelocity(p):Vector3.zero;
                    drag+=(flow-b.Body.GetPointVelocity(p))*Mathf.Min(fluid.linearDrag,1/seconds)/shape.Count;angular+=fluid.angularDrag/shape.Count;
                }
                b.Submerged=(float)wet/shape.Count;b.Reason=wet==0?"No active medium at displacement samples":"";
                if(wet==0||weight<=0)continue;
                // Finite acceleration prevents tiny/light props from receiving an
                // explosive impulse. Drag cannot reverse velocity in a single tick.
                buoyancy=Vector3.ClampMagnitude(buoyancy,b.Body.mass*30);
                drag=Vector3.ClampMagnitude(drag,30);var angularAcceleration=-b.Body.angularVelocity*Mathf.Min(angular,1/seconds);
                b.Body.AddForceAtPosition(buoyancy,centre/weight,ForceMode.Force);
                b.Body.AddForce(drag,ForceMode.Acceleration);b.Body.AddTorque(angularAcceleration,ForceMode.Acceleration);
                b.Applied=true;b.Lift=buoyancy;
            }
        }
        internal JObject ObserveMedium(Vector3 authored,string target){
            if(!editor.Frame.Valid||!RoomViewpoint.ValidPosition(authored))return null;
            RoomItem participant=null;if(!string.IsNullOrEmpty(target)){participant=editor.Find(target);if(!participant)return null;}
            CaptureMedia(false);var point=editor.Frame.PointToWorld(authored);bool found=SampleMedium(point,participant,null,out var m,out float depth,out bool ready);var identity=editor.WorldIdentity;
            return new JObject{["worldId"]=identity.worldId,["regionId"]=identity.regionId,["found"]=found,["active"]=ready,["bodyId"]=found?m.Id:"",["liquid"]=found?m.Contents.liquid:"",["depthMetres"]=found?depth:0,["surface"]=WorkspaceViewpoint.Point(found?editor.Frame.PointToRoom(point+m.Up*depth):Vector3.zero),["densityKgM3"]=found?RoomFluid.Effective(m.Contents.fluid).densityKgM3:0,["reason"]=found&&!ready?"Medium or participant physics is unavailable":found?"":"No finite liquid contains this point"};
        }
        internal JObject ObserveMediumBody(string id){
            if(!mediumBodies.TryGetValue(id,out var b)||!b.Item)return null;
            bool active=b.Applied&&!blocked&&!editor.Ownership.Suspended&&!editor.WriteGate.Frozen&&world&&world.CanSimulate(b.Item.transform.position,b.Item)&&b.Rigid.TryReadMotion(out bool available,out _,out _)&&available;
            return new JObject{["applied"]=active,["submergedFraction"]=b.Submerged,["displacementM3"]=b.Volume,["buoyancyNewtons"]=WorkspaceViewpoint.Point(active?editor.Frame.DirectionToRoom(b.Lift):Vector3.zero),["reason"]=active?b.Reason:b.Applied?"Physics or ownership changed after the last sample":b.Reason};
        }
    }
}

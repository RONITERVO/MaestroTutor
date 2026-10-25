// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal enum RainExposure { Unknown, Covered, Open }
    // Geometry proof shared by collection, facts and presentation. No camera/frustum dependency.
    internal sealed class WeatherCover
    {
        internal const float Distance=100;
        const float ProbeRadius=.0005f;
        readonly RaycastHit[] hits=new RaycastHit[64];readonly Collider[] overlaps=new Collider[64];
        internal RainExposure Exposure(RoomEditor editor,Vector3 position,Vector3 velocity,RoomItem recipient=null,RoomEditor.MovementCandidates spatial=null){
            if(!ExposureReady(editor,position,velocity,recipient,out bool real))return RainExposure.Unknown;
            return Segment(editor,position,position-velocity.normalized*Distance,real,spatial);
        }
        // Only liquid execution calls this after its shared group has admitted
        // every upstream input through SpatialCollisionReady. Do not rediscover the world
        // per aperture sample outside that group's measured work reservation.
        internal RainExposure ExposureWithPreparedInputs(RoomEditor editor,Vector3 position,Vector3 velocity,RoomItem recipient){
            if(!ExposureReady(editor,position,velocity,recipient,out bool real))return RainExposure.Unknown;
            return GeometrySegment(editor,position,position-velocity.normalized*Distance,real);
        }
        static bool ExposureReady(RoomEditor editor,Vector3 position,Vector3 velocity,RoomItem recipient,out bool real){
            real=false;var world=editor?editor.PhysicsWorld:null;
            if(!world||!editor.isActiveAndEnabled||!editor.Frame.Valid||!RoomRecipe.Finite(position)||!RoomRecipe.Finite(velocity)||!float.IsFinite(velocity.sqrMagnitude)||velocity.sqrMagnitude<.001f)return false;
            real=world.IncludesRealRoom(recipient);return !real||world.SurfacesReady;
        }
        internal RainExposure Segment(RoomEditor editor,Vector3 from,Vector3 to,bool real,RoomEditor.MovementCandidates spatial=null){
            if(!editor||!editor.isActiveAndEnabled||!editor.Frame.Valid||!editor.PhysicsWorld||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!float.IsFinite((to-from).sqrMagnitude)||real&&!editor.PhysicsWorld.SurfacesReady)return RainExposure.Unknown;
            var geometry=GeometrySegment(editor,from,to,real);
            if(geometry!=RainExposure.Open)return geometry;
            // Bounds are conservative dependencies, never proof of a solid roof.
            // Capture once for a caller's synchronous batch; one-off facts use a
            // fresh capture and never activate or retain any native object.
            spatial??=editor.CaptureMovementCandidates(false,repeatedQueries:false);
            // Convert the segment before bounding it: enclosing a long tilted
            // world AABB again in room space can spuriously span distant roofs.
            var frame=editor.Frame;var bounds=new Bounds(frame.PointToRoom(from),Vector3.zero);
            bounds.Encapsulate(frame.PointToRoom(to));bounds.Expand(ProbeRadius*2/frame.MetresPerUnit);
            var targets=spatial.Targets(bounds,null);
            return editor.SpatialCollisionReady(targets,out _)==RoomSpatialReadiness.Ready?RainExposure.Open:RainExposure.Unknown;
        }
        RainExposure GeometrySegment(RoomEditor editor,Vector3 from,Vector3 to,bool real){
            int mask=(1<<RoomPhysicsLayers.Environment)|(1<<RoomPhysicsLayers.Item)|(real?1<<RoomPhysicsLayers.Scanned:0);
            int count=Physics.OverlapSphereNonAlloc(from,ProbeRadius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return RainExposure.Unknown;
            bool unknown=false;
            for(int i=0;i<count;i++){var contact=Contact(overlaps[i],editor,real);if(contact==RainExposure.Covered)return contact;unknown|=contact==RainExposure.Unknown;}
            var delta=to-from;float length=delta.magnitude;
            if(length>=.000001f){
                count=Physics.RaycastNonAlloc(from,delta/length,hits,length,mask,QueryTriggerInteraction.Ignore);
                if(count==hits.Length)return RainExposure.Unknown;
                for(int i=0;i<count;i++){var contact=Contact(hits[i].collider,editor,real);if(contact==RainExposure.Covered)return contact;unknown|=contact==RainExposure.Unknown;}
            }
            return unknown?RainExposure.Unknown:RainExposure.Open;
        }
        static RainExposure Contact(Collider collider,RoomEditor editor,bool real){
            if(!collider)return RainExposure.Open;
            if(collider.gameObject.layer==RoomPhysicsLayers.Scanned)return real?RainExposure.Covered:RainExposure.Open;
            var item=collider.GetComponentInParent<RoomItem>();
            if(!item||!item.transform.IsChildOf(editor.transform))return RainExposure.Open;
            return editor.AuthoredCollisionReady(item)&&item.Grab.colliders.Contains(collider)?RainExposure.Covered:RainExposure.Unknown;
        }
        internal static Vector3 Velocity(RoomEditor editor)=>editor.Frame.DirectionToWorld(editor.CurrentWeather.RainVelocity);
    }
}

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
        readonly RaycastHit[] hits=new RaycastHit[64];readonly Collider[] overlaps=new Collider[64];
        internal RainExposure Exposure(RoomEditor editor,Vector3 position,Vector3 velocity,RoomItem recipient=null){
            var world=editor?editor.PhysicsWorld:null;
            if(!world||!editor.Frame.Valid||!float.IsFinite(position.sqrMagnitude)||!float.IsFinite(velocity.sqrMagnitude)||velocity.sqrMagnitude<.001f)return RainExposure.Unknown;
            bool real=world.IncludesRealRoom(recipient);
            if(real&&!world.SurfacesReady)return RainExposure.Unknown;
            return Segment(editor,position,position-velocity.normalized*Distance,real);
        }
        internal RainExposure Segment(RoomEditor editor,Vector3 from,Vector3 to,bool real){
            int mask=(1<<RoomPhysicsLayers.Environment)|(1<<RoomPhysicsLayers.Item)|(real?1<<RoomPhysicsLayers.Scanned:0);
            int count=Physics.OverlapSphereNonAlloc(from,.0005f,overlaps,mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return RainExposure.Unknown;
            for(int i=0;i<count;i++)if(Relevant(overlaps[i],editor,real))return RainExposure.Covered;
            var delta=to-from;float length=delta.magnitude;if(length<.000001f)return RainExposure.Open;
            count=Physics.RaycastNonAlloc(from,delta/length,hits,length,mask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return RainExposure.Unknown;
            for(int i=0;i<count;i++)if(Relevant(hits[i].collider,editor,real))return RainExposure.Covered;
            return RainExposure.Open;
        }
        static bool Relevant(Collider collider,RoomEditor editor,bool real){
            if(!collider)return false;
            if(collider.gameObject.layer==RoomPhysicsLayers.Scanned)return real;
            var item=collider.GetComponentInParent<RoomItem>();
            return item&&item.transform.IsChildOf(editor.transform);
        }
        internal static Vector3 Velocity(RoomEditor editor)=>editor.Frame.DirectionToWorld(editor.CurrentWeather.RainVelocity);
    }
}

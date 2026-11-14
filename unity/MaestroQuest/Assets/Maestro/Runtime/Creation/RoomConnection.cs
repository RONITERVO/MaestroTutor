// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class ConnectionFrame
    {
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public ConnectionFrame Copy()=>new(){position=position,rotation=rotation};
        public bool Valid=>float.IsFinite(position.sqrMagnitude)&&position.sqrMagnitude<=100&&MotionFrame.ValidRotation(rotation);
    }
    [Serializable] public sealed class HingeLimits
    {
        public bool enabled;
        public float minimum=-90,maximum=90;
        public HingeLimits Copy()=>new(){enabled=enabled,minimum=minimum,maximum=maximum};
        public bool Valid=>float.IsFinite(minimum)&&float.IsFinite(maximum)&&minimum>=-170&&maximum<=170&&minimum<maximum;
    }
    [Serializable] public sealed class HingeDrive
    {
        public string mode="passive";
        public float target,spring=10,damper=1,speed=90,force=1;
        public HingeDrive Copy()=>new(){mode=mode,target=target,spring=spring,damper=damper,speed=speed,force=force};
        public bool Valid=>new[]{target,spring,damper,speed,force}.All(float.IsFinite)&&
            mode is "passive" or "spring" or "motor"&&target>=-170&&target<=170&&spring>=0&&spring<=100&&damper>=0&&damper<=20&&speed>=-360&&speed<=360&&force>=0&&force<=20;
    }
    [Serializable] public sealed class SliderSettings
    {
        // Distances and speeds use the connected object's local metres along its frame's +X.
        public float minimum,maximum=.1f,target,spring=250,damper=3,speed=.1f,force=20;
        public string mode="passive";
        public SliderSettings Copy()=>new(){minimum=minimum,maximum=maximum,target=target,spring=spring,damper=damper,speed=speed,force=force,mode=mode};
        public bool Valid=>new[]{minimum,maximum,target,spring,damper,speed,force}.All(float.IsFinite)&&
            minimum>=-1&&maximum<=1&&maximum-minimum>=.005f&&target>=-1&&target<=1&&spring>=0&&spring<=500&&damper>=0&&damper<=50&&speed>=-.5f&&speed<=.5f&&force>=0&&force<=100&&
            mode is "passive" or "spring" or "motor"&&(mode!="spring"||target>=minimum&&target<=maximum);
    }
    /// <summary>Reusable local settings contain no room identity, so construction recipes can bind fresh members.</summary>
    [Serializable] public class ConnectionSettings
    {
        public bool enabled=true;
        public string kind="hinge";
        // Zero explicitly means unbreakable. PhysX receives infinity only at admission.
        public float breakForce,breakTorque;
        public ConnectionFrame ownerFrame=new(),connectedFrame=new();
        public HingeLimits limits=new();
        public HingeDrive drive=new();
        public SliderSettings slide=new();
        public RoomConnection Bind(string connected)=>new(){connected=connected,enabled=enabled,kind=kind,breakForce=breakForce,breakTorque=breakTorque,ownerFrame=ownerFrame?.Copy(),connectedFrame=connectedFrame?.Copy(),limits=limits?.Copy(),drive=drive?.Copy(),slide=slide?.Copy()};
        public bool ValidateDefinition(out string error)
        {
            error="A connection needs valid local frames, ordered limits and bounded break limits and joint settings";
            if(kind is not ("hinge" or "fixed" or "slider")||!float.IsFinite(breakForce)||!float.IsFinite(breakTorque)||breakForce<0||breakForce>10000||breakTorque<0||breakTorque>10000)return false;
            if(ownerFrame?.Valid!=true||connectedFrame?.Valid!=true)return false;
            if(kind=="hinge"&&(limits?.Valid!=true||drive?.Valid!=true)||kind=="slider"&&slide?.Valid!=true)return false;
            if(kind=="hinge"&&limits.enabled&&drive.mode=="spring"&&(drive.target<limits.minimum||drive.target>limits.maximum))return false;
            error=null;return true;
        }
    }
    /// <summary>Two local frames retain the hinge's zero reference through reloads. +X is its axis, +Y its zero direction.</summary>
    [Serializable] public sealed class RoomConnection:ConnectionSettings
    {
        public const int MaximumRoomConnections=16;
        public int version=1;
        public string connected;
        public RoomConnection Copy(){var copy=Bind(connected);copy.version=version;return copy;}
        internal static bool Id(string id)=>Guid.TryParseExact(id,"N",out var parsed)&&id==parsed.ToString("N");
        public bool Validate(string owner,out string error)
        {
            error="A connection needs version 1 and different created-object IDs";
            return version==1&&Id(owner)&&Id(connected)&&owner!=connected&&ValidateDefinition(out error);
        }
        internal static bool Acyclic(IReadOnlyDictionary<string,string> links)
        {
            foreach(var start in links.Keys){var seen=new HashSet<string>();var id=start;while(links.TryGetValue(id,out var next)){if(!seen.Add(id))return false;id=next;}}
            return true;
        }
        public static bool ValidateCollection(RoomObjectData[] objects,out string error)
        {
            error="Keep at most one connection per created object and sixteen per room, without connection cycles";
            var links=new Dictionary<string,string>();
            foreach(var item in objects){error="Keep at most one connection per created object";var connections=item.connections??Array.Empty<RoomConnection>();if(connections.Length>1||item.IsBuiltIn&&connections.Length>0||connections.Any(h=>h==null))return false;
                if(connections.Length==0)continue;if(!connections[0].Validate(item.id,out error))return false;links.Add(item.id,connections[0].connected);}
            error="Keep at most sixteen connections per room without connection cycles";if(links.Count>MaximumRoomConnections)return false;
            if(!Acyclic(links))return false;
            // Missing referenced objects stay explicit; runtime freezes the affected member. Undo can restore the exact identity.
            error=null;return true;
        }
        static float FrameAngle(Quaternion a,Quaternion b)=>Vector3.SignedAngle(b*Vector3.up,a*Vector3.up,b*Vector3.right);
        public float Angle(Transform owner,Transform other)=>FrameAngle(owner.rotation*ownerFrame.rotation,other.rotation*connectedFrame.rotation);
        public float Travel(Transform owner,Transform other)=>Vector3.Dot(owner.TransformPoint(ownerFrame.position)-other.TransformPoint(connectedFrame.position),other.rotation*connectedFrame.rotation*Vector3.right)/other.TransformVector(connectedFrame.rotation*Vector3.right).magnitude;
        bool AlignedFrames(Vector3 aPosition,Quaternion aRotation,Vector3 bPosition,Quaternion bRotation,float connectedScale,out string error)
        {
            error="Align the connection anchors before starting its physics";
            if(kind=="slider"){
                error="Align the sliding frames and place the owner inside its saved travel limits";
                var axis=bRotation*Vector3.right;var delta=aPosition-bPosition;var along=Vector3.Dot(delta,axis);var distance=along/connectedScale;
                if(Quaternion.Angle(aRotation,bRotation)>5||(delta-axis*along).magnitude>.03f||distance<slide.minimum-.003f||distance>slide.maximum+.003f)return false;
                error=null;return true;
            }
            if(Vector3.Distance(aPosition,bPosition)>.03f||Vector3.Angle(aRotation*Vector3.right,bRotation*Vector3.right)>5)return false;
            if(kind=="fixed"){error="Align both complete connection frames before starting physics";if(Quaternion.Angle(aRotation,bRotation)>5)return false;error=null;return true;}
            var angle=FrameAngle(aRotation,bRotation);if(limits.enabled&&(angle<limits.minimum-3||angle>limits.maximum+3))return false;
            error=null;return true;
        }
        public bool Aligned(Transform owner,Transform other,out string error)=>AlignedFrames(owner.TransformPoint(ownerFrame.position),owner.rotation*ownerFrame.rotation,other.TransformPoint(connectedFrame.position),other.rotation*connectedFrame.rotation,other.TransformVector(connectedFrame.rotation*Vector3.right).magnitude,out error);
        internal bool Aligned(RoomObjectData owner,RoomObjectData other,out string error)=>AlignedFrames(owner.position+owner.rotation*(ownerFrame.position*owner.scale),owner.rotation*ownerFrame.rotation,other.position+other.rotation*(connectedFrame.position*other.scale),other.rotation*connectedFrame.rotation,other.scale,out error);
        internal void Slide(RoomObjectData owner,RoomObjectData other,float distance)
        {
            Align(owner,other,0);
            owner.position+=other.rotation*connectedFrame.rotation*Vector3.right*(distance*other.scale);
        }
        internal void Align(RoomObjectData owner,RoomObjectData other,float angle)
        {
            owner.rotation=other.rotation*connectedFrame.rotation*Quaternion.AngleAxis(angle,Vector3.right)*Quaternion.Inverse(ownerFrame.rotation);
            owner.position=other.position+other.rotation*(connectedFrame.position*other.scale)-owner.rotation*(ownerFrame.position*owner.scale);
        }
    }
}

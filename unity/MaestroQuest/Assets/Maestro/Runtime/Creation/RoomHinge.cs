// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class HingeFrame
    {
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public HingeFrame Copy()=>new(){position=position,rotation=rotation};
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
    /// <summary>Two local frames retain the hinge's zero reference through reloads. +X is its axis, +Y its zero direction.</summary>
    [Serializable] public sealed class RoomHinge
    {
        public const int MaximumRoomHinges=16;
        public int version=1;
        public string connected;
        public bool enabled=true;
        public HingeFrame ownerFrame=new(),connectedFrame=new();
        public HingeLimits limits=new();
        public HingeDrive drive=new();
        public RoomHinge Copy()=>new(){version=version,connected=connected,enabled=enabled,ownerFrame=ownerFrame?.Copy(),connectedFrame=connectedFrame?.Copy(),limits=limits?.Copy(),drive=drive?.Copy()};
        internal static bool Id(string id)=>Guid.TryParseExact(id,"N",out var parsed)&&id==parsed.ToString("N");
        public bool Validate(string owner,out string error)
        {
            error="A hinge needs different created-object IDs, valid local frames, limits and one bounded drive";
            if(version!=1||!Id(owner)||!Id(connected)||owner==connected||ownerFrame?.Valid!=true||connectedFrame?.Valid!=true||limits?.Valid!=true||drive?.Valid!=true)return false;
            if(limits.enabled&&drive.mode=="spring"&&(drive.target<limits.minimum||drive.target>limits.maximum))return false;
            error=null;return true;
        }
        public static bool ValidateCollection(RoomObjectData[] objects,out string error)
        {
            error="Keep at most one hinge per created object and sixteen per room, without connection cycles";
            var links=new Dictionary<string,string>();
            foreach(var item in objects){var hinges=item.hinges??Array.Empty<RoomHinge>();if(hinges.Length>1||item.IsBuiltIn&&hinges.Length>0||hinges.Any(h=>h==null))return false;
                if(hinges.Length==0)continue;if(!hinges[0].Validate(item.id,out error))return false;links.Add(item.id,hinges[0].connected);}
            error="Keep at most sixteen hinges per room without connection cycles";if(links.Count>MaximumRoomHinges)return false;
            foreach(var start in links.Keys){var seen=new HashSet<string>();var id=start;while(links.TryGetValue(id,out var next)){if(!seen.Add(id))return false;id=next;}}
            // Missing referenced objects stay explicit; runtime freezes the affected member. Undo can restore the exact identity.
            error=null;return true;
        }
        public float Angle(Transform owner,Transform other)
        {
            var a=owner.rotation*ownerFrame.rotation;var b=other.rotation*connectedFrame.rotation;
            return Vector3.SignedAngle(b*Vector3.up,a*Vector3.up,b*Vector3.right);
        }
        public bool Aligned(Transform owner,Transform other,out string error)
        {
            error="Align the hinge anchors before starting its physics";
            if(Vector3.Distance(owner.TransformPoint(ownerFrame.position),other.TransformPoint(connectedFrame.position))>.03f||
               Vector3.Angle(owner.rotation*ownerFrame.rotation*Vector3.right,other.rotation*connectedFrame.rotation*Vector3.right)>5)return false;
            var angle=Angle(owner,other);if(limits.enabled&&(angle<limits.minimum-3||angle>limits.maximum+3))return false;
            error=null;return true;
        }
        public void Align(Transform owner,Transform other,float angle)
        {
            owner.rotation=other.rotation*connectedFrame.rotation*Quaternion.AngleAxis(angle,Vector3.right)*Quaternion.Inverse(ownerFrame.rotation);
            owner.position+=other.TransformPoint(connectedFrame.position)-owner.TransformPoint(ownerFrame.position);
        }
    }
}

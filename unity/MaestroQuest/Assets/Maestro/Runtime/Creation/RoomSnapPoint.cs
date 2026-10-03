// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Editable local alignment frames. They do not reserve space or own physics.</summary>
    [Serializable] public sealed class RoomSnapPoint {
        public const int MaximumPerObject=64,MaximumPerRoom=256;
        public int version=1;
        public string id,name,family;
        public ConnectionFrame frame=new();
        public RoomSnapPoint Copy()=>new(){version=version,id=id,name=name,family=family,frame=frame?.Copy()};
        internal static bool Identifier(string value)=>value!=null&&Regex.IsMatch(value,"^[a-zA-Z][a-zA-Z0-9_]{0,31}$");
        public bool Validate(out string error){
            error="Use a named version-1 snap point, a matching family and a normalized local frame within ten metres";
            if(version!=1||!Identifier(id)||string.IsNullOrWhiteSpace(name)||name.Length>64||name.Any(char.IsControl)||!Identifier(family)||frame?.Valid!=true)return false;
            error=null;return true;
        }
        public static bool ValidateCollection(RoomObjectData owner,out string error){
            var points=owner.snapPoints??Array.Empty<RoomSnapPoint>();error="Use at most 64 distinct snap points on a created object";
            if(points.Length>MaximumPerObject||owner.IsBuiltIn&&points.Length>0||points.Any(p=>p==null)||points.Select(p=>p.id).Distinct(StringComparer.Ordinal).Count()!=points.Length)return false;
            foreach(var point in points)if(!point.Validate(out error))return false;error=null;return true;
        }
    }
    [Serializable] public sealed class SnapDestination {public string target,point;public int revision;}
    [Serializable] public sealed class RoomSnapPlacement {
        public TransformMember[] members;
        public string point,mode="place";
        public SnapDestination destination;
        public float turn,breakForce,breakTorque;
        public bool Validate(out string error){
            error="Choose 1–15 distinct moving creations, a source point on the first, and a different destination with current revisions";
            if(members==null||members.Length>15||!new RoomGroupTransform{members=members}.Validate(out _)||!RoomSnapPoint.Identifier(point)||destination==null||!RoomSnapPoint.Identifier(destination.point)||!new RoomGroupTransform{members=new[]{new TransformMember{target=destination.target,revision=destination.revision}}}.Validate(out _)||members.Any(m=>m.target==destination.target)||mode is not ("place" or "join")||!float.IsFinite(turn)||turn<-180||turn>180||!float.IsFinite(breakForce)||breakForce<0||breakForce>10000||!float.IsFinite(breakTorque)||breakTorque<0||breakTorque>10000)return false;
            error=null;return true;
        }
        // Room-local inputs and results; no Transform mutation or coordinate-space guessing.
        internal RoomGroupTransform Projection(RoomLayout source,RoomSnapPoint from,RoomObjectData other,RoomSnapPoint to){
            var pivot=source.placements[0];
            var orientation=(other.rotation*to.frame.rotation*Quaternion.AngleAxis(turn,Vector3.up)*Quaternion.Inverse(from.frame.rotation)).normalized;
            var position=other.position+other.rotation*(to.frame.position*other.scale)-orientation*(from.frame.position*pivot.scale);
            return new RoomGroupTransform{members=members,position=position,rotation=orientation,scale=1};
        }
    }
}

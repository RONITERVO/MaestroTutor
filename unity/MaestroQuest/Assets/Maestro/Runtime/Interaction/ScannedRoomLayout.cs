// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Meta.XR.MRUtilityKit;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    internal sealed class ScannedSurface
    {
        public string Id,Label;
        public Vector3 Position;
        public Quaternion Rotation=Quaternion.identity;
        public Rect? Plane;
        public Vector2[] Boundary=Array.Empty<Vector2>();
        public Bounds? Volume;
        public bool Valid=>Guid.TryParseExact(Id,"N",out var id)&&id!=Guid.Empty&&Label!=null&&Label.Length<=96&&!Label.Any(char.IsControl)&&
            Coordinate(Position)&&float.IsFinite(Rotation.x)&&float.IsFinite(Rotation.y)&&float.IsFinite(Rotation.z)&&float.IsFinite(Rotation.w)&&
            Mathf.Abs(Quaternion.Dot(Rotation,Rotation)-1)<.001f&&(Plane.HasValue||Volume.HasValue)&&
            (!Plane.HasValue||Coordinate(new Vector3(Plane.Value.center.x,Plane.Value.center.y,0))&&Size(new Vector3(Plane.Value.width,Plane.Value.height,1)))&&
            (!Volume.HasValue||Coordinate(Volume.Value.center)&&Size(Volume.Value.size))&&Boundary!=null&&Boundary.Length<=256&&(Boundary.Length==0||Boundary.Length>=3)&&Boundary.All(p=>Coordinate(new Vector3(p.x,p.y,0)));
        static bool Coordinate(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&Mathf.Abs(v.x)<=1000&&Mathf.Abs(v.y)<=1000&&Mathf.Abs(v.z)<=1000;
        static bool Size(Vector3 v)=>Coordinate(v)&&v.x>0&&v.y>0&&v.z>0;
        internal static ScannedSurface FromFrame(string id,string label,Transform anchor,Transform frame,Rect? plane,Bounds? volume) {
            var coordinates=new Creation.RoomFrame(frame);
            return new(){Id=id,Label=label,Position=coordinates.PointToRoom(anchor.position),Rotation=coordinates.RotationToRoom(anchor.rotation),Plane=plane,Volume=volume};
        }
        public ScannedSurface Copy()=>new(){Id=Id,Label=Label,Position=Position,Rotation=Rotation,Plane=Plane,Volume=Volume,Boundary=Boundary?.ToArray()};
        public bool Same(ScannedSurface other)=>other!=null&&Id==other.Id&&Label==other.Label&&Position.Equals(other.Position)&&Rotation.Equals(other.Rotation)&&Nullable.Equals(Plane,other.Plane)&&Nullable.Equals(Volume,other.Volume)&&Boundary.SequenceEqual(other.Boundary);
        public JObject Summary()=>new(){["id"]=Id,["label"]=Label,["plane"]=Plane.HasValue,["volume"]=Volume.HasValue};
        public JObject Detail()=>new(){["id"]=Id,["label"]=Label,["pose"]=new JObject{["position"]=ScannedRoom.Triple(Position),["rotation"]=new JObject{["x"]=Rotation.x,["y"]=Rotation.y,["z"]=Rotation.z,["w"]=Rotation.w}},
            ["plane"]=new JObject{["present"]=Plane.HasValue,["center"]=ScannedRoom.Triple(Plane.HasValue?new Vector3(Plane.Value.center.x,Plane.Value.center.y,0):Vector3.zero),["size"]=ScannedRoom.Triple(Plane.HasValue?new Vector3(Plane.Value.width,Plane.Value.height,0):Vector3.zero)},
            ["volume"]=new JObject{["present"]=Volume.HasValue,["center"]=ScannedRoom.Triple(Volume?.center??Vector3.zero),["size"]=ScannedRoom.Triple(Volume?.size??Vector3.zero)}};
    }
    internal interface IRoomLayoutSource
    {
        bool TryRead(Transform frame,out string roomId,out ScannedSurface[] entries,out int omitted,out string reason);
    }
    public sealed partial class ScannedRoom
    {
        internal const int MaximumLayoutSurfaces=128,LayoutPageSize=4;
        IRoomLayoutSource layoutSource;
        ScannedSurface[] layout;
        string layoutId="",layoutRoom="",layoutSetup="",layoutReason="Load a tracked room first";
        int layoutFrame=-1,layoutFrameId,layoutOmitted;
        internal void SetLayoutSourceForTests(IRoomLayoutSource value){layoutSource=value;InvalidateLayout();}
        void InvalidateLayout(){layoutFrame=-1;layout=null;layoutId="";layoutRoom="";layoutOmitted=0;}
        internal static bool RigidFrame(Transform value) {var frame=new Creation.RoomFrame(value);return frame.Valid&&Mathf.Abs(frame.MetresPerUnit-1)<.00005f;}
        bool ReadLayout(Transform frame)
        {
            string reason=null;
            if(!SetupActive||!world||!world.isActiveAndEnabled||world.RuntimeHeld||virtualView)reason="Return to an active mixed-reality room";
            else if(Busy||!geometryAccepted||!world.SurfacesReady||tracked!=null&&!tracked.IsPressed())reason="Load a tracked room with floor and wall colliders first";
            else if(!RigidFrame(frame))reason="Room coordinates are unavailable or scaled";
            if(reason!=null){InvalidateLayout();layoutReason=reason;return false;}
            if(layoutFrame==Time.frameCount&&layoutFrameId==frame.GetInstanceID()&&layoutSetup==setupId)return layout!=null;
            bool setupChanged=layoutSetup!=setupId||layoutFrameId!=frame.GetInstanceID();layoutSetup=setupId;layoutFrameId=frame.GetInstanceID();layoutFrame=Time.frameCount;
            try {
                layoutSource??=new DeviceLayoutSource(this);
                if(!layoutSource.TryRead(frame,out var roomId,out var entries,out var omitted,out reason))return RejectLayout(reason??"Room layout is unavailable");
                if(!Guid.TryParseExact(roomId,"N",out var id)||id==Guid.Empty||entries==null||entries.Length>MaximumLayoutSurfaces||omitted<0||omitted>256||entries.Any(x=>x==null||!x.Valid)||entries.Sum(x=>x.Boundary.Length)>4096||entries.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=entries.Length)
                    return RejectLayout("Room layout exceeds limits or contains invalid surfaces");
                var ordered=entries.OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
                if(setupChanged||layout==null||layoutRoom!=roomId||layoutOmitted!=omitted||layout.Length!=ordered.Length||!layout.Zip(ordered,(a,b)=>a.Same(b)).All(same=>same)){
                    layout=ordered.Select(x=>x.Copy()).ToArray();layoutId=Guid.NewGuid().ToString("N");layoutRoom=roomId;layoutOmitted=omitted;
                }
                layoutReason="Loaded scan bounds; physical alignment is not verified";return true;
            } catch(Exception){return RejectLayout("Room layout could not be read");}
        }
        bool RejectLayout(string reason){layout=null;layoutId="";layoutRoom="";layoutOmitted=0;layoutReason=reason;return false;}
        internal JObject ObserveLayout(Transform frame)
        {
            bool available=ReadLayout(frame);return new JObject{["available"]=available,["stateId"]=layoutId,["roomId"]=layoutRoom,["count"]=layout?.Length??0,["omitted"]=layoutOmitted,["reason"]=layoutReason};
        }
        internal JArray LayoutPage(Transform frame,string stateId,int offset)
        {
            if(!ReadLayout(frame)||layoutId!=stateId||offset<0||offset>layout.Length)return null;
            return new JArray(layout.Skip(offset).Take(LayoutPageSize).Select(x=>x.Summary()));
        }
        internal JObject LayoutSurface(Transform frame,string stateId,string id)=>ReadLayout(frame)&&layoutId==stateId?layout.FirstOrDefault(x=>x.Id==id)?.Detail():null;
        sealed class DeviceLayoutSource:IRoomLayoutSource
        {
            readonly ScannedRoom owner;
            internal DeviceLayoutSource(ScannedRoom owner){this.owner=owner;}
            public bool TryRead(Transform frame,out string roomId,out ScannedSurface[] entries,out int omitted,out string reason)
            {
                roomId=null;entries=null;omitted=0;reason="No current Meta room is loaded";
                var room=owner.current;if(!room||!owner.mruk||!owner.mruk.IsWorldLockActive)return false;
                var anchors=room.Anchors;if(anchors==null||anchors.Count>256){reason="Room layout exceeds its anchor limit";return false;}
                var result=new List<ScannedSurface>();int boundaryPoints=0;
                foreach(var anchor in anchors){
                    if(!anchor){reason="Room anchors changed while reading";return false;}
                    if(!anchor.PlaneRect.HasValue&&!anchor.VolumeBounds.HasValue){omitted++;continue;}
                    if(!RigidFrame(anchor.transform)){reason="A scanned surface has unsupported scale";return false;}
                    int pointCount=anchor.PlaneBoundary2D?.Count??0;boundaryPoints+=pointCount;if(pointCount>256||boundaryPoints>4096){reason="Room plane boundaries exceed their point limit";return false;}
                    var entry=ScannedSurface.FromFrame(anchor.Anchor.Uuid.ToString("N"),anchor.Label.ToString(),anchor.transform,frame,anchor.PlaneRect,anchor.VolumeBounds);
                    entry.Boundary=anchor.PlaneBoundary2D?.ToArray()??Array.Empty<Vector2>();result.Add(entry);
                    if(result.Count>MaximumLayoutSurfaces){reason="Room layout exceeds its surface limit";return false;}
                }
                roomId=room.Anchor.Uuid.ToString("N");entries=result.ToArray();reason=null;return true;
            }
        }
    }
}

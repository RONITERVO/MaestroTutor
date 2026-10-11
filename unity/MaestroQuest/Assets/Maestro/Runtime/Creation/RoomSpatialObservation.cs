// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal static string SpatialSource(RoomObjectData source)
        {
            // Read() gives a detached value. Exclude only identity/display/placement;
            // future authored geometry fields automatically participate in the key.
            source.id="";source.name="";source.position=Vector3.zero;source.rotation=Quaternion.identity;source.scale=1;
            using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(source))));
        }
        RoomSpatialBounds ReadDormantSpatial(string target,DormantEntity entry,out bool authored)=>ReadDormantSpatial(target,entry,out authored,out _);
        RoomSpatialBounds ReadDormantSpatial(string target,DormantEntity entry,out bool authored,out bool prepared)
        {
            authored=prepared=false;int revision=ObjectRevision(target);
            if(entry.AuthoredSpatialSource==null||entry.SpatialRevision!=revision){
                var data=Read(target);if(data==null)return RoomSpatialBounds.Unknown;
                var pose=NativeActivationPose(data);
                entry.SpatialPlacement=Matrix4x4.TRS(pose.position,pose.rotation.normalized,Vector3.one*pose.scale);
                // Cache only the detached source key, never another authored object.
                // Repeated observation does not copy recipes or hash geometry.
                entry.AuthoredSpatialSource=SpatialSource(data);
                entry.SpatialCurrent=entry.SpatialSource==entry.AuthoredSpatialSource;
                entry.SpatialRevision=revision;
            }
            if(entry.SpatialCurrent){prepared=true;return entry.Spatial.Transform(entry.SpatialPlacement);}
            if(TryAuthoredSpatial(target,entry.AuthoredSpatialSource,out var canonical)){authored=prepared=true;return canonical.Transform(entry.SpatialPlacement);}
            // Pending preparation must remain conservative for retention, but it
            // is not yet evidence to queue every unknown object for display.
            return RoomSpatialBounds.Unknown;
        }
        internal RoomSpatialBounds ReadSpatialBounds(string target,out string source)
        {
            source="missing";if(!HasSavedObject(target))return RoomSpatialBounds.Unknown;
            if(dormantNative.TryGetValue(target,out var entry)){
                var value=ReadDormantSpatial(target,entry,out var authored);source=entry.SpatialCurrent?"retained":authored?"authored":"unknown";return value;
            }
            source="unknown";var item=Find(target);
            if(!item){var data=Read(target);if(TryAuthoredSpatial(data,out var canonical)){source="authored";return canonical.Transform(Matrix4x4.TRS(data.position,data.rotation.normalized,Vector3.one*data.scale));}return RoomSpatialBounds.Unknown;}
            if(!Frame.Read(item.transform,out var position,out var rotation,out var scale))return RoomSpatialBounds.Unknown;
            source="native";return RoomSpatialBounds.Capture(item,Read(target)).Transform(Matrix4x4.TRS(position,rotation,Vector3.one*scale));
        }
        internal JObject ObserveSpatialBounds(string target)
        {
            if(journal==null)return null;
            var value=ReadSpatialBounds(target,out var source);
            return new JObject{["target"]=target,["revision"]=HasSavedObject(target)?ObjectRevision(target):0,["source"]=source,["coordinates"]="room",["visual"]=SpatialRecord(value.Visual),["collision"]=SpatialRecord(value.Collision)};
        }
        static JObject SpatialRecord(RoomSpatialExtent value)
        {
            bool present=value.Known&&value.HasBounds;
            return new JObject{["known"]=value.Known,["hasBounds"]=present,["min"]=Interaction.ScannedRoom.Triple(present?value.Bounds.min:Vector3.zero),["max"]=Interaction.ScannedRoom.Triple(present?value.Bounds.max:Vector3.zero)};
        }
    }
}

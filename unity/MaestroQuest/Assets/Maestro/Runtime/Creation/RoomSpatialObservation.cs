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
        static string SpatialSource(RoomObjectData source)
        {
            // Read() gives a detached value. Exclude only identity/display/placement;
            // future authored geometry fields automatically participate in the key.
            source.id="";source.name="";source.position=Vector3.zero;source.rotation=Quaternion.identity;source.scale=1;
            using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(source))));
        }
        RoomSpatialBounds ReadDormantSpatial(string target,DormantEntity entry)
        {
            int revision=ObjectRevision(target);
            if(entry.SpatialRevision!=revision){
                var data=Read(target);if(data==null)return RoomSpatialBounds.Unknown;
                var pose=NativeActivationPose(data);
                entry.SpatialPlacement=Matrix4x4.TRS(pose.position,pose.rotation,Vector3.one*pose.scale);
                entry.SpatialCurrent=entry.SpatialSource==SpatialSource(data);
                entry.SpatialRevision=revision;
            }
            return entry.SpatialCurrent?entry.Spatial.Transform(entry.SpatialPlacement):RoomSpatialBounds.Unknown;
        }
        internal RoomSpatialBounds ReadSpatialBounds(string target,out string source)
        {
            source="missing";if(!HasSavedObject(target))return RoomSpatialBounds.Unknown;
            if(dormantNative.TryGetValue(target,out var entry)){
                var value=ReadDormantSpatial(target,entry);source=entry.SpatialCurrent?"retained":"unknown";return value;
            }
            source="unknown";var item=Find(target);
            if(!item||!Frame.Read(item.transform,out var position,out var rotation,out var scale))return RoomSpatialBounds.Unknown;
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

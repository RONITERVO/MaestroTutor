// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Unity's field codec can move doubles by one ULP. Measured liquid and
    // carried-material quantities cross as exact JSON numbers, never by clamping
    // against capacity. Geometry and other fields keep the existing codec.
    internal static class RoomLiquidCodec {
        internal static void Write(RoomContainer source,JObject wire){
            // JObject replacement can consider nearby floating values equal. Remove
            // the old tokens first so even a one-ULP difference survives.
            wire.Remove("capacityMl");wire.Add("capacityMl",source.capacityMl);
            wire.Remove("amountMl");wire.Add("amountMl",source.amountMl);
        }
        internal static void Read(RoomContainer target,JObject wire){
            double Number(string key){
                if(wire?[key]?.Type is not (JTokenType.Float or JTokenType.Integer))throw new ArgumentException("Liquid quantities must be JSON numbers");
                double value=(double)wire[key];if(!double.IsFinite(value))throw new ArgumentException("Liquid quantities must be finite");
                return value;
            }
            target.capacityMl=Number("capacityMl");target.amountMl=Number("amountMl");
        }
        internal static void Containers(RoomContainer[] values,JToken wire,bool writing){
            if(values==null||values.Length==0)return;
            if(wire is not JArray array||array.Count!=values.Length)throw new ArgumentException("Liquid collection differs from its wire");
            for(int i=0;i<values.Length;i++){
                if(values[i]==null||array[i] is not JObject item)throw new ArgumentException("Invalid liquid wire");
                if(writing)Write(values[i],item);else Read(values[i],item);
            }
        }
        internal static void Write(RoomMaterialStore source,JObject wire){
            // Preserve adjacent quantities even when JObject considers them equal.
            wire.Remove("capacityLitres");wire.Add("capacityLitres",source.capacityLitres);
            wire.Remove("amountLitres");wire.Add("amountLitres",source.amountLitres);
        }
        internal static void Read(RoomMaterialStore target,JObject wire){
            double Number(string key){
                if(wire?[key]?.Type is not (JTokenType.Float or JTokenType.Integer))throw new ArgumentException("Material quantities must be JSON numbers");
                double value=(double)wire[key];if(!double.IsFinite(value))throw new ArgumentException("Material quantities must be finite");
                return value;
            }
            target.capacityLitres=Number("capacityLitres");target.amountLitres=Number("amountLitres");
        }
        internal static void MaterialStores(RoomMaterialStore[] values,JToken wire,bool writing){
            if(values==null||values.Length==0)return;
            if(wire is not JArray array||array.Count!=values.Length)throw new ArgumentException("Material collection differs from its wire");
            for(int i=0;i<values.Length;i++){
                if(values[i]==null||array[i] is not JObject item)throw new ArgumentException("Invalid material wire");
                if(writing)Write(values[i],item);else Read(values[i],item);
            }
        }
        static void Room(RoomDocument value,JObject wire,bool writing){
            if(value.objects==null||wire["objects"] is not JArray objects||objects.Count!=value.objects.Length)throw new ArgumentException("Room objects differ from their wire");
            for(int i=0;i<value.objects.Length;i++){
                if(value.objects[i]==null||objects[i] is not JObject item)throw new ArgumentException("Invalid room object wire");
                Containers(value.objects[i].containers,item["containers"],writing);
                MaterialStores(value.objects[i].materialStores,item["materialStores"],writing);
            }
        }
        internal static string Encode(RoomDocument value){
            var wire=JObject.Parse(JsonUtility.ToJson(value));Room(value,wire,true);return wire.ToString(Formatting.None);
        }
        internal static void Decode(RoomDocument value,JObject wire){
            // Retained old files keep their original read rules. New writes always
            // use format 44+, including paired snapshots and prepared checkpoints.
            if(value.version>=44)Room(value,wire,false);
        }
    }
}

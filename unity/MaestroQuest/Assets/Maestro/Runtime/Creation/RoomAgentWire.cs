// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public static class RoomAgentWire
    {
        // Unity expands null serializable classes into empty default objects. Keep
        // Unity's vector/colour layout, but preserve the contract's actual absences.
        public static string Serialize(RoomAgentState state)
        {
            var json=JObject.Parse(JsonUtility.ToJson(state));
            if(state.inspection==null)json["inspection"]=JValue.CreateNull();
            else if(state.inspection.recipe==null)json["inspection"]["recipe"]=JValue.CreateNull();
            if(state.rules==null)json["rules"]=JValue.CreateNull();
            else if(state.rules.selected==null)json["rules"]["selected"]=JValue.CreateNull();
            if(state.physics==null)json["physics"]=JValue.CreateNull();
            if(state.avatar==null)json["avatar"]=JValue.CreateNull();
            else Movement(json["avatar"],state.avatar.distance,state.avatar.speed);
            for(int i=0;i<state.objects.Length;i++) {
                var item=state.objects[i];var wire=json["objects"][i];
                if(item.movement==null)wire["movement"]=JValue.CreateNull();
                else Movement(wire["movement"],item.movement.distance,item.movement.speed);
            }
            return json.ToString(Formatting.None);
        }
        static void Movement(JToken value,float distance,float speed)
        {
            // Avoid leaking float32 expansion beyond the decimal contract endpoints.
            value["distance"]=Math.Round(distance,6);value["speed"]=Math.Round(speed,6);
        }
    }
}

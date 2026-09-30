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
            json["catalog"]=state.catalog?.DeepClone()??JValue.CreateNull();
            json["execution"]=state.execution?.DeepClone()??JValue.CreateNull();
            if(state.motions==null)json["motions"]=JValue.CreateNull();
            if(state.rules==null)json["rules"]=JValue.CreateNull();
            else if(state.rules.selected==null)json["rules"]["selected"]=JValue.CreateNull();
            if(state.ownership==null)json["ownership"]=JValue.CreateNull();
            if(state.physics==null)json["physics"]=JValue.CreateNull();
            if(state.activityProfile==null)json["activityProfile"]=JValue.CreateNull();
            if(state.walk==null)json["walk"]=JValue.CreateNull();
            if(state.avatar==null)json["avatar"]=JValue.CreateNull();
            else Movement(json["avatar"],state.avatar.distance,state.avatar.speed);
            for(int i=0;i<state.objects.Length;i++) {
                var item=state.objects[i];var wire=json["objects"][i];
                if(item.movement==null)wire["movement"]=JValue.CreateNull();
                else Movement(wire["movement"],item.movement.distance,item.movement.speed);
            }
            return json.ToString(Formatting.None);
        }
        // JsonUtility cannot hydrate arbitrary typed JSON arguments. Only the validated
        // structured catalog payload is copied from the original raw request.
        public static bool PopulateStructured(RoomAgentRequest request,JObject raw)
        {
            if(raw?["commands"] is not JArray commands || request?.commands==null || commands.Count!=request.commands.Length)return false;
            for(int i=0;i<commands.Count;i++) {
                if(request.commands[i]?.action=="rules"&&request.commands[i].rule?.action=="signal") {
                    if(commands[i] is not JObject signal || !Maestro.Quest.Rules.RuleScheduler.ValidSignalWire(signal))return false;
                    request.commands[i].rule.value=signal["rule"]["value"].DeepClone();continue;
                }
                if(request.commands[i]?.action=="execution") {
                    if(commands[i] is not JObject execution || !RoomExecutions.ValidWire(execution))return false;
                    request.commands[i].execution=(JObject)execution["execution"].DeepClone();continue;
                }
                if(request.commands[i]?.action!="catalog")continue;
                if(commands[i] is not JObject command || !RoomCapabilityCatalog.ValidWire(command))return false;
                request.commands[i].catalog=(JObject)command["catalog"].DeepClone();
            }
            return true;
        }
        static void Movement(JToken value,float distance,float speed)
        {
            // Avoid leaking float32 expansion beyond the decimal contract endpoints.
            value["distance"]=Math.Round(distance,6);value["speed"]=Math.Round(speed,6);
        }
    }
}

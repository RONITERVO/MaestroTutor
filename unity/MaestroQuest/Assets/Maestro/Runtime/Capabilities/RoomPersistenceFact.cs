// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class RoomPersistenceFact
    {
        static JObject Store()=>Object(new JObject{
            ["phase"]=Choice("idle","unsaved","saving","unconfirmed","temporary","unavailable"),
            ["pending"]=new JObject{["type"]="boolean"},["dirty"]=new JObject{["type"]="boolean"},
            ["dispatchHeld"]=new JObject{["type"]="boolean"},["error"]=Text("^.{0,128}$",128)});
        internal static JObject Observe(bool available,bool pending,bool dirty,bool held,string error,bool temporary=false)=>new(){
            ["phase"]=!available?"unavailable":pending?"saving":!string.IsNullOrEmpty(error)?"unconfirmed":temporary?"temporary":dirty?"unsaved":"idle",
            ["pending"]=pending,["dirty"]=dirty,["dispatchHeld"]=held,["error"]=ImportObservation.Text(error)};
        internal static BehaviourCatalog.FactDefinition Definition()=>new("room.persistence",
            OutputType(Object(new JObject{["room"]=Store(),["behaviours"]=Store()})),
            "Room and behaviour save status",
            "Passive checkpoint status for journal-accepted room and behaviour edits. saving remains unconfirmed until the owner observes disk completion; dirty also includes edits newer than an in-flight snapshot. idle means these stores have no pending or dirty journal snapshot, not that authoring buffers, program memory, imported assets or modules are saved. unconfirmed retains accepted edits and the save error; original files remain protected by storage recovery. dispatchHeld means a review/recovery operation owns save dispatch. Temporary room forks never save implicitly: temporary means no room checkpoint is pending, not that the fork is durable. Reading this fact never flushes, retries, keeps a fork or waits for disk.",
            Object(new JObject()),new JObject(),(c,a)=>{
                if(!c.Editor)return null;
                var rules=c.Editor.GetComponent<RuleWorkshop>();
                return ProgramValue.Literal(new JObject{["room"]=c.Editor.ObserveCheckpoint(),
                    ["behaviours"]=rules?rules.ObserveCheckpoint():Observe(false,false,false,false,"Behaviour storage is unavailable.")});
            });
    }
}

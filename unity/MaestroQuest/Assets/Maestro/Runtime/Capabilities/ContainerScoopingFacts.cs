// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class ContainerScoopingFacts {
        internal const string Feature="containerScooping.v1";
        internal static BehaviourCatalog.FactDefinition Live()=>new("object.container.scooping",OutputType(Object(new JObject{
            ["sessionId"]=Text("^[a-f0-9]{32}$",32),["phase"]=Choice("idle","flowing","failed"),["scoopedMl"]=Number(0,8000000),["drawnMl"]=Number(0,8000000),["donors"]=Number(0,16,true),["recipients"]=Number(0,16,true)})),
            "Live liquid scooping","Current physical-flow episode: scoopedMl entered this vessel through its submerged opening; drawnMl was taken from it by other vessels. donors/recipients count distinct counterpart vessels. These counters reset on publication or rollback. An empty participating vessel changing liquid identity or colour first closes its prior episode. Read object.container.live for current and accepted quantities. An upward-facing smaller open cavity must fit inside the donor with its entire opening below the measured surface, with a clear bounded path. The same conserved quantities, physics pause, grips, save/Undo and temporary-room episode are used for pouring and scooping. No fluid forces, displacement, air simulation or persistent pools.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},
            (c,a)=>{var data=c.Editor?c.Editor.Liquids?.ObserveScooping((string)a["target"]):null;return data==null?null:ProgramValue.Literal(data);},features:new[]{Feature});
        internal static BehaviourCatalog.EventDefinition Scooped()=>new("object.container.scooped","A liquid scoop was saved",
            "A physical liquid-flow episode containing scooping was accepted by the room journal or temporary fork. Value/source is the receiving vessel ID; scoopedMl is its total intake and donors counts distinct source vessels. Emitted only after successful publication; failed saves, Undo and reload do not emit it. Pouring retains its separate object.container.poured event. A continuous episode checkpoints after ten seconds, or earlier before an empty participating vessel adopts a different liquid identity or colour. Intake is reconsidered next tick only after the prior episode saves, so reported quantities retain the correct liquid identity. Lifecycle-paused listeners may miss an event; inspect current contents instead.",Object(new JObject{["scoopedMl"]=Number(0,8000000),["donors"]=Number(0,16,true),["liquid"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32),["temporary"]=new JObject{["type"]="boolean"}}),objectEvent:true,features:new[]{Feature});
    }
}

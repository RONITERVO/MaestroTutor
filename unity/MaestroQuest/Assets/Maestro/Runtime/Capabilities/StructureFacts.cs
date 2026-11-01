// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class StructureFacts
    {
        static ProgramDataType Type(string fields)=>ProgramDataType.Read(JObject.Parse("{\"record\":"+fields+"}"));
        static JObject Id()=>Text("^[a-f0-9]{32}$",32);
        static JObject Query()=>Object(new JObject {["id"]=Id()});
        static JObject Example()=>new() {["id"]=new string('1',32)};
        static string[] Features=>new[]{StructureCapability.Feature};
        public static BehaviourCatalog.FactDefinition List()=>new("structure.list",Type("{\"total\":\"number\",\"next\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"name\":\"text\",\"revision\":\"number\",\"count\":\"number\"}}}}"),"Saved structures",
            "List up to four saved structure definitions in stable ID order. next is the next offset or -1. Metadata survives missing pieces and comes from the temporary fork when active. Reading does not authorize editing members. Use exact IDs/revisions, never names; structure.definition and structure.slot expose the saved baseline.",
            Object(new JObject {["offset"]=Number(0,16,true)}),new JObject {["offset"]=0},(context,args)=>{
                if(!context.Editor)return null;var all=context.Editor.Structures();int offset=(int)args["offset"];var page=all.Skip(offset).Take(4).ToArray();
                var data=new JObject {["total"]=all.Length,["next"]=offset+page.Length<all.Length?offset+page.Length:-1,["entries"]=new JArray(page.Select(s=>new JObject {["id"]=s.id,["name"]=s.name,["revision"]=context.Editor.StructureRevision(s.id),["count"]=s.slots.Length}))};
                return ProgramValue.Literal(data,Type("{\"total\":\"number\",\"next\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"name\":\"text\",\"revision\":\"number\",\"count\":\"number\"}}}}"));
            },features:Features);
        public static BehaviourCatalog.FactDefinition Definition()=>new("structure.definition",Type("{\"id\":\"text\",\"name\":\"text\",\"revision\":\"number\",\"positionTolerance\":\"number\",\"rotationTolerance\":\"number\",\"scaleTolerance\":\"number\",\"count\":\"number\",\"temporary\":\"boolean\"}"),"Structure definition",
            "Saved structure metadata and revision. Position tolerance is room-local metres, rotation tolerance is degrees, scale tolerance is an absolute uniform-scale difference. Each slot keeps an exact object ID even after deletion. Use structure.slot by index for its saved room-local pose. Changes to a member's live pose do not change this definition revision. Definitions do not contain geometry, replacements or scan anchors.",Query(),Example(),(context,args)=>{
                var e=context.Editor;var s=e?e.ReadStructure((string)args["id"]):null;if(s==null)return null;
                return ProgramValue.Literal(new JObject {["id"]=s.id,["name"]=s.name,["revision"]=e.StructureRevision(s.id),["positionTolerance"]=s.positionTolerance,["rotationTolerance"]=s.rotationTolerance,["scaleTolerance"]=s.scaleTolerance,["count"]=s.slots.Length,["temporary"]=e.TemporaryRoom});
            },features:Features);
        public static BehaviourCatalog.FactDefinition Slot()=>new("structure.slot",Type("{\"id\":\"text\",\"revision\":\"number\",\"index\":\"number\",\"slot\":\"text\",\"target\":\"text\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"rotation\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}},\"scale\":\"number\"}"),"Structure slot baseline",
            "One saved slot by zero-based index. target remains the exact member ID when that object is missing. Position/rotation/scale are the baseline in room-local axes, not the live transform. Read object.placement for a present member's current pose. This read does not authorize editing the returned target.",Object(new JObject {["id"]=Id(),["index"]=Number(0,15,true)}),new JObject {["id"]=new string('1',32),["index"]=0},(context,args)=>{
                var e=context.Editor;var s=e?e.ReadStructure((string)args["id"]):null;int index=(int)args["index"];if(s==null||index>=s.slots.Length)return null;var slot=s.slots[index];var data=JObject.Parse(JsonUtility.ToJson(slot.placement));data["id"]=s.id;data["revision"]=e.StructureRevision(s.id);data["index"]=index;data["slot"]=slot.slot;return ProgramValue.Literal(data);
            },features:Features);
        public static BehaviourCatalog.FactDefinition State()=>new("structure.state",Type("{\"id\":\"text\",\"revision\":\"number\",\"total\":\"number\",\"present\":\"number\",\"displaced\":\"number\",\"missing\":\"number\",\"held\":\"number\",\"available\":\"boolean\"}"),"Live structure state",
            "Compare every live member's room-local origin, rotation and uniform scale with the saved baseline tolerances. displaced counts present pieces outside any tolerance; missing counts deleted members; held counts direct grips. available=false when runtime is paused or any present transform is unavailable/invalid; do not interpret partial counts as a complete outcome then. A grip may intentionally displace a piece. This is a sampled pose comparison, not proof of stability, contact cause, visible deformation or collapse. Programs choose thresholds and settling delays; use contact observations separately when explaining likely causes.",Query(),Example(),(context,args)=>{
                var e=context.Editor;var s=e?e.ReadStructure((string)args["id"]):null;if(s==null)return null;var v=e.ObserveStructure(s);
                return ProgramValue.Literal(new JObject {["id"]=s.id,["revision"]=e.StructureRevision(s.id),["total"]=v.Total,["present"]=v.Present,["displaced"]=v.Displaced,["missing"]=v.Missing,["held"]=v.Held,["available"]=v.Available});
            },features:Features);
    }
}

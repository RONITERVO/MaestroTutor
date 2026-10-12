// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class ContactInputFacts {
        static ProgramDataType Type()=>OutputType(CapabilitySchema.Object(new JObject{["state"]=Choice("inactive","ready","pending","unavailable"),["ready"]=new JObject{["type"]="boolean"},["reason"]=Text(null,128),["dependencyCount"]=Number(0,66,true),["dependencies"]=List(Text(null,64),0,4)}));
        const string Meaning="Read the latest native contact-input ownership without loading, moving, saving or starting physics. pending means bounded preparation; unavailable needs a fresh action or tracking/grab owner after fixing its reason. ready rechecks required local geometry, not a clear path or regional-streaming guarantee. Dependencies report the full count and first four stable identities. The production whole-world physics gate remains.";
        internal static BehaviourCatalog.FactDefinition Object()=>new("object.contact.inputs",Type(),"Held object contact inputs",Meaning+" This fact covers an active physical controller/hand grab of a creation; unheld, paused or animation-owned items are inactive. A waiting grab holds its current pose and needs fresh throw samples before release. Scripted attachments use their separate existing ownership.",
            CapabilitySchema.Object(new JObject{["target"]=Text("^[a-zA-Z0-9_-]{1,64}$",64)}),new JObject{["target"]="example-object"},
            (c,a)=>c.Editor?.Find((string)a["target"])?.GetComponent<RoomGrabContactInputs>() is RoomGrabContactInputs value?ProgramValue.Literal(value.Observe(),Type()):null);
        internal static BehaviourCatalog.FactDefinition Attachment()=>new("object.attachment.inputs",Type(),"Carried object contact inputs",Meaning+" Covers object.hold, fitted animation props and physical catches. Waiting keeps the actual prop in place and discards old throw history; fitted full-body playback shares the wait. This passive read does not follow an anchor, advance playback or prove the release path is clear. A released or retired attachment is inactive; inspect its receipt for the outcome.",
            CapabilitySchema.Object(new JObject{["target"]=RecipeEditCapability.Target()}),new JObject{["target"]=new string('0',32)},
            (c,a)=>{var item=c.Editor?c.Editor.Find((string)a["target"]):null;if(!item||!item.isActiveAndEnabled)return null;var prop=item.GetComponents<HeldRoomProp>().LastOrDefault(p=>p.enabled);return ProgramValue.Literal(prop?prop.ObserveInputs():RoomContactInputs.State("inactive",false,null),Type());});
        internal static BehaviourCatalog.FactDefinition Controller()=>new("controller.contact.inputs",Type(),"Hand or controller contact inputs",Meaning+" Select the left or right tracked source. Tracking jumps and recovery inside an object keep the contact volume disabled until a fresh clear pose; they cannot replay a missed push. The visible hand/controller continues tracking.",
            CapabilitySchema.Object(new JObject{["hand"]=Choice("left","right")}),new JObject{["hand"]="right"},
            (c,a)=>{var input=c.Editor?c.Editor.GetComponentInParent<BookControllerInput>():null;return input&&input.Editor==c.Editor?ProgramValue.Literal(input.ObserveContact((string)a["hand"]=="left"?0:1),Type()):null;});
    }
}

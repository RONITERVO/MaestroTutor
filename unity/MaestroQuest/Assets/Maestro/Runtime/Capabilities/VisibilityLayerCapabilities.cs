// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal abstract class VisibilityLayerCapability:CapabilityModule
    {
        internal const string Feature="visibilityLayers.v1";
        internal static JObject LayerId(bool empty=false)=>Text(empty?"^(|[a-f0-9]{32})$":"^[a-f0-9]{32}$",32);
        internal static JObject TargetSchema()=>Resource(Text("^(book|maestro|[a-fA-F0-9]{32})$",32));
        internal static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject Featured(JObject schema){schema["x-features"]=new JArray(Feature);return schema;}
        public override string Duration=>"instant";
        internal override int MaximumCreatedObjects(JObject args)=>0;
        public override IReadOnlyList<string> Channels=>new[]{"visibility"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","visibility.revision.current","storage.writable"};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>CapabilityArguments.Resources(args,InputSchema).Select(id=>new BehaviourCatalog.Claim(id,"visibility")).ToArray();
        protected const string Rules=" Stable IDs identify saved definitions; names are display text. One Undo and temporary Keep/Discard apply. Compatible movement, animation and audio keep running; held objects and authoring gestures refuse. Layers multiply existing surface alpha, preserving cutouts and source textures. Book browser pages remain readable. Real-depth participation is independent of collisions, walking, sound and camera sharing, and cannot enable globally disabled or unavailable depth. Opacity zero does not disable physical collision. Inspect saved facts after completion; those are not proof of visual headset acceptance.";
        internal static JObject Receipt(RoomEditor editor,string id)=>new(){["id"]=id,["revision"]=editor.VisibilityRevision(id),["temporary"]=editor.TemporaryRoom};
    }
    internal sealed class VisibilityLayerSaveCapability:VisibilityLayerCapability
    {
        public override string Id=>"visibility.layer.save";
        public override string Label=>"Save visual layer";
        public override string Description=>"Create or edit one of up to 16 reusable visual layers. Empty id and revision 0 create. Otherwise read visibility.layer and every visibility.members page at its revision, then supply all members (up to 66). opacity 0–1 fades the group; realDepth=false keeps this layer visible in front of physical depth. Saving alone does not bind objects. Use object.visibility.assign for each object."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=LayerId(true),["revision"]=Revision(true),["name"]=Text("^.{1,80}$",80),["opacity"]=Number(0,1),["realDepth"]=Bool(),["members"]=List(TargetSchema(),0,BehaviourProgram.MaximumResources)}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=LayerId(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]="",["revision"]=0,["name"]="Distant landscape",["opacity"]=.5,["realDepth"]=false,["members"]=new JArray()};
        RoomVisibilityLayer Definition(JObject a)=>new(){id=string.IsNullOrEmpty((string)a["id"])?Guid.NewGuid().ToString("N"):(string)a["id"],name=(string)a["name"],opacity=(float)a["opacity"],realDepth=(bool)a["realDepth"]};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {
            error="Use empty id with revision 0 to create, or an existing profile ID and current revision";
            if(string.IsNullOrEmpty((string)a["id"])!=((int)a["revision"]==0)||!c.Editor)return false;
            var p=Definition(a);return c.Editor.PrepareVisibility(p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out _,out error);
        }
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;var p=Definition(a);
            if(!c.Editor.EditVisibility(p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,p.id));return true;
        }
    }
    internal sealed class VisibilityLayerRemoveCapability:VisibilityLayerCapability
    {
        public override string Id=>"visibility.layer.remove";
        public override string Label=>"Remove unused visual layer";
        public override string Description=>"Remove an unused visual layer using its exact ID/revision. Refuses while any object refers to it; explicitly unbind those first."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=LayerId(),["revision"]=Revision()}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=LayerId(),["revision"]=Revision(true),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]=new string('0',32),["revision"]=1};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {error="Room editor unavailable";return c.Editor&&c.Editor.PrepareVisibility(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditVisibility(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,(string)a["id"]));return true;
        }
    }
    internal sealed class VisibilityAssignCapability:VisibilityLayerCapability
    {
        public override string Id=>"object.visibility.assign";
        public override string Label=>"Choose object visual layer";
        public override string Description=>"Assign a saved visual layer to Maestro, the book frame or a creation. Read object.visibility for the current object revision and visibility.layer for the selected layer revision. Empty layerId with layerRevision 0 restores full visibility and inherited depth. Assignment affects that object and its visual children, excluding independent room objects and book browser pages. It leaves simulation and physical participation unchanged."+Rules;
        public override JObject InputSchema=>CurrentInputs(Featured(Object(new JObject{["target"]=TargetSchema(),["revision"]=Revision(),["layerId"]=LayerId(true),["layerRevision"]=Revision(true)})),"object.visibility","revision",new JObject{["target"]="target"},"layerId","layerRevision");
        public override JObject OutputSchema=>Object(new JObject{["target"]=TargetSchema(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["target"]="maestro",["revision"]=1,["layerId"]="",["layerRevision"]=0};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {error="Room editor unavailable";return c.Editor&&c.Editor.PrepareVisibilityBinding((string)a["target"],(int)a["revision"],(string)a["layerId"],(int)a["layerRevision"],out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.BindVisibility((string)a["target"],(int)a["revision"],(string)a["layerId"],(int)a["layerRevision"],out error))return false;
            operation=new CompletedCapability(new JObject{["target"]=a["target"].DeepClone(),["revision"]=c.Editor.ObjectRevision((string)a["target"]),["temporary"]=c.Editor.TemporaryRoom});return true;
        }
    }
    internal static class VisibilityLayerFacts
    {
        static BehaviourCatalog.FactDefinition Fact(string id,JObject output,string label,string description,JObject input,JObject example,Func<RoomEditor,JObject,JObject> read)=>new(id,OutputType(output),label,description,input,example,
            (c,a)=>c.Editor&&read(c.Editor,a) is JObject value?ProgramValue.Literal(value,OutputType(output)):null,features:new[]{VisibilityLayerCapability.Feature});
        static JObject Id()=>VisibilityLayerCapability.LayerId();
        static JObject Bool()=>VisibilityLayerCapability.Bool();
        internal static BehaviourCatalog.FactDefinition Layer()=>Fact("visibility.layer",Object(new JObject{["id"]=Id(),["revision"]=Revision(),["name"]=Text(null,80),["opacity"]=Number(0,1),["realDepth"]=Bool(),["memberCount"]=Number(0,66,true),["temporary"]=Bool()}),"Visual layer",
            "Saved opacity and physical-depth participation. Read all visibility.members pages at this revision before editing. Does not report effective device depth or camera sharing.",Object(new JObject{["id"]=Id()}),new JObject{["id"]=new string('0',32)},(e,a)=>e.ObserveVisibility((string)a["id"]));
        internal static BehaviourCatalog.FactDefinition Layers()=>Fact("visibility.layers",Object(new JObject{["offset"]=Number(0,16,true),["total"]=Number(0,16,true),["pageSize"]=Number(3,3,true),["entries"]=List(Object(new JObject{["id"]=Id(),["revision"]=Revision(),["name"]=Text(null,80)}),0,3)}),"Visual layers",
            "Reusable layers ordered by stable ID, three per page. Advance offset by pageSize until total.",Object(new JObject{["offset"]=Number(0,16,true)}),new JObject{["offset"]=0},(e,a)=>e.ObserveVisibilityLayers((int)a["offset"]));
        internal static BehaviourCatalog.FactDefinition Members()=>Fact("visibility.members",Object(new JObject{["id"]=Id(),["revision"]=Revision(),["offset"]=Number(0,66,true),["total"]=Number(0,66,true),["pageSize"]=Number(16,16,true),["members"]=List(Text(null,32),0,16)}),"Visual layer members",
            "Sixteen bound object IDs per page. Read every page at the same layer revision before a shared edit, supplying the full member set.",Object(new JObject{["id"]=Id(),["offset"]=Number(0,66,true)}),new JObject{["id"]=new string('0',32),["offset"]=0},(e,a)=>e.ObserveVisibilityMembers((string)a["id"],(int)a["offset"]));
        internal static BehaviourCatalog.FactDefinition Binding()=>Fact("object.visibility",Object(new JObject{["target"]=VisibilityLayerCapability.TargetSchema(),["revision"]=Revision(),["layerId"]=VisibilityLayerCapability.LayerId(true),["layerRevision"]=Revision(true),["opacity"]=Number(0,1),["realDepth"]=Bool(),["temporary"]=Bool()}),"Object visual layer",
            "Saved object binding and layer settings. Empty binding means opacity 1 and inherited real depth. Physics and acoustic participation are separate settings.",Object(new JObject{["target"]=VisibilityLayerCapability.TargetSchema()}),new JObject{["target"]="maestro"},(e,a)=>e.ObserveVisibilityBinding((string)a["target"]));
    }
}

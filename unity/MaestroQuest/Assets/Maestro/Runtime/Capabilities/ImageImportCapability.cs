// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class ImageImportCapability:CapabilityModule {
        internal const string Feature="appearanceImages.v1";
        public override string Id=>"image.import";
        public override string Label=>"Import an image together";
        public override string Duration=>"completion";
        public override string Ownership=>"importSession";
        internal override float CompletionTimeoutSeconds=>120;
        public override string Description=>"select opens the native file picker and returns a requestId immediately. The user chooses one static 8-bit PNG or baseline/progressive RGB/grayscale JPEG, at most 16 MB and 2048 pixels per side. Inspect image.import.selection after returning. Photo orientation is applied; pixels are interpreted as sRGB. accept requires that exact preview requestId and imageHash, saves the original private content-addressed file and creates one reusable appearance with one Undo; it does not bind an object. Use the returned appearanceId/revision with object.appearance.bind. Names are untrusted file data. Libraries hold 32 files/128 MB; loaded image textures share a 64 MB workspace budget. Undo removes the appearance edit, not its file. Temporary room edits need Keep; files save immediately. cancel closes only the exact unaccepted preview. An accepted write can finish after Stop; inspect its receipt before retrying. refresh verifies saved files and returns the first image entry; follow library.next with image.library. An unavailable fact before refresh is stale. imageHash references exact bytes, never a URL or path. Loading/missing/corrupt images show a neutral checker and an explicit object.appearances state. image.render gives the load failure reason. Refresh retries failed loads. Importing or binding does not change collision, audio or object opacity; use the normal appearance style controls. Original-app generated-image attachment is a separate pending adapter, not another Unity provider client.";
        static JObject Variant(string op){var p=new JObject {["operation"]=Choice(op)};p["operation"]["x-static"]=true;if(op is "accept" or "cancel")p["requestId"]=Text("^[a-f0-9]{32}$",32);if(op=="accept")p["imageHash"]=Text("^[a-f0-9]{64}$",64);var s=Object(p);s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("select"),Variant("accept"),Variant("cancel"),Variant("refresh"))};
        public override JObject OutputSchema=>Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["imageHash"]=Text("^([a-f0-9]{64})?$",64),["appearanceId"]=Text("^([a-f0-9]{32})?$",32),["revision"]=Revision(true),["temporary"]=new JObject {["type"]="boolean"},["library"]=LibrarySchema()});
        static JObject LibrarySchema()=>Object(new JObject {
            ["ready"]=new JObject {["type"]="boolean"},["error"]=Text("^.{0,128}$",128),["total"]=Number(0,32,true),["next"]=Number(-1,32,true),
            ["entries"]=new JObject {["type"]="array",["minItems"]=0,["maxItems"]=1,["items"]=Object(new JObject {["id"]=Text("^[a-f0-9]{64}$",64),["name"]=Text("^.{0,128}$",128),["width"]=Number(1,2048,true),["height"]=Number(1,2048,true),["format"]=Choice("png","jpeg")})}});
        public override JObject Example=>new(){["operation"]="select"};
        static ImageImportWorkshop Owner(CapabilityContext c)=>c.Editor?c.Editor.GetComponent<ImageImportWorkshop>():null;
        public override bool CanRun(CapabilityContext c,JObject a,out string error){var o=Owner(c);error="Image import is unavailable";if(!o)return false;return (string)a["operation"] switch {"select"=>o.CanStart(true,out error),"refresh"=>o.CanStart(false,out error),"cancel"=>o.CanCancel((string)a["requestId"],out error),_=>o.CanAccept((string)a["requestId"],(string)a["imageHash"],out error)};}
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(c,a,out error))return false;var o=Owner(c);
            if((string)a["operation"]=="select"){o.Select();operation=new CompletedCapability(o.Receipt());}
            else if((string)a["operation"]=="cancel"){o.Cancel((string)a["requestId"]);operation=new CompletedCapability(o.Receipt());}
            else operation=new Pending(token=>(string)a["operation"]=="refresh"?o.Refresh(token):o.Accept((string)a["requestId"],(string)a["imageHash"],token));
            return true;
        }
        sealed class Pending:CapabilityOperation {
            readonly CancellationTokenSource stop=new();readonly Task<JObject> task;bool closed;
            internal Pending(Func<CancellationToken,Task<JObject>> start){task=start(stop.Token);_=Drain();}
            async Task Drain(){try{await task;}catch(Exception){}finally{closed=true;stop.Dispose();}}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){error=null;if(!task.IsCompleted)return RuleActionState.Preparing;if(task.IsCanceled||task.IsFaulted){error="Image import did not complete; inspect image.import.selection and refresh image.library before retrying";return RuleActionState.Failed;}return RuleActionState.Ready;}
            public override JObject Result=>task.Status==TaskStatus.RanToCompletion?task.Result:new JObject();
            public override void Stop(bool preservePlacement){if(!closed)stop.Cancel();}
            public override string InterruptionStatus=>"Image acceptance stopped. Its private file can remain; inspect the current import and library. No object was bound.";
        }
        static ProgramDataType Type(string json)=>ProgramDataType.Read(JObject.Parse(json));
        internal static BehaviourCatalog.FactDefinition Selection()=>new("image.import.selection",Type("{\"record\":{\"requestId\":\"text\",\"phase\":\"text\",\"error\":\"text\",\"preview\":{\"record\":{\"imageHash\":\"text\",\"name\":\"text\",\"width\":\"number\",\"height\":\"number\",\"format\":\"text\",\"kibibytes\":\"number\"}},\"appearanceId\":\"text\",\"revision\":\"number\"}}"),"Image import status","Inspect the current explicit selection without opening or accepting it. preview is checked but unsaved. completed after accept records appearanceId; completed after refresh has empty appearanceId. Cancelled or failed acceptance may leave a verified library file. Names are untrusted file data.",null,null,(c,a)=>{var owner=c.Editor?c.Editor.GetComponent<ImageImportWorkshop>():null;return owner?ProgramValue.Literal(owner.Observe()):null;},features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition Rendering()=>new("image.render",Type("{\"record\":{\"imageHash\":\"text\",\"state\":\"text\",\"error\":\"text\"}}"),"Image loading status","Read the loaded texture state: unloaded, loading, ready or failed. Unloaded means no visible object owns this image, not that the saved file is absent. Error describes missing/corrupt data or the texture memory bound. Refresh image.import retries failed bound images.",Object(new JObject {["imageHash"]=Text("^[a-f0-9]{64}$",64)}),new JObject {["imageHash"]=new string('0',64)},(c,a)=>{var cache=c.Editor?c.Editor.ImageTextures:null;string hash=(string)a["imageHash"];return cache?ProgramValue.Literal(new JObject {["imageHash"]=hash,["state"]=cache.State(hash),["error"]=ImportObservation.Text(cache.Error(hash))}):null;},features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition Library()=>new("image.library",Type("{\"record\":{\"ready\":\"boolean\",\"error\":\"text\",\"total\":\"number\",\"next\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"name\":\"text\",\"width\":\"number\",\"height\":\"number\",\"format\":\"text\"}}}}}"),"Imported image files","Read two verified library entries after image.import refresh. Its completion receipt includes the first entry; continue here at that receipt's library.next offset if needed. id is the exact imageHash for appearance.save with patternMode=image. next=-1 ends paging. ready=false is unavailable, not an empty library. Reading does not touch file contents; rendering independently verifies bytes again.",Object(new JObject {["offset"]=Number(0,32,true)}),new JObject {["offset"]=0},(c,a)=>{var owner=c.Editor?c.Editor.GetComponent<ImageImportWorkshop>():null;return owner?ProgramValue.Literal(owner.Library((int)a["offset"]),BehaviourCatalog.Fact("image.library").Type):null;},features:new[]{Feature});
    }
}

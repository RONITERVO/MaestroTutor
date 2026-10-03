// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class DrawingResolveCapability:CapabilityModule
    {
        public override string Id=>"object.drawing.resolve";
        public override string Label=>"Retry or discard an unsaved pencil stroke";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"drawing.retained","drawing.session.current"};
        public override string Description=>"Resolve the same frozen failed physical-pencil draft as the tray's Retry stroke and Discard stroke controls. Read object.drawing.capture and supply its current sessionId. For a configured surface, retry appends ink to that unchanged patch and objectId identifies its owner; read object.surface.strokes for stroke IDs. Changed or missing patches refuse retry. For free-space captures, retry creates exactly that stroke through normal saved creation and Undo, or locally in its original temporary room until Keep; failure retains the points and error for another explicit attempt. Discard removes only the unsaved in-memory draft, never a saved selected object. No live stroke is interrupted, no drawing mode is enabled and no automatic retry occurs. The retained draft blocks workspace/temporary-room changes until resolved. App/room destruction loses unsaved memory; it is not a crash recovery archive. Saved output acknowledges an object, while discarded output has empty objectId. A completed receipt is historical and cannot save/discard a later draft. Neither path starts playback or physics.";
        public override JObject InputSchema {
            get {var schema=CurrentInputs(Object(new JObject {["operation"]=Choice("retry","discard"),["sessionId"]=Text("^[a-f0-9]{32}$",32)}),"object.drawing.capture","sessionId");schema["x-features"]=new JArray("drawingEdits.v1","actionResults.v1");return schema;}
        }
        public override JObject OutputSchema=>Object(new JObject {["sessionId"]=Text("^[a-f0-9]{32}$",32),["phase"]=Choice("saved","discarded"),["objectId"]=Text("^(|[a-f0-9]{32})$",32),["points"]=Number(2,2048,true),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["operation"]="retry",["sessionId"]=new string('0',32)};
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="The physical pencil is unavailable";var pencil=context.Editor.GetComponent<SpatialDrawing>();return pencil&&pencil.CanResolve((string)args["sessionId"],(string)args["operation"]=="discard",out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(context,args,out error)||!context.Editor.GetComponent<SpatialDrawing>().Resolve((string)args["sessionId"],(string)args["operation"]=="discard",out var result,out error))return false;operation=new CompletedCapability(result);return true;}
        public static BehaviourCatalog.FactDefinition Fact()=>new("object.drawing.capture",ProgramDataType.Read(JObject.Parse("{\"record\":{\"sessionId\":\"text\",\"phase\":\"text\",\"points\":\"number\",\"temporary\":\"boolean\",\"error\":\"text\"}}")),"Physical pencil draft","Live pencil phase (idle, drawing, unsaved), point count, temporary-room flag and bounded error. Unsaved retains a frozen failed stroke in memory and requires explicit Retry or Discard with this exact identity. Reads do not capture geometry or save it. Completed receipts and a later draft have different identities.",null,null,(context,args)=>context.Editor&&context.Editor.GetComponent<SpatialDrawing>() is SpatialDrawing pencil&&pencil?ProgramValue.Literal(pencil.Observe()):null);
    }
}

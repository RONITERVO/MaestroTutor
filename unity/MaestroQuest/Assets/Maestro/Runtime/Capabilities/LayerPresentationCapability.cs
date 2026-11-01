// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class LayerPresentationCapability:CapabilityModule
    {
        internal const string Feature="layerPresentation.v1";
        public override string Id=>"visibility.layer.present";
        public override string Label=>"Blend a visual layer";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"visibility.presentation.current","tracking.available","manual.released"};
        public override string Description=>"Only when the user requests a view change, start a smooth transient fade of a named visual layer. Read visibility.layers to choose its stable ID, then visibility.presentation for that ID and pass both exact stateId and viewStateId. opacity is a 0–1 multiplier of the saved layer opacity, not a replacement: 1 restores its authored visibility. realDepth is another upper bound on the saved layer depth preference and cannot enable globally disabled depth. seconds 0 snaps, up to 30 blends from the current value. The receipt confirms the requested transition started, not that it finished: inspect progress.blending/currentOpacity/effectiveOpacity for progress. Replacing a fade begins at its current value; an identical active request does not restart it. A started fade continues independently of the requesting program; stopping that program does not reverse a completed command. Use a fresh request to change it. Group membership remains live. Saved layer edits/Undo, the movement tray Stop / MR control, tracking/focus loss, disabling the view, temporary-room boundaries and workspace changes reset to authored defaults. Book/tool Recall preserves the current view and movement opt-ins. No room save or Undo entry; no changes to collisions, sound, animation, ongoing actions, positions, camera sharing or the book browser pages. Replayed receipts do not reapply viewing preferences.";
        public override JObject InputSchema {get{
            var schema=Object(new JObject{["id"]=VisibilityLayerCapability.LayerId(),["stateId"]=VisibilityLayerCapability.LayerId(),["viewStateId"]=VisibilityLayerCapability.LayerId(),["opacity"]=Number(0,1),["realDepth"]=VisibilityLayerCapability.Bool(),["seconds"]=Number(0,30)});
            schema["x-features"]=new JArray(Feature);
            CurrentInputs(schema,"visibility.presentation","stateId",new JObject{["id"]="id"},"viewStateId","opacity","realDepth");
            ((JArray)schema["x-current"]["guards"]).Add("viewStateId");
            ResourceChoice(schema,"Visual layer","visibility.layers","id",lookup:true);return schema;
        }}
        public override JObject OutputSchema=>Object(new JObject{
            ["id"]=VisibilityLayerCapability.LayerId(),["stateId"]=VisibilityLayerCapability.LayerId(),["viewStateId"]=VisibilityLayerCapability.LayerId(),
            ["opacity"]=Number(0,1),["realDepth"]=VisibilityLayerCapability.Bool(),["progress"]=Object(new JObject{
                ["currentOpacity"]=Number(0,1),["effectiveOpacity"]=Number(0,1),["effectiveRealDepth"]=VisibilityLayerCapability.Bool(),
                ["blending"]=VisibilityLayerCapability.Bool(),["remainingSeconds"]=Number(0,30)})});
        public override JObject Example=>new(){["id"]=new string('0',32),["stateId"]=new string('0',32),["viewStateId"]=new string('0',32),["opacity"]=.5,["realDepth"]=true,["seconds"]=1};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {
            error="Room view unavailable";return c.Editor&&c.Editor.CanPresentLayer((string)a["id"],(string)a["stateId"],(string)a["viewStateId"],out error);
        }
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.PresentLayer((string)a["id"],(string)a["stateId"],(string)a["viewStateId"],(float)a["opacity"],(bool)a["realDepth"],(float)a["seconds"],out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact(){var output=new LayerPresentationCapability().OutputSchema;return new("visibility.presentation",OutputType(output),"Visual layer presentation",
            "Transient viewer preferences for one saved layer. opacity is the requested multiplier, progress.currentOpacity is its interpolated value and effectiveOpacity includes the authored default. effectiveRealDepth is only layer eligibility, not proof of device depth. stateId changes on new preferences, resets and saved definition changes, never each blend frame; viewStateId guards tracking/control recovery. Unavailable if the layer or configured view is missing. Reads do not advance a fade or mutate saved contents.",
            Object(new JObject{["id"]=VisibilityLayerCapability.LayerId()}),new JObject{["id"]=new string('0',32)},
            (c,a)=>c.Editor&&c.Editor.ObserveLayerPresentation((string)a["id"]) is JObject value?ProgramValue.Literal(value,OutputType(output)):null,features:new[]{Feature});}
    }
}

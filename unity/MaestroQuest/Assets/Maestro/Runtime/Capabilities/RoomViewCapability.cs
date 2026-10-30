// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class RoomViewCapability:CapabilityModule {
        public override string Id=>"room.view.capture";
        public override string Label=>"Capture virtual room view";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>RoomEditor.ViewPreparationTimeout;
        public override IReadOnlyList<string> Requirements=>new[]{"room.active","viewer.available","workspace.available"};
        public override string Description=>"Capture a single 512×384 virtual-only image from the current viewer pose, with a 60 degree vertical field of view. Prepares relevant saved objects through the shared native loading budget before rendering, for up to 120 seconds. The pose is refreshed while loading; metadata records the actual capture pose. Stop, room edits, session changes and loss of focus interrupt preparation. A loading failure returns no new image. Includes Maestro and user-created room objects with their current appearance and pose. Excludes the book, private page/chat content, tool trays, scanned geometry and real camera/passthrough imagery. Neutral background; this is not a complete headset screenshot or proof of visibility against real walls. Pixels are separate from the metadata result and recurring room observations. The user and the delegated task can inspect the same exact hash-identified image. Capture does not move anything, save the room, create an object, or upload an image by itself. At most once per second; the native image expires after two minutes or session/lifecycle change. A retained receipt does not recreate an expired image. Desktop capture does not prove headset performance.";
        public override JObject InputSchema {get{var schema=Object(new JObject());schema["x-features"]=new JArray(RoomEditor.ViewCaptureFeature);return schema;}}
        internal static JObject MetadataSchema(){
            var position=Object(new JObject{["x"]=Number(-1000000,1000000),["y"]=Number(-1000000,1000000),["z"]=Number(-1000000,1000000)});
            return Object(new JObject{["captureId"]=Text("^[a-f0-9]{32}$",32),["sha256"]=Text("^[a-f0-9]{64}$",64),["mimeType"]=Choice("image/jpeg"),
                ["width"]=Number(512,512,true),["height"]=Number(384,384,true),["capturedAt"]=Text("^[0-9TZ:.-]{20,32}$",32),["sceneRevision"]=Revision(),["verticalFov"]=Number(60,60),["position"]=position,["rotation"]=Vector(true)});
        }
        public override JObject OutputSchema=>MetadataSchema();
        public override JObject Example=>new();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="The virtual room view is unavailable";return context.Editor&&context.Editor.CanCaptureView(out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;error="The virtual room view is unavailable";if(!context.Editor)return false;
            var capture=context.Editor.BeginViewCapture(out error);if(capture==null)return false;
            operation=new Pending(context.Editor,capture);return true;
        }
        sealed class Pending:CapabilityOperation {
            readonly RoomEditor editor;readonly VirtualRoomCapture capture;JObject result=new();
            internal Pending(RoomEditor editor,VirtualRoomCapture capture){this.editor=editor;this.capture=capture;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){error=null;if(!capture.Done)return RuleActionState.Preparing;return capture.Read(out _,out error)?RuleActionState.Ready:RuleActionState.Failed;}
            public override bool Complete(out string error){if(!capture.Read(out var image,out error))return false;result=editor.PublishViewCapture(image);capture.Dispose();return true;}
            public override JObject Result=>(JObject)result.DeepClone();
            public override void Stop(bool preservePlacement)=>capture.Dispose();
        }
    }
}

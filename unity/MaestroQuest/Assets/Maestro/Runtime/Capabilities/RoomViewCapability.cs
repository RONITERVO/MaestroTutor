// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class RoomViewCapability:CapabilityModule {
        public override string Id=>"room.view.capture";
        public override string Label=>"Capture virtual room view";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"room.active","viewer.available","workspace.available"};
        public override string Description=>"Capture a single 512×384 virtual-only image from the current viewer pose, with a 60 degree vertical field of view. Includes Maestro and user-created room objects with their current appearance and pose. Excludes the book, private page/chat content, tool trays, scanned geometry and real camera/passthrough imagery. Neutral background; this is not a complete headset screenshot or proof of visibility against real walls. Pixels are separate from the metadata result and recurring room observations. The user and the delegated task can inspect the same exact hash-identified image. Capture does not move anything, save the room, create an object, or upload an image by itself. At most once per second; the native image expires after two minutes or session/lifecycle change. A retained receipt does not recreate an expired image. Desktop capture does not prove headset performance.";
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
            operation=null;error="The virtual room view is unavailable";if(!context.Editor||!context.Editor.CaptureView(out var result,out error))return false;operation=new CompletedCapability(result);return true;
        }
    }
}

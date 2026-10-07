// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ScanPlacementCapability:CapabilityModule
    {
        internal const string Feature="scanPlacement.v1";
        public override string Id=>"object.scan.place";
        public override string Label=>"Place on an exact scanned surface";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","storage.writable","room.scan.current","room.active"};
        public override string Description=>"Move an existing object onto an exact loaded upward scanned plane (floor or tabletop). Read room.scan for stateId, then room.scan.surfaces and room.scan.surface for the requested anchor's actual ID, label and bounds. x/y locate the collision-bounds centre in that plane's local axes, not room/world axes. Choose an inspected FLOOR for a floor request; never substitute a tabletop or guessed height. If scan data is unavailable, inspect room.environment; loading an available saved scan is a prerequisite, not proof of alignment. This action never loads, scans, requests permission or starts physics. The complete projected collision AABB must fit the stored rectangle/polygon; unknown or stale anchors and steep surfaces fail without edits. Rotation/scale stay unchanged, support uses actual collision bounds with a 1 cm gap. This is one placement, not an attachment, collision-free path, free-space check, dynamic depth query or proof of real alignment. Geometry absent from the scan and other objects may obstruct the chosen location. Success saves one Undo (temporary during trial play) and resets velocity; already-running gravity may continue. Output identifies the exact anchor and actual room-local placement. Use object.surface.place instead when the user wants the first live surface below the object or at gaze.";
        public override JObject InputSchema {get {var s=CurrentInputs(Object(new JObject{["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32)),["stateId"]=Text("^[a-f0-9]{32}$",32),["anchorId"]=Text("^[a-f0-9]{32}$",32),["x"]=Number(-25,25),["y"]=Number(-25,25)}),"room.scan","stateId");s["x-features"]=new JArray(Feature);return s;}}
        static JObject Triple(double bound)=>Object(new JObject{["x"]=Number(-bound,bound),["y"]=Number(-bound,bound),["z"]=Number(-bound,bound)});
        public override JObject OutputSchema=>Object(new JObject{["target"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["revision"]=Revision(),["roomId"]=Text("^[a-f0-9]{32}$",32),["anchorId"]=Text("^[a-f0-9]{32}$",32),["position"]=Triple(25),["point"]=Triple(128),["normal"]=Triple(1),["temporary"]=new JObject{["type"]="boolean"}});
        public override JObject Example=>new(){["target"]="book",["stateId"]=new string('0',32),["anchorId"]=new string('0',32),["x"]=0,["y"]=0};
        static ScannedRoom Room(CapabilityContext c)=>c.Editor&&c.Editor.PhysicsWorld?c.Editor.PhysicsWorld.GetComponent<ScannedRoom>():null;
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="The scanned placement service is unavailable";var room=Room(c);return room&&c.Target(a,out _,out error)&&room.PrepareObjectOnScan(c.Editor,(string)a["target"],(string)a["stateId"],(string)a["anchorId"],(float)a["x"],(float)a["y"],out _,out _,out _,out error);}
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(c,a,out error)||!Room(c).PlaceObjectOnScan(c.Editor,(string)a["target"],(string)a["stateId"],(string)a["anchorId"],(float)a["x"],(float)a["y"],out var result,out error))return false;operation=new CompletedCapability(result);return true;}
    }
}

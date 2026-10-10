// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    // Private provider of the public object.create kind=copy; no legacy enum entry.
    internal sealed class CopyObjectCapability:NativeTargetCapability
    {
        public override string Id=>"object.create.copy";
        public override string Label=>"Copy an existing creation";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","source.revision.current","authoring.inactive","room.capacity","storage.writable"};
        public override string Description=>"Copy one user-created object's canonical data, using object.definition for its exact revision. The included book and Maestro cannot be copied. Choose the new origin x/y/z in room metres; preserve current rotation/scale, colour, physics settings, drawing points, recipe parts/tracks and exact model reference. Empty name keeps the source name. Recorded frame positions receive the same translation as the copied origin; their times, rotations, scales and loop remain unchanged. Copying never starts playback: recipe tracks are retained with playing=false, and live playback, velocities, grabs, program/button bindings and unsaved authoring are not copied. A copied recording may still begin away from the object's current pose, exactly as the source's timeline does. The source remains unchanged and unrelated actors continue. The exact new objectId is returned only after a normal saved creation with one Undo (local in a temporary room until Keep). This acknowledges the saved object, not completed asynchronous model loading; imported assets retain their bounded loader and unavailable placeholder behavior. No model or motion bytes are duplicated. Source ownership, stale revisions, aggregate room/model/point/frame budgets and failed saves reject without a partial copy. Stop after completion does not erase it; replaying a receipt cannot create another object.";
        public override JObject InputSchema=>CurrentInputs(Object(new JObject {["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["revision"]=Revision(),["name"]=Text("^.{0,80}$",80),["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)}),"object.definition","revision",new JObject {["target"]="target"});
        public override JObject OutputSchema=>Object(new JObject {["objectId"]=Resource(Text("^[a-f0-9]{32}$",32))});
        public override JObject Example=>new() {["target"]=new string('0',32),["revision"]=1,["name"]="",["x"]=.5,["y"]=1.3,["z"]=.65};
        public override bool Validate(JObject args,out string error){error="Position must be within 25 metres of the room origin";if(ObjectCapabilityData.Position(args).sqrMagnitude>625)return false;error=null;return true;}
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Target(args,out _,out error)&&context.Editor.CanCopyObject((string)args["target"],(int)args["revision"],out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error)||!context.Editor.CopyObject((string)args["target"],(int)args["revision"],(string)args["name"],ObjectCapabilityData.Position(args),out var id,out error))return false;
            operation=new CompletedCapability(new JObject {["objectId"]=id});return true;
        }
    }
}

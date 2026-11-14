// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class EnvironmentRecoveryCapability:CapabilityModule
    {
        internal const string Feature="environmentRecovery.v1";
        static JObject Featured(JObject value){value["x-features"]=new JArray(Feature);return value;}
        public override string Id=>"object.environment.recheck";
        public override string Label=>"Check restored object collisions";
        public override string Duration=>"instant";
        internal override int MaximumCreatedObjects(JObject args)=>0;
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","environment.revision.current","room.active"};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>new[]{new BehaviourCatalog.Claim((string)args["target"],"wholeTarget")};
        public override string Description=>"Check a restored object's collision hold by exact saved revision and stateId from object.environment.recovery. Native restoration never moves its saved pose. Unproven scanned overlap or unavailable selected ground keeps physics/navigation stopped while the view remains editable. This explicit retry checks current enabled shapes; held=true in the result means it still cannot resume. Success releases only this restoration hold, from rest; it does not start physics, replay a throw, start a walk or replay an animation. Already-clear requests preserve motion. Loading, scan readiness, visibility and observing do not retry failed holds. A grab-and-release or an explicit Start physics also checks eligible held objects. Other ownership, geometry, contact recovery and ground checks still apply. Runtime-only: no saved edit or Undo. Reading this fact never loads an object.";
        static JObject StateId()=>Text("^[a-f0-9]{32}$",32);
        internal static JObject Schema()=>Object(new JObject{["target"]=EnvironmentProfileCapability.TargetSchema(),["revision"]=Revision(),["stateId"]=StateId(),["held"]=EnvironmentProfileCapability.Bool(),["reason"]=Text("^.{0,256}$",256)});
        public override JObject InputSchema {get {
            var schema=CurrentInputs(Featured(Object(new JObject{["target"]=EnvironmentProfileCapability.TargetSchema(),["revision"]=Revision(),["stateId"]=StateId()})),"object.environment.recovery","stateId",new JObject{["target"]="target"},"revision");
            schema["x-current"]["guards"]=new JArray("stateId","revision");return schema;
        }}
        public override JObject OutputSchema=>Schema();
        public override JObject Example=>new(){["target"]="maestro",["revision"]=1,["stateId"]=new string('0',32)};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            var editor=context.Editor;error="The room is unavailable";if(!editor)return false;
            string target=(string)args["target"];
            if(!editor.CanEditObject(target,false,out error)||!context.Target(args,out var item,out error))return false;
            error="The restored object changed; read object.environment.recovery again";
            var binding=item.GetComponent<RoomEnvironmentBinding>();
            return binding&&editor.ObjectRevision(target)==(int)args["revision"]&&binding.RestorationState==(string)args["stateId"]&&binding.CanRetry(out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            string target=(string)args["target"];context.Editor.Find(target).GetComponent<RoomEnvironmentBinding>().RetryRestoration();
            operation=new CompletedCapability(Observe(context.Editor,target));return true;
        }
        internal static JObject Observe(Creation.RoomEditor editor,string target)
        {
            if(!editor||!editor.TryGetLiveObject(target,out var item,out _)||!item.TryGetComponent<RoomEnvironmentBinding>(out var binding))return null;
            return new JObject{["target"]=target,["revision"]=editor.ObjectRevision(target),["stateId"]=binding.RestorationState,["held"]=binding.RestorationHeld,["reason"]=binding.RestorationReason};
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.environment.recovery",OutputType(Schema()),"Restored object collision hold",
            "Passive runtime restoration hold and last check reason, with exact saved revision and native stateId for object.environment.recheck. held=false only means no restoration hold, not overall motion readiness or a clear route. It never performs geometry work, loads a view, resumes motion or updates the last check when the environment changes. Unloaded/inactive views are unavailable; use object.presence and object.environment.settings for saved state.",
            Object(new JObject{["target"]=EnvironmentProfileCapability.TargetSchema()}),new JObject{["target"]="maestro"},(c,a)=>Observe(c.Editor,(string)a["target"]) is JObject value?ProgramValue.Literal(value,OutputType(Schema())):null,features:new[]{Feature});
    }
}

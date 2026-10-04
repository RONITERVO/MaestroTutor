// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class CatchObjectCapability:CapabilityModule
    {
        internal const string Feature="physicalCatching.v1";
        public override string Id=>"object.physics.catch";
        public override string Label=>"Catch a physical object";
        public override string Duration=>"completion";
        public override int MinimumProgramVersion=>3;
        public override string Ownership=>"catchSocketThenContactProp";
        public override IReadOnlyList<string> Channels=>new[]{"catchSocket","upperBody"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.dynamicCreation","holder.anchorReady","room.physicsRunning","authoring.inactive"};
        public override string Description=>"Wait locally for a small solid/bouncy creation to physically reach an exact root, recipe-part or Maestro-hand socket. The incoming prop stays free while waiting, including user grip/release and throws from other actions. The holder's catch socket is reserved; avatarHand also owns upperBody and uses bounded rotation-only shoulder/elbow reaching on its current visible rig. Recipe/root sockets use their existing motion. offset is the desired collision-volume centre in holder-scale socket axes (not the object's pivot). The enclosing collision radius must be at most 0.3 world metres. Fixed-step relative sweeps detect crossings at radius+gripRadius, reject motion discontinuities and speeds above maxSpeed, and check scanned/object/controller obstruction and head clearance before contact capture. This is assisted catching, not finger physics or guaranteed interception. On contact a native reflex lease takes only the free prop; human grip/control has priority. It holds for holdSeconds then drops using real gravity. A timeout completes with caught=false and phase=missed; caught=true is only observed physical capture, dropped=true only a successful release. Missing/replaced anchors, authoring, pause or lost tracking stop without replay; cancellation keeps an already caught prop at its current placement. Use a version 3 program for per-action claims; legacy programs reserving the incoming prop are refused. No saved catch component or automatic restart. Read object.catch for live attempts and the receipt for terminal outcome; branch on caught instead of assuming success.";
        public override BehaviourCatalog.Claim[] Claims(JObject args){var holder=(JObject)args["holder"];string id=(string)holder["objectId"];return (string)holder["kind"]=="avatarHand"?new[]{new BehaviourCatalog.Claim(id,"upperBody"),new BehaviourCatalog.Claim(id,"catchSocket:"+(string)holder["hand"])}:new[]{new BehaviourCatalog.Claim(id,"catchSocket:"+((string)holder["part"]??"root"))};}
        public override JObject InputSchema{get{var s=Object(new JObject{["target"]=RecipeEditCapability.Target(),["holder"]=HoldObjectCapability.AnchorSchema(),["offset"]=Vector(),["timeout"]=Number(.1,15),["holdSeconds"]=Number(.1,10),["gripRadius"]=Number(.02,.15),["maxSpeed"]=Number(.1,8)});s["x-features"]=new JArray(Feature,"objectAttachments.v1","actionResults.v1");return s;}}
        public override JObject OutputSchema=>Object(new JObject{["target"]=Text("^[a-fA-F0-9]{32}$",32),["holder"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["phase"]=Choice("missed","dropped"),["caught"]=new JObject{["type"]="boolean"},["dropped"]=new JObject{["type"]="boolean"},["reason"]=Text("^[^\\x00-\\x1f]*$",256)});
        public override JObject Example=>new(){["target"]=new string('0',32),["holder"]=new JObject{["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=""},["offset"]=new JObject{["x"]=0,["y"]=0,["z"]=.18},["timeout"]=5,["holdSeconds"]=1,["gripRadius"]=.05,["maxSpeed"]=5};
        public override bool CanRun(CapabilityContext context,JObject args,out string error){
            if(context.Workshop&&(context.Workshop.ControlsTarget((string)args["target"])||context.Workshop.ControlsTarget((string)args["holder"]["objectId"]))){error="Finish authoring before catching";return false;}
            return RoomCatch.Check(context.Editor,(string)args["target"],HoldObjectCapability.Anchor((JObject)args["holder"]),out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;
            var attempt=RoomCatch.Begin(context.Editor,runId,(string)args["target"],HoldObjectCapability.Anchor((JObject)args["holder"]),args["offset"].ToObject<Vector3>(),(float)args["timeout"],(float)args["holdSeconds"],(float)args["gripRadius"],(float)args["maxSpeed"],out error);
            if(!attempt)return false;operation=new CatchOperation(attempt);return true;
        }
        sealed class CatchOperation:CapabilityOperation
        {
            readonly RoomCatch attempt;public CatchOperation(RoomCatch attempt){this.attempt=attempt;}
            public override float Seconds=>0;
            public override bool WaitsThroughGrab(string target)=>attempt&&attempt.WaitsThroughGrab(target);
            public override RuleActionState State(out string error){error=attempt.Error??(!attempt?"The catch was removed":null);return error!=null?RuleActionState.Failed:attempt.Finished?RuleActionState.Ready:RuleActionState.Preparing;}
            public override bool Complete(out string error)=>State(out error)==RuleActionState.Ready;
            public override JObject Result=>new(){["target"]=attempt.TargetId,["holder"]=attempt.HolderId,["phase"]=attempt.Phase,["caught"]=attempt.Caught,["dropped"]=attempt.Dropped,["reason"]=attempt.Reason};
            public override string InterruptionStatus=>attempt&&attempt.Caught?"The object was physically caught; cancellation keeps its current placement":null;
            public override void Stop(bool preservePlacement){if(attempt)attempt.End();}
        }
        public static BehaviourCatalog.EventDefinition ContactEvent()=>new("object.caught","Object physically caught","Emitted once after a successful native physical catch, never merely near a socket or on a missed attempt. Source and primary value are the incoming object ID; holder and part identify the actual catcher, speed is the incoming world speed in metres/second, and x/y/z are the captured volume centre. The event is queued through the shared bounded scheduler; it does not grant mutation authority or replay after a pause/restart. Observe the attempt or receipt for later drop, manual takeover or failure.",Object(new JObject{["holder"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["speed"]=Number(0,8),["x"]=Number(-1000000,1000000),["y"]=Number(-1000000,1000000),["z"]=Number(-1000000,1000000)}),objectEvent:true,features:new[]{Feature});
        static ProgramDataType CatchType()=>ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"attempts\":{\"list\":{\"record\":{\"holder\":\"text\",\"part\":\"text\",\"phase\":\"text\",\"caught\":\"boolean\",\"reason\":\"text\"}}}}}"));
        public static BehaviourCatalog.FactDefinition Fact()=>new("object.catch",CatchType(),"Live physical catch attempts","Read active attempts for a loaded prop. Waiting does not own the prop, holding means actual contact capture, and missed means timeout. Retired attempts leave this list; their durable one-off receipt remains authoritative. No inference from avatar proximity.",Object(new JObject{["target"]=RecipeEditCapability.Target()}),new JObject{["target"]=new string('0',32)},(context,args)=>{
            string target=(string)args["target"];if(!context.Editor||!context.Editor.Find(target))return null;
            return ProgramValue.Literal(new JObject{["target"]=target,["attempts"]=new JArray(context.Editor.GetComponentsInChildren<RoomCatch>(true).Where(a=>a.isActiveAndEnabled&&a.TargetId==target).Select(a=>new JObject{["holder"]=a.HolderId,["part"]=a.Part,["phase"]=a.Phase,["caught"]=a.Caught,["reason"]=a.Reason.Length<=256?a.Reason:a.Reason.Substring(0,256)}))},CatchType());
        },features:new[]{Feature});
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class LaunchObjectCapability:CapabilityModule
    {
        public override string Id=>"object.physics.launch";
        public override string Label=>"Aim and throw object";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.creationReady","target.unheld","authoring.inactive","physics.running","geometry.ready","trajectory.clear","viewer.available"};
        public override string Description=>"Launch a solid/bouncy creation toward an explicit world point or the current pose of an exact object/recipe-part/Maestro-hand anchor. The destination is the desired collision-volume centre. Anchor offset uses holder-scale metres. Replaces velocity and clears spin through the same rigid body as controller throws; this does not animate an arm or catch an object. Flight time 0.2–2 seconds is rounded up to fixed physics steps, with requested maximum speed up to 8 m/s. A bounded free-flight estimate includes gravity and damping. Current room/object/controller colliders and the head clearance zone can refuse a throw. Non-sphere objects use a conservative enclosing sphere. Read object.physics.trajectory to preview; execution always recomputes current readiness and path. Preview is not a reservation or a promised landing: moving targets, new obstructions and collisions change the outcome. Completion means launched, never arrived/caught. Stop cannot retract launched motion; pause clears motion through room physics. The launch itself adds no saved edit or Undo. Normal room physics autosave still captures later positions; use a temporary room to keep those changes in its fork until Keep. No automatic resume or retry.";
        static JObject Position()=>Object(new JObject{["x"]=Number(-1000000,1000000),["y"]=Number(-1000000,1000000),["z"]=Number(-1000000,1000000)});
        static JObject DestinationSchema(){
            JObject Branch(string kind,string label,JObject fields){fields["kind"]=Choice(kind);fields["kind"]["x-static"]=true;var s=Object(fields);s["title"]=label;return s;}
            return new JObject{["type"]="object",["title"]="Aim at",["x-discriminators"]=new JArray("kind"),["oneOf"]=new JArray(
                Branch("point","World point",new JObject{["position"]=Position()}),
                Branch("anchor","Attachment point",new JObject{["anchor"]=HoldObjectCapability.AnchorSchema(),["offset"]=Vector()}))};
        }
        public override JObject InputSchema{get{var s=Object(new JObject{["target"]=RecipeEditCapability.Target(),["destination"]=DestinationSchema(),["seconds"]=Number(.2,2),["maxSpeed"]=Number(.1,8)});s["x-features"]=new JArray("objectLaunch.v1","actionResults.v1");return s;}}
        public override JObject OutputSchema=>Object(new JObject{["target"]=Text("^[a-fA-F0-9]{32}$",32),["phase"]=Choice("launched"),["origin"]=Position(),["destination"]=Position(),["velocity"]=Position(),["seconds"]=Number(.2,2.05),["speed"]=Number(0,8),["radius"]=Number(0,1)});
        public override JObject Example=>new(){["target"]=new string('0',32),["destination"]=new JObject{["kind"]="point",["position"]=new JObject{["x"]=1,["y"]=1,["z"]=1}},["seconds"]=.75,["maxSpeed"]=5};
        static JObject Json(Vector3 v)=>new(){["x"]=v.x,["y"]=v.y,["z"]=v.z};
        static JObject Result(JObject args,RoomBallistics plan)=>new(){["target"]=args["target"],["origin"]=Json(plan.Origin),["destination"]=Json(plan.Destination),["velocity"]=Json(plan.Velocity),["seconds"]=plan.Seconds,["speed"]=plan.Speed,["radius"]=plan.Radius};
        static bool Plan(CapabilityContext context,JObject args,out RoomBallistics plan,out string error){
            plan=null;if(!context.Target(args,out var item,out error))return false;
            var destination=(JObject)args["destination"];Vector3 point;
            if((string)destination["kind"]=="anchor"){
                var anchor=HoldObjectCapability.Anchor((JObject)destination["anchor"]);
                if(anchor.HolderId==(string)args["target"]){error="Choose a different object as the throw destination";return false;}
                if(!anchor.Resolve(context.Editor,out var holder,out var socket,out error))return false;
                point=socket.position+socket.rotation*(destination["offset"].ToObject<Vector3>()*holder.transform.lossyScale.y);
            }else point=destination["position"].ToObject<Vector3>();
            var candidate=new RoomBallistics();if(!candidate.Prepare(item,context.Editor.PhysicsWorld,context.Editor.Viewer,point,(float)args["seconds"],(float)args["maxSpeed"],out error))return false;
            plan=candidate;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>Plan(context,args,out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!Plan(context,args,out var plan,out error))return false;
            if(!context.Editor.Find((string)args["target"]).GetComponent<RigidRoomItem>().Launch(plan.Velocity,Vector3.zero)){error="Object physics changed before launch";return false;}
            var result=Result(args,plan);result["phase"]="launched";operation=new CompletedCapability(result);return true;
        }
        public static BehaviourCatalog.FactDefinition Trajectory(){
            var vector=new JObject{["record"]=new JObject{["x"]="number",["y"]="number",["z"]="number"}};
            var type=ProgramDataType.Read(new JObject{["record"]=new JObject{["ready"]="boolean",["reason"]="text",["origin"]=vector,["destination"]=vector.DeepClone(),["velocity"]=vector.DeepClone(),["seconds"]="number",["speed"]="number",["radius"]="number"}});
            var module=new LaunchObjectCapability();
            return new BehaviourCatalog.FactDefinition("object.physics.trajectory",type,"Preview aimed throw","Read a fresh bounded estimate using the exact same arguments and collision checks as Aim and throw object. ready=false gives a reason; numerical fields are zero placeholders when not ready. This never launches, reserves or authorizes motion. Ready estimates are instantaneous snapshots, with no guarantee of arrival or a moving target remaining in place.",module.InputSchema,module.Example,(context,args)=>{
                if(!context.Editor)return null;var native=new CapabilityContext(context.Editor,context.Editor?context.Editor.GetComponent<Maestro.Quest.Creation.AnimationWorkshop>():null);
                bool ready=Plan(native,args,out var plan,out var error);var result=ready?Result(args,plan):new JObject{["target"]=args["target"],["origin"]=Json(Vector3.zero),["destination"]=Json(Vector3.zero),["velocity"]=Json(Vector3.zero),["seconds"]=0,["speed"]=0,["radius"]=0};
                result.Remove("target");result["ready"]=ready;result["reason"]=error??"";return ProgramValue.Literal(result);
            });
        }
    }
}

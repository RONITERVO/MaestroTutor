// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    // Named authoring edits operate on the same saved motion as the physical frame tools.
    internal sealed class AnimationAuthoringCapability:CapabilityModule
    {
        public override string Id=>"animation.author";
        public override string Label=>"Edit pose or recorded motion";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","storage.writable","object.revision.current"};
        public override string Description=>"Author the same canonical pose and motion used by physical joint/keyframe tools. Inspect animation.authored, animation.frame and animation.joint first. Pass the exact object revision. Each call is one atomic saved edit and Undo outside temporary-room mode; inside temporary mode it edits the fork until Keep. No playback starts. frames replaces the motion or patches up to eight frames by exact time (seconds); all resulting frames must begin at zero, have distinct increasing times and identical ordered joint channels, at most 301 frames/30 seconds. removeFrames removes exact existing times and rebases the first remaining time to zero; removing all clears the motion. settings adjusts loop and/or total duration. clear removes the motion but keeps the saved pose. pose replaces the complete saved Maestro pose; null restores automatic activity. Positions/rotations are in the target parent room space, scale is uniform, and joint rotations are canonical local rotations, not world rotations or imported-model bone coordinates. Use rest joint facts for neutral authored poses; omitted joints are not synthesized. Physical recording/pose preview must be stopped first. Stop after completion does not revert an edit; Undo is separate.";
        internal static JObject Target()=>Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32));
        internal static JObject JointSchema()=>Object(new JObject {["joint"]=Choice(Enum.GetNames(typeof(PoseJoint))),["rotation"]=Vector(true)});
        internal static JObject JointsSchema(){var value=List(JointSchema(),0,17);value["nullable"]=true;return value;}
        internal static JObject FrameSchema()=>Object(new JObject {["time"]=Number(0,30),["position"]=Object(new JObject {["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)}),["rotation"]=Vector(true),["scale"]=Number(.1,4),["joints"]=JointsSchema()});
        static JObject Variant(string operation,string label,JObject fields,params string[] optional)
        {
            var props=new JObject {["operation"]=Choice(operation),["target"]=operation=="pose"?Resource(Choice("maestro")):Target(),["revision"]=Revision()};props["operation"]["x-static"]=true;
            foreach(var property in fields.Properties())props[property.Name]=property.Value.DeepClone();var value=Object(props,optional);value["title"]=label;value["x-features"]=new JArray("animationAuthoring.v1");return value;
        }
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Animation edit",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(
            Variant("frames","Write keyframes",new JObject {["replace"]=new JObject {["type"]="boolean"},["frames"]=List(FrameSchema(),1,8)}),
            Variant("removeFrames","Remove keyframes",new JObject {["times"]=List(Number(0,30),1,8)}),
            Variant("settings","Timing and looping",new JObject {["duration"]=Number(.1,30),["loop"]=new JObject {["type"]="boolean"}},"duration","loop"),
            Variant("pose","Saved Maestro pose",new JObject {["joints"]=JointsSchema()}),
            Variant("clear","Clear recorded motion",new JObject()))};
        public override JObject OutputSchema=>Object(new JObject {["target"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["revision"]=Revision(),["frames"]=Number(0,301,true),["duration"]=Number(0,30),["loop"]=new JObject {["type"]="boolean"},["poseSaved"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["operation"]="settings",["target"]="maestro",["revision"]=1,["loop"]=true};
        internal static Quaternion Rotation(JToken value)=>new((float)value["x"],(float)value["y"],(float)value["z"],(float)value["w"]);
        internal static JointPose[] Joints(JToken value)=>value.Type==JTokenType.Null?null:((JArray)value).Select(x=>new JointPose {joint=Enum.Parse<PoseJoint>((string)x["joint"]),rotation=Rotation(x["rotation"])}).ToArray();
        internal static MotionFrame Frame(JToken value)=>new() {time=(float)value["time"],position=new Vector3((float)value["position"]["x"],(float)value["position"]["y"],(float)value["position"]["z"]),rotation=Rotation(value["rotation"]),scale=(float)value["scale"],joints=Joints(value["joints"])};
        public override bool Validate(JObject args,out string error)
        {
            error="Joint names and input frame times must be distinct; frame positions stay within 25 metres";
            string kind=(string)args["operation"];
            if(kind=="pose"&&!MotionFrame.ValidJoints(Joints(args["joints"])))return false;
            if(kind=="frames"){
                var frames=((JArray)args["frames"]).Select(Frame).ToArray();
                if(frames.Select(f=>f.time).Distinct().Count()!=frames.Length||frames.Any(f=>f.position.sqrMagnitude>625||!MotionFrame.ValidJoints(f.joints)))return false;
            }
            if(kind=="removeFrames"&&((JArray)args["times"]).Select(t=>(float)t).Distinct().Count()!=((JArray)args["times"]).Count)return false;
            if(kind=="settings"&&!args.ContainsKey("duration")&&!args.ContainsKey("loop")){error="Choose a duration or loop setting";return false;}
            error=null;return true;
        }
        bool Prepare(CapabilityContext context,JObject args,out RoomMotion motion,out JointPose[] pose,out bool writePose,out string error)
        {
            motion=null;pose=null;writePose=(string)args["operation"]=="pose";
            if(!context.Target(args,out _,out error))return false;
            var editor=context.Editor;string target=(string)args["target"];
            if(!editor.CanEditObject(target,false,out error))return false;
            if(editor.ObjectRevision(target)!=(int)args["revision"]){error="The object changed; inspect its animation before editing";return false;}
            var data=editor.Read(target);motion=data.motion;
            switch((string)args["operation"]){
                case "frames":if(!RoomMotionEdits.Put(motion,((JArray)args["frames"]).Select(Frame).ToArray(),(bool)args["replace"],out motion,out error))return false;break;
                case "removeFrames":if(!RoomMotionEdits.Remove(motion,((JArray)args["times"]).Select(t=>(float)t).ToArray(),out motion,out error))return false;break;
                case "settings":if(!RoomMotionEdits.Settings(motion,(bool?)args["loop"],(float?)args["duration"],out motion,out error))return false;break;
                case "pose":pose=Joints(args["joints"]);break;
                case "clear":motion=null;break;
            }
            data.motion=motion;if(writePose)data.joints=pose;
            var document=editor.Snapshot();document.objects=document.objects.Select(x=>x.id==target?data:x).ToArray();return document.Validate(out error);
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>Prepare(context,args,out _,out _,out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!Prepare(context,args,out var motion,out var pose,out var writePose,out error))return false;string target=(string)args["target"];
            if(!context.Editor.WriteAnimation(target,(int)args["revision"],motion,pose,writePose,out error))return false;
            operation=new CompletedCapability(Summary(context.Editor,target));return true;
        }
        internal static JObject Summary(RoomEditor editor,string id)
        {
            var data=editor.Read(id);return new JObject {["target"]=id,["revision"]=editor.ObjectRevision(id),["frames"]=data.motion?.frames.Length??0,["duration"]=data.motion?.Duration??0,["loop"]=data.motion?.loop??false,["poseSaved"]=data.joints!=null};
        }
    }
    internal static class AnimationAuthoringFacts
    {
        static JObject TargetRevision()=>new() {["target"]=AnimationAuthoringCapability.Target(),["revision"]=Revision()};
        static bool Current(BehaviourCatalog.FactContext context,JObject args,out RoomObjectData data)
        {
            data=null;var editor=context.Editor;string id=(string)args["target"];
            if(!editor||editor.ObjectRevision(id)!=(int)args["revision"])return false;data=editor.Read(id);return data!=null;
        }
        internal static BehaviourCatalog.FactDefinition Summary()=>new("animation.authored",ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"frames\":\"number\",\"duration\":\"number\",\"loop\":\"boolean\",\"poseSaved\":\"boolean\"}}")),
            "Authored animation","Saved pose/motion metadata at the current object revision, or the temporary-room fork when active. No animation is started and no live take is captured. poseSaved=false means automatic activity. Use this revision for frame/joint reads and edits; another edit or Undo invalidates it.",Object(new JObject {["target"]=AnimationAuthoringCapability.Target()}),new JObject {["target"]="maestro"},(context,args)=>context.Editor&&context.Editor.HasSavedObject((string)args["target"])?ProgramValue.Literal(AnimationAuthoringCapability.Summary(context.Editor,(string)args["target"])):null);
        internal static BehaviourCatalog.FactDefinition Frame()=>new("animation.frame",ProgramDataType.Read(JObject.Parse("{\"record\":{\"time\":\"number\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"rotation\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}},\"scale\":\"number\",\"jointChannels\":\"boolean\",\"joints\":{\"list\":\"text\"}}}")),
            "Authored frame","Read a saved frame by zero-based index at an exact object revision. Index -1 returns the saved object placement at time zero (not live physics/animation); its joint names come from the saved pose. Position/rotation are local to the room parent and scale is uniform. jointChannels distinguishes a present joint array (even empty) from null. Read individual rotations with animation.joint. Missing frames or stale revisions are unavailable.",Object(new JObject(TargetRevision().Properties()) {["index"]=Number(-1,300,true)}),new JObject {["target"]="maestro",["revision"]=1,["index"]=-1},(context,args)=>{
                if(!Current(context,args,out var data))return null;int index=(int)args["index"];var frame=index==-1?new MotionFrame {time=0,position=data.position,rotation=data.rotation,scale=data.scale,joints=data.joints}:data.motion?.frames.ElementAtOrDefault(index);if(frame==null)return null;
                var type=BehaviourCatalog.Fact("animation.frame").Type;return ProgramValue.Literal(new JObject {["time"]=frame.time,["position"]=JObject.FromObject(new {x=frame.position.x,y=frame.position.y,z=frame.position.z}),["rotation"]=QuaternionJson(frame.rotation),["scale"]=frame.scale,["jointChannels"]=frame.joints!=null,["joints"]=new JArray(frame.joints?.Select(j=>j.joint.ToString())??Array.Empty<string>())},type);
            });
        internal static BehaviourCatalog.FactDefinition Joint()=>new("animation.joint",ProgramDataType.Read(JObject.Parse("{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}}")),
            "Canonical joint rotation","Read one canonical local joint rotation. index -2 is the included canonical rest pose (also used for supported imported Maestro rigs), -1 is the saved pose, and 0 or higher selects a recorded frame. Missing joints/poses/frames and stale object revisions are unavailable, never synthesized identity rotations. This is not an imported model bone's raw local rotation. Reading does not enter posing mode.",Object(new JObject(TargetRevision().Properties()) {["index"]=Number(-2,300,true),["joint"]=Choice(Enum.GetNames(typeof(PoseJoint)))}),new JObject {["target"]="maestro",["revision"]=1,["index"]=-2,["joint"]="Head"},(context,args)=>{
                if(!Current(context,args,out var data)||data.kind!=RoomObjectKind.Maestro)return null;int index=(int)args["index"];var pose=index==-2?(context.Editor.TryGetLiveObject(data.id,out var item,out _)?item.GetComponent<MaestroAvatar>()?.PoseRig?.RestPose():null):index==-1?data.joints:data.motion?.frames.ElementAtOrDefault(index)?.joints;
                var found=pose?.FirstOrDefault(j=>j.joint==Enum.Parse<PoseJoint>((string)args["joint"]));return found==null?null:ProgramValue.Literal(QuaternionJson(found.rotation));
            });
        static JObject QuaternionJson(Quaternion q)=>new() {["x"]=q.x,["y"]=q.y,["z"]=q.z,["w"]=q.w};
    }
}

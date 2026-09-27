// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Playables;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RecordedAnimationCapability : FullBodyCapability
    {
        public override string Id=>"animation.recording.play";
        public override string Label=>"Recorded animation";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","recording.available"};
        public override JObject InputSchema=>Object(new JObject {
            ["target"]=AnimationTargets.TargetSchema(),["seconds"]=Number(0,30),["loop"]=new JObject {["type"]="boolean"},["prop"]=AnimationTargets.PropSchema()
        },"prop");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!base.CanRun(context,arguments,out error))return false;
            if(context.Editor.Read((string)arguments["target"]).motion!=null)return true;
            error="Record an animation on the target first";return false;
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new RecordedMotionOperation(context,arguments);operation=value;return value.Begin((bool)arguments["loop"],out error);
        }
    }
    internal sealed class ThrowRecordingCapability : FullBodyCapability
    {
        public override string Id=>"object.recording.throw";
        public override string Label=>"Throw recording";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","recording.twoFrames","rigidBody.dynamic","physics.running"};
        public override JObject InputSchema=>Object(new JObject {["target"]=AnimationTargets.TargetSchema()},"prop");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!base.CanRun(context,arguments,out error))return false;
            string id=(string)arguments["target"];var motion=context.Editor.Read(id).motion;
            if(motion==null) {error="Record an animation on the target first";return false;}
            var rigid=context.Editor.Find(id).GetComponent<RigidRoomItem>();
            if(rigid&&rigid.Dynamic&&motion.frames.Length>=2&&context.Editor.PhysicsWorld&&context.Editor.PhysicsWorld.Running)return true;
            error="Throw recording needs a physical creation, two motion frames and running room physics";return false;
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new ThrowRecordingOperation(context,arguments);operation=value;return value.Begin(false,out error);
        }
    }
    internal class RecordedMotionOperation : FullBodyOperation
    {
        PlayableGraph graph;
        ScriptPlayable<RoomMotionPlayable> player;
        protected RoomMotion Motion;
        float began;
        public RecordedMotionOperation(CapabilityContext context,JObject arguments):base(context,arguments) {}
        public bool Begin(bool loop,out string error) {
            if(!Acquire(out error))return false;
            Motion=Context.Editor.Read(TargetId).motion.Copy();Motion.loop=loop;
            if(Duration==0)Duration=Mathf.Max(.1f,Motion.Duration);
            graph=PlayableGraph.Create("Rule recording");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            player=ScriptPlayable<RoomMotionPlayable>.Create(graph);player.GetBehaviour().Motion=Motion;
            player.GetBehaviour().Apply=frame=>{
                if(!Target)return;
                Target.transform.SetLocalPositionAndRotation(frame.position,frame.rotation);Target.transform.localScale=Vector3.one*frame.scale;
                if(Avatar&&Avatar.PoseRig) {Avatar.PoseRig.SetManual(true);Avatar.PoseRig.Apply(frame.joints);}
            };
            ScriptPlayableOutput.Create(graph,"Motion").SetSourcePlayable(player);graph.Play();began=Time.unscaledTime;
            Evaluate(0);return BeginProp(out error);
        }
        protected void Evaluate(float time) {if(graph.IsValid()) {player.SetTime(time);graph.Evaluate(0);}}
        public override void Tick()=>Evaluate(Time.unscaledTime-began);
        protected override void ReleasePlayback() {if(graph.IsValid())graph.Destroy();}
    }
    internal sealed class ThrowRecordingOperation : RecordedMotionOperation
    {
        public ThrowRecordingOperation(CapabilityContext context,JObject arguments):base(context,arguments) {}
        public override bool Complete(out string error) {
            if(!base.Complete(out error))return false;
            if(!Target||!Context.Editor||!Context.Editor.PhysicsWorld||!Context.Editor.PhysicsWorld.Running) {error="Room physics stopped before release";return false;}
            // Sample the precise release pose even if the last rendered frame was skipped.
            Evaluate(Motion.Duration);
            var end=Motion.frames[^1];var before=Motion.Sample(Mathf.Max(0,Motion.Duration-.1f));
            float dt=Mathf.Max(.001f,Motion.Duration-before.time);var velocity=(end.position-before.position)/dt;
            var delta=end.rotation*Quaternion.Inverse(before.rotation);delta.ToAngleAxis(out float angle,out var axis);
            if(angle>180)angle-=360;
            var spin=Mathf.Abs(angle)<.001f?Vector3.zero:axis*(angle*Mathf.Deg2Rad/dt);
            if(Target.transform.parent) {velocity=Target.transform.parent.TransformVector(velocity);spin=Target.transform.parent.TransformDirection(spin);}
            Stop(true);
            if(Target.GetComponent<RigidRoomItem>().Launch(velocity,spin))return true;
            Context.Editor.RestorePose(TargetId);error="Room physics could not take ownership after the recording";return false;
        }
    }
}

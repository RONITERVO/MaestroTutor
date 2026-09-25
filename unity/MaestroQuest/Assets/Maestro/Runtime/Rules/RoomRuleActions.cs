// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;
using UnityEngine.Playables;

namespace Maestro.Quest.Rules
{
    public sealed class RoomRuleActions : IRuleActions, IRuleCompletion
    {
        sealed class Effect
        {
            public string TargetId;
            public PlayableGraph Graph;
            public ScriptPlayable<RoomMotionPlayable> Player;
            public float Began;
            public RoomMotion ThrowMotion;
            public AvatarSpatialMotion Spatial;
        }
        readonly RoomEditor editor;
        readonly AnimationWorkshop workshop;
        readonly Dictionary<string,Effect> effects = new();
        public RoomRuleActions(RoomEditor editor, AnimationWorkshop workshop) { this.editor = editor; this.workshop = workshop; }
        public bool CanRun(RuleStep step, out string error)
        {
            error = null;
            if (step.action == RuleActionKind.Wait) return true;
            var item = editor.Find(step.targetId);
            if (!item) { error = "An action target was removed; choose another target"; return false; }
            if (item.Grab.isSelected || (workshop && workshop.ControlsTarget(step.targetId))) { error = "Release the target and stop authoring before running its rule"; return false; }
            if ((step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ThrowRecording) && editor.Read(step.targetId).motion == null) { error = "Record an animation on the target first"; return false; }
            if (step.action == RuleActionKind.ThrowRecording && (!item.GetComponent<RigidRoomItem>() || !item.GetComponent<RigidRoomItem>().Dynamic || editor.Read(step.targetId).motion.frames.Length < 2 || !editor.PhysicsWorld || !editor.PhysicsWorld.Running))
            { error = "Throw recording needs a physical creation, two motion frames and running room physics"; return false; }
            if (step.action == RuleActionKind.Gesture && !item.GetComponent<MaestroAvatar>()) { error = "Gestures need a compatible Maestro avatar"; return false; }
            if (RuleDocument.IsSpatial(step.action))
            {
                var spatial = item.GetComponent<AvatarSpatialMotion>();
                if (!spatial) { error = "This target has no Maestro movement controls"; return false; }
                return spatial.CanBegin(step.action == RuleActionKind.FollowUser ? AvatarSpatialMode.Follow : AvatarSpatialMode.Look,out error);
            }
            return true;
        }
        public bool Start(string runId, RuleStep step, out float seconds, out string error)
        {
            seconds = step.seconds;
            if (!CanRun(step,out error)) return false;
            if (step.action == RuleActionKind.Wait) return true;
            var target = editor.Find(step.targetId); var avatar = target.GetComponent<MaestroAvatar>();
            if (avatar && !RuleDocument.IsSpatial(step.action)) { avatar.GetComponent<AvatarSpatialMotion>()?.Stop(); avatar.SetEditing(true); }
            var effect = new Effect { TargetId = step.targetId, Began = Time.unscaledTime }; effects.Add(runId,effect);
            target.GetComponent<RigidRoomItem>()?.SetAnimationOwner(effect,true);
            if (RuleDocument.IsSpatial(step.action))
            {
                effect.Spatial = target.GetComponent<AvatarSpatialMotion>();
                return effect.Spatial.Begin(runId,step.action == RuleActionKind.FollowUser ? AvatarSpatialMode.Follow : AvatarSpatialMode.Look,out error);
            }
            if (step.action == RuleActionKind.Gesture) { avatar.Gesture(step.gesture.ToString()); return true; }
            var motion = editor.Read(step.targetId).motion; motion.loop = step.loop;
            if (step.action == RuleActionKind.ThrowRecording) { motion.loop = false; effect.ThrowMotion = motion; }
            if (seconds == 0) seconds = Mathf.Max(.1f,motion.Duration);
            effect.Graph = PlayableGraph.Create("Rule recording"); effect.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            effect.Player = ScriptPlayable<RoomMotionPlayable>.Create(effect.Graph);
            effect.Player.GetBehaviour().Motion = motion;
            effect.Player.GetBehaviour().Apply = frame => {
                if (!target) return;
                target.transform.SetLocalPositionAndRotation(frame.position,frame.rotation); target.transform.localScale = Vector3.one*frame.scale;
                if (avatar && avatar.PoseRig) { avatar.PoseRig.SetManual(true); avatar.PoseRig.Apply(frame.joints); }
            };
            ScriptPlayableOutput.Create(effect.Graph,"Motion").SetSourcePlayable(effect.Player); effect.Graph.Play(); return true;
        }
        public void Tick()
        {
            foreach (var effect in effects.Values) if (effect.Graph.IsValid()) { effect.Player.SetTime(Time.unscaledTime-effect.Began); effect.Graph.Evaluate(0); }
        }
        public void Complete(string runId)
        {
            if (!effects.TryGetValue(runId,out var effect) || effect.ThrowMotion == null) { Stop(runId,false); return; }
            var item = editor.Find(effect.TargetId);
            if (!item || !editor.PhysicsWorld || !editor.PhysicsWorld.Running) { Stop(runId,false); return; }
            // Evaluate the precise release pose even when the final animation frame was skipped.
            var motion = effect.ThrowMotion;
            effect.Player.SetTime(motion.Duration); effect.Graph.Evaluate(0);
            var end = motion.frames[^1]; var before = motion.Sample(Mathf.Max(0,motion.Duration-.1f));
            float dt = Mathf.Max(.001f,motion.Duration-before.time);
            var velocity = (end.position-before.position)/dt;
            var delta = end.rotation * Quaternion.Inverse(before.rotation); delta.ToAngleAxis(out float angle,out var axis);
            if (angle > 180) angle -= 360;
            var spin = Mathf.Abs(angle) < .001f ? Vector3.zero : axis * (angle*Mathf.Deg2Rad/dt);
            if (item.transform.parent) { velocity = item.transform.parent.TransformVector(velocity); spin = item.transform.parent.TransformDirection(spin); }
            Stop(runId,true);
            if (!item.GetComponent<RigidRoomItem>().Launch(velocity,spin)) editor.RestorePose(effect.TargetId);
        }
        public void Stop(string runId, bool preservePlacement)
        {
            if (!effects.Remove(runId,out var effect)) return;
            if (effect.Graph.IsValid()) effect.Graph.Destroy();
            if (!editor) return;
            var item = editor.Find(effect.TargetId);
            if (effect.Spatial) { effect.Spatial.End(runId); return; }
            if (item) item.GetComponent<MaestroAvatar>()?.SetEditing(false);
            if (!preservePlacement) editor.RestorePose(effect.TargetId);
            if (item) item.GetComponent<RigidRoomItem>()?.SetAnimationOwner(effect,false);
        }
    }
}

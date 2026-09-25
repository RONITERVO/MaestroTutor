// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using UnityEngine;
using UnityEngine.Playables;

namespace Maestro.Quest.Rules
{
    public sealed class RoomRuleActions : IRuleActions
    {
        sealed class Effect
        {
            public string TargetId;
            public PlayableGraph Graph;
            public ScriptPlayable<RoomMotionPlayable> Player;
            public float Began;
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
            if (step.action == RuleActionKind.RecordedAnimation && editor.Read(step.targetId).motion == null) { error = "Record an animation on the target first"; return false; }
            if (step.action == RuleActionKind.Gesture && !item.GetComponent<MaestroAvatar>()) { error = "Gestures need a compatible Maestro avatar"; return false; }
            return true;
        }
        public bool Start(string runId, RuleStep step, out float seconds, out string error)
        {
            seconds = step.seconds;
            if (!CanRun(step,out error)) return false;
            if (step.action == RuleActionKind.Wait) return true;
            var target = editor.Find(step.targetId); var avatar = target.GetComponent<MaestroAvatar>();
            if (avatar) avatar.SetEditing(true);
            var effect = new Effect { TargetId = step.targetId, Began = Time.unscaledTime }; effects.Add(runId,effect);
            if (step.action == RuleActionKind.Gesture) { avatar.Gesture(step.gesture.ToString()); return true; }
            var motion = editor.Read(step.targetId).motion; motion.loop = step.loop;
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
        public void Stop(string runId, bool preservePlacement)
        {
            if (!effects.Remove(runId,out var effect)) return;
            if (effect.Graph.IsValid()) effect.Graph.Destroy();
            if (!editor) return;
            var item = editor.Find(effect.TargetId);
            if (item) item.GetComponent<MaestroAvatar>()?.SetEditing(false);
            if (!preservePlacement) editor.RestorePose(effect.TargetId);
        }
    }
}

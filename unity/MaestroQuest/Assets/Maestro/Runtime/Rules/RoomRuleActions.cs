// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using UnityEngine;
using UnityEngine.Playables;
using Maestro.Quest.Programs;

namespace Maestro.Quest.Rules
{
    public sealed class RoomRuleActions : IRuleActions, IRuleCompletion, IRuleReadiness, IProgramFacts
    {
        sealed class Effect
        {
            public string TargetId;
            public RecipeObject Recipe;
            public RuleStep Step;
            public float Duration;
            public AvatarHeldProp Prop;
            public RigidRoomItem PropReservation;
            public PlayableGraph Graph;
            public ScriptPlayable<RoomMotionPlayable> Player;
            public float Began;
            public RoomMotion ThrowMotion;
            public AvatarSpatialMotion Spatial;
            public ImportedModel ClipModel;
            public MotionLibrary.Lease Motion;
            public Task Preparation;
            public string LoadError, RigHash, ModelHash;
            public bool Cancelled, Started, Loop;
        }
        readonly RoomEditor editor;
        readonly AnimationWorkshop workshop;
        readonly Dictionary<string,Effect> effects = new();
        public static ImportedModel ClipModel(RoomItem item) => !item ? null : item.GetComponent<MaestroAvatar>()?.CustomModel ?? item.GetComponent<CreatedRoomObject>()?.Model;
        public RoomRuleActions(RoomEditor editor, AnimationWorkshop workshop) { this.editor = editor; this.workshop = workshop; }
        public bool TryRead(string name,out ProgramValue value) {
            value=default;if(!editor.PhysicsWorld)return false;
            return BehaviourCatalog.TryRead(name,new BehaviourCatalog.FactContext(physicsReady:editor.PhysicsWorld.SurfacesReady,
                physicsRunning:editor.PhysicsWorld.Running),out value);
        }
        public bool CanRun(RuleStep step, out string error)
        {
            error = null;
            if (step.action == RuleActionKind.Wait) return true;
            if (!AvatarHeldProp.CanAttach(editor,step,out error)) return false;
            if (!string.IsNullOrEmpty(step.propId) && workshop && workshop.ControlsTarget(step.propId)) { error="Stop authoring the prop before running this action"; return false; }
            var item = editor.Find(step.targetId);
            if (!item) { error = "An action target was removed; choose another target"; return false; }
            if (item.Grab.isSelected || (workshop && workshop.ControlsTarget(step.targetId))) { error = "Release the target and stop authoring before running its rule"; return false; }
            if ((step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ThrowRecording) && editor.Read(step.targetId).motion == null) { error = "Record an animation on the target first"; return false; }
            if (step.action == RuleActionKind.ThrowRecording && (!item.GetComponent<RigidRoomItem>() || !item.GetComponent<RigidRoomItem>().Dynamic || editor.Read(step.targetId).motion.frames.Length < 2 || !editor.PhysicsWorld || !editor.PhysicsWorld.Running))
            { error = "Throw recording needs a physical creation, two motion frames and running room physics"; return false; }
            if ((step.action == RuleActionKind.Gesture || step.action == RuleActionKind.UpperBodyGesture) && !item.GetComponent<MaestroAvatar>()) { error = "Gestures need a compatible Maestro avatar"; return false; }
            var tutor=item.GetComponent<MaestroAvatar>();
            if(tutor && tutor.ModelBusy) {error="Wait for Maestro to finish loading";return false;}
            // Direct tools/controller movement also own channels, even though
            // they are not scheduler runs. A program cannot silently take them.
            if(tutor && tutor.GetComponent<AvatarSpatialMotion>()?.Active==true && step.action!=RuleActionKind.UpperBodyGesture)
            {error="Stop Maestro's current movement before starting a conflicting action";return false;}
            if(tutor && tutor.UpperBodyActive && !RuleDocument.IsSpatial(step.action))
            {error="An upper-body gesture is already running";return false;}
            if (step.action == RuleActionKind.RecipeAnimation)
            {
                var recipe=editor.Read(step.targetId)?.recipe;
                if(recipe==null || recipe.tracks.Length==0 || !item.GetComponent<RecipeObject>()) {error="This object has no recipe animation";return false;}
            }
            if (step.action == RuleActionKind.ImportedClip)
            {
                var model = ClipModel(item); var avatar = item.GetComponent<MaestroAvatar>();
                if (string.IsNullOrEmpty(step.clipModelHash) || editor.Read(step.targetId).modelHash != step.clipModelHash ||
                    !model || !model.Ready || avatar && (avatar.ModelBusy || avatar.ModelHash != step.clipModelHash) || step.clipIndex < 0 || step.clipIndex >= model.ClipCount || model.ClipDuration(step.clipIndex) <= 0)
                { error = "Choose a clip from the target's current loaded model using Motion"; return false; }
                if (step.seconds == 0 && model.ClipDuration(step.clipIndex) > 30) { error = "Choose an explicit duration for clips longer than 30 seconds"; return false; }
            }
            if (step.action == RuleActionKind.LibraryMotion)
            {
                var model = ClipModel(item); var avatar = item.GetComponent<MaestroAvatar>(); var motion = editor.Motions.Find(step.motionId);
                if (motion == null) { error = "This saved motion is missing; choose one using Motion"; return false; }
                if (!model || !model.Ready || avatar && avatar.ModelBusy || string.IsNullOrEmpty(model.MotionRigHash) || motion.rigHash != model.MotionRigHash)
                { error = "This saved motion needs a compatible loaded model and rest pose"; return false; }
                if (step.seconds == 0 && motion.duration > 30) { error = "Choose an explicit duration for motions longer than 30 seconds"; return false; }
            }
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
            if (avatar && step.action != RuleActionKind.UpperBodyGesture && !RuleDocument.IsSpatial(step.action)) { avatar.GetComponent<AvatarSpatialMotion>()?.Stop(); avatar.SetEditing(true); }
            var effect = new Effect { TargetId = step.targetId, Began = Time.unscaledTime,Step=step.Copy(),Duration=seconds }; effects.Add(runId,effect);
            target.GetComponent<RigidRoomItem>()?.SetAnimationOwner(effect,true);
            if (!string.IsNullOrEmpty(step.propId))
            {
                effect.PropReservation=editor.Find(step.propId).GetComponent<RigidRoomItem>();
                if (effect.PropReservation.AnimationOwned) { error="Another animation owns this prop"; return false; }
                effect.PropReservation.SetAnimationOwner(effect,true);
            }
            if(step.action==RuleActionKind.UpperBodyGesture) {
                if(avatar.BeginUpperBody(runId,step.gesture.ToString()))return true;
                error="This upper-body gesture is unavailable";return false;
            }
            if (RuleDocument.IsSpatial(step.action))
            {
                effect.Spatial = target.GetComponent<AvatarSpatialMotion>();
                return effect.Spatial.Begin(runId,step.action == RuleActionKind.FollowUser ? AvatarSpatialMode.Follow : AvatarSpatialMode.Look,out error);
            }
            if (step.action == RuleActionKind.RecipeAnimation) {
                effect.Recipe=target.GetComponent<RecipeObject>();if(seconds==0)seconds=editor.Read(step.targetId).recipe.duration;
                effect.Duration=seconds;effect.Recipe.StartRule(step.loop);return true;
            }
            if (step.action == RuleActionKind.Gesture) { avatar.Gesture(step.gesture.ToString()); return BeginProp(effect,out error); }
            if (step.action == RuleActionKind.ImportedClip)
            {
                effect.ClipModel = ClipModel(target);
                if (seconds == 0) seconds = Mathf.Max(.1f,effect.ClipModel.ClipDuration(step.clipIndex));
                effect.Duration=seconds;
                if (avatar) { if (avatar.PlayImportedClip(step.clipIndex,step.loop)) return BeginProp(effect,out error); error = "This motion is unavailable; choose a loaded clip"; return false; }
                effect.ClipModel.Play(step.clipIndex,step.loop); return true;
            }
            if (step.action == RuleActionKind.LibraryMotion)
            {
                var entry = editor.Motions.Find(step.motionId); effect.ClipModel = ClipModel(target); effect.RigHash = effect.ClipModel.MotionRigHash;
                effect.ModelHash = editor.Read(step.targetId).modelHash; effect.Loop = step.loop;
                if (seconds == 0) seconds = Mathf.Max(.1f,entry.duration);
                effect.Duration=seconds; effect.Preparation = Prepare(effect,entry.id); return true;
            }
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
            ScriptPlayableOutput.Create(effect.Graph,"Motion").SetSourcePlayable(effect.Player); effect.Graph.Play(); effect.Duration=seconds;
            effect.Player.SetTime(0); effect.Graph.Evaluate(0); return BeginProp(effect,out error);
        }
        bool BeginProp(Effect effect,out string error)
        {
            error=null; if (string.IsNullOrEmpty(effect.Step.propId)) return true;
            if (effect.PropReservation) { effect.PropReservation.SetAnimationOwner(effect,false); effect.PropReservation=null; }
            effect.Prop=AvatarHeldProp.Begin(editor,effect.Step,effect.Duration,out error); return effect.Prop;
        }
        async Task Prepare(Effect effect,string motionId)
        {
            MotionLibrary.Lease lease = null;
            try
            {
                lease = await editor.Motions.AcquireAsync(motionId,effect.RigHash);
                if (!effect.Cancelled) { effect.Motion = lease; lease = null; }
            }
            catch (Exception error) { if (!effect.Cancelled) effect.LoadError = error is ModelImportException ? error.Message : "This saved motion could not load; import the original again"; }
            finally { lease?.Dispose(); }
        }
        public RuleActionState State(string runId,out string error)
        {
            error = null;
            if (!effects.TryGetValue(runId,out var effect)) return RuleActionState.Ready;
            if(effect.Step.action==RuleActionKind.UpperBodyGesture) {
                var tutor=editor?editor.Find(effect.TargetId)?.GetComponent<MaestroAvatar>():null;
                if(!tutor || !tutor.UpperBodyOwnedBy(runId)) {error="The upper-body gesture was interrupted";return RuleActionState.Failed;}
            }
            if(effect.Spatial&&!effect.Spatial.OwnedBy(runId)) {error=effect.Spatial.Status;return RuleActionState.Failed;}
            if (effect.Prop && !effect.Prop.Valid(out error)) return RuleActionState.Failed;
            if (effect.Preparation == null || effect.Started) return RuleActionState.Ready;
            if (!effect.Preparation.IsCompleted) return RuleActionState.Preparing;
            if (effect.LoadError != null) { error = effect.LoadError; return RuleActionState.Failed; }
            var target = editor ? editor.Find(effect.TargetId) : null; var avatar = target ? target.GetComponent<MaestroAvatar>() : null;
            if (!target || target.Grab.isSelected || workshop && workshop.ControlsTarget(effect.TargetId) ||
                !effect.ClipModel || ClipModel(target) != effect.ClipModel || editor.Read(effect.TargetId)?.modelHash != effect.ModelHash ||
                avatar && avatar.ModelBusy || effect.Motion == null || effect.Motion.RigHash != effect.ClipModel.MotionRigHash)
            { error = "The motion target changed while loading; choose it again"; return RuleActionState.Failed; }
            if (avatar)
            {
                if (!avatar.PlayLibraryMotion(effect.Motion,effect.Loop)) { error = "This motion no longer matches Maestro"; return RuleActionState.Failed; }
                effect.Motion = null; // Avatar now owns the lease.
            }
            else if (!effect.ClipModel.SampleMotion(effect.Motion,0,effect.Loop)) { error = "This saved motion cannot play on this object"; return RuleActionState.Failed; }
            effect.Began = Time.unscaledTime; effect.Started = true;
            return BeginProp(effect,out error) ? RuleActionState.Ready : RuleActionState.Failed;
        }
        public void Tick()
        {
            foreach (var effect in effects.Values)
            {
                if (effect.Graph.IsValid()) { effect.Player.SetTime(Time.unscaledTime-effect.Began); effect.Graph.Evaluate(0); }
                if (effect.Started && effect.Motion != null && effect.ClipModel) effect.ClipModel.SampleMotion(effect.Motion,Time.unscaledTime-effect.Began,effect.Loop);
            }
        }
        public bool Complete(string runId,out string error)
        {
            error=null;
            if (!effects.TryGetValue(runId,out var effect)) return true;
            if (effect.Prop) { effect.Prop.Finish(); error=effect.Prop.Error; }
            if (effect.ThrowMotion == null || error != null) { Stop(runId,false); return error == null; }
            var item = editor.Find(effect.TargetId);
            if (!item || !editor.PhysicsWorld || !editor.PhysicsWorld.Running) { Stop(runId,false); error="Room physics stopped before release"; return false; }
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
            if (item.GetComponent<RigidRoomItem>().Launch(velocity,spin)) return true;
            editor.RestorePose(effect.TargetId); error="Room physics could not take ownership after the recording"; return false;
        }
        public void Stop(string runId, bool preservePlacement)
        {
            if (!effects.Remove(runId,out var effect)) return;
            if (effect.PropReservation) effect.PropReservation.SetAnimationOwner(effect,false);
            if (effect.Prop) effect.Prop.End(preservePlacement);
            effect.Cancelled = true; effect.Motion?.Dispose(); effect.Motion = null;
            if (effect.Graph.IsValid()) effect.Graph.Destroy();
            if (effect.ClipModel) effect.ClipModel.Stop();
            if (effect.Recipe) effect.Recipe.StopRule();
            if (!editor) return;
            var item = editor.Find(effect.TargetId);
            if(effect.Step.action==RuleActionKind.UpperBodyGesture) {
                if(item)item.GetComponent<MaestroAvatar>()?.EndUpperBody(runId);
                return; // Another owner may be walking: never restore the root or stop its clip.
            }
            if (effect.Spatial) { effect.Spatial.End(runId); return; }
            if (item) item.GetComponent<MaestroAvatar>()?.SetEditing(false);
            if (!preservePlacement) editor.RestorePose(effect.TargetId);
            if (item) item.GetComponent<RigidRoomItem>()?.SetAnimationOwner(effect,false);
        }
    }
}

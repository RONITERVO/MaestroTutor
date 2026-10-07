// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using UnityEngine;
using UnityEngine.Playables;

namespace Maestro.Quest.Creation
{
    /// <summary>One explicit authoring/preview owner; clips never autoplay on app launch.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class AnimationWorkshop : MonoBehaviour
    {
        RoomEditor editor;
        string targetId;
        RoomItem target;
        MaestroAvatar avatar;
        List<MotionFrame> recording;
        float began, nextSample;
        bool posing, stopping, saving;
        string saveError;
        PlayableGraph graph;
        ScriptPlayable<RoomMotionPlayable> player;
        RoomMotion preview;
        int selectedFrame = -1;
        int gestureIndex;
        bool controlling;
        bool importedPreview, walkPreview;
        public event Action<string> Starting;
        public bool ControlsTarget(string id) => controlling && targetId == id;
        readonly string ownershipId="authoring:"+Guid.NewGuid().ToString("N");
        RoomOwnership.Lease ownershipLease;
        bool TakeControl(bool allowsGrab=false,bool notify=true) {
            if(target&&target.PoseLocked){Say("Scanned ink layers follow their room anchors; edit the layer or ink instead");return false;}
            if(HasUnsavedPose){Say(RetainedPosePrompt);return false;}
            if(editor.RuntimeGate.Held){Say(editor.RuntimeGate.Reason);return false;}
            if(ownershipLease?.Held!=true&&!editor.Ownership.TryAcquire(ownershipId,"Animation authoring",RoomActorRole.Control,
                new[]{new Maestro.Quest.Programs.BehaviourCatalog.Claim(targetId,"wholeTarget")},_=>Stop(),out ownershipLease,out var error,replaceControl:true,allowsGrab:allowsGrab)) {Say(error);return false;}
            if(!ownershipLease.SetAllowsGrab(allowsGrab)){Say("Release the object before changing animation controls");return false;}
            controlling=true;if(notify)Starting?.Invoke(targetId);target?.GetComponent<RigidRoomItem>()?.SetAnimationOwner(this,true);return true;
        }
        public bool IsRecording => recording != null;
        public bool IsPlaying => graph.IsValid();
        public bool IsPosing => posing;
        public bool IsImportedPreview => importedPreview || walkPreview;
        public string Status { get; private set; } = "Select an object, or choose Pose Maestro";
        public event Action Changed;

        public void Initialize(RoomEditor source)
        {
            editor = source; editor.Changed += SelectionChanged; editor.Editing += Stop;
            editor.ItemGrabbed += Grabbed; SelectionChanged();
        }
        void SelectionChanged()
        {
            if (saving || stopping || targetId == editor.SelectedId) return;
            Stop(); targetId = editor.SelectedId; target = editor.Find(targetId); avatar = target ? target.GetComponent<MaestroAvatar>() : null;
            selectedFrame = -1; Say(HasUnsavedPose ? RetainedPosePrompt : target ? "Selected " + editor.Read(targetId).kind : "Select an object, or choose Pose Maestro");
        }
        void Grabbed(RoomItem item) { if (IsPlaying || importedPreview || walkPreview) Stop(); }
        void Say(string value) { Status = value; Changed?.Invoke(); }
        bool Ready()
        {
            if(HasUnsavedPose){Say(RetainedPosePrompt);return false;}
            if(editor.RuntimeGate.Held){Say(editor.RuntimeGate.Reason);return false;}
            if(HasUnsavedRecording){Say("Save or discard the retained take first");return false;}
            if (!target) { Say("Select an object first"); return false; }
            if(!editor.Frame.Read(target.transform,out _,out _,out _)){Say("The animation needs a valid uniform room frame");return false;}
            if (avatar && avatar.ModelBusy) { Say("Wait for Maestro to finish changing avatars"); return false; }
            if (editor.AnyHeld || (avatar && avatar.PoseRig && avatar.PoseRig.IsHolding)) { Say("Release the object or joint first"); return false; }
            return true;
        }
        MotionFrame Capture(float time) {
            var pose=editor.Frame.Placement(targetId,target.transform);
            return new() {time=time,position=pose.position,rotation=pose.rotation,scale=pose.scale,joints=avatar&&avatar.PoseRig?avatar.PoseRig.Capture():null};
        }
        bool Save(RoomMotion motion, bool savePose = false, JointPose[] joints = null)
        {
            saving = true;
            try
            {
                bool accepted = editor.SaveAnimation(targetId,motion,joints,savePose);
                saveError = accepted ? null : editor.Status;
                if(accepted&&posing)PoseEdited();
                if (!accepted) Say(saveError);
                return accepted;
            }
            finally { saving = false; }
        }
        public void TogglePose()
        {
            if(HasUnsavedPose){Say(RetainedPosePrompt);return;}
            if(HasUnsavedRecording){Say("Save or discard the retained take first");return;}
            if (posing) { Stop(); return; }
            if (editor.AnyHeld) { Say("Release the object first"); return; }
            var currentPose = avatar && avatar.PoseRig ? avatar.PoseRig.Capture() : null;
            Stop(); if(HasUnsavedPose||HasUnsavedRecording)return;
            if(!StartPose(poseSession,editor.ObjectRevision("maestro"),out _,out var error,manual:true,initial:currentPose))Say(error);
        }

        void SavePose()
        {
            if (stopping || !posing || IsRecording || !avatar || HasUnsavedPose) return;
            if (SaveCurrentPose()) Say("Pose saved — use Undo to restore the previous pose");
            else Stop();
        }
        public void AddFrame()
        {
            if (!Ready() || IsRecording) return;
            if(!TakeControl(allowsGrab:true))return;
            StopPlayback();
            var motion = editor.Read(targetId).motion ?? new RoomMotion();
            if (motion.frames.Length >= RoomMotion.MaximumFrames || motion.Duration >= RoomMotion.MaximumSeconds) { Say("This animation is full"); return; }
            if(!RoomMotionEdits.Put(motion,new[]{Capture(motion.frames.Length==0?0:motion.Duration+1)},false,out var edited,out var error)){Say(error);return;}
            if (Save(edited,avatar,avatar ? avatar.PoseRig.Capture() : null)) { selectedFrame = edited.frames.Length - 1; Say("Frame " + edited.frames.Length + " saved; move or pose, then add another"); }
        }
        public void StepFrame(int direction)
        {
            if (!Ready() || IsRecording) return;
            StopPlayback(); var motion = editor.Read(targetId).motion;
            if (motion == null) { Say("Save a frame first"); return; }
            selectedFrame = Mathf.Clamp(selectedFrame + direction,0,motion.frames.Length - 1);
            if(!TakeControl(allowsGrab:true))return;
            if (avatar) avatar.SetEditing(true);
            Apply(motion.frames[selectedFrame]); Say("Frame " + (selectedFrame+1) + " of " + motion.frames.Length);
        }
        public void ReplaceFrame()
        {
            if (!Ready() || IsRecording || IsPlaying) return;
            var motion = editor.Read(targetId).motion;
            if (motion == null || selectedFrame < 0 || selectedFrame >= motion.frames.Length) { Say("Choose a frame first"); return; }
            if(!RoomMotionEdits.Put(motion,new[]{Capture(motion.frames[selectedFrame].time)},false,out var edited,out var error)){Say(error);return;}
            if (Save(edited)) Say("Frame " + (selectedFrame+1) + " replaced");
        }
        public void DeleteFrame()
        {
            if (!Ready() || IsRecording) return;
            StopPlayback(); var motion = editor.Read(targetId).motion;
            if (motion == null || selectedFrame < 0 || selectedFrame >= motion.frames.Length) { Say("Choose a frame first"); return; }
            if(!RoomMotionEdits.Remove(motion,new[]{motion.frames[selectedFrame].time},out var edited,out var error)){Say(error);return;}
            if (Save(edited)) { selectedFrame = -1; Say("Frame removed — Undo restores it"); }
        }
        public void Play()
        {
            if (!Ready() || IsRecording) return;
            Stop(); preview = editor.Read(targetId).motion;
            if (preview == null || preview.frames.Length < 2) { Say("Save at least two frames to play"); return; }
            if(!TakeControl())return;
            if (avatar) avatar.SetEditing(true);
            target.Grab.enabled = false;
            graph = PlayableGraph.Create("User animation preview"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            player = ScriptPlayable<RoomMotionPlayable>.Create(graph); player.GetBehaviour().Motion = preview; player.GetBehaviour().Apply = Apply;
            var output = ScriptPlayableOutput.Create(graph,"Room motion"); output.SetSourcePlayable(player);
            graph.Play(); began = Time.unscaledTime; Say(preview.loop ? "Playing loop — Stop returns to the saved pose" : "Playing — Stop returns to the saved pose");
        }
        public void ToggleLoop()
        {
            if (!Ready() || IsRecording) return;
            StopPlayback(); var motion = editor.Read(targetId).motion;
            if (motion == null) { Say("Save a frame first"); return; }
            if(!RoomMotionEdits.Settings(motion,!motion.loop,null,out var edited,out var error)){Say(error);return;}
            if (Save(edited)) Say(edited.loop ? "Loop on" : "Loop off");
        }
        void Apply(MotionFrame frame)
        {
            if (!target) return;
            if(!editor.Frame.Apply(target.transform,frame.position,frame.rotation,frame.scale)){Say("The animation needs a valid uniform room frame");return;}
            if (avatar && avatar.PoseRig) { avatar.PoseRig.SetManual(true); avatar.PoseRig.Apply(frame.joints); }
        }
        public void ResetPose()
        {
            if (!Ready() || IsRecording) return;
            Stop(); if(!Ready())return;
            if (!avatar) { Say("Choose Maestro to restore automatic gestures"); return; }
            if(Save(editor.Read(targetId).motion,true,null))Say("Maestro follows tutor activity again");
        }
        public void Gesture()
        {
            if (!Ready() || IsRecording) return;
            Stop(); if (!avatar) { Say("Choose Maestro for gestures"); return; }
            var names = new[] { "Greeting","Pointing","Listening","Speaking","Idle","Walk" };
            string name = names[gestureIndex++ % names.Length];
            if(!TakeControl())return; avatar.SetEditing(true); avatar.Gesture(name); Say(name + " preview — tap Gesture to choose another");
        }
        public bool PreviewImportedClip(int index, bool loop)
        {
            if (!Ready() || IsRecording || !avatar) return false;
            Stop(); if(!TakeControl())return false; avatar.SetEditing(true);
            if (!avatar.PlayImportedClip(index,loop)) { Stop(); Say("This Maestro has no playable clip at that index"); return false; }
            importedPreview = true; Say("Playing " + avatar.CustomModel.ClipName(index) + " — Stop ends preview"); return true;
        }
        public bool PreviewLibraryMotion(MotionLibrary.Lease motion,bool loop)
        {
            if (!Ready() || IsRecording || !avatar) return false;
            Stop(); if(!TakeControl())return false; avatar.SetEditing(true);
            if (!avatar.PlayLibraryMotion(motion,loop)) { Stop(); Say("This motion is incompatible with the loaded Maestro"); return false; }
            importedPreview = true; Say("Playing library motion — Stop ends preview"); return true;
        }
        public void PreviewWalk()
        {
            Stop(); editor.Select(editor.Find("maestro")); SelectionChanged(); if (!Ready() || !avatar) return;
            if (!string.IsNullOrEmpty(avatar.WalkMotionId))
            {
                if(!TakeControl())return; avatar.SetEditing(true); walkPreview = true; avatar.SpatialWalk(.65f*avatar.transform.lossyScale.y);
                Say("Saved walk preview — Stop ends preview"); return;
            }
            if (avatar.CustomModel && avatar.WalkClip >= 0) { PreviewImportedClip(avatar.WalkClip,true); return; }
            if(!TakeControl())return; avatar.SetEditing(true); avatar.Gesture("Walk"); Say("Included walk preview — Stop ends preview");
        }
        public void ChangeSpeed(float factor)
        {
            if (!Ready() || IsRecording || (factor != .8f && factor != 1.25f)) return;
            StopPlayback(); var motion = editor.Read(targetId).motion;
            if (motion == null || motion.frames.Length < 2) { Say("Save at least two frames first"); return; }
            float duration = Mathf.Clamp(motion.Duration * factor,.1f,RoomMotion.MaximumSeconds);
            if(!RoomMotionEdits.Settings(motion,null,duration,out var edited,out var error)){Say(error);return;}
            if (Save(edited)) Say("Animation duration: " + duration.ToString("0.0") + " seconds");
        }
        void StopPlayback()
        {
            if (!graph.IsValid()) return;
            graph.Destroy(); preview = null;
            if (target) { target.Grab.enabled = true; editor.RestorePose(targetId); }
            if (avatar) avatar.SetEditing(false);
        }
        public void Stop()
        {
            if (stopping) return;
            if (!controlling && !posing && !IsPlaying && !IsRecording) return;
            bool wasPosing=posing;stopping = true;
            try
            {
                if (!resolvingPose && !resolvingRecording && posing && !IsRecording && !HasUnsavedRecording && !HasUnsavedPose && avatar && avatar.PoseRig) SaveCurrentPose();
                if(!resolvingRecording)FinishRecording(); StopPlayback();
                if (avatar && avatar.PoseRig)
                {
                    avatar.PoseRig.PoseChanged -= SavePose; avatar.PoseRig.PoseEdited -= PoseEdited; avatar.PoseRig.SetPosing(false); avatar.SetEditing(false);
                }
                if (target) { foreach (var collider in target.Grab.colliders) collider.enabled = true; target.Grab.enabled = true; if(!target.Grab.isSelected)editor.RestorePose(targetId); }
                posing = false;if(wasPosing&&!HasUnsavedPose)EndPoseSession();
                importedPreview = false; walkPreview = false;
                controlling = false;
                target?.GetComponent<RigidRoomItem>()?.SetAnimationOwner(this,false);
            }
            finally { ownershipLease?.Dispose();ownershipLease=null;stopping = false; }
            Say(saveError ?? "Stopped — saved animation is ready");
        }
        void Update()
        {
            if(editor&&editor.RuntimeGate.Held){if(controlling||IsRecording||IsPlaying||IsPosing||IsImportedPreview)Stop();return;}
            if (walkPreview) { if (!avatar) { Stop(); return; } avatar.SpatialWalk(.65f*avatar.transform.lossyScale.y); }
            if (importedPreview && (!avatar || !avatar.IsImportedClipPlaying)) { Stop(); return; }
            if (IsRecording)
            {
                float time = Mathf.Min(Time.unscaledTime - began,RoomMotion.MaximumSeconds);
                if (time >= nextSample && recording.Count < RoomMotion.MaximumFrames) { recording.Add(Capture(time)); nextSample = time + .1f; }
                if (time >= RoomMotion.MaximumSeconds || recording.Count >= RoomMotion.MaximumFrames) FinishRecording();
            }
            if (IsPlaying)
            {
                float time = Time.unscaledTime - began;
                player.SetTime(time); graph.Evaluate(0);
                if (!preview.loop && time >= preview.Duration) Stop();
            }
        }
        void OnApplicationPause(bool paused) { if (paused) Stop(); }
        void OnApplicationFocus(bool focused) { if (!focused) Stop(); }
        void OnDisable() { if (editor) Stop(); else if (graph.IsValid()) graph.Destroy(); }
        void OnDestroy() { if (editor) { Stop(); editor.Changed -= SelectionChanged; editor.Editing -= Stop; editor.ItemGrabbed -= Grabbed; } if (graph.IsValid()) graph.Destroy(); }
    }
}

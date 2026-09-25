// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using UnityEngine;
using UnityEngine.Playables;

namespace Maestro.Quest.Creation
{
    /// <summary>One explicit authoring/preview owner; clips never autoplay on app launch.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class AnimationWorkshop : MonoBehaviour
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
        public event Action<string> Starting;
        public bool ControlsTarget(string id) => controlling && targetId == id;
        void TakeControl() { Starting?.Invoke(targetId); controlling = true; }
        public bool IsRecording => recording != null;
        public bool IsPlaying => graph.IsValid();
        public bool IsPosing => posing;
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
            selectedFrame = -1; Say(target ? "Selected " + editor.Read(targetId).kind : "Select an object, or choose Pose Maestro");
        }
        void Grabbed(RoomItem item) { if (IsPlaying) Stop(); }
        void Say(string value) { Status = value; Changed?.Invoke(); }
        bool Ready()
        {
            if (!target) { Say("Select an object first"); return false; }
            if (editor.AnyHeld || (avatar && avatar.PoseRig && avatar.PoseRig.IsHolding)) { Say("Release the object or joint first"); return false; }
            return true;
        }
        MotionFrame Capture(float time) => new() { time = time, position = target.transform.localPosition, rotation = target.transform.localRotation.normalized, scale = target.transform.localScale.x, joints = avatar && avatar.PoseRig ? avatar.PoseRig.Capture() : null };
        bool Save(RoomMotion motion, bool savePose = false, JointPose[] joints = null)
        {
            saving = true;
            try
            {
                bool accepted = editor.SaveAnimation(targetId,motion,joints,savePose);
                saveError = accepted ? null : editor.Status;
                if (!accepted) Say(saveError);
                return accepted;
            }
            finally { saving = false; }
        }
        public void TogglePose()
        {
            if (posing) { Stop(); return; }
            if (editor.AnyHeld) { Say("Release the object first"); return; }
            var currentPose = avatar && avatar.PoseRig ? avatar.PoseRig.Capture() : null;
            Stop(); editor.Select(editor.Find("maestro")); SelectionChanged();
            if (editor.DrawingMode) editor.ToggleDrawing();
            if (!avatar || !avatar.PoseRig) { Say("Maestro is still loading"); return; }
            TakeControl();
            avatar.SetEditing(true); avatar.PoseRig.SetManual(true); avatar.PoseRig.SetPosing(true);
            avatar.PoseRig.Apply(currentPose);
            avatar.PoseRig.PoseChanged += SavePose;
            // Keep the body from intercepting grips aimed at a joint handle.
            foreach (var collider in target.Grab.colliders) collider.enabled = false;
            target.Grab.enabled = false; posing = true;
            Say("Grip a teal joint handle to pose; Save frame keeps a keyframe");
        }
        void SavePose()
        {
            if (stopping || !posing || IsRecording || !avatar) return;
            if (Save(editor.Read(targetId).motion,true,avatar.PoseRig.Capture())) Say("Pose saved — use Undo to restore the previous pose");
        }
        public void AddFrame()
        {
            if (!Ready() || IsRecording) return;
            TakeControl();
            StopPlayback();
            var motion = editor.Read(targetId).motion ?? new RoomMotion();
            if (motion.frames.Length >= RoomMotion.MaximumFrames || motion.Duration >= RoomMotion.MaximumSeconds) { Say("This animation is full"); return; }
            var frames = new List<MotionFrame>(motion.frames);
            frames.Add(Capture(frames.Count == 0 ? 0 : motion.Duration + 1)); motion.frames = frames.ToArray();
            if (Save(motion,avatar,avatar ? avatar.PoseRig.Capture() : null)) { selectedFrame = frames.Count - 1; Say("Frame " + frames.Count + " saved; move or pose, then add another"); }
        }
        public void StepFrame(int direction)
        {
            if (!Ready() || IsRecording) return;
            StopPlayback(); var motion = editor.Read(targetId).motion;
            if (motion == null) { Say("Save a frame first"); return; }
            selectedFrame = Mathf.Clamp(selectedFrame + direction,0,motion.frames.Length - 1);
            TakeControl();
            if (avatar) avatar.SetEditing(true);
            Apply(motion.frames[selectedFrame]); Say("Frame " + (selectedFrame+1) + " of " + motion.frames.Length);
        }
        public void ReplaceFrame()
        {
            if (!Ready() || IsRecording || IsPlaying) return;
            var motion = editor.Read(targetId).motion;
            if (motion == null || selectedFrame < 0 || selectedFrame >= motion.frames.Length) { Say("Choose a frame first"); return; }
            motion.frames[selectedFrame] = Capture(motion.frames[selectedFrame].time);
            if (Save(motion)) Say("Frame " + (selectedFrame+1) + " replaced");
        }
        public void DeleteFrame()
        {
            if (!Ready() || IsRecording) return;
            StopPlayback(); var motion = editor.Read(targetId).motion;
            if (motion == null || selectedFrame < 0 || selectedFrame >= motion.frames.Length) { Say("Choose a frame first"); return; }
            var frames = new List<MotionFrame>(motion.frames); frames.RemoveAt(selectedFrame);
            if (frames.Count > 0) { float start = frames[0].time; foreach (var frame in frames) frame.time -= start; }
            motion.frames = frames.ToArray(); if (Save(frames.Count == 0 ? null : motion)) { selectedFrame = -1; Say("Frame removed — Undo restores it"); }
        }
        public void ToggleRecord()
        {
            if (IsRecording) { FinishRecording(); return; }
            if (!Ready()) return;
            StopPlayback(); TakeControl(); recording = new List<MotionFrame> { Capture(0) }; began = Time.unscaledTime; nextSample = .1f;
            Say("Recording a new take — move or pose; tap Record again to save");
        }
        void FinishRecording()
        {
            if (!IsRecording) return;
            var frames = recording; recording = null;
            float end = Mathf.Min(Time.unscaledTime - began,RoomMotion.MaximumSeconds);
            if (target && end > frames[^1].time + .001f && frames.Count < RoomMotion.MaximumFrames) frames.Add(Capture(end));
            if (target && Save(new RoomMotion { frames = frames.ToArray() },posing,posing ? avatar.PoseRig.Capture() : null)) { selectedFrame = -1; Say("Recorded " + end.ToString("0.0") + " seconds — Undo restores the previous take"); }
        }
        public void Play()
        {
            if (!Ready() || IsRecording) return;
            Stop(); preview = editor.Read(targetId).motion;
            if (preview == null || preview.frames.Length < 2) { Say("Save at least two frames to play"); return; }
            TakeControl();
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
            motion.loop = !motion.loop; if (Save(motion)) Say(motion.loop ? "Loop on" : "Loop off");
        }
        void Apply(MotionFrame frame)
        {
            if (!target) return;
            target.transform.SetLocalPositionAndRotation(frame.position,frame.rotation); target.transform.localScale = Vector3.one * frame.scale;
            if (avatar && avatar.PoseRig) { avatar.PoseRig.SetManual(true); avatar.PoseRig.Apply(frame.joints); }
        }
        public void ResetPose()
        {
            if (!Ready() || IsRecording) return;
            Stop(); if (!avatar) { Say("Choose Maestro to restore automatic gestures"); return; }
            Save(editor.Read(targetId).motion,true,null); Say("Maestro follows tutor activity again");
        }
        public void Gesture()
        {
            if (!Ready() || IsRecording) return;
            Stop(); if (!avatar) { Say("Choose Maestro for gestures"); return; }
            var names = new[] { "Greeting","Pointing","Listening","Speaking","Idle" };
            string name = names[gestureIndex++ % names.Length];
            TakeControl(); avatar.SetEditing(true); avatar.Gesture(name); Say(name + " preview — tap Gesture to choose another");
        }
        public void ChangeSpeed(float factor)
        {
            if (!Ready() || IsRecording || (factor != .8f && factor != 1.25f)) return;
            StopPlayback(); var motion = editor.Read(targetId).motion;
            if (motion == null || motion.frames.Length < 2) { Say("Save at least two frames first"); return; }
            float duration = Mathf.Clamp(motion.Duration * factor,.1f,RoomMotion.MaximumSeconds);
            float ratio = duration / motion.Duration;
            foreach (var frame in motion.frames) frame.time *= ratio;
            motion.frames[^1].time = duration;
            if (Save(motion)) Say("Animation duration: " + duration.ToString("0.0") + " seconds");
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
            stopping = true;
            try
            {
                if (posing && !IsRecording && avatar && avatar.PoseRig) Save(editor.Read(targetId).motion,true,avatar.PoseRig.Capture());
                FinishRecording(); StopPlayback();
                if (avatar && avatar.PoseRig)
                {
                    avatar.PoseRig.PoseChanged -= SavePose; avatar.PoseRig.SetPosing(false); avatar.SetEditing(false);
                }
                if (target) { foreach (var collider in target.Grab.colliders) collider.enabled = true; target.Grab.enabled = true; editor.RestorePose(targetId); }
                posing = false;
                controlling = false;
            }
            finally { stopping = false; }
            Say(saveError ?? "Stopped — saved animation is ready");
        }
        void Update()
        {
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

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    [DefaultExecutionOrder(-50)]
    public sealed class RuleWorkshop : MonoBehaviour
    {
        RoomEditor editor;
        RuleStorage storage;
        RuleDocument document = new();
        readonly List<RuleDocument> undo = new(), redo = new();
        int sequenceIndex = -1, stepIndex, bindingIndex = -1;
        bool dirty;
        float saveAt;
        Task<string> saveTask;
        string sourceId = "maestro";
        RuleEventKind trigger;
        RuleCondition condition;
        bool stopOnExit;
        public RoomRules Runtime;
        public string Status { get; private set; } = "Create an action sequence, then add triggers or buttons";
        public event Action Changed, DocumentChanged;
        public RuleDocument Snapshot() => document.Copy();
        public RuleSequence Selected => sequenceIndex >= 0 && sequenceIndex < document.sequences.Length ? document.sequences[sequenceIndex].Copy() : null;
        public int SelectedStepIndex => stepIndex;
        public string Summary
        {
            get
            {
                var sequence = Selected;
                if (sequence == null) return "No action sequence selected";
                var step = sequence.steps[Mathf.Clamp(stepIndex,0,sequence.steps.Length-1)];
                string action = step.action switch { RuleActionKind.RecordedAnimation => "Play recording",RuleActionKind.ThrowRecording => "Play then throw",RuleActionKind.Gesture => step.gesture.ToString(),RuleActionKind.ImportedClip => ClipName(step),RuleActionKind.LibraryMotion => "Saved: " + (editor.Motions.Find(step.motionId)?.name ?? "Choose motion"),RuleActionKind.LookAtUser => "Look at user",RuleActionKind.FollowUser => "Follow user",_ => "Wait" };
                string target = step.action == RuleActionKind.Wait ? "" : " · " + TargetName(step.targetId);
                string source = RuleDocument.IsObjectEvent(trigger) ? " on " + TargetName(sourceId) : "";
                string policy = sequence.interruption == RuleInterruption.QueueLatest ? "Queue latest" : sequence.interruption.ToString();
                return sequence.name + " · Step " + (stepIndex+1) + "/" + sequence.steps.Length + ": " + action + target +
                    "\n" + (step.seconds == 0 ? "Full clip duration" : step.seconds+" seconds") + ((step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion) ? " · Clip loop " + OnOff(step.loop) : "") + " · Repeat " + OnOff(sequence.repeat) + " · " + policy +
                    "\n" + EventName(trigger) + source + " · If " + condition + " · While state " + OnOff(stopOnExit);
            }
        }
        string ClipName(RuleStep step)
        {
            var model = RoomRuleActions.ClipModel(editor.Find(step.targetId));
            return model && editor.Read(step.targetId)?.modelHash == step.clipModelHash && step.clipIndex < model.ClipCount ? model.ClipName(step.clipIndex) : "Choose imported motion";
        }
        void ChooseClip(RuleStep step, bool advance)
        {
            var model = RoomRuleActions.ClipModel(editor.Find(step.targetId));
            string hash = editor.Read(step.targetId)?.modelHash;
            var avatar = editor.Find(step.targetId)?.GetComponent<Maestro.Quest.Avatar.MaestroAvatar>();
            if (!model || !model.Ready || model.ClipCount == 0 || avatar && (avatar.ModelBusy || avatar.ModelHash != hash)) { step.clipModelHash = null; step.clipIndex = 0; return; }
            step.clipIndex = advance && step.clipModelHash == hash ? (step.clipIndex+1)%model.ClipCount : 0;
            step.clipModelHash = hash;
        }
        void ChooseLibraryMotion(RuleStep step,bool advance)
        {
            var model = RoomRuleActions.ClipModel(editor.Find(step.targetId)); var avatar = editor.Find(step.targetId)?.GetComponent<Maestro.Quest.Avatar.MaestroAvatar>();
            if (!model || !model.Ready || avatar && avatar.ModelBusy) { step.motionId = null; return; }
            var choices = editor.Motions.List(rigHash:model.MotionRigHash ?? "");
            int index = Array.FindIndex(choices,x => x.id == step.motionId);
            step.motionId = choices.Length == 0 ? null : choices[advance ? (index+1)%choices.Length : Mathf.Max(0,index)].id;
        }
        public void AssignLibraryMotion(string id)
        {
            var entry = editor.Motions.Find(id);
            if (entry == null) { Say("Choose a motion from the saved library"); return; }
            EditStep(step => { step.action = RuleActionKind.LibraryMotion; step.motionId = id; step.seconds = 0; },"Saved motion assigned");
        }
        static string OnOff(bool value) => value ? "on" : "off";
        static string EventName(RuleEventKind value) => value switch { RuleEventKind.ItemTapped => "On tap", RuleEventKind.ItemGrabbed => "On grab", RuleEventKind.ItemReleased => "On release", _ => "When " + value.ToString().ToLowerInvariant() };
        string TargetName(string id)
        {
            var value = editor.Read(id); if (value == null) return "Missing object";
            return value.IsBuiltIn ? value.kind.ToString() : value.kind + " " + id.Substring(0,4);
        }
        public void Initialize(RoomEditor source, string saveDirectory = null)
        {
            editor = source; storage = new RuleStorage(saveDirectory ?? Path.Combine(Application.persistentDataPath,"room"));
            document = storage.Load(out var message); sequenceIndex = document.sequences.Length > 0 ? 0 : -1;
            if (message != null) Status = message;
        }
        public void Say(string value) { Status = value; Changed?.Invoke(); }
        bool Edit(Action<RuleDocument> action, string message, bool placement = false)
        {
            if (!placement && Runtime && Runtime.AnyButtonHeld) { Say("Release your action buttons before editing rules"); return false; }
            var candidate = document.Copy(); action(candidate);
            if (!candidate.Validate(out var error)) { Say(error); return false; }
            if (JsonUtility.ToJson(candidate) == JsonUtility.ToJson(document)) return true;
            undo.Add(document); if (undo.Count > 32) undo.RemoveAt(0); redo.Clear(); document = candidate;
            Updated(); Say(message); return true;
        }
        void Updated()
        {
            sequenceIndex = document.sequences.Length == 0 ? -1 : Mathf.Clamp(sequenceIndex,0,document.sequences.Length-1);
            stepIndex = Selected == null ? 0 : Mathf.Clamp(stepIndex,0,Selected.steps.Length-1);
            dirty = true; saveAt = Time.unscaledTime + .5f; DocumentChanged?.Invoke(); Changed?.Invoke();
        }
        public void NewSequence()
        {
            if (document.sequences.Length >= 32) { Say("This room has reached its action limit"); return; }
            int number = 1; while (document.sequences.Any(x => x.name == "Action " + number)) number++;
            var sequence = new RuleSequence { id = Guid.NewGuid().ToString("N"), name = "Action " + number, steps = new[] { new RuleStep { action = RuleActionKind.Gesture, seconds = 2.5f } } };
            if (Edit(value => value.sequences = value.sequences.Append(sequence).ToArray(),"Action created — choose its steps and triggers")) { sequenceIndex = document.sequences.Length-1; stepIndex = 0; bindingIndex = -1; Changed?.Invoke(); }
        }
        public void SelectSequence(int direction)
        {
            if (document.sequences.Length == 0) return;
            sequenceIndex = (sequenceIndex + direction + document.sequences.Length) % document.sequences.Length; stepIndex = 0; bindingIndex = -1; Changed?.Invoke();
        }
        public void DeleteSequence()
        {
            var sequence = Selected; if (sequence == null) return;
            Edit(value => { value.sequences = value.sequences.Where(x => x.id != sequence.id).ToArray(); value.bindings = value.bindings.Where(x => x.sequenceId != sequence.id).ToArray(); value.buttons = value.buttons.Where(x => x.sequenceId != sequence.id).ToArray(); },"Action and its triggers removed — Undo restores them");
        }
        void EditStep(Action<RuleStep> action, string message)
        {
            if (Selected == null) { Say("Create an action first"); return; }
            Edit(value => action(value.sequences[sequenceIndex].steps[stepIndex]),message);
        }
        public void CycleAction() => EditStep(step => { step.action = (RuleActionKind)(((int)step.action+1)%Enum.GetValues(typeof(RuleActionKind)).Length); step.seconds = step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ThrowRecording || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion ? 0 : 2.5f; if (step.action == RuleActionKind.Gesture || RuleDocument.IsSpatial(step.action)) step.targetId = "maestro"; if (step.action == RuleActionKind.ThrowRecording) step.loop = false; if (step.action == RuleActionKind.ImportedClip) ChooseClip(step,false); if (step.action == RuleActionKind.LibraryMotion) ChooseLibraryMotion(step,false); },"Action type changed");
        public void UseTarget()
        {
            var id = editor.SelectedId; if (id == null) { Say("Select a room object first"); return; }
            EditStep(step => { if ((step.action == RuleActionKind.Gesture || RuleDocument.IsSpatial(step.action)) && id != "maestro") { Say("This action targets Maestro"); return; } step.targetId = id; if (step.action == RuleActionKind.ImportedClip) ChooseClip(step,false); if (step.action == RuleActionKind.LibraryMotion) ChooseLibraryMotion(step,false); },"Target assigned from your room selection");
        }
        public void Step(int direction) { if (Selected == null) return; stepIndex = (stepIndex + direction + Selected.steps.Length) % Selected.steps.Length; Changed?.Invoke(); }
        public void AddStep()
        {
            if (Selected == null || Selected.steps.Length >= 16) { Say("Choose an action with fewer than 16 steps"); return; }
            if (Edit(value => value.sequences[sequenceIndex].steps = value.sequences[sequenceIndex].steps.Append(new RuleStep { action = RuleActionKind.Wait, seconds = 1 }).ToArray(),"Step added")) { stepIndex = Selected.steps.Length-1; Changed?.Invoke(); }
        }
        public void DeleteStep()
        {
            if (Selected == null || Selected.steps.Length <= 1) { Say("Keep at least one step in the action"); return; }
            Edit(value => value.sequences[sequenceIndex].steps = value.sequences[sequenceIndex].steps.Where((_,i) => i != stepIndex).ToArray(),"Step removed");
        }
        public void CycleTime() => EditStep(step => { if (step.action == RuleActionKind.ThrowRecording) return; float[] times = (step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion) ? new[] { 0f,1,2,3,5,10,20,30 } : new[] { 1f,2,3,5,10,20,30 }; int i = Array.FindIndex(times,x => x > step.seconds); step.seconds = times[i < 0 ? 0 : i]; },"Step duration changed");
        public void CycleGesture() => EditStep(step => { if (step.action == RuleActionKind.ImportedClip) ChooseClip(step,true); else if (step.action == RuleActionKind.LibraryMotion) ChooseLibraryMotion(step,true); else step.gesture = (RuleGesture)(((int)step.gesture+1)%Enum.GetValues(typeof(RuleGesture)).Length); },"Motion changed");
        public void ToggleClipLoop() => EditStep(step => { if (step.action != RuleActionKind.ThrowRecording) step.loop = !step.loop; },"Clip looping changed");
        public void ToggleRepeat() { if (Selected != null) Edit(value => value.sequences[sequenceIndex].repeat = !value.sequences[sequenceIndex].repeat,"Sequence repeat changed"); }
        public void CyclePolicy() { if (Selected != null) Edit(value => value.sequences[sequenceIndex].interruption = (RuleInterruption)(((int)value.sequences[sequenceIndex].interruption+1)%3),"Interruption behaviour changed"); }
        public void CycleEvent() { trigger = (RuleEventKind)(((int)trigger+1)%7); Changed?.Invoke(); }
        public void CycleCondition() { condition = (RuleCondition)(((int)condition+1)%5); Changed?.Invoke(); }
        public void ToggleWhileState() { stopOnExit = !stopOnExit; Changed?.Invoke(); }
        public void UseSource() { if (editor.SelectedId != null) sourceId = editor.SelectedId; Say("VR event source: " + (editor.Read(sourceId)?.kind.ToString() ?? "Missing object")); }
        public void AddBinding()
        {
            var sequence = Selected; if (sequence == null) return;
            if (document.bindings.Length >= 128) { Say("This room has reached its trigger limit"); return; }
            var binding = new RuleBinding { id = Guid.NewGuid().ToString("N"), sequenceId = sequence.id, trigger = trigger, condition = condition, sourceId = sourceId, stopOnExit = stopOnExit };
            Edit(value => value.bindings = value.bindings.Append(binding).ToArray(),"Trigger added to " + sequence.name);
        }
        public void NextBinding()
        {
            if (Selected == null) return;
            var bindings = document.bindings.Where(x => x.sequenceId == Selected.id).ToArray();
            if (bindings.Length == 0) { Say("This action has no triggers yet"); return; }
            bindingIndex = (bindingIndex+1)%bindings.Length; var binding = bindings[bindingIndex];
            trigger = binding.trigger; condition = binding.condition; sourceId = binding.sourceId; stopOnExit = binding.stopOnExit;
            Say("Trigger " + (bindingIndex+1) + " of " + bindings.Length + " — Remove trigger deletes this binding");
        }
        public void RemoveBinding()
        {
            if (Selected == null) return;
            var bindings = document.bindings.Where(x => x.sequenceId == Selected.id).ToArray();
            if (bindings.Length == 0) return;
            string id = bindings[Mathf.Clamp(bindingIndex,0,bindings.Length-1)].id;
            Edit(value => value.bindings = value.bindings.Where(x => x.id != id).ToArray(),"Trigger removed"); bindingIndex = -1;
        }
        public void AddButton(ButtonMount mount)
        {
            if (Selected == null) { Say("Create an action first"); return; }
            if (document.buttons.Length >= 16) { Say("This room has reached its button limit"); return; }
            int slot = document.buttons.Count(x => x.mount == mount);
            if (mount != ButtonMount.Room && slot >= 4) { Say("Keep at most four buttons on each controller"); return; }
            var button = new RuleButtonData { id = Guid.NewGuid().ToString("N"), sequenceId = Selected.id, mount = mount,
                position = mount == ButtonMount.Room ? new Vector3(.3f+slot*.08f,1.25f,.7f) : new Vector3(mount == ButtonMount.LeftController ? -.12f : .12f,.06f + (slot%2)*.08f,.05f+(slot/2)*.08f) };
            Edit(value => value.buttons = value.buttons.Append(button).ToArray(),"Button created — grip it to adjust placement");
        }
        public void RemoveButton()
        {
            if (Selected == null) return;
            var button = document.buttons.LastOrDefault(x => x.sequenceId == Selected.id); if (button == null) return;
            Edit(value => value.buttons = value.buttons.Where(x => x.id != button.id).ToArray(),"Last button for this action removed");
        }
        public bool PlaceButton(string id, Vector3 position, Quaternion rotation) => Edit(value => { var button = value.buttons.FirstOrDefault(x => x.id == id); if (button != null) { button.position = position; button.rotation = rotation.normalized; } },"Button placement saved",true);
        public void RecoverButtons()
        {
            Edit(value => { int i = 0; foreach (var button in value.buttons) if (button.mount == ButtonMount.Room) { button.position = new Vector3(.28f+(i%6)*.09f,1.2f-(i/6)*.09f,.68f); button.rotation = Quaternion.identity; i++; } },"Room buttons brought back within reach");
        }
        public void Undo() { if (undo.Count == 0 || (Runtime && Runtime.AnyButtonHeld)) return; redo.Add(document); document = undo[^1]; undo.RemoveAt(undo.Count-1); Updated(); Say("Rule edit undone"); }
        public void Redo() { if (redo.Count == 0 || (Runtime && Runtime.AnyButtonHeld)) return; undo.Add(document); document = redo[^1]; redo.RemoveAt(redo.Count-1); Updated(); Say("Rule edit redone"); }
        void Update()
        {
            if (saveTask != null && saveTask.IsCompleted) { var error = saveTask.GetAwaiter().GetResult(); saveTask = null; if (error != null) Say(error); }
            if (!dirty || storage == null || saveTask != null || Time.unscaledTime < saveAt) return;
            var snapshot = document.Copy(); dirty = false;
            saveTask = Task.Run(() => { storage.Save(snapshot,out var error); return error; });
        }
        void Flush()
        {
            var pending = saveTask?.GetAwaiter().GetResult(); saveTask = null; if (pending != null) Say(pending);
            if (!dirty || storage == null) return; dirty = false; if (!storage.Save(document,out var error)) Say(error);
        }
        void OnApplicationPause(bool paused) { if (paused) Flush(); }
        void OnApplicationFocus(bool focused) { if (!focused) Flush(); }
        void OnApplicationQuit() => Flush();
        void OnDestroy() => Flush();
    }
}

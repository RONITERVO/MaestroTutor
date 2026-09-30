// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Avatar;
using Maestro.Quest.Programs;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    [DefaultExecutionOrder(-50)]
    public sealed partial class RuleWorkshop : MonoBehaviour
    {
        RoomEditor editor;
        RuleStorage storage;
        public ProgramModuleLibrary Modules {get;private set;}
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
        public int Revision { get; private set; }=1;
        public bool ReadOnly => storage?.ReadOnly ?? false;
        public bool CanUndo => !ReadOnly && undo.Count>0;
        public bool CanRedo => !ReadOnly && redo.Count>0;
        public string Status { get; private set; } = "Create an action sequence, then add triggers or buttons";
        public event Action Changed, DocumentChanged;
        public bool HistoricalMotion(string id) => undo.Concat(redo).Any(x => x.sequences.Any(sequence => sequence.UsesMotion(id)));
        public bool SavedMotion(string id,out bool uncertain,bool force=false) => storage.RetainsMotion(id,out uncertain,force);
        public RuleDocument Snapshot() => document.Copy();
        public RuleSequence Selected => sequenceIndex >= 0 && sequenceIndex < document.sequences.Length ? document.sequences[sequenceIndex].Copy() : null;
        public int SelectedStepIndex => stepIndex;
        internal RoomEditor Editor=>editor;
        internal string TriggerSummary=>EventName(trigger)+(RuleDocument.IsObjectEvent(trigger)?" on "+TargetName(sourceId):"")+" · If "+condition+" · While "+OnOff(stopOnExit);
        internal bool SelectLiteralNode(string nodeId) {
            var steps=Selected?.SimpleSteps();if(steps==null)return false;
            int index=Array.FindIndex(steps,x=>x.id==nodeId);if(index<0)return false;
            if(stepIndex!=index) {stepIndex=index;Changed?.Invoke();}return true;
        }
        // Physical tools edit a detached view of literal action blocks in the program.
        public RuleStep SelectedStep
        {
            get
            {
                var steps = Selected?.SimpleSteps();
                return steps == null || stepIndex < 0 || stepIndex >= steps.Length ? null : steps[stepIndex];
            }
        }
        public string Summary
        {
            get
            {
                var sequence = Selected;
                if (sequence == null) return "No action sequence selected";
                var issue=document.ProgramError(sequence);if(issue!=null)return sequence.name+" — Unavailable: "+issue+" · Repair or delete it in the book";
                var steps=sequence.SimpleSteps();
                if(steps==null || steps.Length==0)return sequence.name+" · Program · Edit its functions in the book";
                var step = steps[Mathf.Clamp(stepIndex,0,steps.Length-1)];
                string action = step.action switch { RuleActionKind.RecordedAnimation => "Play recording",RuleActionKind.ThrowRecording => "Play then throw",RuleActionKind.Gesture => step.gesture.ToString(),RuleActionKind.UpperBodyGesture => "Upper body: "+step.gesture,RuleActionKind.ImportedClip => ClipName(step),RuleActionKind.LibraryMotion => "Saved: " + (editor.Motions.Find(step.motionId)?.name ?? "Choose motion"),RuleActionKind.RecipeAnimation => "Recipe animation",RuleActionKind.LookAtUser => "Look at user",RuleActionKind.FollowUser => "Follow user",RuleActionKind.PhysicsImpulse => "Push "+step.impulse.ToString("F1")+" N·s",RuleActionKind.PhysicsStop => "Stop object motion",RuleActionKind.CreatePrimitive => "Create "+step.shape,RuleActionKind.CreateRecipe => "Create recipe object",RuleActionKind.MoveObject => "Move object",RuleActionKind.ResizeObject => "Resize object",RuleActionKind.PaintObject => "Paint object",RuleActionKind.DeleteObject => "Delete object",_ => "Wait" };
                string target = step.action == RuleActionKind.Wait || RuleDocument.IsCreation(step.action) ? "" : " · " + TargetName(step.targetId);
                string source = RuleDocument.IsObjectEvent(trigger) ? " on " + TargetName(sourceId) : "";
                string policy = sequence.interruption == RuleInterruption.QueueLatest ? "Queue latest" : sequence.interruption.ToString();
                return sequence.name + " · Step " + (stepIndex+1) + "/" + steps.Length + ": " + action + target +
                    "\n" + (RuleDocument.IsCreation(step.action)?"Creates one object; Undo removes it":RuleDocument.IsObjectEdit(step.action)?"Saves one object edit; room Undo restores it":RuleDocument.IsInstant(step.action)?"Applies once; gravity continues":step.seconds == 0 ? "Full clip duration" : step.seconds+" seconds") + ((step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion || step.action == RuleActionKind.RecipeAnimation) ? " · Clip loop " + OnOff(step.loop) : "") + " · Repeat " + OnOff(sequence.repeat) + " · " + policy +
                    (!string.IsNullOrEmpty(step.propId) ? "\nProp: "+TargetName(step.propId)+" · "+step.propHand+" hand · "+step.propRelease+(step.propRelease == PropRelease.Return ? " after motion" : " at "+Mathf.RoundToInt(step.propReleaseAt*100)+"%") : "") +
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
            editor = source; string directory=saveDirectory ?? Path.Combine(Application.persistentDataPath,"room");storage = new RuleStorage(directory);Modules=new ProgramModuleLibrary(directory);
            document = storage.Load(out var message); sequenceIndex = document.sequences.Length > 0 ? 0 : -1;
            if (message != null) Status = message;
        }
        public void Say(string value) { Status = value; Changed?.Invoke(); }
        bool Edit(Action<RuleDocument> action, string message, bool placement = false)
        {
            if (ReadOnly) { Say("Saved rules are unavailable for editing; original files are preserved"); return false; }
            if (!placement && Runtime && Runtime.AnyButtonHeld) { Say("Release your action buttons before editing rules"); return false; }
            var candidate = document.Copy(); action(candidate);
            if (!candidate.ValidateEdit(document,out var error)) { Say(error); return false; }
            if (JsonUtility.ToJson(candidate) == JsonUtility.ToJson(document)) return true;
            undo.Add(document); if (undo.Count > 32) undo.RemoveAt(0); redo.Clear(); document = candidate;
            Updated(); Say(message); return true;
        }
        void Updated()
        {
            sequenceIndex = document.sequences.Length == 0 ? -1 : Mathf.Clamp(sequenceIndex,0,document.sequences.Length-1);
            stepIndex = Mathf.Clamp(stepIndex,0,Mathf.Max(0,(Selected?.SimpleSteps()?.Length??0)-1));
            Revision++; dirty = true; saveAt = Time.unscaledTime + .5f; DocumentChanged?.Invoke(); Changed?.Invoke();
        }
        public void NewSequence()
        {
            if (document.sequences.Length >= 32) { Say("This room has reached its action limit"); return; }
            int number = 1; while (document.sequences.Any(x => x.name == "Action " + number)) number++;
            var sequence = new RuleSequence { id = null, name = "Action " + number, program = BehaviourProgram.FromSteps(new RuleStep { action = RuleActionKind.Gesture, seconds = 2.5f }) };
            Execute(new RuleRequest {action="edit",revision=Revision,edits=new[] {new RuleEdit {kind="save",reference="newAction",sequence=sequence}}},out _,out _);
        }
        public void SelectSequence(int direction)
        {
            if (document.sequences.Length == 0) return;
            sequenceIndex = (sequenceIndex + direction + document.sequences.Length) % document.sequences.Length; stepIndex = 0; bindingIndex = -1; Changed?.Invoke();
        }
        public void DeleteSequence()
        {
            var sequence = Selected; if (sequence == null) return;
            Execute(new RuleRequest {action="edit",revision=Revision,edits=new[] {new RuleEdit {kind="delete",target=sequence.id}}},out _,out _);
        }
        void EditStep(Action<RuleStep> action, string message)
        {
            if (Selected == null) { Say("Create an action first"); return; }
            if(SelectedStep==null) {Say("Edit program blocks in the book");return;}
            Edit(value => {var sequence=value.sequences[sequenceIndex];var steps=sequence.SimpleSteps();action(steps[stepIndex]);sequence.SetSimpleSteps(steps);},message);
        }
        public void CycleAction() => EditStep(step => { step.action = (RuleActionKind)(((int)step.action+1)%Enum.GetValues(typeof(RuleActionKind)).Length); step.seconds = step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ThrowRecording || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion || step.action == RuleActionKind.RecipeAnimation || RuleDocument.IsInstant(step.action) ? 0 : 2.5f; if (step.action == RuleActionKind.Gesture || step.action == RuleActionKind.UpperBodyGesture || RuleDocument.IsSpatial(step.action)) step.targetId = "maestro"; if (step.action == RuleActionKind.ThrowRecording || RuleDocument.IsInstant(step.action)) step.loop = false; if(step.action is RuleActionKind.PhysicsImpulse or RuleActionKind.PhysicsStop or RuleActionKind.PaintObject or RuleActionKind.DeleteObject) {step.targetId=editor.Snapshot().objects.FirstOrDefault(x=>!x.IsBuiltIn&&x.id==editor.SelectedId)?.id??editor.Snapshot().objects.FirstOrDefault(x=>!x.IsBuiltIn)?.id; if(step.targetId==null) {step.action=RuleActionKind.Wait;step.targetId="maestro";step.seconds=1;} else step.impulse=Vector3.up*.6f;} if(step.action==RuleActionKind.MoveObject)step.editPosition=editor.Read(step.targetId)?.position??Vector3.zero; if(step.action==RuleActionKind.ResizeObject)step.editScale=editor.Read(step.targetId)?.scale??1; if(step.action==RuleActionKind.CreateRecipe&&step.creationRecipe==null) {step.creationRecipe=RecipeTemplates.BoxRobot(true);step.creationRecipe.playing=false;} if (step.action == RuleActionKind.ImportedClip) ChooseClip(step,false); if (step.action == RuleActionKind.LibraryMotion) ChooseLibraryMotion(step,false); if (!RuleDocument.CanCarry(step)) step.propId=null; if(step.action==RuleActionKind.UpperBodyGesture && step.gesture==RuleGesture.Walk)step.gesture=RuleGesture.Greeting; },"Action type changed");
        public void UseTarget()
        {
            var id = editor.SelectedId; if (id == null) { Say("Select a room object first"); return; }
            EditStep(step => { if((step.action is RuleActionKind.PhysicsImpulse or RuleActionKind.PhysicsStop or RuleActionKind.PaintObject or RuleActionKind.DeleteObject)&&!RuleDocument.IsId(id)) {Say("Select a user-created object for this action");return;} if ((step.action == RuleActionKind.Gesture || step.action == RuleActionKind.UpperBodyGesture || RuleDocument.IsSpatial(step.action)) && id != "maestro") { Say("This action targets Maestro"); return; } step.targetId = id; if (!RuleDocument.CanCarry(step)) step.propId=null; if(step.action==RuleActionKind.CreateRecipe&&step.creationRecipe==null) {step.creationRecipe=RecipeTemplates.BoxRobot(true);step.creationRecipe.playing=false;} if (step.action == RuleActionKind.ImportedClip) ChooseClip(step,false); if (step.action == RuleActionKind.LibraryMotion) ChooseLibraryMotion(step,false); },"Target assigned from your room selection");
        }
        public void Step(int direction) { var steps=Selected?.SimpleSteps();if(steps==null || steps.Length==0)return;stepIndex=(stepIndex+direction+steps.Length)%steps.Length;Changed?.Invoke(); }
        public void AddStep()
        {
            if (Selected?.SimpleSteps() is not {Length:<16}) { Say("Choose an action with fewer than 16 steps"); return; }
            if (Edit(value => {var sequence=value.sequences[sequenceIndex];sequence.SetSimpleSteps(sequence.SimpleSteps().Append(new RuleStep { action=RuleActionKind.Wait,seconds=1 }).ToArray());},"Step added")) {stepIndex=Selected.SimpleSteps().Length-1;Changed?.Invoke();}
        }
        public void DeleteStep()
        {
            if (Selected?.SimpleSteps() is not {Length:>1}) { Say("Keep at least one step in the action"); return; }
            Edit(value => {var sequence=value.sequences[sequenceIndex];sequence.SetSimpleSteps(sequence.SimpleSteps().Where((_,i)=>i!=stepIndex).ToArray());},"Step removed");
        }
        public void UseProp()
        {
            var selected=SelectedStep; var item=editor.Read(editor.SelectedId);
            var avatar=editor.Find("maestro").GetComponent<MaestroAvatar>();
            if (selected == null || !RuleDocument.CanCarry(selected)) { Say("Choose a Maestro gesture, recording or imported motion step first"); return; }
            if (item == null || item.IsBuiltIn || !avatar || avatar.ModelBusy) { Say("Select a loaded creation to carry, then Use prop"); return; }
            EditStep(step => { step.propId=item.id; step.propAvatarHash=avatar.ModelHash; step.propOffset=Vector3.forward*.12f; step.propRotation=Quaternion.identity; step.propReleaseAt=1; step.propRelease=PropRelease.Return; },"Prop assigned — move it beside the hand and use Fit prop, or Try action");
        }
        public void FitProp()
        {
            var step=SelectedStep; var item=editor.Find(step?.propId); var avatar=editor.Find("maestro").GetComponent<MaestroAvatar>();
            if (step == null || !item || !avatar || avatar.ModelBusy || item.Grab.isSelected) { Say("Assign a prop, position it at the chosen hand, then release your grip"); return; }
            Runtime?.StopAll();
            var hand=avatar.PoseRig ? avatar.PoseRig.Bone(step.propHand == PropHand.Left ? PoseJoint.LeftHand : PoseJoint.RightHand) : null;
            if (!hand) { Say("This avatar has no mapped hand for the prop"); return; }
            var offset=Quaternion.Inverse(hand.rotation)*(item.transform.position-hand.position)/avatar.transform.lossyScale.y;
            if (!float.IsFinite(offset.sqrMagnitude) || offset.sqrMagnitude > 1) { Say("Move the prop closer to the chosen hand before fitting"); return; }
            var rotation=Quaternion.Inverse(hand.rotation)*item.transform.rotation;
            EditStep(value => { value.propOffset=offset; value.propRotation=rotation.normalized; value.propAvatarHash=avatar.ModelHash; },"Current prop position and rotation fitted to this avatar's hand");
        }
        public void CyclePropHand() => EditStep(step => { step.propHand=step.propHand == PropHand.Left ? PropHand.Right : PropHand.Left; },"Prop hand changed — use Fit prop to adjust its hold");
        public void CyclePropRelease() => EditStep(step => { if (!string.IsNullOrEmpty(step.propId)) step.propRelease=(PropRelease)(((int)step.propRelease+1)%3); },"Prop release mode changed");
        public void CyclePropTime() => EditStep(step => { step.propReleaseAt=step.propReleaseAt >= .999f ? .05f : Mathf.Min(1,Mathf.Round((step.propReleaseAt+.05f)*20)/20); },"Prop release time changed in 5% motion increments");
        public void ClearProp() => EditStep(step => step.propId=null,"Prop removed from this step");
        public void CycleTime() => EditStep(step => { if (step.action == RuleActionKind.ThrowRecording || RuleDocument.IsInstant(step.action)) return; float[] times = (step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion || step.action == RuleActionKind.RecipeAnimation) ? new[] { 0f,1,2,3,5,10,20,30 } : new[] { 1f,2,3,5,10,20,30 }; int i = Array.FindIndex(times,x => x > step.seconds); step.seconds = times[i < 0 ? 0 : i]; },"Step duration changed");
        public void CycleGesture() => EditStep(step => { if (step.action == RuleActionKind.ImportedClip) ChooseClip(step,true); else if (step.action == RuleActionKind.LibraryMotion) ChooseLibraryMotion(step,true); else step.gesture = (RuleGesture)(((int)step.gesture+1)%(step.action==RuleActionKind.UpperBodyGesture?5:Enum.GetValues(typeof(RuleGesture)).Length)); },"Motion changed");
        public void ToggleClipLoop() => EditStep(step => { if (step.action != RuleActionKind.ThrowRecording && !RuleDocument.IsInstant(step.action)) step.loop = !step.loop; },"Clip looping changed");
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
            var binding = new RuleBinding { id = null, sequenceId = sequence.id, trigger = trigger, condition = condition, sourceId = sourceId, stopOnExit = stopOnExit };
            Execute(new RuleRequest {action="edit",revision=Revision,edits=new[] {new RuleEdit {kind="bind",binding=binding}}},out _,out _);
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
            Execute(new RuleRequest {action="edit",revision=Revision,edits=new[] {new RuleEdit {kind="button",target=Selected.id,mount=mount}}},out _,out _);
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
        public void Undo() { if (ReadOnly || undo.Count == 0 || (Runtime && Runtime.AnyButtonHeld)) return; redo.Add(document); document = undo[^1]; undo.RemoveAt(undo.Count-1); Updated(); Say("Rule edit undone"); }
        public void Redo() { if (ReadOnly || redo.Count == 0 || (Runtime && Runtime.AnyButtonHeld)) return; undo.Add(document); document = redo[^1]; redo.RemoveAt(redo.Count-1); Updated(); Say("Rule edit redone"); }
        void Update()
        {
            Modules?.Poll();
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
        void OnApplicationPause(bool paused) { if (paused) {Flush();Modules?.Flush();} }
        void OnApplicationFocus(bool focused) { if (!focused) Flush(); }
        void OnApplicationQuit() {Flush();Modules?.Flush();}
        void OnDestroy() {Flush();Modules?.Flush();}
    }
}

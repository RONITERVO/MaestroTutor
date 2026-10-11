// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    public enum RuleActionKind { RecordedAnimation, Gesture, Wait, ThrowRecording, LookAtUser, FollowUser, ImportedClip, LibraryMotion, RecipeAnimation, UpperBodyGesture, PhysicsImpulse, PhysicsStop, CreatePrimitive, CreateRecipe, MoveObject, ResizeObject, PaintObject, DeleteObject }
    public enum PropHand { Left, Right }
    public enum PropRelease { Return, Drop, Throw }
    public enum RuleGesture { Greeting, Pointing, Listening, Speaking, Idle, Walk }
    public enum RuleInterruption { Restart, Ignore, QueueLatest }
    public enum RuleEventKind { Speaking, Listening, Thinking, Idle, ItemTapped, ItemGrabbed, ItemReleased }
    public enum RuleCondition { Any, Speaking, Listening, Thinking, Idle }
    public enum ButtonMount { Room, LeftController, RightController }

    [Serializable] public sealed class RuleStep
    {
        public string id = Guid.NewGuid().ToString("N");
        public RuleActionKind action;
        public string targetId = "maestro";
        public RuleGesture gesture;
        // Zero uses a recording/imported clip duration; other actions use an explicit duration.
        public float seconds;
        public bool loop;
        public Vector3 impulse;
        public string shape="ball", objectName="";
        public Vector3 creationPosition=new(.3f,1.3f,.65f);
        public Color creationColor=Color.white;
        public float creationScale=1;
        public RoomRecipe creationRecipe;
        public Vector3 editPosition;
        public float editScale=1;
        public Color editColor=Color.white;
        public string clipModelHash;
        public int clipIndex;
        public string motionId;
        public string propId,propAvatarHash;
        public PropHand propHand=PropHand.Right;
        public PropRelease propRelease;
        public Vector3 propOffset;
        public Quaternion propRotation=Quaternion.identity;
        public float propReleaseAt=1;
        public RuleStep Copy() {var value=(RuleStep)MemberwiseClone();value.creationRecipe=creationRecipe?.Copy();return value;}
    }
    [Serializable] public sealed class RuleSequence
    {
        public string id, name;
        public RuleInterruption interruption;
        public bool repeat;
        public string program;
        BehaviourProgram compiled;
        string compiledSource,compiledError;
        bool compilationAttempted;
        public BehaviourProgram Compile(out string error) {
            if(compilationAttempted&&compiledSource==program){error=compiledError;return compiled;}
            compilationAttempted=true;compiledSource=program;
            BehaviourProgram.TryParse(program,out compiled,out compiledError);error=compiledError;return compiled;
        }
        public static bool ValidWire(Newtonsoft.Json.Linq.JToken token)
        {
            if(token is not Newtonsoft.Json.Linq.JObject value)return false;
            var keys=new[] {"id","name","interruption","repeat","program"};
            return value.Count==keys.Length && keys.All(value.ContainsKey) &&
                value["id"].Type==Newtonsoft.Json.Linq.JTokenType.String && value["name"].Type==Newtonsoft.Json.Linq.JTokenType.String &&
                value["interruption"].Type==Newtonsoft.Json.Linq.JTokenType.Integer && value["repeat"].Type==Newtonsoft.Json.Linq.JTokenType.Boolean &&
                value["program"].Type==Newtonsoft.Json.Linq.JTokenType.String;
        }
        public IEnumerable<string> Targets()=>Compile(out _)?.Resources??Array.Empty<string>();
        // An unreadable program may still reference any saved motion.
        public bool UsesMotion(string id)=>Compile(out _) is not BehaviourProgram value||value.ReferencesMotion(id);
        public IEnumerable<string> MotionIds()=>Compile(out _)?.ReferencedIds??Array.Empty<string>();
        // A detached view for simple controls, never a second serialized representation.
        public RuleStep[] SimpleSteps()=>Compile(out _)?.SimpleSteps();
        public void SetSimpleSteps(RuleStep[] values) => program=Compile(out _)?.WithSimpleSteps(values) ?? throw new ArgumentException("Invalid program");
        public RuleSequence Copy() => new() { id=id,name=name,interruption=interruption,repeat=repeat,program=program,compiled=compiled,compiledSource=compiledSource,compiledError=compiledError,compilationAttempted=compilationAttempted };
    }
    [Serializable] public sealed class RuleBinding
    {
        public string id, sequenceId, sourceId;
        public RuleEventKind trigger;
        public RuleCondition condition;
        public float cooldown = 1;
        public bool enabled = true;
        public bool stopOnExit;
        public RuleBinding Copy() => (RuleBinding)MemberwiseClone();
    }
    [Serializable] public sealed class RuleButtonData
    {
        public string id, sequenceId;
        public ButtonMount mount;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public RuleButtonData Copy() => (RuleButtonData)MemberwiseClone();
    }
    [Serializable] public sealed class RuleDocument
    {
        public int version = 2;
        public RuleSequence[] sequences = Array.Empty<RuleSequence>();
        public RuleBinding[] bindings = Array.Empty<RuleBinding>();
        public RuleButtonData[] buttons = Array.Empty<RuleButtonData>();
        public RuleDocument Copy() => new() { version = version, sequences = sequences.Select(x => x.Copy()).ToArray(), bindings = bindings.Select(x => x.Copy()).ToArray(), buttons = buttons.Select(x => x.Copy()).ToArray() };
        public static bool IsId(string value) => Guid.TryParseExact(value,"N",out _);
        public static bool IsTarget(string value) => value == "maestro" || value == "book" || IsId(value);
        public static bool IsObjectEvent(RuleEventKind kind) => BehaviourCatalog.Event(kind)?.ObjectEvent == true;
        public static bool CanCarry(RuleStep step) => step.targetId == "maestro" && (step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.Gesture || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion);
        public static IEnumerable<string> Targets(RuleStep step)
        {
            if (step.action != RuleActionKind.Wait && !IsCreation(step.action)) yield return step.targetId;
            if (!string.IsNullOrEmpty(step.propId)) yield return step.propId;
        }
        public static bool IsInstant(RuleActionKind kind) => kind == RuleActionKind.PhysicsImpulse || kind == RuleActionKind.PhysicsStop || IsCreation(kind) || IsObjectEdit(kind);
        public static bool IsObjectEdit(RuleActionKind kind)=>kind is RuleActionKind.MoveObject or RuleActionKind.ResizeObject or RuleActionKind.PaintObject or RuleActionKind.DeleteObject;
        public static bool IsCreation(RuleActionKind kind)=>kind==RuleActionKind.CreatePrimitive||kind==RuleActionKind.CreateRecipe;
        public static bool IsSpatial(RuleActionKind kind) => kind == RuleActionKind.LookAtUser || kind == RuleActionKind.FollowUser;
        public static string Activity(RuleEventKind kind) => BehaviourCatalog.Event(kind)?.Activity;
        public static bool ConditionMatches(RuleCondition condition, string activity) => condition == RuleCondition.Any || condition.ToString().ToLowerInvariant() == activity;

        public static bool ValidStep(RuleStep step,out string error)
        {
            error="Invalid native action";
            if (step == null || !BehaviourCatalog.HasAction(step.action) || !Enum.IsDefined(typeof(RuleGesture),step.gesture) || !float.IsFinite(step.seconds) || step.seconds < 0 || step.seconds > 30) return false;
            if (!string.IsNullOrEmpty(step.propId) && (!IsId(step.propId) || !CanCarry(step) || !Enum.IsDefined(typeof(PropHand),step.propHand) ||
                !Enum.IsDefined(typeof(PropRelease),step.propRelease) || !float.IsFinite(step.propReleaseAt) || step.propReleaseAt < .05f || step.propReleaseAt > 1 ||
                !float.IsFinite(step.propOffset.sqrMagnitude) || step.propOffset.sqrMagnitude > 1 || !MotionFrame.ValidRotation(step.propRotation) ||
                !string.IsNullOrEmpty(step.propAvatarHash) && !ModelLibrary.ValidHash(step.propAvatarHash))) return false;
            if (step.action != RuleActionKind.RecordedAnimation && step.action != RuleActionKind.ThrowRecording && step.action != RuleActionKind.ImportedClip && step.action != RuleActionKind.LibraryMotion && step.action != RuleActionKind.RecipeAnimation && !IsInstant(step.action) && step.seconds < .1f) return false;
            if (IsInstant(step.action) && (step.seconds != 0 || step.loop || !IsCreation(step.action) && !(step.action is RuleActionKind.MoveObject or RuleActionKind.ResizeObject) && !IsId(step.targetId))) return false;
            if (step.action == RuleActionKind.CreatePrimitive && !new[]{"block","ball","cylinder"}.Contains(step.shape)) return false;
            if (step.action == RuleActionKind.CreateRecipe && (step.creationRecipe==null || !step.creationRecipe.Validate(out _))) return false;
            if (IsCreation(step.action) && (step.objectName==null || step.objectName.Length>80 || step.objectName.Any(char.IsControl) || !RoomRecipe.Finite(step.creationPosition) || step.creationPosition.sqrMagnitude>625 || !float.IsFinite(step.creationScale) || step.creationScale<.1f || step.creationScale>4 || !RoomRecipe.ValidColor(step.creationColor))) return false;
            if(step.action==RuleActionKind.MoveObject&&(!RoomRecipe.Finite(step.editPosition)||step.editPosition.sqrMagnitude>625))return false;
            if(step.action==RuleActionKind.ResizeObject&&(!float.IsFinite(step.editScale)||step.editScale<.1f||step.editScale>4))return false;
            if(step.action==RuleActionKind.PaintObject&&!RoomRecipe.ValidColor(step.editColor))return false;
            if (step.action == RuleActionKind.PhysicsImpulse && (!float.IsFinite(step.impulse.sqrMagnitude) || Mathf.Abs(step.impulse.x)>20 || Mathf.Abs(step.impulse.y)>20 || Mathf.Abs(step.impulse.z)>20)) return false;
            if (step.action == RuleActionKind.UpperBodyGesture && step.gesture == RuleGesture.Walk) return false;
            if (!string.IsNullOrEmpty(step.motionId) && !IsId(step.motionId)) return false;
            if (step.clipIndex < 0 || step.clipIndex >= 32 || !string.IsNullOrEmpty(step.clipModelHash) && !ModelLibrary.ValidHash(step.clipModelHash)) return false;
            if (step.action == RuleActionKind.ThrowRecording && (step.loop || step.seconds != 0)) return false;
            if (step.action != RuleActionKind.Wait && !IsTarget(step.targetId)) return false;
            if ((step.action == RuleActionKind.Gesture || step.action == RuleActionKind.UpperBodyGesture || IsSpatial(step.action)) && step.targetId != "maestro") return false;
            error=null;return true;
        }

        public string ProgramError(RuleSequence sequence)
        {
            var compiled=sequence.Compile(out var error);
            if(compiled==null)return string.IsNullOrEmpty(error)?"This program is unavailable":error.Length>2048?error.Substring(0,2048):error;
            if(sequence.repeat&&compiled.Version==3)return "Use a Forever block for a version-3 program";
            foreach(var other in sequences) {
                if(other.id==sequence.id)continue;
                var peer=other.Compile(out _);if(peer==null||other.repeat&&peer.Version==3)continue;
                foreach(var declaration in compiled.CustomEvents)
                    if(peer.CustomEvents.TryGetValue(declaration.Key,out var type)&&type!=declaration.Value)
                        return "Custom event "+declaration.Key+" has a different payload type in another program";
            }
            return null;
        }
        // Existing unavailable definitions may be carried unchanged, never introduced
        // through normal editing. Undo may restore a known preserved definition.
        public bool ValidateEdit(RuleDocument previous,out string error,Func<RuleSequence,bool> preserved=null)
        {
            if(!Validate(out error,true))return false;
            foreach(var sequence in sequences){
                string issue=ProgramError(sequence);if(issue==null)continue;
                var before=previous?.sequences.FirstOrDefault(x=>x.id==sequence.id);
                if(before!=null&&JsonUtility.ToJson(before)==JsonUtility.ToJson(sequence)||preserved?.Invoke(sequence)==true)continue;
                error=sequence.name+": "+issue;return false;
            }
            return true;
        }

        public bool Validate(out string error,bool allowUnavailable=false)
        {
            error = "This rule file has an unsupported version or invalid data.";
            if (version != 2 || sequences == null || bindings == null || buttons == null || sequences.Length > 32 || bindings.Length > 128 || buttons.Length > 16) return false;
            if(sequences.Where(x=>x!=null).Sum(x=>x.program?.Length??0)>128000)return false;
            var eventTypes=new Dictionary<string,ProgramType>();
            var sequenceIds = new HashSet<string>(); var bindingIds = new HashSet<string>(); var buttonIds = new HashSet<string>();
            foreach (var sequence in sequences)
            {
                if (sequence == null || !IsId(sequence.id) || !sequenceIds.Add(sequence.id) || string.IsNullOrWhiteSpace(sequence.name) || sequence.name.Length > 32 || sequence.name.Any(char.IsControl) || !Enum.IsDefined(typeof(RuleInterruption),sequence.interruption) || sequence.program==null || sequence.program.Length>24000) return false;
                if(!allowUnavailable){
                    var compiled=sequence.Compile(out error);
                    if(compiled==null)return false;
                    if(sequence.repeat&&compiled.Version==3){error="Use a Forever block for a version-3 program";return false;}
                    foreach(var declaration in compiled.CustomEvents) {
                        if(eventTypes.TryGetValue(declaration.Key,out var previous)&&previous!=declaration.Value) {error="Custom event payload types must agree across programs";return false;}
                        eventTypes[declaration.Key]=declaration.Value;
                    }
                }
            }
            foreach (var binding in bindings)
            {
                if (binding == null || !IsId(binding.id) || !bindingIds.Add(binding.id) || !sequenceIds.Contains(binding.sequenceId) || BehaviourCatalog.Event(binding.trigger)==null || !Enum.IsDefined(typeof(RuleCondition),binding.condition) || !float.IsFinite(binding.cooldown) || binding.cooldown < .25f || binding.cooldown > 30 || (IsObjectEvent(binding.trigger) && !IsTarget(binding.sourceId))) return false;
            }
            foreach (var button in buttons)
            {
                if (button == null || !IsId(button.id) || !buttonIds.Add(button.id) || !sequenceIds.Contains(button.sequenceId) || !Enum.IsDefined(typeof(ButtonMount),button.mount) || !float.IsFinite(button.position.sqrMagnitude) || button.position.sqrMagnitude > (button.mount == ButtonMount.Room ? 625 : .25f) || !MotionFrame.ValidRotation(button.rotation)) return false;
            }
            if (buttons.Count(x => x.mount == ButtonMount.LeftController) > 4 || buttons.Count(x => x.mount == ButtonMount.RightController) > 4) return false;
            error = null; return true;
        }
    }
}

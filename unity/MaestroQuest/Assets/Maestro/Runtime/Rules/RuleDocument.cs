// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    public enum RuleActionKind { RecordedAnimation, Gesture, Wait, ThrowRecording, LookAtUser, FollowUser, ImportedClip, LibraryMotion }
    public enum PropHand { Left, Right }
    public enum PropRelease { Return, Drop, Throw }
    public enum RuleGesture { Greeting, Pointing, Listening, Speaking, Idle, Walk }
    public enum RuleInterruption { Restart, Ignore, QueueLatest }
    public enum RuleEventKind { Speaking, Listening, Thinking, Idle, ItemTapped, ItemGrabbed, ItemReleased }
    public enum RuleCondition { Any, Speaking, Listening, Thinking, Idle }
    public enum ButtonMount { Room, LeftController, RightController }

    [Serializable] public sealed class RuleStep
    {
        public RuleActionKind action;
        public string targetId = "maestro";
        public RuleGesture gesture;
        // Zero uses a recording/imported clip duration; other actions use an explicit duration.
        public float seconds;
        public bool loop;
        public string clipModelHash;
        public int clipIndex;
        public string motionId;
        public string propId,propAvatarHash;
        public PropHand propHand=PropHand.Right;
        public PropRelease propRelease;
        public Vector3 propOffset;
        public Quaternion propRotation=Quaternion.identity;
        public float propReleaseAt=1;
        public RuleStep Copy() => (RuleStep)MemberwiseClone();
    }
    [Serializable] public sealed class RuleSequence
    {
        public string id, name;
        public RuleInterruption interruption;
        public bool repeat;
        public RuleStep[] steps = Array.Empty<RuleStep>();
        public RuleSequence Copy() => new() { id = id, name = name, interruption = interruption, repeat = repeat, steps = steps.Select(x => x.Copy()).ToArray() };
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
        public int version = 3;
        public RuleSequence[] sequences = Array.Empty<RuleSequence>();
        public RuleBinding[] bindings = Array.Empty<RuleBinding>();
        public RuleButtonData[] buttons = Array.Empty<RuleButtonData>();
        public RuleDocument Copy() => new() { version = version, sequences = sequences.Select(x => x.Copy()).ToArray(), bindings = bindings.Select(x => x.Copy()).ToArray(), buttons = buttons.Select(x => x.Copy()).ToArray() };
        public static bool IsId(string value) => Guid.TryParseExact(value,"N",out _);
        public static bool IsTarget(string value) => value == "maestro" || value == "book" || IsId(value);
        public static bool IsObjectEvent(RuleEventKind kind) => kind >= RuleEventKind.ItemTapped;
        public static bool CanCarry(RuleStep step) => step.targetId == "maestro" && (step.action == RuleActionKind.RecordedAnimation || step.action == RuleActionKind.Gesture || step.action == RuleActionKind.ImportedClip || step.action == RuleActionKind.LibraryMotion);
        public static IEnumerable<string> Targets(RuleStep step)
        {
            if (step.action != RuleActionKind.Wait) yield return step.targetId;
            if (!string.IsNullOrEmpty(step.propId)) yield return step.propId;
        }
        public static bool IsSpatial(RuleActionKind kind) => kind == RuleActionKind.LookAtUser || kind == RuleActionKind.FollowUser;
        public static string Activity(RuleEventKind kind) => kind switch { RuleEventKind.Speaking => "speaking",RuleEventKind.Listening => "listening",RuleEventKind.Thinking => "thinking",RuleEventKind.Idle => "idle",_ => null };
        public static bool ConditionMatches(RuleCondition condition, string activity) => condition == RuleCondition.Any || condition.ToString().ToLowerInvariant() == activity;

        public bool Validate(out string error)
        {
            error = "This rule file has an unsupported version or invalid data.";
            if (version != 1 && version != 2 && version != 3 || sequences == null || bindings == null || buttons == null || sequences.Length > 32 || bindings.Length > 128 || buttons.Length > 16) return false;
            var sequenceIds = new HashSet<string>(); var bindingIds = new HashSet<string>(); var buttonIds = new HashSet<string>();
            foreach (var sequence in sequences)
            {
                if (sequence == null || !IsId(sequence.id) || !sequenceIds.Add(sequence.id) || string.IsNullOrWhiteSpace(sequence.name) || sequence.name.Length > 32 || sequence.name.Any(char.IsControl) || !Enum.IsDefined(typeof(RuleInterruption),sequence.interruption) || sequence.steps == null || sequence.steps.Length < 1 || sequence.steps.Length > 16) return false;
                foreach (var step in sequence.steps)
                {
                    if (step == null || !Enum.IsDefined(typeof(RuleActionKind),step.action) || !Enum.IsDefined(typeof(RuleGesture),step.gesture) || !float.IsFinite(step.seconds) || step.seconds < 0 || step.seconds > 30) return false;
                    if (!string.IsNullOrEmpty(step.propId) && (version < 3 || !IsId(step.propId) || !CanCarry(step) || !Enum.IsDefined(typeof(PropHand),step.propHand) ||
                        !Enum.IsDefined(typeof(PropRelease),step.propRelease) || !float.IsFinite(step.propReleaseAt) || step.propReleaseAt < .05f || step.propReleaseAt > 1 ||
                        !float.IsFinite(step.propOffset.sqrMagnitude) || step.propOffset.sqrMagnitude > 1 || !MotionFrame.ValidRotation(step.propRotation) ||
                        !string.IsNullOrEmpty(step.propAvatarHash) && !ModelLibrary.ValidHash(step.propAvatarHash))) return false;
                    if (step.action != RuleActionKind.RecordedAnimation && step.action != RuleActionKind.ThrowRecording && step.action != RuleActionKind.ImportedClip && step.action != RuleActionKind.LibraryMotion && step.seconds < .1f) return false;
                    if (!string.IsNullOrEmpty(step.motionId) && !IsId(step.motionId) || version == 1 && (step.action == RuleActionKind.LibraryMotion || !string.IsNullOrEmpty(step.motionId))) return false;
                    if (step.clipIndex < 0 || step.clipIndex >= 32 || !string.IsNullOrEmpty(step.clipModelHash) && !ModelLibrary.ValidHash(step.clipModelHash)) return false;
                    if (step.action == RuleActionKind.ThrowRecording && (step.loop || step.seconds != 0)) return false;
                    if (step.action != RuleActionKind.Wait && !IsTarget(step.targetId)) return false;
                    if ((step.action == RuleActionKind.Gesture || IsSpatial(step.action)) && step.targetId != "maestro") return false;
                }
            }
            foreach (var binding in bindings)
            {
                if (binding == null || !IsId(binding.id) || !bindingIds.Add(binding.id) || !sequenceIds.Contains(binding.sequenceId) || !Enum.IsDefined(typeof(RuleEventKind),binding.trigger) || !Enum.IsDefined(typeof(RuleCondition),binding.condition) || !float.IsFinite(binding.cooldown) || binding.cooldown < .25f || binding.cooldown > 30 || (IsObjectEvent(binding.trigger) && !IsTarget(binding.sourceId))) return false;
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

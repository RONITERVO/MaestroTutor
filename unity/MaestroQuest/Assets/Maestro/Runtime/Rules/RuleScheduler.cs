// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;

namespace Maestro.Quest.Rules
{
    public interface IRuleActions
    {
        bool CanRun(RuleStep step, out string error);
        bool Start(string runId, RuleStep step, out float seconds, out string error);
        void Stop(string runId, bool preservePlacement);
    }
    public interface IRuleCompletion { void Complete(string runId); }

    /// <summary>Bounded scheduler; disjoint targets can run concurrently. No user code executes.</summary>
    public sealed class RuleScheduler
    {
        sealed class Run
        {
            public string Id;
            public RuleSequence Sequence;
            public RuleBinding Binding;
            public HashSet<string> Targets;
            public int Step;
            public float Ends;
        }
        sealed class Pending { public string SequenceId; public RuleBinding Binding; }
        readonly IRuleActions actions;
        readonly List<Run> running = new();
        readonly List<Pending> queued = new();
        readonly Dictionary<string,float> firedAt = new();
        RuleDocument document = new();
        string activity;
        bool suspended;
        public int RunningCount => running.Count;
        public int QueuedCount => queued.Count;
        public string LastError { get; private set; }
        public RuleScheduler(IRuleActions actions) { this.actions = actions; }
        public void Configure(RuleDocument value)
        {
            if (!value.Validate(out var error)) throw new ArgumentException(error);
            StopAll(); document = value.Copy(); firedAt.Clear();
        }
        public void Suspend(bool value) { suspended = value; if (value) { StopAll(); activity = null; } }
        public void ForgetActivity()
        {
            activity = null;
            foreach (var run in running.ToArray()) if (run.Binding?.stopOnExit == true && !BindingStillValid(run.Binding)) Stop(run,false);
            queued.RemoveAll(x => !BindingStillValid(x.Binding));
        }
        public void SetActivity(string value, float now)
        {
            if (suspended || (value != "speaking" && value != "listening" && value != "thinking" && value != "idle")) return;
            string previous = activity; activity = value;
            foreach (var run in running.ToArray()) if (run.Binding?.stopOnExit == true && !BindingStillValid(run.Binding)) Stop(run,false);
            queued.RemoveAll(x => !BindingStillValid(x.Binding));
            // The first snapshot establishes a baseline; loading a save is not an event.
            if (previous == null || previous == value) return;
            foreach (RuleEventKind kind in Enum.GetValues(typeof(RuleEventKind))) if (RuleDocument.Activity(kind) == value) Emit(kind,null,now);
        }
        bool BindingStillValid(RuleBinding binding) => binding == null ||
            (RuleDocument.ConditionMatches(binding.condition,activity) && (!binding.stopOnExit || RuleDocument.IsObjectEvent(binding.trigger) || RuleDocument.Activity(binding.trigger) == activity));
        public void Emit(RuleEventKind kind, string sourceId, float now)
        {
            if (suspended || !float.IsFinite(now)) return;
            foreach (var binding in document.bindings)
            {
                if (!binding.enabled || binding.trigger != kind || (RuleDocument.IsObjectEvent(kind) && binding.sourceId != sourceId) || !RuleDocument.ConditionMatches(binding.condition,activity)) continue;
                if (firedAt.TryGetValue(binding.id,out float last) && now-last < binding.cooldown) continue;
                if (Trigger(binding.sequenceId,now,binding)) firedAt[binding.id] = now;
            }
        }
        public bool Trigger(string sequenceId, float now, RuleBinding binding = null)
        {
            LastError = null;
            if (suspended || !float.IsFinite(now)) { LastError = "Rules are paused"; return false; }
            var sequence = document.sequences.FirstOrDefault(x => x.id == sequenceId);
            if (sequence == null) { LastError = "That action sequence no longer exists"; return false; }
            if (!BindingStillValid(binding)) return false;
            foreach (var step in sequence.steps) if (!actions.CanRun(step,out var error)) { LastError = error; return false; }
            var targets = sequence.steps.Where(x => x.action != RuleActionKind.Wait).Select(x => x.targetId).ToHashSet();
            var conflicts = running.Where(x => x.Sequence.id == sequenceId || x.Targets.Overlaps(targets)).ToArray();
            if (conflicts.Length > 0 || running.Count >= 8)
            {
                if (sequence.interruption == RuleInterruption.Ignore) { LastError = "An action already owns this target"; return false; }
                if (sequence.interruption == RuleInterruption.QueueLatest || (conflicts.Length == 0 && running.Count >= 8))
                {
                    queued.RemoveAll(x => x.SequenceId == sequenceId);
                    if (queued.Count >= 8) { LastError = "The action queue is full"; return false; }
                    queued.Add(new Pending { SequenceId = sequenceId, Binding = binding?.Copy() }); return true;
                }
                foreach (var run in conflicts) Stop(run,false);
            }
            var next = new Run { Id = Guid.NewGuid().ToString("N"), Sequence = sequence.Copy(), Binding = binding?.Copy(), Targets = targets };
            running.Add(next); return StartStep(next,now);
        }
        bool StartStep(Run run, float now)
        {
            if (!actions.Start(run.Id,run.Sequence.steps[run.Step],out float seconds,out var error) || !float.IsFinite(seconds) || seconds < .01f || seconds > 30)
            { LastError = error ?? "This action has an invalid duration"; Stop(run,false); return false; }
            run.Ends = now + seconds; return true;
        }
        public void Tick(float now)
        {
            if (suspended || !float.IsFinite(now)) return;
            foreach (var run in running.ToArray())
            {
                if (now < run.Ends) continue;
                if (actions is IRuleCompletion completion) completion.Complete(run.Id); else actions.Stop(run.Id,false);
                run.Step++;
                if (run.Step >= run.Sequence.steps.Length)
                {
                    if (!run.Sequence.repeat) { running.Remove(run); continue; }
                    run.Step = 0;
                }
                // At most one step per run per tick, even after a long frame.
                StartStep(run,now);
            }
            foreach (var pending in queued.ToArray())
            {
                var sequence = document.sequences.FirstOrDefault(x => x.id == pending.SequenceId);
                if (sequence == null || !BindingStillValid(pending.Binding)) { queued.Remove(pending); continue; }
                var targets = sequence.steps.Where(x => x.action != RuleActionKind.Wait).Select(x => x.targetId).ToHashSet();
                if (running.Count >= 8 || running.Any(x => x.Sequence.id == sequence.id || x.Targets.Overlaps(targets))) continue;
                queued.Remove(pending); Trigger(sequence.id,now,pending.Binding);
            }
        }
        public void StopTarget(string targetId, bool preservePlacement)
        {
            foreach (var run in running.Where(x => x.Targets.Contains(targetId)).ToArray()) Stop(run,preservePlacement && run.Sequence.steps[run.Step].targetId == targetId);
            queued.RemoveAll(x => document.sequences.FirstOrDefault(y => y.id == x.SequenceId)?.steps.Any(y => y.targetId == targetId) == true);
        }
        void Stop(Run run, bool preservePlacement) { actions.Stop(run.Id,preservePlacement); running.Remove(run); }
        public void StopAll() { foreach (var run in running.ToArray()) Stop(run,false); queued.Clear(); }
    }
}

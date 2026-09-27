// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Programs;

namespace Maestro.Quest.Rules
{
    public interface IRuleActions
    {
        bool CanRun(CapabilityCall step, out string error);
        bool Start(string runId, CapabilityCall step, out float seconds, out string error);
        void Stop(string runId, bool preservePlacement);
    }
    public interface IRuleResults { Newtonsoft.Json.Linq.JObject TakeResult(string runId); }
    public interface IRuleCompletion { bool Complete(string runId,out string error); }
    public enum RuleActionState { Preparing, Ready, Failed }
    public interface IRuleReadiness { RuleActionState State(string runId,out string error); }

    /// <summary>Bounded scheduler; disjoint targets can run concurrently. No user code executes.</summary>
    public sealed partial class RuleScheduler : IProgramFacts
    {
        sealed class Run
        {
            public string Id;
            public RuleSequence Sequence;
            public RuleBinding Binding;
            public HashSet<string> Targets;
            public BehaviourCatalog.Claim[] Claims=Array.Empty<BehaviourCatalog.Claim>();
            public float Ends, Duration, PrepareDeadline;
            public bool Preparing,Computing;
            public int EventDepth,WaitSerial;
            public bool Reactive=>Sequence.Compile(out _).Version==3;
            public ProgramMachine Machine;
            public CapabilityCall Active;
            public Newtonsoft.Json.Linq.JObject Invocation,Output;
        }
        sealed class Pending { public string SequenceId; public RuleBinding Binding; }
        readonly IRuleActions actions;
        readonly List<Run> running = new();
        readonly List<Pending> queued = new();
        readonly Dictionary<string,float> firedAt = new();
        sealed class FinishedRun {public RuleOutcome Outcome;public Newtonsoft.Json.Linq.JObject Invocation,Output;public string[] Resources;}
        readonly Queue<FinishedRun> outcomes=new();
        public RuleOutcome[] Outcomes=>outcomes.Where(x=>x.Invocation==null).Select(x=>x.Outcome).ToArray();
        public const int MaximumConcurrent=8,MaximumOutcomes=16;
        public bool HasCapacity=>running.Count<MaximumConcurrent;
        RuleDocument document = new();
        readonly Dictionary<string,string> unavailable=new();
        string activity;
        bool suspended;
        public int RunningCount => running.Count;
        public int PreparingCount => running.Count(x => x.Preparing);
        public int QueuedCount => queued.Count;
        public bool TargetsBusy(IEnumerable<string> targets) {var ids=targets.ToHashSet();return running.Any(x=>x.Targets.Overlaps(ids));}
        static BehaviourCatalog.Claim[] Whole(IEnumerable<string> targets)=>targets.Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();
        static bool Conflicts(Run run,IEnumerable<BehaviourCatalog.Claim> claims)=>claims.Any(claim=>run.Claims.Any(claim.Conflicts));
        public bool ActionBusy(CapabilityCall call)=>running.Any(run=>Conflicts(run,call.Claims));
        public string LastError { get; private set; }
        public RuleRunView[] ObserveRuns() => running.Where(x=>x.Invocation==null).Select(x=>new RuleRunView {id=x.Id,sequenceId=x.Sequence.id,preparing=x.Preparing,nodeId=x.Machine?.NodeId,functionName=x.Machine?.Function,status=x.Machine?.Wait!=null?x.Machine.Wait.Event==null?"Waiting for timer":"Waiting for "+x.Machine.Wait.Event:x.Computing?"Evaluating":x.Preparing?"Loading":"Running",
            waiting=x.Machine?.Wait!=null,waitEvent=x.Machine?.Wait?.Event,waitSeconds=x.Machine?.Wait!=null&&x.Machine.Wait.Seconds>0?Math.Max(0,x.Ends-lastNow):0,
            state=x.Machine?.State.Select(v=>new ProgramVariableView {name=v.Key,type=v.Value.Type.ToString().ToLowerInvariant(),value=Convert.ToString(v.Value.Value,System.Globalization.CultureInfo.InvariantCulture)}).ToArray()??Array.Empty<ProgramVariableView>(),
            locals=x.Machine?.Locals.Select(v=>new ProgramVariableView {name=v.Key,type=v.Value.Type.ToString().ToLowerInvariant(),value=Convert.ToString(v.Value.Value,System.Globalization.CultureInfo.InvariantCulture)}).ToArray()??Array.Empty<ProgramVariableView>()}).ToArray();
        public InvocationReceipts Receipts { get; }
        public RuleScheduler(IRuleActions actions,InvocationReceipts receipts=null) { this.actions = actions; Receipts=receipts; }
        public bool TryRead(string name,out ProgramValue value) {
            if(BehaviourCatalog.TryRead(name,new BehaviourCatalog.FactContext(activity),out value))return true;
            if(actions is IProgramFacts source)return source.TryRead(name,out value);value=default;return false;
        }
        public void Configure(RuleDocument value)
        {
            if (!value.Validate(out var error,true)) throw new ArgumentException(error);
            StopAll(); document = value.Copy(); firedAt.Clear();unavailable.Clear();
            foreach(var sequence in document.sequences){var issue=document.ProgramError(sequence);if(issue!=null)unavailable.Add(sequence.id,issue);}
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
            var definition=BehaviourCatalog.Event(kind);if(definition!=null)EnqueueEvent(definition.Id,sourceId??"",new ProgramValue(definition.ObjectEvent?sourceId:definition.Activity),now,0,out _);
            foreach (var binding in document.bindings)
            {
                if (unavailable.ContainsKey(binding.sequenceId) || !binding.enabled || binding.trigger != kind || (RuleDocument.IsObjectEvent(kind) && binding.sourceId != sourceId) || !RuleDocument.ConditionMatches(binding.condition,activity)) continue;
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
            if (unavailable.TryGetValue(sequenceId,out var issue)){LastError=issue;return false;}
            if (!BindingStillValid(binding)) return false;
            var targets = (sequence.Compile(out _).Version==3?Array.Empty<string>():sequence.Targets()).ToHashSet();
            var conflicts = running.Where(x => x.Sequence.id == sequenceId || Conflicts(x,Whole(targets))).ToArray();
            if (conflicts.Length > 0 || !HasCapacity)
            {
                if (sequence.interruption == RuleInterruption.Ignore) { LastError = "An action already owns this target"; return false; }
                if (sequence.interruption == RuleInterruption.QueueLatest || (conflicts.Length == 0 && !HasCapacity))
                {
                    queued.RemoveAll(x => x.SequenceId == sequenceId);
                    if (queued.Count >= 8) { LastError = "The action queue is full"; return false; }
                    queued.Add(new Pending { SequenceId = sequenceId, Binding = binding?.Copy() }); return true;
                }
                foreach (var run in conflicts) Stop(run,false);
            }
            var next = new Run { Id = Guid.NewGuid().ToString("N"), Sequence = sequence.Copy(), Binding = binding?.Copy(), Targets = targets, Claims=Whole(targets) };
            next.Machine=new ProgramMachine(sequence.Compile(out _),this);
            running.Add(next); return StartStep(next,now);
        }
        bool StartStep(Run run, float now)
        {
            run.Computing=false;
            {
                var yielded=run.Machine.Advance(out run.Active);
                if(yielded==ProgramYield.Waiting) {WaitForEvent(run,now);return true;}
                if(yielded==ProgramYield.Signal) {
                    var signal=run.Machine.Signal;
                    if(!EnqueueEvent(signal.Event,"",signal.Value,now,run.EventDepth+1,out var eventError)) {LastError=eventError;Stop(run,false,"failed",eventError);return false;}
                    run.Computing=true;return true;
                }
                if(yielded==ProgramYield.Yield) {run.Computing=true;return true;}
                if(yielded==ProgramYield.Failed) {LastError=run.Machine.Error;Stop(run,false,"failed",LastError);return false;}
                if(yielded==ProgramYield.Completed) {
                    if(run.Sequence.repeat) {run.Machine=new ProgramMachine(run.Sequence.Compile(out _),this);run.Computing=true;}
                    else Finish(run,"completed","Program completed");
                    return true;
                }
            }
            if(run.Reactive) {
                var targets=run.Active.Resources.ToHashSet();
                var claims=run.Active.Claims;
                if(running.Any(x=>x!=run&&Conflicts(x,claims))) {LastError="An action already owns this target";Stop(run,false,"failed",LastError);return false;}
                run.Targets=targets;run.Claims=claims;
            }
            if(!actions.CanRun(run.Active,out var unavailable)) {LastError=unavailable;Stop(run,false,"failed",LastError);return false;}
            bool instant=run.Active.Instant;
            if (!actions.Start(run.Id,run.Active,out float seconds,out var error) || !float.IsFinite(seconds) || (instant?seconds!=0:seconds<.01f) || seconds > 30)
            { LastError = error ?? "This action has an invalid duration"; Stop(run,false,"failed",LastError); return false; }
            run.Duration = seconds; run.PrepareDeadline = now+30;
            var state = actions is IRuleReadiness readiness ? readiness.State(run.Id,out error) : RuleActionState.Ready;
            if (state == RuleActionState.Failed) { LastError = error ?? "This action could not load"; Stop(run,false,"failed",LastError); return false; }
            if(instant) {
                if(state!=RuleActionState.Ready) {LastError="An instant action cannot defer its effect";Stop(run,false,"failed",LastError);return false;}
                if(actions is IRuleCompletion completion) {
                    if(!completion.Complete(run.Id,out error)) {LastError=error??"This action could not finish";Stop(run,false,"failed",LastError);return false;}
                } else actions.Stop(run.Id,false);
                if(!CompleteResult(run,out error)) {LastError=error;Stop(run,false,"failed",error);return false;}
                run.Active=null;
                if(run.Reactive) {run.Targets.Clear();run.Claims=Array.Empty<BehaviourCatalog.Claim>();}
                // An instant effect is already done. Don't reset activation work or
                // causal depth, and don't execute a second effect in this frame.
                if(run.Invocation!=null) {
                    if(run.Machine.Advance(out _)!=ProgramYield.Completed) {LastError="Invalid one-off completion";Stop(run,false,"failed",LastError);return false;}
                    Finish(run,"completed","Action completed");
                } else run.Computing=true;
                return true;
            }
            run.Preparing = state == RuleActionState.Preparing;
            run.Ends = now + seconds; return true;
        }
        bool CompleteResult(Run run,out string error) {
            var result=actions is IRuleResults source?source.TakeResult(run.Id):new Newtonsoft.Json.Linq.JObject();
            if(!run.Machine.CompleteAction(result,out error))return false;
            if(result.Count>0)run.Output=(Newtonsoft.Json.Linq.JObject)result.DeepClone();
            return true;
        }
        public void Tick(float now)
        {
            if (suspended || !float.IsFinite(now)) return;
            lastNow=now;DispatchEvents(now);
            foreach (var run in running.ToArray())
            {
                if(run.Machine.Wait!=null) {
                    if(run.Machine.Wait.Seconds==0||now<run.Ends)continue;
                    Unsubscribe(run);run.Machine.Resume(false);run.EventDepth=0;StartStep(run,now);continue;
                }
                if(run.Computing) {StartStep(run,now);continue;}
                if (run.Preparing)
                {
                    if (now >= run.PrepareDeadline) { LastError = "The action took too long to load; try again"; Stop(run,false,"failed",LastError); continue; }
                    var state = ((IRuleReadiness)actions).State(run.Id,out var error);
                    if (state == RuleActionState.Failed)
                    { LastError = error ?? "The action took too long to load; try again"; Stop(run,false,"failed",LastError); continue; }
                    if (state == RuleActionState.Ready) { run.Preparing = false; run.Ends = now+run.Duration; }
                    continue; // Loading time never consumes any of the requested playback.
                }
                if (actions is IRuleReadiness active && active.State(run.Id,out var activeError) == RuleActionState.Failed)
                { LastError=activeError ?? "This action stopped because its target changed"; Stop(run,false,"failed",LastError); continue; }
                if (now < run.Ends) continue;
                if (actions is IRuleCompletion completion)
                { if (!completion.Complete(run.Id,out var completionError)) { LastError=completionError ?? "This action could not finish"; Stop(run,false,"failed",LastError); continue; } }
                else actions.Stop(run.Id,false);
                if(!CompleteResult(run,out var resultError)) {LastError=resultError;Stop(run,false,"failed",resultError);continue;}
                bool timed=run.Active!=null&&!run.Active.Instant;
                run.Active=null;if(run.Reactive) {run.Targets.Clear();run.Claims=Array.Empty<BehaviourCatalog.Claim>();if(timed) {run.Machine.BeginActivation();run.EventDepth=0;}}
                // At most one step per run per tick, even after a long frame.
                StartStep(run,now);
            }
            foreach (var pending in queued.ToArray())
            {
                var sequence = document.sequences.FirstOrDefault(x => x.id == pending.SequenceId);
                if (sequence == null || !BindingStillValid(pending.Binding)) { queued.Remove(pending); continue; }
                var targets = (sequence.Compile(out _).Version==3?Array.Empty<string>():sequence.Targets()).ToHashSet();
                if (!HasCapacity || running.Any(x => x.Sequence.id == sequence.id || Conflicts(x,Whole(targets)))) continue;
                queued.Remove(pending); Trigger(sequence.id,now,pending.Binding);
            }
        }
        public void StopConflicting(RuleStep step,bool preservePlacement)
        {
            var claims=BehaviourCatalog.Claims(step);
            foreach(var run in running.Where(x=>Conflicts(x,claims)).ToArray())Stop(run,preservePlacement);
            // Pending v2 sequences reserve whole objects. Pending v3 programs
            // own nothing until their next invocation and are rechecked then.
            queued.RemoveAll(x=>document.sequences.FirstOrDefault(y=>y.id==x.SequenceId) is RuleSequence sequence &&
                sequence.Compile(out _).Version!=3 && Whole(sequence.Targets()).Any(claim=>claims.Any(claim.Conflicts)));
        }
        public void StopTarget(string targetId, bool preservePlacement)
        {
            foreach (var run in running.Where(x => x.Targets.Contains(targetId)).ToArray()) Stop(run,preservePlacement && run.Active!=null && run.Active.Resources.Contains(targetId));
            queued.RemoveAll(x => document.sequences.FirstOrDefault(y => y.id == x.SequenceId)?.Targets().Contains(targetId) == true);
        }
        void Finish(Run run,string phase,string status) {
            if(run.Invocation!=null) {if(phase=="completed")status="Action completed";else if(phase=="cancelled"&&status=="Behaviour stopped")status="Action cancelled";}
            Unsubscribe(run);running.Remove(run);outcomes.Enqueue(new FinishedRun {Outcome=new RuleOutcome {id=run.Id,sequenceId=run.Sequence.id,phase=phase,nodeId=run.Machine?.NodeId,status=status??phase},Invocation=run.Invocation,Output=run.Output,Resources=run.Targets.ToArray()});
            while(outcomes.Count>MaximumOutcomes)outcomes.Dequeue();
            if(run.Invocation!=null)Receipts?.Update(LiveInvocation(run.Id));
        }
        void Stop(Run run,bool preservePlacement,string phase="cancelled",string status="Behaviour stopped") {actions.Stop(run.Id,preservePlacement);Finish(run,phase,status);}
        public bool StopSequence(string id)
        {
            if(!document.sequences.Any(x=>x.id==id))return false;
            foreach(var run in running.Where(x=>x.Sequence.id==id).ToArray())Stop(run,false);
            queued.RemoveAll(x=>x.SequenceId==id);return true;
        }
        public void StopAll() { foreach (var run in running.ToArray()) Stop(run,false); queued.Clear();eventQueue.Clear(); }
    }
}

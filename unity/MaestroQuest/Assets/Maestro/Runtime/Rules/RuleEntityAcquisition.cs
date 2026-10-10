// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Programs;
namespace Maestro.Quest.Rules
{
    internal interface IRuleEntityAcquisition
    {
        RuleEntityDemand Acquire(CapabilityCall call,Func<bool> admission);
    }
    internal abstract class RuleEntityDemand : IDisposable
    {
        internal abstract RuleActionState State(out string error);
        public abstract void Dispose();
    }
    public sealed partial class RoomRuleActions : IRuleEntityAcquisition
    {
        internal bool CanAdmit(CapabilityCall call,out bool loading,out string error)
        {
            loading=false;
            var targets=call.Definition.Module.NativeEntities(call.Arguments).Distinct(StringComparer.Ordinal).ToArray();
            if(targets.Length==0||call.Definition.Module.Domain!="room"||!context.Editor)return CanRun(call,out error);
            if(!context.Editor.CanAcquireNativeEntities(targets,out loading,out error))return false;
            // Activation is itself admitted, not a promise that the later native
            // preflight will succeed. No geometry, file I/O or leases in this check.
            return loading||CanRun(call,out error);
        }
        RuleEntityDemand IRuleEntityAcquisition.Acquire(CapabilityCall call,Func<bool> admission)
        {
            var targets=call.Definition.Module.NativeEntities(call.Arguments).Distinct(StringComparer.Ordinal).ToArray();
            return targets.Length==0?null:new NativeDemand(context,targets,admission);
        }
        sealed class NativeDemand : RuleEntityDemand
        {
            readonly CancellationTokenSource cancellation=new();
            readonly Task<string> pending;
            readonly Func<bool> inputCurrent;
            bool disposed;
            internal NativeDemand(CapabilityContext context,string[] targets,Func<bool> admission)
            {
                inputCurrent=context.Editor?context.Editor.CaptureNativeActionInput():null;
                pending=context.Editor?context.Editor.AcquireNativeEntities(targets,admission,cancellation.Token):Task.FromResult("The room is unavailable");
            }
            internal override RuleActionState State(out string error)
            {
                error=null;if(!pending.IsCompleted)return RuleActionState.Preparing;
                if(pending.IsCanceled||pending.IsFaulted){error="Required objects could not be loaded";_ = pending.Exception;return RuleActionState.Failed;}
                error=pending.Result;
                if(error==null&&inputCurrent?.Invoke()!=true)error="Required objects changed before the action could start; inspect their latest state and retry";
                return error==null?RuleActionState.Ready:RuleActionState.Failed;
            }
            public override void Dispose()
            {
                if(disposed)return;disposed=true;
                if(pending.IsCompleted)cancellation.Dispose();
                else {cancellation.Cancel();_ = pending.ContinueWith(task=>{_ = task.Exception;cancellation.Dispose();},TaskScheduler.Default);}
            }
        }
    }
}

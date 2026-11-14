// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
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
            var targets=call.Definition.Module.NativeEntities(context,call.Arguments).Distinct(StringComparer.Ordinal).ToArray();
            if(targets.Length==0||call.Definition.Module.Domain!="room"||!context.Editor)return CanRun(call,out error);
            if(!call.Definition.Module.CanPrepare(context,call.Arguments,out error)||!context.Editor.CanAcquireNativeEntities(targets,out loading,out error))return false;
            // Activation is itself admitted, not a promise that the later native
            // preflight will succeed. No geometry, file I/O or leases in this check.
            return loading||CanRun(call,out error);
        }
        RuleEntityDemand IRuleEntityAcquisition.Acquire(CapabilityCall call,Func<bool> admission)
        {
            var targets=call.Definition.Module.NativeEntities(context,call.Arguments).Distinct(StringComparer.Ordinal).ToArray();
            return targets.Length==0?null:new NativeDemand(context,call,targets,admission);
        }
        sealed class NativeDemand : RuleEntityDemand
        {
            readonly RoomNativeDemand demand;
            internal NativeDemand(CapabilityContext context,CapabilityCall call,string[] targets,Func<bool> admission)
            {
                if(context.Editor)demand=context.Editor.DemandNativeEntities(targets,
                    ()=>call.Definition.Module.NativeEntities(context,call.Arguments),
                    ()=>admission()&&call.Definition.Module.CanPrepare(context,call.Arguments,out _));
            }
            internal override RuleActionState State(out string error)
            {
                error=null;if(demand!=null&&!demand.Preparation.IsCompleted)return RuleActionState.Preparing;
                error=demand?.Error??(demand==null?"The room is unavailable":null);
                return error==null?RuleActionState.Ready:RuleActionState.Failed;
            }
            public override void Dispose()=>demand?.Dispose();
        }
    }
}

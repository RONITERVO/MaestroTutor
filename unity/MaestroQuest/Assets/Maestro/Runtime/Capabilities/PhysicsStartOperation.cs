// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    internal sealed class PhysicsStartOperation:CapabilityOperation
    {
        readonly CapabilityContext context;readonly RoomEditor editor;readonly RoomPhysicsWorld world;
        readonly CancellationTokenSource cancellation=new();readonly Func<bool> inputCurrent;
        readonly Task<string> pending;readonly int intent;readonly string[] targets;bool stopped,completed;JObject result;
        internal PhysicsStartOperation(CapabilityContext context,string[] targets,int intent,Func<bool> startCurrent=null)
        {
            this.context=context;this.targets=(string[])targets.Clone();editor=context.Editor;world=editor.PhysicsWorld;this.intent=intent;
            var captured=editor.CaptureNativeActionInput(targets);
            inputCurrent=()=>captured()&&(startCurrent?.Invoke()??true);
            pending=editor.AcquireNativeEntities(targets,Current,cancellation.Token);
        }
        bool Current()=>!stopped&&editor&&context.Editor==editor&&editor.PhysicsWorld==world&&world&&world.PreparedSimulationCurrent(intent)&&inputCurrent();
        public override float Seconds=>0;
        public override RuleActionState State(out string error)
        {
            error=null;
            if(!Current()){error="Physics start was cancelled or the room changed; inspect it and request Start again";return RuleActionState.Failed;}
            if(!pending.IsCompleted)return RuleActionState.Preparing;
            if(pending.IsFaulted||pending.IsCanceled){_ = pending.Exception;error="Required physics objects could not be prepared";return RuleActionState.Failed;}
            error=pending.Result;return error==null?RuleActionState.Ready:RuleActionState.Failed;
        }
        public override bool Complete(out string error)
        {
            if(State(out error)!=RuleActionState.Ready)return false;
            if(!editor.CanAcquireNativeEntities(targets,out var loading,out error))return false;
            if(loading){error="Required native physics objects changed before Start; inspect the room and retry";return false;}
            completed=world.CompletePreparedSimulation(intent,out result,out error);return completed;
        }
        public override JObject Result=>result==null?new JObject():(JObject)result.DeepClone();
        public override void Stop(bool preservePlacement)
        {
            if(stopped)return;stopped=true;
            if(!completed&&world)world.CancelPreparedSimulation(intent);
            if(pending.IsCompleted)cancellation.Dispose();
            else{cancellation.Cancel();_ = pending.ContinueWith(task=>{_ = task.Exception;cancellation.Dispose();},TaskScheduler.Default);}
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Persistence
{
    /// <summary>Persistent one-off maintenance runner. Uses the ordinary interpreter, handlers and
    /// receipts, but room review/retention holds cannot disable workspace maintenance.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class WorkspaceRuntime:MonoBehaviour
    {
        RoomRuleActions actions;bool paused,focused=true;
        internal string SelectedInvocation {get;set;}
        public RuleScheduler Scheduler {get;private set;}
        internal void Initialize(WorkspaceHost host,string directory)
        {actions=RoomRuleActions.ForWorkspace(host);Scheduler=new RuleScheduler(actions,new InvocationReceipts(directory));Scheduler.Configure(new RuleDocument());Suspend();}
        public bool CanRun(CapabilityCall call,out string error)
        {
            error="Workspace actions are paused";if(Scheduler==null||paused||!focused||!isActiveAndEnabled)return false;
            return actions.CanRun(call,out error);
        }
        public bool TryRead(string id,int version,JObject arguments,out ProgramValue value)
        {value=default;return Scheduler!=null&&!paused&&focused&&isActiveAndEnabled&&actions.TryRead(id,version,arguments,out value);}
        void Update(){if(Scheduler==null||paused||!focused)return;Scheduler.Tick(Time.unscaledTime);actions.Tick();}
        void Suspend()=>Scheduler?.Suspend(paused||!focused||!isActiveAndEnabled);
        void OnApplicationPause(bool value){paused=value;Suspend();}
        void OnApplicationFocus(bool value){focused=value;Suspend();}
        void OnEnable()=>Suspend();
        void OnDisable()=>Suspend();
        void OnDestroy()=>Scheduler?.StopAll();
    }
}

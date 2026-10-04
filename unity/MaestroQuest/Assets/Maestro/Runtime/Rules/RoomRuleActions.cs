// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Rules
{
    /// <summary>One lifecycle host for every native module. No capability IDs, enum dispatch or argument fields.</summary>
    public sealed class RoomRuleActions : IRuleActions, IRuleCompletion, IRuleReadiness, IRuleInterruptionInfo, IRuleGrabPolicy, IRuleOwnershipSource, IProgramFacts, IProgramFactQueries, IProgramEventWorld, IProgramPhysicsWorld, IProgramClockWorld, IProgramAnchorWorld, IRuleResults
    {
        readonly CapabilityContext context;
        readonly Dictionary<string,CapabilityOperation> operations=new();
        readonly Dictionary<string,JObject> results=new();
        readonly Dictionary<string,RoomOwnership.Lease> owned=new();
        readonly Dictionary<string,string> interrupted=new();
        public RoomOwnership Ownership {get;}
        public IProgramClock Clock {get;private set;}=SystemProgramClock.Instance;
        readonly string domain;
        public RoomRuleActions(RoomEditor editor,AnimationWorkshop workshop,IProgramClock clock=null):this(new CapabilityContext(editor,workshop)){Clock=clock??SystemProgramClock.Instance;}
        internal static RoomRuleActions ForWorkspace(Maestro.Quest.Persistence.WorkspaceHost host)=>new(new CapabilityContext(host),"workspace");
        RoomRuleActions(CapabilityContext context,string domain=null){this.context=context;this.domain=domain;Ownership=domain=="workspace"?new RoomOwnership():context.Editor?context.Editor.Ownership:new RoomOwnership();}
        public static ImportedModel ClipModel(RoomItem item)=>AnimationTargets.ClipModel(item);
        public bool TryPosition(string id,out UnityEngine.Vector3 position) {
            position=default;var item=context.Editor?context.Editor.Find(id):null;
            if(!item||!item.isActiveAndEnabled)return false;position=item.transform.position;
            return float.IsFinite(position.x)&&float.IsFinite(position.y)&&float.IsFinite(position.z);
        }
        public bool TryPhysicsMotion(string id,out PhysicsMotionSample sample) {
            sample=default;var item=context.Editor?context.Editor.Find(id):null;if(!item||!item.isActiveAndEnabled)return false;
            var rigid=item.GetComponent<RigidRoomItem>();if(!rigid||!rigid.TryReadMotion(out bool available,out float speed,out float spin))return false;
            sample=new PhysicsMotionSample(rigid.GetInstanceID(),rigid.MotionRevision,available,speed,spin);return true;
        }
        public IProgramAnchorProbe OpenAnchorProbe(string target,JObject holder,UnityEngine.Vector3 offset,bool requirePhysics)=>new RoomAnchorProbe(context.Editor,target,HoldObjectCapability.Anchor(holder),offset,requirePhysics);
        public bool TryRead(string name,out ProgramValue value)=>TryRead(name,1,null,out value);
        public bool TryRead(string name,int version,JObject arguments,out ProgramValue value) {
            value=default;var definition=BehaviourCatalog.Fact(name);if(definition==null||domain!=null&&definition.Domain!=domain||!context.Editor&&!context.Workspace)return false;
            return BehaviourCatalog.TryRead(name,version,arguments,new BehaviourCatalog.FactContext(physicsReady:context.Editor&&context.Editor.PhysicsWorld?context.Editor.PhysicsWorld.SurfacesReady:null,
                physicsRunning:context.Editor&&context.Editor.PhysicsWorld?context.Editor.PhysicsWorld.Running:null,roomSessionId:context.Editor?.TemporarySessionId,world:this,editor:context.Editor,workspace:context.Workspace),out value);
        }
        public bool CanRun(CapabilityCall call,out string error){if(domain!=null&&call.Definition.Module.Domain!=domain){error="This action belongs to another execution domain.";return false;}if(call.Definition.Module.Domain=="room"&&context.Editor&&context.Editor.RuntimeGate.Held){error=context.Editor.RuntimeGate.Reason;return false;}return call.Definition.Module.CanRun(context,call.Arguments,out error);}
        public bool Start(string runId,CapabilityCall call,out float seconds,out string error) {
            seconds=0;error="This action is already running";if(operations.ContainsKey(runId))return false;
            if(!CanRun(call,out error))return false;results.Remove(runId);interrupted.Remove(runId);
            if(!Ownership.Covers(runId,call.Claims)) {
                if(!Ownership.TryAcquire(runId,call.Definition.Label,RoomActorRole.Program,call.Claims,
                    notice=>{Stop(runId,notice.PreservePlacement);interrupted[runId]=notice.Message;},out var lease,out error))return false;
                owned[runId]=lease;
            }
            bool started=call.Definition.Module.Start(context,runId,call.Arguments,out var operation,out error);
            // Retain partial starts so scheduler cancellation releases every acquired resource.
            if(operation!=null) {operations.Add(runId,operation);seconds=operation.Seconds;}
            if(operation==null) {ReleaseOwned(runId);if(started){error="Capability did not return an operation";return false;}}
            return started;
        }
        public RuleActionState State(string runId,out string error) {
            error=null;if(interrupted.TryGetValue(runId,out error))return RuleActionState.Failed;
            return operations.TryGetValue(runId,out var operation)?operation.State(out error):RuleActionState.Ready;
        }
        public void Tick() {foreach(var entry in operations)if(!owned.TryGetValue(entry.Key,out var lease)||lease.Held)entry.Value.Tick();}
        public bool Complete(string runId,out string error) {
            error=null;if(interrupted.Remove(runId,out error))return false;
            if(!operations.Remove(runId,out var operation))return true;
            try {if(!operation.Complete(out error))return false;var result=operation.Result;if(result.Count>0)results[runId]=result;return true;}
            finally {try {operation.Stop(false);} finally {ReleaseOwned(runId);}}
        }
        public JObject TakeResult(string runId)=>results.Remove(runId,out var result)?result:new JObject();
        public bool WaitsThroughGrab(string runId,string target)=>operations.TryGetValue(runId,out var operation)&&operation.WaitsThroughGrab(target);
        public string InterruptionStatus(string runId)=>operations.TryGetValue(runId,out var operation)?operation.InterruptionStatus:null;
        void ReleaseOwned(string runId) {if(owned.Remove(runId,out var lease))lease.Dispose();}
        public void Stop(string runId,bool preservePlacement) {results.Remove(runId);interrupted.Remove(runId);try {if(operations.Remove(runId,out var operation))operation.Stop(preservePlacement);}finally {ReleaseOwned(runId);}}
        // Existing direct tool/tests enter the same module path, not a second handler.
        public bool CanRun(RuleStep step,out string error)=>LegacyCapabilityAdapters.TryCall(step,out var call,out error)&&CanRun(call,out error);
        public bool Start(string runId,RuleStep step,out float seconds,out string error) {
            seconds=0;return LegacyCapabilityAdapters.TryCall(step,out var call,out error)&&Start(runId,call,out seconds,out error);
        }
    }
}

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
    public sealed class RoomRuleActions : IRuleActions, IRuleCompletion, IRuleReadiness, IProgramFacts, IRuleResults
    {
        readonly CapabilityContext context;
        readonly Dictionary<string,CapabilityOperation> operations=new();
        readonly Dictionary<string,JObject> results=new();
        public RoomRuleActions(RoomEditor editor,AnimationWorkshop workshop) {context=new CapabilityContext(editor,workshop);}
        public static ImportedModel ClipModel(RoomItem item)=>AnimationTargets.ClipModel(item);
        public bool TryRead(string name,out ProgramValue value) {
            value=default;if(!context.Editor.PhysicsWorld)return false;
            return BehaviourCatalog.TryRead(name,new BehaviourCatalog.FactContext(physicsReady:context.Editor.PhysicsWorld.SurfacesReady,
                physicsRunning:context.Editor.PhysicsWorld.Running),out value);
        }
        public bool CanRun(CapabilityCall call,out string error)=>call.Definition.Module.CanRun(context,call.Arguments,out error);
        public bool Start(string runId,CapabilityCall call,out float seconds,out string error) {
            seconds=0;error="This action is already running";if(operations.ContainsKey(runId))return false;
            if(!CanRun(call,out error))return false;results.Remove(runId);
            bool started=call.Definition.Module.Start(context,runId,call.Arguments,out var operation,out error);
            // Retain partial starts so scheduler cancellation releases every acquired resource.
            if(operation!=null) {operations.Add(runId,operation);seconds=operation.Seconds;}
            if(started&&operation==null) {error="Capability did not return an operation";return false;}
            return started;
        }
        public RuleActionState State(string runId,out string error) {
            error=null;return operations.TryGetValue(runId,out var operation)?operation.State(out error):RuleActionState.Ready;
        }
        public void Tick() {foreach(var operation in operations.Values)operation.Tick();}
        public bool Complete(string runId,out string error) {
            error=null;if(!operations.Remove(runId,out var operation))return true;
            try {if(!operation.Complete(out error))return false;var result=operation.Result;if(result.Count>0)results[runId]=result;return true;}
            finally {operation.Stop(false);}
        }
        public JObject TakeResult(string runId)=>results.Remove(runId,out var result)?result:new JObject();
        public void Stop(string runId,bool preservePlacement) {results.Remove(runId);if(operations.Remove(runId,out var operation))operation.Stop(preservePlacement);}
        // Existing direct tool/tests enter the same module path, not a second handler.
        public bool CanRun(RuleStep step,out string error)=>LegacyCapabilityAdapters.TryCall(step,out var call,out error)&&CanRun(call,out error);
        public bool Start(string runId,RuleStep step,out float seconds,out string error) {
            seconds=0;return LegacyCapabilityAdapters.TryCall(step,out var call,out error)&&Start(runId,call,out seconds,out error);
        }
    }
}

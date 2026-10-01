// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Persistence
{
    /// <summary>Preserves accepted state even when a native store is unreadable. It does not repair,
    /// select or load a workspace. Release only after dispatched saves and capture have settled.</summary>
    internal sealed class WorkspaceRecoveryHold:IDisposable
    {
        readonly int thread=Environment.CurrentManagedThreadId;
        readonly RoomEditor editor;readonly Task drained;readonly byte[] accepted;readonly string source;
        IDisposable activity,writes,roomSaves,ruleSaves;Task<CapturedRecoveryEvidence> capture;bool disposed;
        WorkspaceRecoveryHold(RoomEditor editor,IDisposable activity,IDisposable writes,IDisposable roomSaves,IDisposable ruleSaves,Task drained,byte[] accepted)
        {this.editor=editor;this.activity=activity;this.writes=writes;this.roomSaves=roomSaves;this.ruleSaves=ruleSaves;this.drained=drained;this.accepted=accepted;source=editor.SaveDirectory;}
        internal bool Owns(RoomEditor owner)=>!disposed&&ReferenceEquals(editor,owner)&&writes!=null&&activity!=null&&roomSaves!=null&&ruleSaves!=null&&owner&&owner.WriteGate.Frozen;
        internal Task Completion=>(Task)capture??drained;
        internal static bool CanAcquire(RoomEditor editor,RuleWorkshop rules,MovementControls controls,out string error)
        {
            error=null;
            if(!editor||!rules||!controls||rules.Editor!=editor||controls.ArchiveEditor!=editor){error="Workspace owners are not ready for recovery preservation.";return false;}
            if(editor.TemporarySavePending){error="Wait for the dispatched temporary-room save before preserving recovery data.";return false;}
            if(!editor.WriteGate.CanFreeze(out error)||!editor.CanChangeTemporaryBoundary(out error))return false;
            if(rules.Runtime&&rules.Runtime.AnyButtonHeld){error="Release action buttons before preserving recovery data.";return false;}
            rules.Modules.Poll();if(!rules.Modules.Ready||rules.Modules.Pending){error="Wait for the reusable library's current work to finish.";return false;}
            return true;
        }
        internal static bool TryAcquire(RoomEditor editor,RuleWorkshop rules,MovementControls controls,out WorkspaceRecoveryHold hold,out string error)
        {
            hold=null;if(!CanAcquire(editor,rules,controls,out error))return false;
            IDisposable activity=null,writes=null,roomSaves=null,ruleSaves=null;
            try {
                activity=editor.RuntimeGate.Hold("Preserving damaged workspace data for recovery");editor.PrepareAgentEdit();
                if(editor.RuntimeGate.Failure!=null){error=editor.RuntimeGate.Failure;return false;}
                if(!CanAcquire(editor,rules,controls,out error))return false;
                writes=editor.WriteGate.TryFreeze(out error);if(writes==null)return false;
                roomSaves=editor.HoldSaveDispatch(out var roomPending,out error);if(roomSaves==null)return false;
                ruleSaves=rules.HoldSaveDispatch(out var rulesPending,out error);if(ruleSaves==null)return false;
                // No save is dispatched here. Read-only fallbacks are labelled, never treated as
                // repaired originals. The temporary fork is kept separately from its saved base.
                JToken Json(object value)=>JToken.Parse(JsonUtility.ToJson(value));
                var accepted=new JObject {["version"]=1,["available"]=new JObject {["room"]=editor.CanSaveRoom,["behaviours"]=!rules.ReadOnly,["controls"]=controls.ArchiveReady,["activities"]=!editor.ActivityProfiles.ReadOnly},
                    ["room"]=Json(editor.RecoverySavedSnapshot()),["behaviours"]=Json(rules.Snapshot()),["controls"]=Json(controls.Preferences),["activities"]=Json(editor.ActivityProfiles.Snapshot()),
                    ["temporaryRoom"]=editor.TemporaryRoom?Json(editor.Snapshot()):JValue.CreateNull()};
                var bytes=WorkspaceRecoveryEvidence.EncodeAccepted(accepted);
                hold=new WorkspaceRecoveryHold(editor,activity,writes,roomSaves,ruleSaves,Task.WhenAll(Drain(roomPending),Drain(rulesPending)),bytes);
                activity=writes=roomSaves=ruleSaves=null;return true;
            }catch(Exception){error="Recovery preservation could not begin. Current owners and original files are unchanged.";return false;}
            finally{ruleSaves?.Dispose();roomSaves?.Dispose();writes?.Dispose();activity?.Dispose();}
        }
        static async Task Drain(Task task){try{await task.ConfigureAwait(false);}catch(Exception){/* Failed saves still finish; their original bytes and accepted documents need preservation. */}}
        void OwnerThread(){if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Use the recovery hold on its Unity owner thread.");}
        internal Task<CapturedRecoveryEvidence> Capture(string outputDirectory,CancellationToken cancellation=default)
        {
            OwnerThread();if(disposed||capture!=null)throw new InvalidOperationException("This recovery hold already captured or closed.");
            capture=Task.Run(async()=>{await drained.ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return WorkspaceRecoveryEvidence.Write(source,outputDirectory,accepted,cancellation);});return capture;
        }
        public void Dispose()
        {
            OwnerThread();if(disposed)return;if(!Completion.IsCompleted)throw new InvalidOperationException("Wait for dispatched saves and recovery capture before releasing ownership.");
            disposed=true;ruleSaves?.Dispose();ruleSaves=null;roomSaves?.Dispose();roomSaves=null;writes?.Dispose();writes=null;activity?.Dispose();activity=null;
        }
    }
}

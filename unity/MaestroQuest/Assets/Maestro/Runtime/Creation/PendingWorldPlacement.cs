// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // A location is personal navigation, never an authored geometry edit.
        // Rebuild the prepared body transfer after every frame or IO wait.
        internal sealed class PendingWorldPlacement:IPersistedRoomEdit
        {
            readonly RoomEditor editor;
            readonly RoomJournal journal;
            readonly RoomStorage storage;
            readonly RoomViewpoint target;
            readonly string expected,session;
            readonly bool manual,temporary;
            readonly IDisposable write;
            readonly RoomNativeDemand demand;
            readonly Action<string> checkpoint;
            Task<string> saving;Task cleanup;
            bool accepted,applied,ended,success,released,committing;
            string failure;
            JObject result;
            internal PendingWorldPlacement(RoomEditor editor,string expected,RoomViewpoint target,bool manual,
                IDisposable write,RoomNativeDemand demand,Action<string> checkpoint)
            {
                this.editor=editor;journal=editor.journal;storage=editor.storage;this.expected=expected;this.target=target;
                this.manual=manual;this.write=write;this.demand=demand;this.checkpoint=checkpoint;session=editor.TemporarySessionId;temporary=editor.TemporaryRoom;
            }
            public bool Accepted=>accepted;
            internal JObject Result=>(JObject)result?.DeepClone();
            public bool Drained=>released&&(cleanup==null||cleanup.IsCompleted);
            public string InterruptionStatus=>!accepted?null:temporary?
                "The temporary world location was accepted. Stopping cannot move you back; Keep a snapshot to save it.":
                saving?.IsCompletedSuccessfully==true&&saving.Result==null?
                "The world location was accepted and saved. Stopping cannot move you back; inspect before requesting another relocation.":
                "The world location was accepted. Saving is pending or unconfirmed; stopping cannot move you back. Inspect before requesting another relocation.";
            RoomSpatialReadiness Readiness(out string error)
            {
                error="The world placement workspace is no longer available";
                if(!editor||!editor.isActiveAndEnabled||editor.Ownership.Suspended||editor.RuntimeGate.Held||
                    editor.saveDispatch.Frozen||!ReferenceEquals(editor.journal,journal)||editor.TemporarySessionId!=session)
                    return RoomSpatialReadiness.Unavailable;
                if(!editor.CanPrepareWorldViewpoint(expected,target,manual,out error))return RoomSpatialReadiness.Unavailable;
                if(!demand.Preparation.IsCompleted){error=null;return RoomSpatialReadiness.Preparing;}
                if(demand.Error is string changed){error=changed;return RoomSpatialReadiness.Unavailable;}
                return editor.WorldPlacementReadiness(expected,target,manual,out error);
            }
            public bool Poll(out bool ready,out string error)
            {
                ready=false;error=failure;
                if(committing)return true;
                if(ended){ready=success;return success;}
                if(!accepted){
                    var readiness=Readiness(out error);
                    if(readiness==RoomSpatialReadiness.Unavailable)return Fail(error);
                    if(readiness==RoomSpatialReadiness.Preparing){error=null;return true;}
                    if(editor.saveTask!=null&&!editor.saveTask.IsCompleted)return true;
                    editor.CompleteSave();
                    // A status observer may stop or invalidate this request.
                    if(ended){error=failure;return success;}
                    readiness=Readiness(out error);
                    if(readiness==RoomSpatialReadiness.Unavailable)return Fail(error);
                    if(readiness==RoomSpatialReadiness.Preparing){error=null;return true;}
                    committing=true;
                    try{
                        // Last validation prepared against this frame's actual head,
                        // ground, obstacles, bindings and physical transfer. Accept
                        // and apply without a callback or frame boundary between.
                        accepted=true;journal.UpdateViewpoint(target,exact:true);
                        if(!temporary){
                            saving=editor.saveTask=RoomCheckpointSave.Start(storage,journal.Snapshot(),checkpoint);
                            editor.dirty=false;editor.lastSaveError=null;
                        }
                        var placement=editor.GetComponent<WorkspaceViewpoint>();placement.ApplyPlacement();applied=true;
                        result=new JObject {["stateId"]=placement.PlacementState(),["position"]=WorkspaceViewpoint.Point(target.position),
                            ["yaw"]=target.yaw,["temporary"]=temporary};
                        Release();
                    }catch(Exception){
                        if(!temporary&&saving==null){
                            saving=editor.saveTask=RoomCheckpointSave.Start(storage,journal.Snapshot(),checkpoint);
                            editor.dirty=false;
                        }
                        error="The world location was accepted but native completion could not be confirmed. Inspect or reload the room before retrying.";
                        return Fail(error);
                    }finally{committing=false;}
                }
                if(!temporary&&!saving.IsCompleted)return true;
                if(!temporary){error=saving.GetAwaiter().GetResult();if(error!=null)return Fail(error);}
                if(!applied)return Fail(error="World placement did not confirm native application");
                success=true;ended=true;Release();ready=true;
                if(editor&&editor.isActiveAndEnabled)editor.SetStatus(temporary?
                    "Temporary world location — activity and real-room tracking retained":
                    "World location saved — activity and real-room tracking retained");
                return true;
            }
            public bool Complete(out string error)
            {
                if(committing){error="World placement is already completing";return false;}
                if(!Poll(out var ready,out error))return false;
                if(!ready){error="World placement is still preparing or saving";return false;}
                return true;
            }
            bool Fail(string error){failure=error??"World placement failed";ended=true;success=false;Release();return false;}
            void Release(){if(released)return;released=true;try{demand.Dispose();}finally{cleanup=Drain(saving,write);}}
            static async Task Drain(Task saving,IDisposable write)
            {try{if(saving!=null)try{await saving.ConfigureAwait(false);}catch(Exception){}}finally{write.Dispose();}}
            public void Dispose()
            {
                if(!ended&&!committing)Fail(accepted?
                    "World placement stopped after acceptance; saving continues. Inspect the outcome before requesting another move.":
                    "World placement cancelled before acceptance");
            }
        }
    }
}

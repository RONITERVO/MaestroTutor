// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        readonly string recoveryOwner="tool-recovery:"+Guid.NewGuid().ToString("N");
        string recoveryState=Guid.NewGuid().ToString("N"),recoverySession;
        string CurrentRecoveryState(){
            if(recoverySession!=TemporarySessionId){recoverySession=TemporarySessionId;recoveryState=Guid.NewGuid().ToString("N");}
            return recoveryState;
        }
        static readonly BehaviourCatalog.Claim[] RecoveryClaims={new("book","wholeTarget")};
        public JObject ObserveToolRecovery(){
            bool ready=CanRecallTools(CurrentRecoveryState(),ObjectRevision("book"),false,out var error);
            return new JObject { ["stateId"]=recoveryState,["revision"]=ObjectRevision("book"),["ready"]=ready,["reason"]=error??"" };
        }
        public bool CanRecallTools(string expected,int revision,bool manual,out string error)
        {
            error="Book and tools are unavailable";
            if(!isActiveAndEnabled||!room||journal==null||!Find("book"))return false;
            if(!manual&&(expected!=CurrentRecoveryState()||revision!=ObjectRevision("book"))){error="Book or recovery state changed; read room.tools.recovery again";return false;}
            if(WriteGate.Frozen||RuntimeGate.Held||!CanSaveRoom){error="Finish the workspace operation before recalling tools";return false;}
            if(GetComponent<MovementControls>() is MovementControls controls&&controls&&!controls.RecoveryHeadReady){error="Look into the headset with tracking active before recalling tools";return false;}
            if(!Frame.Valid||!Frame.Read(Find("book").transform,out _,out _,out _)){error="The book needs a valid room coordinate frame";return false;}
            if(!PlanToolRecovery(out _,out _,out error))return false;
            return Ownership.CanAcquire(recoveryOwner,manual?RoomActorRole.Control:RoomActorRole.Program,RecoveryClaims,out error);
        }
        bool PlanToolRecovery(out List<RoomInteraction.RecoveryPlacement> plan,out RoomObjectData book,out string error)
        {
            book=null;if(!room.PlanRecovery(out plan,out error))return false;
            var item=Find("book");if(!plan.Any(p=>p.Item==item)){error="The book recovery layout is unavailable";return false;}
            var pose=plan.Single(p=>p.Item==item);var frame=Frame;var candidate=journal.Snapshot();book=candidate.objects.Single(p=>p.id=="book");
            book.position=frame.PointToRoom(pose.Position);book.rotation=frame.RotationToRoom(pose.Rotation);book.scale=pose.Scale/frame.MetresPerUnit;
            // Saved book bounds still apply in authored coordinates. Validate before
            // taking over any book actor, including in translated/scaled worlds.
            return candidate.Validate(out error);
        }
        public bool RecallTools(string expected,int revision,bool manual,out JObject result,out string error)
        {
            result=null;if(!CanRecallTools(expected,revision,manual,out error))return false;
            using var write=WriteGate.TryWrite(out error);if(write==null)return false;
            if(!Ownership.TryAcquire(recoveryOwner,"Bring book and tools back",manual?RoomActorRole.Control:RoomActorRole.Program,RecoveryClaims,null,out var lease,out error,preservePlacement:true))return false;
            using(lease){
                // Interruption callbacks may save a book animation. Recheck all admission
                // conditions before any pose changes; unrelated owners are never displaced.
                if(!CanRecallTools(expected,revision,manual,out error)||!PlanToolRecovery(out var plan,out var book,out error))return false;
                // Save the book through the same journal/Undo transaction as placement.
                // Tray poses are transient. A failed save leaves every tool where it was.
                if(!CommitPersisted(new[]{book},Array.Empty<string>(),"Book and tools brought back — world kept in place",true,out error))return false;
                RoomInteraction.ApplyRecovery(plan);
                recoveryState=Guid.NewGuid().ToString("N");
                result=new JObject { ["stateId"]=recoveryState,["revision"]=ObjectRevision("book"),["recovered"]=plan.Count,["temporary"]=TemporaryRoom };
                return true;
            }
        }
    }
}

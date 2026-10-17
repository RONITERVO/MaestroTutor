// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
namespace Maestro.Quest.Programs
{
    // One adapter for shared persisted object edits. Workers and cleanup are owned
    // by the editor operation; receipt observation never owns their lifetime.
    internal sealed class PersistedEditOperation:CapabilityOperation
    {
        internal const string Persistence=" Outside temporary play, prepares and publishes its save in the background with one Undo edit. Completion confirms native application and saving. Once preparation begins, newer authored changes invalidate its candidate. Cancellation before native acceptance leaves the requested edit unapplied. After acceptance, stopping cannot retract the edit or its pending save. A save conflict preserves files, blocks further saves and requires inspection; never blindly replay it. Runtime clock and viewer movement continue. Concurrent persisted object edits enter preparation in arrival order after earlier saves drain, without copying a waiting candidate. Stopping or invalidating a waiter releases its place; target changes while waiting require a new request. In a temporary room, changes the live room and local Undo; a kept snapshot makes it durable. Other objects keep running.";
        internal const string PlacementPersistence=Persistence+" Only the target body is held during placement preparation; it resumes after application. Placement resets velocity, then normal gravity resumes.";
        internal delegate bool Validate(out string error);
        internal delegate bool Begin(out RoomEditor.PendingPersistedEdit pending,out string error);
        readonly RoomEditor editor;
        readonly string target,temporarySession;
        readonly int revision;
        readonly Begin begin;
        readonly Validate validate;
        RoomEditor.PendingPersistedEdit pending;
        RoomEditor.EditAdmission admission;
        bool stopped;
        string failure;
        PersistedEditOperation(RoomEditor editor,string target,Begin begin,Validate validate)
        {
            this.editor=editor;this.target=target;this.begin=begin;
            this.validate=validate??((out string error)=>editor.CanEditObject(target,false,out error));
            revision=editor.ObjectRevision(target);temporarySession=editor.TemporarySessionId;
        }
        internal static bool Start(RoomEditor editor,string target,Begin begin,out CapabilityOperation operation,out string error,Validate validate=null)
        {
            var value=new PersistedEditOperation(editor,target,begin,validate);
            operation=null;if(!value.TryBegin(out error))return false;
            operation=value;return true;
        }
        bool TryBegin(out string error)
        {
            error=failure;
            if(stopped){error??="The object edit action was stopped";return false;}
            if(failure!=null)return false;
            if(pending!=null)return true;
            if(!editor||!editor.isActiveAndEnabled||editor.TemporarySessionId!=temporarySession)
                return Fail("The object edit workspace changed while waiting",out error);
            if(editor.ObjectRevision(target)!=revision)
                return Fail("The target changed while waiting for object edit; inspect it before retrying",out error);
            if(!validate(out error))return Fail(error,out error);
            if(admission==null&&!editor.ReserveEdit(out admission,out error))return Fail(error,out error);
            if(!admission.Valid)return Fail("The object edit workspace ended while waiting",out error);
            // Poll order and a program immediately starting its next step must not
            // let newer requests pass an older waiter. Scheduler timeouts still apply.
            if(editor.PendingEditSave||!admission.First)return true;
            try {
                if(!begin(out pending,out error))return Fail(error,out error);
                return true;
            } finally {admission.Dispose();}

        }
        bool Fail(string reason,out string error){admission?.Dispose();failure=error=reason??"The object edit could not start";return false;}
        public override float Seconds=>0;
        public override RuleActionState State(out string error)
        {
            if(!TryBegin(out error))return RuleActionState.Failed;
            if(pending==null)return RuleActionState.Preparing;
            return !pending.Poll(out var ready,out error)?RuleActionState.Failed:ready?RuleActionState.Ready:RuleActionState.Preparing;
        }
        public override bool Complete(out string error)
        {
            if(pending!=null&&!stopped)return pending.Complete(out error);
            error=failure??"The object edit has not completed";return false;
        }
        public override void Stop(bool preservePlacement){stopped=true;admission?.Dispose();pending?.Dispose();}
        public override string InterruptionStatus=>pending?.InterruptionStatus;
    }
}

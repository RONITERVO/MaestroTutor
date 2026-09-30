// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
namespace Maestro.Quest.Persistence
{
    /// <summary>Owner-thread boundary for retaining accepted content before a workspace switch.
    /// Keep this lease until capture/activation finishes. Failure releases editing on the same
    /// live owners; interrupted effects never resume. This does not itself select a workspace.</summary>
    internal sealed class WorkspaceEditHold:IDisposable
    {
        readonly int thread=Environment.CurrentManagedThreadId;
        IDisposable activity,writes;
        WorkspaceEditHold(IDisposable activity,IDisposable writes){this.activity=activity;this.writes=writes;}
        internal static bool TryAcquire(RoomEditor editor,RuleWorkshop rules,MovementControls controls,out WorkspaceEditHold hold,out string error)
        {
            hold=null;
            if(!WorkspaceArchiveCapture.CanStart(editor,rules,controls,out error)||!editor.WriteGate.CanFreeze(out error)||!editor.CanChangeTemporaryBoundary(out error))return false;
            if(rules.Runtime&&rules.Runtime.AnyButtonHeld){error="Release action buttons before preserving the workspace.";return false;}
            IDisposable activity=null,writes=null;
            try {
                // Stop may accept a final authored pose/take. Finish it before freezing writes.
                activity=editor.RuntimeGate.Hold("Preserving the current workspace before a restore");
                editor.PrepareAgentEdit();
                if(editor.RuntimeGate.Failure!=null){error=editor.RuntimeGate.Failure;return false;}
                if(!WorkspaceArchiveCapture.CanStart(editor,rules,controls,out error)||!editor.CanChangeTemporaryBoundary(out error))return false;
                if(rules.Runtime&&rules.Runtime.AnyButtonHeld){error="Release action buttons before preserving the workspace.";return false;}
                writes=editor.WriteGate.TryFreeze(out error);if(writes==null)return false;
                hold=new WorkspaceEditHold(activity,writes);activity=null;writes=null;return true;
            }catch(Exception){error="Workspace preparation failed. Your current room and accepted edits remain available.";return false;}
            finally {writes?.Dispose();activity?.Dispose();}
        }
        public void Dispose()
        {
            if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Release the workspace hold on its Unity owner thread.");
            writes?.Dispose();writes=null;activity?.Dispose();activity=null;
        }
    }
}

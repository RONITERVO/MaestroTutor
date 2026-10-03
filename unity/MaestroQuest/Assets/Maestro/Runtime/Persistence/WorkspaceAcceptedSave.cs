// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
namespace Maestro.Quest.Persistence
{
    // Saves the accepted documents under an existing edit hold. The caller retains
    // this owner until both saving and any concurrent archive verification finish.
    // Cancellation must drain accepted writes, never abandon or replay them.
    internal sealed class WorkspaceAcceptedSave:IDisposable
    {
        readonly int thread=Environment.CurrentManagedThreadId;
        IDisposable roomLease,rulesLease;
        internal Task Completion {get;private set;}
        internal static WorkspaceAcceptedSave Start(WorkspaceEditHold held,RoomEditor editor,RuleWorkshop rules)
        {
            if(held==null||!held.Owns(editor)||!rules||rules.Editor!=editor)throw new InvalidOperationException("Hold these workspace owners before saving accepted contents.");
            var result=new WorkspaceAcceptedSave();Task<string> room=Task.FromResult<string>(null),behaviours=room;
            try {
                result.roomLease=editor.SaveAcceptedAsync(out room);
                if(result.roomLease==null)throw new InvalidOperationException("Room saves are already held.");
                result.rulesLease=rules.SaveAcceptedAsync(out behaviours);
                if(result.rulesLease==null)throw new InvalidOperationException("Behaviour saves are already held.");
                result.Completion=Finish(room,behaviours);
            }catch(Exception error){result.Completion=FailAfterDrain(room,behaviours,error);}
            return result;
        }
        internal static async Task<string> FailureAfter(Task earlier,string error)
        {
            if(earlier!=null)try{await earlier.ConfigureAwait(false);}catch(Exception){}
            return error;
        }
        static async Task Finish(Task<string> room,Task<string> behaviours)
        {
            var errors=await Task.WhenAll(room,behaviours).ConfigureAwait(false);
            if(errors.Any(error=>error!=null))throw new IOException("Accepted workspace contents could not be saved.");
        }
        static async Task FailAfterDrain(Task room,Task behaviours,Exception failure)
        {
            try{await Task.WhenAll(room,behaviours).ConfigureAwait(false);}catch(Exception){}
            throw new IOException("Accepted workspace saves could not start.",failure);
        }
        public void Dispose()
        {
            if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Release accepted saves on their Unity owner thread.");
            if(!Completion.IsCompleted)throw new InvalidOperationException("Wait for accepted writes before releasing their save holds.");
            rulesLease?.Dispose();rulesLease=null;roomLease?.Dispose();roomLease=null;
        }
    }
}

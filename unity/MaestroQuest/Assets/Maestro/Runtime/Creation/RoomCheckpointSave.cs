// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
namespace Maestro.Quest.Creation
{
    // Accepted native changes own their save lifetime. Only detached snapshots
    // and storage handles reach workers; Stop cannot revoke acceptance.
    internal static class RoomCheckpointSave
    {
        internal static async Task<string> Start(RoomStorage storage,RoomDocument snapshot,Action<string> checkpoint=null) {
            try {
                using var prepared=await storage.PrepareSaveAsync(snapshot).ConfigureAwait(false);
                var publication=storage.StartPreparedPublication(prepared,checkpoint);
                return await Observe(storage,publication.Completion).ConfigureAwait(false);
            }catch(Exception){return storage.PreserveUnconfirmedEdit("Accepted checkpoint preparation did not complete.");}
        }
        internal static async Task<string> Observe(RoomStorage storage,Task<RoomStorage.PublicationResult> task) {
            try {var result=await task.ConfigureAwait(false);return result.Saved?null:storage.PreserveUnconfirmedEdit(result.Error);}
            catch(Exception){return storage.PreserveUnconfirmedEdit("Publication worker did not confirm its outcome.");}
        }
    }
}

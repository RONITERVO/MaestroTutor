// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;

namespace Maestro.Quest.Creation
{
    public sealed partial class RoomStorage
    {
        int preparationOwners;
        sealed class PreparationOwner:IDisposable
        {
            RoomStorage owner;
            internal PreparationOwner(RoomStorage owner){this.owner=owner;}
            public void Dispose(){var previous=Interlocked.Exchange(ref owner,null);if(previous!=null)Interlocked.Decrement(ref previous.preparationOwners);}
        }
        internal sealed class PreparedSave:IDisposable
        {
            readonly RoomStorage owner;
            readonly IDisposable reservation;
            VersionedRoomFile<RoomDocument>.PreparedSave candidate;
            internal PreparedSave(RoomStorage owner,VersionedRoomFile<RoomDocument>.PreparedSave candidate,IDisposable reservation)
            {this.owner=owner;this.candidate=candidate;this.reservation=reservation;}
            internal bool Take(RoomStorage expected,out VersionedRoomFile<RoomDocument>.PreparedSave value)
            {value=null;if(!ReferenceEquals(owner,expected))return false;value=Interlocked.Exchange(ref candidate,null);return value!=null;}
            internal void FinishPublication()=>reservation.Dispose();
            public void Dispose(){var previous=Interlocked.Exchange(ref candidate,null);if(previous==null)return;previous.Dispose();reservation.Dispose();}
        }

        // Admit one preparation before copying caller data. This reservation spans
        // worker execution, the returned candidate and publication/disposal. A future
        // native command queue must wait for admission without queuing private copies.
        internal async Task<PreparedSave> PrepareSaveAsync(RoomDocument room,CancellationToken cancellation=default)
        {
            cancellation.ThrowIfCancellationRequested();
            if(Interlocked.CompareExchange(ref preparationOwners,1,0)!=0)
                throw new InvalidOperationException("Another room save is being prepared; finish or discard it before preparing another.");
            var reservation=new PreparationOwner(this);bool transferred=false;
            try {
                if(ReadOnly)throw new InvalidOperationException("Room storage is unavailable; original files are preserved.");
                if(room==null||!room.Validate(out _))throw new ArgumentException("Invalid room data.",nameof(room));
                // The caller owns the input during this synchronous copy. No
                // caller-owned mutable values cross the worker boundary.
                VersionedRoomFile<RoomDocument>.PrimaryCapture captured;
                // Bind the disk baseline before yielding or copying caller state.
                // A worker that captured later could bless an intervening save as
                // the baseline of an older candidate. Busy coordination is refused,
                // never waited for on the caller's interaction thread.
                using(var owner=RoomSnapshotTransaction.Enter(directory,wait:false,initialize:false))
                    captured=file.CaptureForPreparation();
                var owned=room.Copy();
                var prepared=await Task.Run(()=>{
                    cancellation.ThrowIfCancellationRequested();
                    if(!file.PrepareSave(owned,captured,out var value,out var error,stageFile:true,cancellation:cancellation))
                        throw new InvalidOperationException(error);
                    if(cancellation.IsCancellationRequested){value.Dispose();cancellation.ThrowIfCancellationRequested();}
                    return new PreparedSave(this,value,reservation);
                },cancellation).ConfigureAwait(false);
                transferred=true;return prepared;
            } finally {if(!transferred)reservation.Dispose();}
        }

        // The native caller must revalidate its candidate revision/lifecycle before
        // calling. Disk compare-and-publish protects only the captured saved baseline.
        // Encoding, writing and durable flush already ran on the worker. Baseline
        // checks, backup copy and one rename/replace remain synchronous.
        internal bool PublishPrepared(PreparedSave prepared,out string error)
        {
            error="This room save preparation was already used or belongs to another store.";
            if(prepared==null||!prepared.Take(this,out var candidate))return false;
            try {
                error=coordinationError;if(error!=null)return false;
                using var owner=RoomSnapshotTransaction.Enter(directory,wait:false);return file.PublishPrepared(candidate,out error);
            }
            catch(Exception failure) {
                error="Prepared room publication was not confirmed; preserve the saved room/memory files. "+failure.Message;return false;
            }
            finally {candidate.Dispose();prepared.FinishPublication();}
        }
    }
}

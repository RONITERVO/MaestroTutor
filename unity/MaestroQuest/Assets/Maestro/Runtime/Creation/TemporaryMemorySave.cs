// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
namespace Maestro.Quest.Creation
{
    // Captured on the owner thread; publication and recovery use detached documents only.
    internal sealed class TemporaryMemorySave:IDisposable
    {
        readonly ProgramMemoryStore store;
        readonly ProgramMemoryDocument expected,captured;
        readonly RoomDocument room;
        readonly RoomSnapshotTransaction.Snapshot previous;
        readonly IDisposable pin,write;
        internal RoomSnapshotTransaction.Snapshot Pair {get;private set;}
        internal bool Uncertain {get;private set;}
        internal TemporaryMemorySave(ProgramMemoryStore store,RoomSnapshotTransaction.Snapshot previous,RoomDocument room,WorkspaceWriteGate writes)
        {
            this.store=store;this.previous=previous;this.room=room;
            expected=store.SavedSnapshot();captured=store.CaptureTemporary();
            write=writes.Write();try{pin=store.RetainSnapshot(captured);}catch{write.Dispose();throw;}
        }
        internal string Run(Action<string> fault=null)
        {
            RoomSnapshotTransaction.Snapshot candidate=null,before=null;bool attempted=false;
            try {
                candidate=RoomSnapshotTransaction.FromDocuments(room,captured);
                before=previous??RoomSnapshotTransaction.Capture(store.DirectoryPath,out _,wait:true);
                var saved=before.Memory is byte[] bytes?ProgramMemoryDocument.Decode(bytes):ProgramMemoryDocument.Empty();
                if(saved.Identity!=expected.Identity){Uncertain=true;throw new IOException("Saved memory changed outside this temporary session; reopen it before keeping a snapshot.");}
                attempted=true;
                RoomSnapshotTransaction.Publish(store.DirectoryPath,before,candidate,fault,wait:true);Pair=candidate;return null;
            }catch(Exception error){
                if(attempted){
                    try {
                        var recovered=RoomSnapshotTransaction.Capture(store.DirectoryPath,out _,wait:true);
                        if(recovered.Identity==candidate.Identity){Pair=candidate;return null;}
                        if(recovered.Identity!=before.Identity)Uncertain=true;
                    }catch{
                        // A pre-existing ordinary pending file may reject publication before
                        // any paired write. If both primaries still match, an explicit retry is safe.
                        try{Uncertain=RoomSnapshotTransaction.InspectSnapshot(store.DirectoryPath).Identity!=before.Identity;}catch{Uncertain=true;}
                    }
                }
                if(before==null){
                    try{var bytes=RoomSnapshotTransaction.InspectSnapshot(store.DirectoryPath).Memory;Uncertain=(bytes==null?ProgramMemoryDocument.Empty():ProgramMemoryDocument.Decode(bytes)).Identity!=expected.Identity;}catch{Uncertain=true;}
                }
                return "Room and remembered values were not confirmed together. Temporary edits remain available. "+error.Message;
            }
        }
        internal void Confirm(){if(Pair==null||Uncertain)throw new InvalidOperationException("The paired save was not confirmed.");store.ConfirmTemporary(expected,captured);}
        public void Dispose(){pin.Dispose();write.Dispose();}
    }
}

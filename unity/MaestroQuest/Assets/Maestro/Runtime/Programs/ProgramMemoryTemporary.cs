// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
namespace Maestro.Quest.Programs
{
    internal sealed partial class ProgramMemoryStore
    {
        ProgramMemoryDocument durable;
        bool temporary;
        int retentionRevision;
        readonly Dictionary<ProgramMemoryDocument,int> pins=new();
        internal bool Temporary {get{lock(sync)return temporary;}}
        internal string DirectoryPath=>directory;
        // Recovery evidence labels cached documents separately from disk validity.
        internal ProgramMemoryDocument RecoverySnapshot(bool saved){lock(sync)return saved?durable:document;}
        internal ProgramMemoryDocument SavedSnapshot(){lock(sync){CheckReady();return durable;}}
        void Boundary(bool expected)
        {
            CheckReady();if(temporary!=expected)throw new InvalidOperationException(expected?"Start a temporary memory session first.":"Temporary memory is already active.");
            if(busy)throw new InvalidOperationException("Wait for the accepted remembered-value edit before changing the temporary room.");
        }
        internal void BeginTemporary()
        {
            using var write=writes.Write();lock(sync){Boundary(false);durable=document;temporary=true;retentionRevision++;}
        }
        internal ProgramMemoryDocument CaptureTemporary(){lock(sync){Boundary(true);return document;}}
        // Called only after the exact room/memory pair is durably confirmed. Later fork
        // writes may already be running; they are deliberately not replaced or rewound.
        internal void ConfirmTemporary(ProgramMemoryDocument expected,ProgramMemoryDocument captured)
        {
            lock(sync){CheckReady();if(!temporary||durable.Identity!=expected.Identity||captured==null)throw new InvalidOperationException("The saved memory base changed during publication.");durable=captured;retentionRevision++;}
        }
        internal void EndTemporary()
        {
            using var write=writes.Write();lock(sync){Boundary(true);document=durable;temporary=false;retentionRevision++;}
        }
        sealed class Pin:IDisposable
        {
            ProgramMemoryStore owner;readonly ProgramMemoryDocument snapshot;
            internal Pin(ProgramMemoryStore owner,ProgramMemoryDocument snapshot){this.owner=owner;this.snapshot=snapshot;}
            public void Dispose(){var previous=System.Threading.Interlocked.Exchange(ref owner,null);if(previous==null)return;lock(previous.sync){int count=previous.pins[snapshot];if(count==1)previous.pins.Remove(snapshot);else previous.pins[snapshot]=count-1;previous.retentionRevision++;}}
        }
        // Keep candidates retain referenced downloads even if later temporary edits reset
        // their values before the background publication reaches disk.
        internal IDisposable RetainSnapshot(ProgramMemoryDocument snapshot)
        {
            lock(sync){CheckReady();if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));pins.TryGetValue(snapshot,out int count);pins[snapshot]=count+1;retentionRevision++;return new Pin(this,snapshot);}
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Persistence
{
    /// <summary>Coordinates accepted edits, including asynchronous imports. A native freeze
    /// starts only between complete writes; an in-flight write is never silently cancelled.
    /// Ordinary autosaves of already accepted documents do not need a write lease.</summary>
    public sealed class WorkspaceWriteGate
    {
        sealed class Lease:IDisposable
        {
            WorkspaceWriteGate owner;readonly bool freeze,reader;
            internal Lease(WorkspaceWriteGate owner,bool freeze,bool reader=false){this.owner=owner;this.freeze=freeze;this.reader=reader;}
            public void Dispose(){var previous=Interlocked.Exchange(ref owner,null);if(previous==null)return;lock(previous.sync){if(freeze)previous.frozen=false;else {if(reader)--previous.readers;else --previous.writers;if(previous.writers+previous.readers==0)previous.drained?.TrySetResult(true);}}}
        }
        internal const string FrozenReason="The workspace is being preserved. Editing will be available when it finishes.";
        readonly object sync=new();int writers,readers;bool frozen,retired;TaskCompletionSource<bool> drained;
        public bool Frozen {get {lock(sync)return frozen||retired;}}
        internal bool CanFreeze(out string error){lock(sync){error=retired?"The previous workspace is closing.":frozen?FrozenReason:writers!=0?"Finish the current edit or import before preserving the workspace.":null;return error==null;}}
        internal IDisposable TryFreeze(out string error){lock(sync){if(!CanFreeze(out error))return null;frozen=true;return new Lease(this,true);}}
        internal IDisposable TryWrite(out string error){lock(sync){error=retired?"The previous workspace is closing.":frozen?FrozenReason:writers>=256?"Too many concurrent workspace edits. Wait for the current edits to finish.":null;if(error!=null)return null;if(writers++ + readers==0)drained=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);return new Lease(this,false);}}
        // Immutable source readers may coexist with a preserved snapshot. Only
        // retirement waits for them; an ordinary backup must not wait for a song.
        internal IDisposable RetainRead(){lock(sync){
            if(retired)throw new InvalidOperationException("The previous workspace is closing.");
            if(readers>=256)throw new InvalidOperationException("Too many retained workspace readers.");
            if(readers++ + writers==0)drained=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return new Lease(this,false,true);
        }}
        internal Task Retire(){lock(sync){retired=true;return writers+readers==0?Task.CompletedTask:drained.Task;}}
        internal IDisposable Write(){var lease=TryWrite(out var error);return lease??throw new InvalidOperationException(error);}
    }
}

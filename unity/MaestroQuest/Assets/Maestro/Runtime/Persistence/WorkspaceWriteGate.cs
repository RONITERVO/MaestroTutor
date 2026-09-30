// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
namespace Maestro.Quest.Persistence
{
    /// <summary>Coordinates accepted edits, including asynchronous imports. A native freeze
    /// starts only between complete writes; an in-flight write is never silently cancelled.
    /// Ordinary autosaves of already accepted documents do not need a write lease.</summary>
    public sealed class WorkspaceWriteGate
    {
        sealed class Lease:IDisposable
        {
            WorkspaceWriteGate owner;readonly bool freeze;
            internal Lease(WorkspaceWriteGate owner,bool freeze){this.owner=owner;this.freeze=freeze;}
            public void Dispose(){var previous=Interlocked.Exchange(ref owner,null);if(previous==null)return;lock(previous.sync){if(freeze)previous.frozen=false;else previous.writers--;}}
        }
        internal const string FrozenReason="The workspace is being preserved. Editing will be available when it finishes.";
        readonly object sync=new();int writers;bool frozen;
        public bool Frozen {get {lock(sync)return frozen;}}
        internal bool CanFreeze(out string error){lock(sync){error=frozen?FrozenReason:writers!=0?"Finish the current edit or import before preserving the workspace.":null;return error==null;}}
        internal IDisposable TryFreeze(out string error){lock(sync){if(!CanFreeze(out error))return null;frozen=true;return new Lease(this,true);}}
        internal IDisposable TryWrite(out string error){lock(sync){error=frozen?FrozenReason:writers>=256?"Too many concurrent workspace edits. Wait for the current edits to finish.":null;if(error!=null)return null;writers++;return new Lease(this,false);}}
        internal IDisposable Write(){var lease=TryWrite(out var error);return lease??throw new InvalidOperationException(error);}
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
namespace Maestro.Quest.Persistence
{
    // One running snapshot and one replaceable waiting snapshot. Callers supply
    // detached data; the worker never reads a live scene/document. Completion
    // confirms the latest queued snapshot, not just dispatch or an older write.
    internal sealed class LatestCheckpoint<T> where T:class
    {
        readonly object gate=new();
        readonly Func<T,string> save;
        T waiting;bool closed;
        internal Task<string> Completion {get;}
        internal LatestCheckpoint(Task previous,T snapshot,Func<T,string> save)
        {
            this.save=save??throw new ArgumentNullException(nameof(save));
            waiting=snapshot??throw new ArgumentNullException(nameof(snapshot));
            Completion=Task.Run(()=>Run(previous));
        }
        internal bool TryQueue(T snapshot)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            lock(gate){if(closed)return false;waiting=snapshot;return true;}
        }
        async Task<string> Run(Task previous)
        {
            // An older failed checkpoint does not discard newer accepted state.
            if(previous!=null)try{await previous.ConfigureAwait(false);}catch(Exception){}
            string error=null;
            while(true){
                T next;
                lock(gate){next=waiting;waiting=null;if(next==null){closed=true;return error;}}
                try{error=save(next);}
                catch(Exception){error="The latest checkpoint could not be saved; accepted changes remain unconfirmed.";}
            }
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
namespace Maestro.Quest.Persistence
{
    // Process-local ownership persists past GameObject destruction while old workers drain.
    // A new process has no surviving workers; durable stores reconcile its interrupted records.
    internal static class WorkspaceHostOwnership
    {
        static readonly Dictionary<string,Lease> owners=new(Path.DirectorySeparatorChar=='\\'?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        sealed class Lease:IDisposable
        {
            readonly string key;bool disposed;internal Lease(string key){this.key=key;}
            public void Dispose(){lock(owners){if(disposed)return;disposed=true;if(owners.TryGetValue(key,out var current)&&ReferenceEquals(current,this))owners.Remove(key);}}
        }
        internal static IDisposable TryAcquire(string directory)
        {
            string key=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            lock(owners){if(owners.ContainsKey(key))return null;var lease=new Lease(key);owners.Add(key,lease);return lease;}
        }
    }
}

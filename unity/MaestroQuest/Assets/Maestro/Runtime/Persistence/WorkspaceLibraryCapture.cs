// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace Maestro.Quest.Persistence
{
    // A short-lived writer exclusion owned by an archive worker, never by Unity's pause/resume state.
    internal sealed class WorkspaceLibraryCapture:IDisposable
    {
        Action release;
        readonly Action<Dictionary<string,byte[]>,Dictionary<string,Func<Stream>>> collect;
        internal WorkspaceLibraryCapture(Action release,Action<Dictionary<string,byte[]>,Dictionary<string,Func<Stream>>> collect){this.release=release;this.collect=collect;}
        internal void Collect(Dictionary<string,byte[]> documents,Dictionary<string,Func<Stream>> assets){if(release==null)throw new ObjectDisposedException(nameof(WorkspaceLibraryCapture));collect(documents,assets);}
        public void Dispose()=>Interlocked.Exchange(ref release,null)?.Invoke();
        internal static Stream Open(string path){WorkspaceArchive.NoLink(path);return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);}
        internal static byte[] ReadDocument(string path,int maximum)
        {
            using var input=Open(path);if(input.Length<1||input.Length>maximum)throw new InvalidDataException("Model information exceeds its archive limit.");var bytes=new byte[(int)input.Length];int at=0;while(at<bytes.Length){int n=input.Read(bytes,at,bytes.Length-at);if(n==0)throw new EndOfStreamException();at+=n;}if(input.ReadByte()!=-1)throw new IOException("Document changed during capture.");return bytes;
        }
    }
}

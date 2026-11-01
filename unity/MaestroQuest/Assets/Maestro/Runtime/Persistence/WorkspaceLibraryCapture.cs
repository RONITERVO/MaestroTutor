// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
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
        // Downloads and retained metadata have independent lifetimes. Capture every supported
        // sidecar, including metadata-only identities, under the library's writer exclusion.
        // A global entry limit also bounds filesystem enumeration before documents allocate.
        internal static void CollectAssetLibrary(string directory,string prefix,string extension,int maximumFiles,long maximumBytes,Dictionary<string,byte[]> documents,Dictionary<string,Func<Stream>> assets)
        {
            WorkspaceFileInventory.Parents(directory);string kind=WorkspaceFileInventory.Kind(directory);
            if(kind=="absent")return;if(kind!="directory")throw new InvalidDataException("Asset library is not a directory.");
            var paths=Directory.EnumerateFileSystemEntries(directory).Take(WorkspaceArchive.MaximumEntries+1).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            if(paths.Length>WorkspaceArchive.MaximumEntries)throw new InvalidDataException("The asset library exceeds its bounded archive inventory. Its files are preserved.");
            int files=0;long bytes=0;
            foreach(string path in paths){
                string name=Path.GetFileName(path),suffix=Path.GetExtension(name);kind=WorkspaceFileInventory.Kind(path);
                bool payload=string.Equals(suffix,extension,StringComparison.OrdinalIgnoreCase),metadata=string.Equals(suffix,".txt",StringComparison.OrdinalIgnoreCase);
                if(!payload&&!metadata)continue;
                string hash=Path.GetFileNameWithoutExtension(name);
                if(kind!="file"||!ModelLibrary.ValidHash(hash)||name!=hash+(payload?extension:".txt"))throw new InvalidDataException("Invalid asset library archive entry. Its files are preserved.");
                string relative=prefix+name;int limit=WorkspaceArchiveMetadata.Limit(relative);
                if(documents.Count+assets.Count>=WorkspaceArchive.MaximumEntries)throw new InvalidDataException("Workspace archive has too many entries. Its source files are preserved.");
                if(metadata)documents.Add(relative,ReadDocument(path,limit));
                else {
                    long length=new FileInfo(path).Length;bytes=checked(bytes+length);
                    if(++files>maximumFiles||length<1||length>limit||bytes>maximumBytes)throw new InvalidDataException("The asset library exceeds its archive payload budget. Its files are preserved.");
                    assets.Add(relative,()=>Open(path));
                }
            }
        }
        internal static Stream Open(string path){WorkspaceFileInventory.Parents(Path.GetDirectoryName(path));WorkspaceArchive.NoLink(path);return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);}
        internal static byte[] ReadDocument(string path,int maximum)
        {
            using var input=Open(path);if(input.Length<1||input.Length>maximum)throw new InvalidDataException("Asset information exceeds its archive limit.");var bytes=new byte[(int)input.Length];int at=0;while(at<bytes.Length){int n=input.Read(bytes,at,bytes.Length-at);if(n==0)throw new EndOfStreamException();at+=n;}if(input.ReadByte()!=-1)throw new IOException("Document changed during capture.");return bytes;
        }
    }
}

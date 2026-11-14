// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

namespace Maestro.Quest.Persistence
{
    // Worker-only maintenance for the private preparation family of one exact
    // room primary. No path supplied by a caller is accepted as a deletion target.
    internal sealed class PreparedStageInventory
    {
        internal const int MaximumStages=4,MaximumStageBytes=4*1024*1024,MaximumEntries=32;
        internal const string CapacityError="Unfinished room save preparations need review. Inspect saved preparations before trying another edit.";
        internal sealed class Entry
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal string Name,Hash,Reason;internal long Bytes;internal bool Eligible;
        }
        readonly string primary,directory;
        internal readonly string Id=Guid.NewGuid().ToString("N");
        internal readonly Entry[] Entries;
        internal readonly bool More;
        int consumed;
        PreparedStageInventory(string primary,Entry[] entries,bool more)
        {this.primary=primary;directory=Path.GetDirectoryName(primary);Entries=entries;More=more;}
        static IEnumerable<string> Paths(string primary)=>Directory.EnumerateFileSystemEntries(
            Path.GetDirectoryName(primary),Path.GetFileName(primary)+".prepare.*");
        internal static FileStream Reserve(string primary,CancellationToken cancellation,out string path)
        {
            path=null;cancellation.ThrowIfCancellationRequested();
            string directory=Path.GetDirectoryName(primary);
            // This short reservation serializes the count and CreateNew across
            // stores/processes. Expensive writing/flush happens after releasing it.
            using var owner=RoomSnapshotTransaction.Enter(directory);
            cancellation.ThrowIfCancellationRequested();
            var existing=Paths(primary).Take(MaximumStages+1).ToArray();
            if(existing.Length>=MaximumStages)throw new IOException(CapacityError);
            foreach(string item in existing)
                if(WorkspaceFileInventory.Kind(item)!="file"||new FileInfo(item).Length>MaximumStageBytes)
                    throw new IOException(CapacityError);
            path=primary+".prepare."+Guid.NewGuid().ToString("N");
            return new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read);
        }
        static string Digest(FileStream input,CancellationToken cancellation)
        {
            long length=input.Length;
            if(length<0||length>MaximumStageBytes)throw new IOException("Preparation exceeds its maintenance limit.");
            using var sha=SHA256.Create();var buffer=new byte[65536];long read=0;int count;
            while((count=input.Read(buffer,0,(int)Math.Min(buffer.Length,length-read+1)))>0){
                cancellation.ThrowIfCancellationRequested();read+=count;if(read>length)throw new IOException("Preparation changed during inspection.");
                sha.TransformBlock(buffer,0,count,buffer,0);
            }
            if(read!=length)throw new IOException("Preparation changed during inspection.");
            sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
            return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
        }
        internal static PreparedStageInventory Inspect(string primary,CancellationToken cancellation=default,Action<string> checkpoint=null)
        {
            cancellation.ThrowIfCancellationRequested();
            string directory=Path.GetDirectoryName(primary);
            string[] paths;
            using(var owner=RoomSnapshotTransaction.Inspect(directory)){
                if(!Directory.Exists(directory))return new PreparedStageInventory(primary,Array.Empty<Entry>(),false);
                paths=Paths(primary).Take(MaximumEntries+1).ToArray();
            }
            var entries=new List<Entry>();
            foreach(string path in paths.Take(MaximumEntries)){
                cancellation.ThrowIfCancellationRequested();
                var entry=new Entry{Name=Path.GetFileName(path),Reason="Unavailable, unsafe or active preparation; its files are preserved."};
                entries.Add(entry);
                try {
                    FileStream input;
                    using(var owner=RoomSnapshotTransaction.Inspect(directory)){
                        if(WorkspaceFileInventory.Kind(path)!="file")continue;
                        entry.Bytes=new FileInfo(path).Length;
                        // Pin only an inactive file while briefly coordinated with
                        // creation/publication. Hash outside the global room lock.
                        input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None);
                    }
                    using(input){
                        checkpoint?.Invoke("opened");
                        entry.Hash=Digest(input,cancellation);entry.Eligible=true;entry.Reason="";
                    }
                } catch(Exception failure) when(failure is IOException||failure is UnauthorizedAccessException||failure is NotSupportedException) {}
            }
            return new PreparedStageInventory(primary,entries.ToArray(),paths.Length>MaximumEntries);
        }
        internal bool Matches(string inspectionId,string entryId,string hash,out Entry entry)
        {
            entry=Entries.SingleOrDefault(x=>x.Id==entryId);
            return Volatile.Read(ref consumed)==0&&Id==inspectionId&&entry!=null&&entry.Eligible&&entry.Hash==hash;
        }
        internal void Discard(string inspectionId,string entryId,string hash,string confirmation,CancellationToken cancellation=default,Action<string> checkpoint=null)
        {
            if(!Matches(inspectionId,entryId,hash,out var entry)||confirmation!="discard "+entryId)
                throw new InvalidOperationException("Review this exact preparation and explicitly confirm its permanent disposal.");
            // Every real attempt consumes this review, including cancellation/failure.
            if(Interlocked.Exchange(ref consumed,1)!=0)throw new InvalidOperationException("This preparation review was already used.");
            cancellation.ThrowIfCancellationRequested();
            using var owner=RoomSnapshotTransaction.Enter(directory);
            cancellation.ThrowIfCancellationRequested();
            string path=Path.Combine(directory,entry.Name);
            if(WorkspaceFileInventory.Kind(path)!="file")throw new IOException("The reviewed preparation changed.");
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){
                if(input.Length!=entry.Bytes||Digest(input,cancellation)!=hash)throw new IOException("The reviewed preparation changed.");
            }
            checkpoint?.Invoke("verified");cancellation.ThrowIfCancellationRequested();
            string claimed=path+".discard."+Guid.NewGuid().ToString("N");
            File.Move(path,claimed);checkpoint?.Invoke("claimed");
            // Prove the claimed bytes again. A substitution or partial rename
            // remains evidence; never delete an unexpected claimed file.
            if(WorkspaceFileInventory.Kind(claimed)!="file")throw new IOException("The claimed preparation changed.");
            using(var input=new FileStream(claimed,FileMode.Open,FileAccess.Read,FileShare.None))
                if(input.Length!=entry.Bytes||Digest(input,cancellation)!=hash)throw new IOException("The claimed preparation changed.");
            cancellation.ThrowIfCancellationRequested();File.Delete(claimed);
        }
    }
}

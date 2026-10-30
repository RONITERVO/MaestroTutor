// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Maestro.Quest.Persistence
{
    // Private preparation, never a recoverable/accepted room. The live owner pins
    // its bytes against ordinary writers. A crash leaves evidence, not a new primary.
    internal sealed class PreparedFileStage:IDisposable
    {
        readonly string path;
        readonly byte[] fingerprint;
        readonly int length;
        FileStream handle;
        bool moved;
        PreparedFileStage(string path,byte[] bytes,FileStream handle)
        {this.path=path;length=bytes.Length;this.handle=handle;using var sha=SHA256.Create();fingerprint=sha.ComputeHash(bytes);}
        internal static PreparedFileStage Create(string primary,byte[] bytes,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if(bytes==null||bytes.Length<1||bytes.Length>PreparedStageInventory.MaximumStageBytes)
                throw new ArgumentException("Prepared room bytes exceed their staging limit.",nameof(bytes));
            var handle=PreparedStageInventory.Reserve(primary,cancellation,out string path);
            PreparedFileStage stage=null;
            try {
                stage=new PreparedFileStage(path,bytes,handle);
                for(int offset=0;offset<bytes.Length;offset+=65536){
                    handle.Write(bytes,offset,Math.Min(65536,bytes.Length-offset));
                }
                handle.Flush(true);cancellation.ThrowIfCancellationRequested();
                // Inspection can take an exclusive inactive-file pin. Do not
                // expose a live stage between its writing and read-only handles.
                using(var owner=RoomSnapshotTransaction.Enter(Path.GetDirectoryName(primary))){
                    handle.Dispose();
                    stage.handle=handle=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                }
                cancellation.ThrowIfCancellationRequested();return stage;
            } catch {
                // Remove only the exact complete candidate. Failed partial writes
                // or a substituted path remain evidence; never guess ownership.
                handle.Dispose();stage?.Dispose();throw;
            }
        }
        bool Matches()
        {
            WorkspaceFileInventory.Parents(Path.GetDirectoryName(path));
            if(WorkspaceFileInventory.Kind(path)!="file")return false;
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            if(input.Length!=length)return false;
            using var sha=SHA256.Create();var actual=sha.ComputeHash(input);
            if(input.Position!=length)return false;
            return System.Linq.Enumerable.SequenceEqual(actual,fingerprint);
        }
        internal bool Ready=>!moved&&handle!=null&&Matches();
        internal void MoveTo(string pending)
        {
            if(moved||handle==null||!Matches())throw new IOException("Prepared room bytes changed; preserve the staging file.");
            // No overwriting: an unfinished writer's .pending file is not ours.
            handle.Dispose();handle=null;File.Move(path,pending);moved=true;
        }
        public void Dispose()
        {
            handle?.Dispose();handle=null;
            if(moved)return;
            try {if(Matches())File.Delete(path);}
            catch(IOException){}catch(UnauthorizedAccessException){}catch(NotSupportedException){}
        }
    }
}

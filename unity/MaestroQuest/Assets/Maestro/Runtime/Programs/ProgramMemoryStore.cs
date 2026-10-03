// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;

namespace Maestro.Quest.Programs
{
    /// <summary>Private workspace remembered values.
    /// Accepted writes drain off-thread even if a future caller stops waiting.</summary>
    internal sealed partial class ProgramMemoryStore
    {
        internal const string FileName="program-memory.v1.json";
        internal sealed class Result
        {
            internal string Revision {get;set;}
            internal string Error {get;set;}
            internal bool Changed {get;set;}
        }
        sealed class OwnerChanged:IOException {internal OwnerChanged(string message):base(message){}}
        // A competing memory cache must fail promptly; ordinary room saves still queue
        // behind the shared owner. Claims exist only while a memory write is pending.
        static readonly ConcurrentDictionary<string,byte> writerClaims=new(Path.DirectorySeparatorChar=='\\'?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        readonly object sync=new();
        readonly string directory,path;
        readonly WorkspaceWriteGate writes;
        readonly Action<string> fault;
        ProgramMemoryDocument document;
        Task<Result> pending;
        string unavailable;
        bool ready,busy;
        internal Task Initialization {get;}
        internal bool Ready {get {lock(sync)return ready;}}
        internal bool Pending {get {lock(sync)return busy;}}
        internal string Error {get {lock(sync)return unavailable;}}
        internal ProgramMemoryStore(string directory,WorkspaceWriteGate writes,Action<string> fault=null)
        {
            this.directory=Path.GetFullPath(directory);
            if(this.directory.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Length<=Path.GetPathRoot(this.directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Length)throw new ArgumentException("Use a workspace directory below the drive root.");
            this.directory=this.directory.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            path=Path.Combine(this.directory,FileName);
            this.writes=writes??throw new ArgumentNullException(nameof(writes));this.fault=fault;
            Initialization=Task.Run(()=>{
                ProgramMemoryDocument loaded=null;string error=null;
                try{using var owner=RoomSnapshotTransaction.Enter(this.directory,recover:true,initialize:false);loaded=ReadSavedOwned(this.directory);}catch(Exception e){error="Saved program memory is unavailable; its files are preserved. "+e.Message;}
                lock(sync){document=durable=loaded;unavailable=error;ready=true;}
            });
        }
        internal ProgramMemoryDocument Snapshot(){lock(sync){CheckReady();return document;}}
        void CheckReady()
        {
            if(!ready)throw new InvalidOperationException("Program memory is loading.");
            if(unavailable!=null)throw new InvalidOperationException(unavailable);
        }
        void CheckPaths()=>CheckPaths(directory);
        static void CheckPaths(string directory)
        {
            WorkspaceFileInventory.Parents(directory);
            if(WorkspaceFileInventory.Kind(directory)=="absent")return;
            var candidates=Directory.EnumerateFileSystemEntries(directory,"program-memory.v*").Take(65).ToArray();
            if(candidates.Length>64)throw new InvalidDataException("Too many retained program memory files.");
            foreach(string file in candidates){
                string name=Path.GetFileName(file);
                if(WorkspaceFileInventory.Kind(file)!="file")throw new InvalidDataException("Program memory path is not a regular file.");
                if(name!=FileName&&name!=FileName+".backup"&&!(name.StartsWith(FileName+".pending.",StringComparison.Ordinal)&&ProgramMemoryDocument.Id(name.Substring((FileName+".pending.").Length))))
                    throw new InvalidDataException("Unrecognized program memory format; use the app version that wrote it.");
            }
        }
        static ProgramMemoryDocument Read(string file)
        {
            if(WorkspaceFileInventory.Kind(file)!="file")throw new InvalidDataException("Program memory is not a regular file.");
            using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
            if(stream.Length<1||stream.Length>ProgramMemoryDocument.MaximumBytes)throw new InvalidDataException("Invalid program memory file size.");
            var bytes=new byte[(int)stream.Length];int offset=0;
            while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
            if(stream.ReadByte()!=-1)throw new IOException("Program memory changed while reading.");return ProgramMemoryDocument.Decode(bytes);
        }
        ProgramMemoryDocument ReadCurrent()=>ReadSaved(directory);
        internal static ProgramMemoryDocument ReadSaved(string directory)
        {
            using var owner=RoomSnapshotTransaction.Inspect(directory);return ReadSavedOwned(directory);
        }
        static ProgramMemoryDocument ReadSavedOwned(string directory)
        {
            CheckPaths(directory);string path=Path.Combine(directory,FileName);string kind=WorkspaceFileInventory.Kind(path);
            if(kind=="file")return Read(path);
            if(kind!="absent")throw new InvalidDataException("Program memory is not a regular file.");
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory,"program-memory.v*").Any())
                throw new InvalidDataException("Only retained memory remains; recover it explicitly instead of resetting values.");
            return ProgramMemoryDocument.Empty();
        }
        internal Task<Result> Write(string expectedRevision,string program,IReadOnlyDictionary<string,ProgramMemoryDocument.Cell> values)
        {
            if(!ProgramMemoryDocument.ProgramId(program)||values==null||values.Count<1||values.Count>ProgramMemoryDocument.MaximumWriteCells)throw new InvalidDataException("Invalid memory checkpoint.");
            var captured=new Dictionary<string,ProgramMemoryDocument.Cell>(values);
            if(captured.Any(x=>!ProgramMemoryDocument.Id(x.Key)||x.Value==null))throw new InvalidDataException("Invalid memory variable.");
            return Start(expectedRevision,current=>current.WithValues(program,captured));
        }
        internal Task<Result> Reset(string expectedRevision,string program,string cell=null)
        {
            if(!ProgramMemoryDocument.ProgramId(program)||cell!=null&&!ProgramMemoryDocument.Id(cell))throw new InvalidDataException("Invalid memory reset.");
            return Start(expectedRevision,current=>current.Forget(program,cell));
        }
        Task<Result> Start(string expectedRevision,Func<ProgramMemoryDocument,ProgramMemoryDocument> change)
        {
            lock(sync){
                CheckReady();if(busy)throw new InvalidOperationException("Wait for the accepted memory write to finish.");
                if(expectedRevision!=document.Revision)throw new InvalidOperationException("Program memory changed; inspect it before writing.");
                var earlier=document;bool transient=temporary;
                // Even identical/reset-absent requests check the on-disk identity.
                // A freeze denies every mutation request, including these no-ops.
                var lease=writes.Write();busy=true;
                pending=Task.Run(()=>{
                    string error=null;bool changed=false,claimed=false;var candidate=earlier;
                    try {
                        // Whole-document encoding/hashing stays off Unity's owner thread.
                        candidate=change(earlier);
                        if(transient)changed=candidate.Identity!=earlier.Identity;
                        else {
                        if(!writerClaims.TryAdd(directory,0))throw new OwnerChanged("Another memory owner is saving; reopen memory before writing.");
                        claimed=true;
                        using var pairOwner=RoomSnapshotTransaction.Enter(directory);
                        CheckPaths();Directory.CreateDirectory(directory);CheckPaths();
                        string lockPath=Path.Combine(directory,"program-memory.writer.lock");
                        if(WorkspaceFileInventory.Kind(lockPath)=="directory")throw new IOException("Program memory writer is unavailable.");
                        using var owner=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
                        void Match(){
                            try{if(ReadSavedOwned(directory).Identity==earlier.Identity)return;}
                            catch(Exception){throw new OwnerChanged("Program memory cannot be revalidated; reopen it before writing.");}
                            throw new OwnerChanged("Program memory changed outside this owner; reopen it before writing.");
                        }
                        Match();
                        if(candidate.Identity!=earlier.Identity){
                            if(WorkspaceFileInventory.Kind(path+".backup")!="absent")_=Read(path+".backup"); // Never overwrite damaged retained evidence.
                            string temporary=path+".pending."+Guid.NewGuid().ToString("N");
                            try {
                                var bytes=candidate.Encode();using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                                fault?.Invoke("written");
                                // Our own pending file is not evidence of an earlier lost primary.
                                CheckPaths();
                                if(earlier.Revision==ProgramMemoryDocument.InitialRevision&&!File.Exists(path)){
                                    if(File.Exists(path+".backup")||Directory.EnumerateFiles(directory,FileName+".pending.*").Any(p=>p!=temporary))throw new IOException("Retained program memory appeared during the write.");
                                }else Match();
                                fault?.Invoke("before-publish");
                                if(File.Exists(path))Maestro.Quest.Persistence.FilePublication.Replace(temporary,path,path+".backup");else File.Move(temporary,path);
                                changed=true;
                            }finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
                        }
                        }
                        lock(sync){document=candidate;if(!transient)durable=candidate;}
                    }catch(Exception e){
                        error="Program memory checkpoint was not confirmed; inspect it before retrying. "+e.Message;
                        // A failed path/format check can happen before Match. Do not
                        // keep presenting a cached value as usable after that change.
                        bool current=!claimed;if(claimed)try{current=ReadCurrent().Identity==earlier.Identity;}catch(Exception){}
                        if(!current||e is OwnerChanged)lock(sync)unavailable=error;
                    }
                    finally {if(claimed)writerClaims.TryRemove(directory,out _);lock(sync){busy=false;lease.Dispose();}}
                    return new Result {Revision=changed?candidate.Revision:earlier.Revision,Changed=changed,Error=error};
                });
                return pending;
            }
        }
        // Called by the existing background retained-save audit. Unknown or changing
        // evidence protects referenced downloads; strings never grant object authority.
        internal bool Retains(string id,out bool uncertain)
        {
            ProgramMemoryDocument before;ProgramMemoryDocument[] pinned;int revision;lock(sync){before=document;pinned=pins.Keys.Append(durable).Where(x=>x!=null).ToArray();revision=retentionRevision;uncertain=!ready||busy||unavailable!=null;}
            bool retained=before?.Retains(id)==true||pinned.Any(x=>x.Retains(id));
            try{using var owner=RoomSnapshotTransaction.Inspect(directory,wait:false);CheckPaths();if(Directory.Exists(directory))foreach(string file in Directory.EnumerateFiles(directory,"program-memory.v*"))retained|=Read(file).Retains(id);}
            catch(Exception){uncertain=true;}
            lock(sync)uncertain|=busy||unavailable!=null||document!=before||retentionRevision!=revision;return retained;
        }
        internal Task Drain(){lock(sync)return pending??Initialization;}
    }
}

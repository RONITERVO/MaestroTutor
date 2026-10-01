// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    // No repair journal to repair recursively: immutable evidence precedes one atomic rename.
    // Only latest.json (or a file obstructing its parent directory) moves. Captures stay in place.
    internal sealed class WorkspaceHistoryArchive
    {
        internal const long MaxSourceBytes=16*1024*1024,MaxEvidenceBytes=128*1024*1024;
        readonly string root,evidence;internal Action<string> Fault;
        internal WorkspaceHistoryArchive(string applicationData){root=Path.GetFullPath(applicationData);evidence=Path.Combine(root,"workspace-history.v1","evidence");}
        internal sealed class Snapshot
        {
            internal string Target,Location,Kind,Fingerprint;internal byte[] Accepted;internal JArray Entries;
            internal int Files;internal long Bytes;
            internal JObject Manifest()=>new() {["version"]=1,["target"]=Target,["location"]=Location,["kind"]=Kind,["acceptedHash"]=Hash(Accepted),["entries"]=Entries.DeepClone()};
        }
        internal static bool ValidTarget(string target)=>target is "activation" or "review" or "recovery";
        internal static byte[] Accepted(JToken value)
        {
            var bytes=new UTF8Encoding(false,true).GetBytes((value??JValue.CreateNull()).ToString(Formatting.None));
            if(bytes.Length>65536)throw new InvalidDataException("Accepted history exceeds its preservation limit.");return bytes;
        }
        static string Hash(byte[] bytes){using var sha=SHA256.Create();return ConvertHash(sha.ComputeHash(bytes));}
        static string ConvertHash(byte[] hash)=>BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();
        static string Kind(string path)
        {
            try {var attributes=File.GetAttributes(path);if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked history paths are not supported.");return (attributes&FileAttributes.Directory)!=0?"directory":"file";}
            catch(FileNotFoundException){return "absent";}catch(DirectoryNotFoundException){return "absent";}
        }
        // Check every existing parent, including the application root. Never traverse a reparse point.
        static void Parents(string path){for(string p=path;p!=null;p=Path.GetDirectoryName(p)){string kind=Kind(p);if(kind=="file")throw new IOException("A history parent is not a directory.");}}
        static void Scan(string path,string relative,JArray entries,ref int files,ref long bytes,long maxBytes,int maxEntries,bool hash,CancellationToken token,int depth=0)
        {
            token.ThrowIfCancellationRequested();if(depth>(hash?6:10)||relative.Length>512||entries.Count>=maxEntries)throw new InvalidDataException("History inventory exceeds its preservation limit.");
            string kind=Kind(path);var entry=new JObject {["path"]=relative,["kind"]=kind};entries.Add(entry);
            if(kind=="file"){
                using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);long length=input.Length;
                bytes=checked(bytes+length);if(bytes>maxBytes)throw new InvalidDataException("History bytes exceed their preservation limit.");files++;entry["bytes"]=length;
                if(hash){using var sha=SHA256.Create();var buffer=new byte[65536];long read=0;int count;while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();read+=count;if(read>length)throw new IOException("History changed during inspection.");sha.TransformBlock(buffer,0,count,buffer,0);}sha.TransformFinalBlock(Array.Empty<byte>(),0,0);if(read!=length)throw new IOException("History changed during inspection.");entry["sha256"]=ConvertHash(sha.Hash);}
            }else if(kind=="directory"){
                var children=Directory.EnumerateFileSystemEntries(path).Take(maxEntries+1).OrderBy(p=>Path.GetFileName(p),StringComparer.Ordinal).ToArray();
                if(children.Length>maxEntries)throw new InvalidDataException("History inventory exceeds its preservation limit.");
                foreach(string child in children)Scan(child,relative==""?Path.GetFileName(child):relative+"/"+Path.GetFileName(child),entries,ref files,ref bytes,maxBytes,maxEntries,hash,token,depth+1);
            }
        }
        internal Snapshot Inspect(string target,byte[] accepted,CancellationToken token)
        {
            if(!ValidTarget(target)||accepted==null||accepted.Length>65536)throw new ArgumentException("Invalid history inspection.");Parents(root);
            string parent=Path.Combine(root,"workspace-"+target+".v1"),kind=Kind(parent);
            string location=kind=="file"?"parent":"latest.json",source=location=="parent"?parent:Path.Combine(parent,"latest.json");
            var result=new Snapshot {Target=target,Location=location,Kind=Kind(source),Accepted=(byte[])accepted.Clone(),Entries=new JArray()};
            Scan(source,"",result.Entries,ref result.Files,ref result.Bytes,MaxSourceBytes,256,true,token);
            result.Fingerprint=Hash(Encoding.UTF8.GetBytes(result.Manifest().ToString(Formatting.None)));return result;
        }
        string Source(Snapshot value){string parent=Path.Combine(root,"workspace-"+value.Target+".v1");return value.Location=="parent"?parent:Path.Combine(parent,"latest.json");}
        void Same(Snapshot expected,byte[] accepted,CancellationToken token){if(Inspect(expected.Target,accepted,token).Fingerprint!=expected.Fingerprint)throw new InvalidOperationException("History changed. Inspect it again before resetting.");}
        static void Write(string path,byte[] bytes){using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(bytes,0,bytes.Length);file.Flush(true);}
        internal string Reset(Snapshot snapshot,byte[] accepted,CancellationToken token)
        {
            Same(snapshot,accepted,token);Parents(Path.GetDirectoryName(evidence));
            // Inventory is bounded and does not follow links. Partial evidence is retained too.
            var entries=new JArray();int files=0;long bytes=0;Scan(evidence,"",entries,ref files,ref bytes,MaxEvidenceBytes,4096,false,token);
            byte[] manifest=Encoding.UTF8.GetBytes(snapshot.Manifest().ToString(Formatting.None));
            if(bytes+snapshot.Bytes+accepted.Length+manifest.Length>MaxEvidenceBytes||entries.Count+snapshot.Entries.Count+4>4096)throw new IOException("History evidence is full. Preserve/export it before repairing more history.");
            Directory.CreateDirectory(evidence);Parents(evidence);
            string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(evidence,id);Directory.CreateDirectory(folder);
            Write(Path.Combine(folder,"accepted.json"),accepted);Write(Path.Combine(folder,"manifest.json"),manifest);
            Fault?.Invoke("history.beforeMove");Same(snapshot,accepted,token);token.ThrowIfCancellationRequested();
            string source=Source(snapshot),destination=Path.Combine(folder,"original");
            try {if(snapshot.Kind=="directory")Directory.Move(source,destination);else if(snapshot.Kind=="file")File.Move(source,destination);}
            catch {if(Kind(destination)=="absent"||Kind(source)!="absent")throw;}
            // Once renamed, a late cancellation or acknowledgement failure cannot undo preservation.
            try{Fault?.Invoke("history.afterMove");}catch(Exception){}
            return id;
        }
    }
}

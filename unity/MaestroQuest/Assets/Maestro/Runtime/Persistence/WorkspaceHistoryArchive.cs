// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using static Maestro.Quest.Persistence.WorkspaceFileInventory;
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
            var entries=new JArray();int files=0;long bytes=0;Scan(evidence,"",entries,ref files,ref bytes,MaxEvidenceBytes,4096,false,token,maxDepth:10);
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

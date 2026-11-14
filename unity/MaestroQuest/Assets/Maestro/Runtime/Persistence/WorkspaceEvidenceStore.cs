// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Persistence.WorkspaceFileInventory;
namespace Maestro.Quest.Persistence
{
    // Diagnostic bundles deliberately differ from playable workspace archives. Raw status bytes
    // are never interpreted or replayed. Payload names are numeric, not untrusted filesystem names.
    internal sealed class WorkspaceEvidenceStore
    {
        internal const long MaximumBytes=128L*1024*1024;
        internal const int MaximumEntries=4096;
        readonly string root;internal Action<string> Fault;
        internal WorkspaceEvidenceStore(string applicationData){root=Path.Combine(Path.GetFullPath(applicationData),"workspace-history.v1","evidence");}
        internal sealed class Entry
        {
            internal string Id,Fingerprint;internal JArray Nodes;internal int Files;internal long Bytes;
            internal JObject Manifest()=>new() {["format"]="maestro-evidence",["version"]=1,["kind"]="history",["evidenceId"]=Id,["entries"]=Nodes.DeepClone()};
            internal JObject View()=>new() {["kind"]="history",["evidenceId"]=Id,["fingerprint"]=Fingerprint,["files"]=Files,["sizeKiB"]=(Bytes+1023)/1024};
        }
        internal sealed class Bundle:IDisposable
        {
            internal string Path,Hash;internal long Bytes;
            public void Dispose(){try{File.Delete(Path);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        static bool Id(string id)=>id!=null&&System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-f0-9]{32}$");
        string Source(string id){if(!Id(id))throw new InvalidDataException("Invalid evidence identity.");return Path.Combine(root,id);}
        internal Entry Inspect(string id,CancellationToken token)
        {
            Parents(root);string source=Source(id);if(Kind(source)!="directory")throw new IOException("This evidence is no longer available.");
            var value=new Entry {Id=id,Nodes=new JArray()};Scan(source,"",value.Nodes,ref value.Files,ref value.Bytes,MaximumBytes,MaximumEntries,true,token,maxDepth:8);
            value.Fingerprint=Hash(Encoding.UTF8.GetBytes(value.Manifest().ToString(Formatting.None)));return value;
        }
        internal Entry[] List(CancellationToken token)
        {
            Parents(root);if(Kind(root)=="absent")return Array.Empty<Entry>();
            var paths=Directory.EnumerateFileSystemEntries(root).Take(MaximumEntries+1).OrderBy(Path.GetFileName,StringComparer.Ordinal).ToArray();
            if(paths.Length>MaximumEntries)throw new InvalidDataException("Evidence inventory is too large.");
            var result=new List<Entry>();long bytes=0;int nodes=0;
            foreach(string path in paths){token.ThrowIfCancellationRequested();var item=Inspect(Path.GetFileName(path),token);bytes=checked(bytes+item.Bytes);nodes+=item.Nodes.Count;if(bytes>MaximumBytes||nodes>MaximumEntries)throw new InvalidDataException("Evidence inventory exceeds its supported limit.");result.Add(item);}return result.ToArray();
        }
        void Same(Entry expected,CancellationToken token){if(Inspect(expected.Id,token).Fingerprint!=expected.Fingerprint)throw new InvalidOperationException("Evidence changed. Inspect it again before continuing.");}
        string NodePath(Entry value,JObject node)
        {
            string source=Source(value.Id),relative=(string)node["path"],path=Path.GetFullPath(Path.Combine(source,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(path!=source&&!path.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidDataException("Unsafe evidence path.");return path;
        }
        internal Bundle Capture(Entry expected,string cache,CancellationToken token)
        {
            Same(expected,token);Fault?.Invoke("evidence.beforeCapture");cache=Path.GetFullPath(cache);if(cache==root||cache.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new IOException("Export cache must be separate from evidence.");Parents(cache);Directory.CreateDirectory(cache);Parents(cache);
            var result=new Bundle {Path=Path.Combine(cache,"maestro-evidence-"+Guid.NewGuid().ToString("N")+".zip")};
            try {
                using(var output=new FileStream(result.Path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){
                        var manifest=expected.Manifest();manifest["fingerprint"]=expected.Fingerprint;var mapping=(JArray)manifest["entries"];
                        for(int i=0;i<mapping.Count;i++){
                            token.ThrowIfCancellationRequested();var node=(JObject)mapping[i];if((string)node["kind"]!="file")continue;string name="payload/"+i.ToString("D4");node["payload"]=name;
                            string path=NodePath(expected,node);Parents(Path.GetDirectoryName(path));if(Kind(path)!="file")throw new IOException("Evidence changed while exporting.");
                            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);using var entry=zip.CreateEntry(name,CompressionLevel.Optimal).Open();using var sha=SHA256.Create();var buffer=new byte[65536];long total=0;int count;
                            while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();total+=count;if(total>(long)node["bytes"])throw new IOException("Evidence changed while exporting.");sha.TransformBlock(buffer,0,count,buffer,0);entry.Write(buffer,0,count);}sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
                            string hash=BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();if(total!=(long)node["bytes"]||hash!=(string)node["sha256"])throw new IOException("Evidence changed while exporting.");
                        }
                        using var description=zip.CreateEntry("evidence-manifest.json",CompressionLevel.Optimal).Open();byte[] data=Encoding.UTF8.GetBytes(manifest.ToString(Formatting.None));description.Write(data,0,data.Length);
                    }
                    output.Flush(true);
                }
                token.ThrowIfCancellationRequested();using(var file=File.OpenRead(result.Path)){result.Bytes=file.Length;using var sha=SHA256.Create();result.Hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
                Same(expected,token);return result;
            }catch{result.Dispose();throw;}
        }
        internal void Remove(Entry expected,CancellationToken token)
        {
            Same(expected,token);Fault?.Invoke("evidence.beforeRemove");Same(expected,token);token.ThrowIfCancellationRequested();
            // From the first delete onward cancellation cannot roll back removal. Never delete an
            // uninspected child: nonrecursive directory deletion refuses unexpected new contents.
            foreach(JObject node in expected.Nodes.OrderByDescending(n=>((string)n["path"]).Length)){
                string path=NodePath(expected,node);Parents(Path.GetDirectoryName(path));if(Kind(path)!=(string)node["kind"])throw new IOException("Evidence changed during removal.");
                if((string)node["kind"]=="file")File.Delete(path);else Directory.Delete(path,false);if(path!=Source(expected.Id))Fault?.Invoke("evidence.removing");
            }
            // A lost acknowledgement never turns a known completed removal into a failed operation.
            try{Fault?.Invoke("evidence.removed");}catch(Exception){}
        }
    }
}

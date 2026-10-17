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
using Maestro.Quest.Imports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    internal sealed class CapturedRecoveryEvidence
    {
        internal string Path {get;} internal string Sha256 {get;} internal string AcceptedHash {get;} internal int RawFiles {get;} internal long RawBytes {get;}
        internal CapturedRecoveryEvidence(string path,string hash,string accepted,int files,long bytes){Path=path;Sha256=hash;AcceptedHash=accepted;RawFiles=files;RawBytes=bytes;}
    }
    /// <summary>Forensic bytes plus labelled accepted documents, not a runnable workspace archive.
    /// The caller must hold accepted edits and save dispatch and drain already dispatched writers.</summary>
    internal static class WorkspaceRecoveryEvidence
    {
        internal const int MaximumAcceptedBytes=12*1024*1024,MaximumFiles=4096;
        internal const long MaximumBytes=512L*1024*1024;
        static readonly UTF8Encoding Utf8=new(false,true);
        internal static byte[] EncodeAccepted(JObject value)
        {
            bool memory=value?["version"]?.Type==JTokenType.Integer&&(int)value["version"]==2;
            var documents=new[]{"room","behaviours","controls","activities"};
            var keys=new[]{"version","available","room","behaviours","controls","activities","temporaryRoom"}.Concat(memory?new[]{"programMemory","temporaryMemory"}:Array.Empty<string>()).ToArray();
            var flags=documents.Concat(memory?new[]{"programMemory"}:Array.Empty<string>()).ToArray();
            if(value==null||value.Count!=keys.Length||!keys.All(value.ContainsKey)||value["version"]?.Type!=JTokenType.Integer||((int)value["version"]!=1&&!memory)||value["available"] is not JObject available||available.Count!=flags.Length||flags.Any(x=>available[x]?.Type!=JTokenType.Boolean)||documents.Any(x=>value[x] is not JObject)||value["temporaryRoom"]?.Type is not (JTokenType.Object or JTokenType.Null))throw new InvalidDataException("Invalid accepted recovery documents.");
            if(memory){
                foreach(string name in new[]{"programMemory","temporaryMemory"}){
                    if(value[name]?.Type is not (JTokenType.Object or JTokenType.Null))throw new InvalidDataException("Invalid accepted memory document.");
                    if(value[name].Type==JTokenType.Object)_=Programs.ProgramMemoryDocument.Decode(Utf8.GetBytes(value[name].ToString(Formatting.None)));
                }
                if((bool)available["programMemory"]&&value["programMemory"].Type!=JTokenType.Object||value["temporaryRoom"].Type==JTokenType.Null&&value["temporaryMemory"].Type!=JTokenType.Null)throw new InvalidDataException("Invalid accepted memory availability.");
            }
            var bytes=Utf8.GetBytes(value.ToString(Formatting.None));if(bytes.Length>MaximumAcceptedBytes)throw new InvalidDataException("Accepted recovery documents exceed their limit.");return bytes;
        }
        static void PlainParents(string path)
        {
            for(var current=new DirectoryInfo(path);current!=null;current=current.Parent){if(File.Exists(current.FullName))throw new IOException("Recovery directory is a file.");if(Directory.Exists(current.FullName))WorkspaceArchive.NoLink(current.FullName);}
        }
        static string BelowDrive(string path)
        {
            string full=System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar);
            if(full.Length<=System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(path)).Length)throw new IOException("Use a private recovery directory below the drive root.");return full;
        }
        internal static CapturedRecoveryEvidence Write(string sourceDirectory,string outputDirectory,byte[] accepted,CancellationToken cancellation=default)
        {
            cancellation.ThrowIfCancellationRequested();if(accepted==null||accepted.Length<1||accepted.Length>MaximumAcceptedBytes)throw new InvalidDataException("Invalid accepted recovery document size.");
            string source=BelowDrive(sourceDirectory),output=BelowDrive(outputDirectory);
            var comparison=System.IO.Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
            if(output.Equals(source,comparison)||output.StartsWith(source+System.IO.Path.DirectorySeparatorChar,comparison))throw new IOException("Recovery output must be outside the workspace being preserved.");
            PlainParents(source);PlainParents(output);Directory.CreateDirectory(output);PlainParents(output);
            var files=new List<(string Path,string Name,long Bytes)>();long total=accepted.Length;int visited=0;
            void Walk(string folder,int depth)
            {
                if(depth>8)throw new InvalidDataException("Recovery data directories are too deep.");
                foreach(string path in Directory.EnumerateFileSystemEntries(folder)){
                    cancellation.ThrowIfCancellationRequested();if(++visited>MaximumFiles*2)throw new InvalidDataException("Too many recovery paths.");WorkspaceArchive.NoLink(path);
                    if(Directory.Exists(path)){Walk(path,depth+1);continue;}
                    string name=System.IO.Path.GetRelativePath(source,path).Replace(System.IO.Path.DirectorySeparatorChar,'/');
                    if(name.Length>512||name.Any(char.IsControl)||name.Split('/').Any(x=>x is "" or "." or "..")||name.Contains('\\')||name.Contains(':'))throw new InvalidDataException("Unsupported recovery file name.");
                    long length=new FileInfo(path).Length;total=checked(total+length);if(files.Count>=MaximumFiles||total>MaximumBytes)throw new InvalidDataException("Recovery data exceeds its preservation budget.");files.Add((path,"raw/"+name,length));
                }
            }
            if(Directory.Exists(source))Walk(source,0);files.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Name,b.Name));
            string nameRoot="maestro-recovery-"+Guid.NewGuid().ToString("N"),pending=System.IO.Path.Combine(output,nameRoot+".zip.partial"),final=System.IO.Path.Combine(output,nameRoot+".zip");bool created=false,moved=false;
            try {
                using(var file=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    created=true;using(var zip=new ZipArchive(file,ZipArchiveMode.Create,true,Utf8)){
                        var inventory=new JArray();var buffer=new byte[65536];string acceptedHash=ModelLibrary.Hash(accepted);
                        var entry=zip.CreateEntry("accepted.json",CompressionLevel.Fastest);using(var target=entry.Open())target.Write(accepted,0,accepted.Length);
                        foreach(var item in files){
                            cancellation.ThrowIfCancellationRequested();WorkspaceArchive.NoLink(item.Path);using var input=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.Read);if(input.Length!=item.Bytes)throw new IOException("Original recovery file changed during preservation.");
                            using var hash=SHA256.Create();var archived=zip.CreateEntry(item.Name,CompressionLevel.Fastest);using(var target=archived.Open()){
                                long copied=0;int length;while((length=input.Read(buffer,0,buffer.Length))>0){cancellation.ThrowIfCancellationRequested();copied+=length;if(copied>item.Bytes)throw new IOException("Original recovery file grew during preservation.");target.Write(buffer,0,length);hash.TransformBlock(buffer,0,length,null,0);}
                                if(copied!=item.Bytes)throw new IOException("Original recovery file was truncated during preservation.");hash.TransformFinalBlock(Array.Empty<byte>(),0,0);
                            }
                            inventory.Add(new JObject {["path"]=item.Name,["bytes"]=item.Bytes,["sha256"]=BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant()});
                        }
                        var manifest=new JObject {["format"]="maestro-workspace-recovery-evidence",["version"]=1,["accepted"]=new JObject {["bytes"]=accepted.Length,["sha256"]=acceptedHash},["files"]=inventory};
                        var bytes=Utf8.GetBytes(manifest.ToString(Formatting.None));var manifestEntry=zip.CreateEntry("manifest.json",CompressionLevel.Fastest);using var stream=manifestEntry.Open();stream.Write(bytes,0,bytes.Length);
                    }file.Flush(true);
                }
                cancellation.ThrowIfCancellationRequested();File.Move(pending,final);moved=true;using var verify=File.OpenRead(final);using var digest=SHA256.Create();string complete=BitConverter.ToString(digest.ComputeHash(verify)).Replace("-","").ToLowerInvariant();
                cancellation.ThrowIfCancellationRequested();return new CapturedRecoveryEvidence(final,complete,ModelLibrary.Hash(accepted),files.Count,total-accepted.Length);
            }catch{if(created&&File.Exists(pending))File.Delete(pending);if(moved&&File.Exists(final))File.Delete(final);throw;}
        }
    }
}

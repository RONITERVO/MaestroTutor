// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Imports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Persistence
{
    public sealed class WorkspaceArchiveReceipt
    {
        public string ManifestHash {get;internal set;}
        public WorkspaceArchiveSummary Summary {get;internal set;}
    }
    /// <summary>A completely verified, private staging directory. No live store is changed and no program
    /// is started. Keep this handle alive through preview; disposing abandons only this owned stage.</summary>
    public sealed class PreparedWorkspaceArchive:IDisposable
    {
        public string DirectoryPath {get;}
        public WorkspaceArchiveReceipt Receipt {get;}
        readonly string parent;readonly byte[] manifest;
        internal byte[] ManifestBytes=>(byte[])manifest.Clone();
        internal PreparedWorkspaceArchive(string directory,string parent,WorkspaceArchiveReceipt receipt,byte[] manifest){DirectoryPath=directory;this.parent=parent;Receipt=receipt;this.manifest=(byte[])manifest.Clone();}
        public void Dispose()
        {
            if(!Directory.Exists(DirectoryPath))return;
            if(Path.GetDirectoryName(Path.GetFullPath(DirectoryPath))!=parent||!Path.GetFileName(DirectoryPath).StartsWith("workspace-import-",StringComparison.Ordinal))throw new IOException("Unexpected workspace staging location.");
            WorkspaceArchive.NoLink(DirectoryPath);Directory.Delete(DirectoryPath,true);
        }
    }
    /// <summary>Versioned portable content, not a raw app-data dump. Does not capture live state, publish a
    /// download, activate a restore, migrate data, rebind IDs, or restore execution receipts/room scans.</summary>
    public static class WorkspaceArchive
    {
        public const int MaximumEntries=1400,MaximumManifestBytes=512*1024;
        public const long MaximumArchiveBytes=512L*1024*1024;
        static readonly UTF8Encoding Utf8=new(false,true);
        sealed class Entry {internal string Path,Hash;internal long Bytes;}
        static InvalidDataException Invalid(string message)=>new(message);
        internal static void NoLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw Invalid("Linked workspace paths are not supported.");}
        static byte[] Read(Stream source,int limit,CancellationToken cancellation)
        {
            using var output=new MemoryStream();var buffer=new byte[65536];int count;
            while((count=source.Read(buffer,0,Math.Min(buffer.Length,limit-(int)output.Length+1)))>0){cancellation.ThrowIfCancellationRequested();if(output.Length+count>limit)throw Invalid("Archive entry exceeds its limit.");output.Write(buffer,0,count);}
            cancellation.ThrowIfCancellationRequested();if(output.Length==0)throw Invalid("Empty archive entry.");return output.ToArray();
        }
        static WorkspaceArchiveSummary CopySummary(WorkspaceArchiveSummary v,long bytes)=>new() {Files=v.Files,Models=v.Models,Motions=v.Motions,Modules=v.Modules,UnavailablePrograms=v.UnavailablePrograms,MissingModels=(string[])v.MissingModels.Clone(),MissingMotions=(string[])v.MissingMotions.Clone(),MissingControllerPrograms=(string[])v.MissingControllerPrograms.Clone(),Bytes=bytes};
        sealed class Budget
        {
            internal long Total,Models,Motions;
            internal void Add(string path,long bytes)
            {
                if(bytes<1||bytes>WorkspaceArchiveMetadata.Limit(path))throw Invalid("Archive entry exceeds its limit.");
                Total=checked(Total+bytes);if(WorkspaceArchiveMetadata.IsModel(path))Models+=bytes;if(WorkspaceArchiveMetadata.IsMotion(path))Motions+=bytes;
                if(Total>MaximumArchiveBytes||Models>256L*1024*1024||Motions>MotionLibrary.MaximumDiskBytes)throw Invalid("Workspace archive exceeds its content budget.");
            }
        }
        // Leave the caller's stream open. The caller must publish only after this returns and its own
        // flush/close succeeds. A failure may leave partial ZIP bytes and is never a save receipt.
        public static WorkspaceArchiveReceipt Write(Stream destination,WorkspaceArchiveSnapshot snapshot,CancellationToken cancellation=default)
        {
            if(destination==null||!destination.CanWrite||snapshot==null)throw new ArgumentException("A writable archive stream and detached snapshot are required.");
            cancellation.ThrowIfCancellationRequested();WorkspaceArchiveReceipt receipt;
            using(var zip=new ZipArchive(new OutputLimit(destination),ZipArchiveMode.Create,true)) {
                receipt=Manifest(snapshot,(path,bytes)=>{
                    var entry=zip.CreateEntry(path,WorkspaceArchiveMetadata.IsAsset(path)?CompressionLevel.NoCompression:CompressionLevel.Optimal);
                    using var stream=entry.Open();for(int at=0;at<bytes.Length;at+=65536){cancellation.ThrowIfCancellationRequested();stream.Write(bytes,at,Math.Min(65536,bytes.Length-at));}
                },cancellation,out var manifest);
                using var output=zip.CreateEntry("manifest.json",CompressionLevel.Optimal).Open();output.Write(manifest,0,manifest.Length);
            }
            cancellation.ThrowIfCancellationRequested();return receipt;
        }
        // Review hashes use the same validation, ordering, limits and manifest as ZIP export,
        // without another 512 MB private file or a redundant compression/write pass.
        internal static WorkspaceArchiveReceipt Fingerprint(WorkspaceArchiveSnapshot snapshot,CancellationToken cancellation=default)=>Manifest(snapshot,null,cancellation,out _);
        static WorkspaceArchiveReceipt Manifest(WorkspaceArchiveSnapshot snapshot,Action<string,byte[]> write,CancellationToken cancellation,out byte[] manifest)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            cancellation.ThrowIfCancellationRequested();var entries=new List<Entry>();var budget=new Budget();
            void Put(string path,byte[] bytes){cancellation.ThrowIfCancellationRequested();budget.Add(path,bytes.Length);write?.Invoke(path,bytes);entries.Add(new Entry {Path=path,Bytes=bytes.Length,Hash=ModelLibrary.Hash(bytes)});}
            foreach(var pair in snapshot.Documents.OrderBy(x=>x.Key,StringComparer.Ordinal))Put(pair.Key,pair.Value);
            foreach(var pair in snapshot.Assets.OrderBy(x=>x.Key,StringComparer.Ordinal)){
                cancellation.ThrowIfCancellationRequested();using var stream=pair.Value();if(stream==null||!stream.CanRead)throw Invalid("The asset could not be opened.");
                var bytes=Read(stream,WorkspaceArchiveMetadata.Limit(pair.Key),cancellation);snapshot.Metadata.ValidateAsset(pair.Key,bytes);Put(pair.Key,bytes);
            }
            var json=new JObject {["format"]="maestro-native-workspace",["version"]=16,["entries"]=new JArray(entries.Select(x=>new JObject {["path"]=x.Path,["bytes"]=x.Bytes,["sha256"]=x.Hash}))};
            manifest=Utf8.GetBytes(json.ToString(Formatting.None));if(manifest.Length>MaximumManifestBytes)throw Invalid("Archive manifest is too large.");
            cancellation.ThrowIfCancellationRequested();return new WorkspaceArchiveReceipt {ManifestHash=ModelLibrary.Hash(manifest),Summary=CopySummary(snapshot.Metadata.Summary,budget.Total)};
        }
        static List<Entry> Manifest(byte[] bytes)
        {
            using var reader=new JsonTextReader(new StringReader(Utf8.GetString(bytes))) {MaxDepth=8,DateParseHandling=DateParseHandling.None};
            var root=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            if(reader.Read()||root.Count!=3||(string)root["format"]!="maestro-native-workspace"||root["version"]?.Type!=JTokenType.Integer||(int)root["version"]!=16||root["entries"] is not JArray array||array.Count>MaximumEntries)throw Invalid("Unsupported workspace archive manifest.");
            var entries=new List<Entry>();var budget=new Budget();
            foreach(var value in array){
                if(value is not JObject item||item.Count!=3||item["path"]?.Type!=JTokenType.String||item["bytes"]?.Type!=JTokenType.Integer||item["sha256"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)item["sha256"]))throw Invalid("Invalid manifest entry.");
                var e=new Entry {Path=(string)item["path"],Bytes=(long)item["bytes"],Hash=(string)item["sha256"]};budget.Add(e.Path,e.Bytes);entries.Add(e);
            }
            WorkspaceArchiveMetadata.CheckNames(entries.Select(x=>x.Path));return entries;
        }
        // Inspect the bounded central directory before ZipArchive allocates its entry collection.
        // This format deliberately excludes split/encrypted/ZIP64 containers; our writer needs none.
        static void Preflight(Stream source,CancellationToken cancellation)
        {
            if(!source.CanSeek||source.Length<22||source.Length>MaximumArchiveBytes)throw Invalid("Choose a seekable workspace archive of 512 MiB or smaller.");
            int length=(int)Math.Min(source.Length,65557);source.Position=source.Length-length;var tail=new byte[length];ExactRead(source,tail);
            int end=-1;for(int i=length-22;i>=0;i--)if(BitConverter.ToUInt32(tail,i)==0x06054b50&&i+22+BitConverter.ToUInt16(tail,i+20)==length){end=i;break;}
            if(end<0)throw Invalid("Incomplete archive directory.");
            int count=BitConverter.ToUInt16(tail,end+10);long size=BitConverter.ToUInt32(tail,end+12),offset=BitConverter.ToUInt32(tail,end+16),endAt=source.Length-length+end;
            if(BitConverter.ToUInt16(tail,end+4)!=0||BitConverter.ToUInt16(tail,end+6)!=0||BitConverter.ToUInt16(tail,end+8)!=count||count<2||count>MaximumEntries+1||size>MaximumManifestBytes||offset+size!=endAt)throw Invalid("Unsupported or oversized ZIP directory.");
            source.Position=offset;var header=new byte[46];var names=new HashSet<string>(StringComparer.Ordinal);
            for(int index=0;index<count;index++){
                cancellation.ThrowIfCancellationRequested();if(source.Position+46>endAt)throw Invalid("Truncated archive directory.");ExactRead(source,header);
                int nameLength=BitConverter.ToUInt16(header,28),extra=BitConverter.ToUInt16(header,30),comment=BitConverter.ToUInt16(header,32),flags=BitConverter.ToUInt16(header,8),method=BitConverter.ToUInt16(header,10);
                if(BitConverter.ToUInt32(header,0)!=0x02014b50||(flags&1)!=0||(method!=0&&method!=8)||BitConverter.ToUInt16(header,34)!=0||nameLength<1||nameLength>128||source.Position+nameLength+extra+comment>endAt||BitConverter.ToUInt32(header,42)>=offset)throw Invalid("Unsupported archive entry.");
                var raw=new byte[nameLength];ExactRead(source,raw);string name=Utf8.GetString(raw);
                if(!names.Add(name))throw Invalid("Duplicate archive entry.");long bytes=BitConverter.ToUInt32(header,24),compressed=BitConverter.ToUInt32(header,20);
                if(bytes<1||bytes>(name=="manifest.json"?MaximumManifestBytes:WorkspaceArchiveMetadata.Limit(name))||compressed>MaximumArchiveBytes)throw Invalid("Archive entry exceeds its limit.");
                source.Position+=extra+comment;
            }
            if(source.Position!=endAt||!names.Contains("manifest.json"))throw Invalid("Archive directory does not match its entry count.");source.Position=0;
        }
        static void ExactRead(Stream source,byte[] bytes){int at=0;while(at<bytes.Length){int n=source.Read(bytes,at,bytes.Length-at);if(n==0)throw Invalid("Truncated archive.");at+=n;}}
        public static PreparedWorkspaceArchive Stage(Stream source,string stagingParent,CancellationToken cancellation=default)
        {
            cancellation.ThrowIfCancellationRequested();Preflight(source,cancellation);
            string parent=Path.GetFullPath(stagingParent).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(parent.Length<=Path.GetPathRoot(Path.GetFullPath(stagingParent)).Length||!Directory.Exists(parent))throw new DirectoryNotFoundException("Use an existing private staging directory below the drive root.");NoLink(parent);
            string directory=Path.Combine(parent,"workspace-import-"+Guid.NewGuid().ToString("N"));PreparedWorkspaceArchive owned=null;
            try{
                using var zip=new ZipArchive(source,ZipArchiveMode.Read,true);if(zip.Entries.Count>MaximumEntries+1)throw Invalid("Too many archive entries.");
                var index=zip.Entries.ToDictionary(x=>x.FullName,StringComparer.Ordinal);
                using var manifestStream=index["manifest.json"].Open();byte[] manifest=Read(manifestStream,MaximumManifestBytes,cancellation);var entries=Manifest(manifest);
                if(index.Count!=entries.Count+1||entries.Any(x=>!index.ContainsKey(x.Path)))throw Invalid("Archive inventory differs from its manifest.");
                byte[] Verified(Entry entry){var zipped=index[entry.Path];if(zipped.Length!=entry.Bytes)throw Invalid("Archive entry length mismatch.");using var input=zipped.Open();var bytes=Read(input,(int)entry.Bytes,cancellation);if(bytes.Length!=entry.Bytes||ModelLibrary.Hash(bytes)!=entry.Hash)throw Invalid("Archive entry integrity mismatch.");return bytes;}
                var documents=entries.Where(x=>!WorkspaceArchiveMetadata.IsAsset(x.Path)).ToDictionary(x=>x.Path,Verified,StringComparer.Ordinal);
                var metadata=WorkspaceArchiveMetadata.Read(documents,entries.Where(x=>WorkspaceArchiveMetadata.IsAsset(x.Path)).Select(x=>x.Path));
                var receipt=new WorkspaceArchiveReceipt {ManifestHash=ModelLibrary.Hash(manifest),Summary=CopySummary(metadata.Summary,entries.Sum(x=>x.Bytes))};
                Directory.CreateDirectory(directory);owned=new PreparedWorkspaceArchive(directory,parent,receipt,manifest);
                void Save(string path,byte[] bytes){cancellation.ThrowIfCancellationRequested();string target=Path.GetFullPath(Path.Combine(directory,path.Replace('/',Path.DirectorySeparatorChar)));if(!target.StartsWith(directory+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw Invalid("Unsafe archive target.");Directory.CreateDirectory(Path.GetDirectoryName(target));using var file=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(bytes,0,bytes.Length);file.Flush(true);}
                foreach(var pair in documents)Save(pair.Key,pair.Value);
                foreach(var entry in entries.Where(x=>WorkspaceArchiveMetadata.IsAsset(x.Path))){var bytes=Verified(entry);metadata.ValidateAsset(entry.Path,bytes);Save(entry.Path,bytes);}
                cancellation.ThrowIfCancellationRequested();return owned;
            }catch{owned?.Dispose();throw;}
        }
        // Reverify the private prepared generation immediately before activation. Its manifest
        // is retained outside the writable workspace, and is never imported as a live document.
        internal static WorkspaceArchiveReceipt VerifyPreparedDirectory(string directory,byte[] manifest,CancellationToken cancellation=default,Action<string,byte[]> copy=null)
        {
            cancellation.ThrowIfCancellationRequested();
            if(manifest==null||manifest.Length<1||manifest.Length>MaximumManifestBytes)throw Invalid("Invalid prepared manifest size.");
            NoLink(directory);var entries=Manifest(manifest);var actual=new HashSet<string>(StringComparer.Ordinal);int visited=0;
            void Walk(string folder,int depth=0){if(depth>2)throw Invalid("Prepared paths are too deep.");foreach(string path in Directory.EnumerateFileSystemEntries(folder)){
                cancellation.ThrowIfCancellationRequested();if(++visited>MaximumEntries*2)throw Invalid("Too many prepared paths.");NoLink(path);
                if(Directory.Exists(path))Walk(path,depth+1);else {string name=path.Substring(directory.Length+1).Replace(Path.DirectorySeparatorChar,'/');if(!actual.Add(name))throw Invalid("Duplicate prepared file.");}
            }}
            Walk(directory);if(actual.Count!=entries.Count||entries.Any(x=>!actual.Contains(x.Path)))throw Invalid("Prepared files differ from the inspected inventory.");
            byte[] Verified(Entry entry){string path=Path.Combine(directory,entry.Path.Replace('/',Path.DirectorySeparatorChar));using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(input.Length!=entry.Bytes)throw Invalid("Prepared file length changed.");var bytes=Read(input,(int)entry.Bytes,cancellation);if(bytes.Length!=entry.Bytes||ModelLibrary.Hash(bytes)!=entry.Hash)throw Invalid("Prepared file changed after inspection.");return bytes;}
            var documents=entries.Where(x=>!WorkspaceArchiveMetadata.IsAsset(x.Path)).ToDictionary(x=>x.Path,Verified,StringComparer.Ordinal);
            var metadata=WorkspaceArchiveMetadata.Read(documents,entries.Where(x=>WorkspaceArchiveMetadata.IsAsset(x.Path)).Select(x=>x.Path));
            foreach(var document in documents)copy?.Invoke(document.Key,document.Value);
            foreach(var entry in entries.Where(x=>WorkspaceArchiveMetadata.IsAsset(x.Path))){var bytes=Verified(entry);metadata.ValidateAsset(entry.Path,bytes);copy?.Invoke(entry.Path,bytes);}
            cancellation.ThrowIfCancellationRequested();return new WorkspaceArchiveReceipt {ManifestHash=ModelLibrary.Hash(manifest),Summary=CopySummary(metadata.Summary,entries.Sum(x=>x.Bytes))};
        }
        sealed class OutputLimit:Stream
        {
            readonly Stream target;long bytes;
            internal OutputLimit(Stream target){this.target=target;}
            public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
            public override long Length=>bytes;public override long Position {get=>bytes;set=>throw new NotSupportedException();}
            public override void Write(byte[] buffer,int offset,int count){if(count<0||bytes+count>MaximumArchiveBytes)throw Invalid("Compressed archive exceeds its limit.");target.Write(buffer,offset,count);bytes+=count;}
            public override void Flush()=>target.Flush();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
        }
    }
}

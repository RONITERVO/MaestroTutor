// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Persistence
{
    /// <summary>Paired passive room/memory publication. Every participating reader/writer must
    /// enter this coordinator before using either primary.
    /// A durable prepared intent rolls back; a durable committed intent rolls forward.
    /// Recovery never executes actions, and unexpected file identities preserve all evidence.</summary>
    internal static class RoomSnapshotTransaction
    {
        internal const string FileName="room-snapshot.v3.json";
        internal const int RoomLimit=4*1024*1024,JournalLimit=16*1024*1024;
        static readonly string[] Names={"room.v4.json",ProgramMemoryStore.FileName};
        static readonly int[] Limits={RoomLimit,ProgramMemoryDocument.MaximumBytes};
        static readonly UTF8Encoding Utf8=new(false,true);
        internal sealed class Snapshot
        {
            readonly byte[][] files;
            internal Snapshot(byte[][] files){this.files=files.Select(Clone).ToArray();}
            internal byte[] Room=>Clone(files[0]);
            internal byte[] Memory=>Clone(files[1]);
            internal string Identity=>Fingerprint(Utf8.GetBytes(string.Join("/",files.Select(Fingerprint))));
            internal byte[] Read(int index)=>Clone(files[index]);
        }
        internal sealed class Recovery
        {
            internal readonly string Id;
            internal readonly bool Committed;
            internal Recovery(string id,bool committed){Id=id;Committed=committed;}
        }
        sealed class Intent
        {
            internal string Id,Phase;
            internal Snapshot Before,After;
            internal string[] Backups;
            internal byte[] Wire;
        }
        static byte[] Clone(byte[] bytes)=>bytes==null?null:(byte[])bytes.Clone();
        static string Fingerprint(byte[] bytes)=>bytes==null?"absent":WorkspaceFileInventory.Hash(bytes);
        static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        static bool Exact(JObject value,params string[] names)=>value!=null&&value.Count==names.Length&&names.All(value.ContainsKey);
        static JObject Json(byte[] bytes,int depth)
        {
            using var reader=new JsonTextReader(new StringReader(Utf8.GetString(bytes))){MaxDepth=depth,DateParseHandling=DateParseHandling.None};
            var json=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            Need(!reader.Read(),"Extra snapshot content.");return json;
        }
        static void Validate(int index,byte[] bytes,bool required=false)
        {
            if(bytes==null){Need(!required,"Missing snapshot document.");return;}
            Need(bytes.Length>0&&bytes.Length<=Limits[index],"Snapshot document exceeds its limit.");
            if(index==1){_=ProgramMemoryDocument.Decode(bytes);return;}
            var json=Json(bytes,48);
            Need(Exact(json,"version","objects","structures")&&json["version"]?.Type==JTokenType.Integer&&(int)json["version"]==RoomDocument.CurrentVersion,"Unsupported room snapshot.");
            var room=JsonUtility.FromJson<RoomDocument>(Utf8.GetString(bytes));Need(room!=null,"Missing room snapshot.");RoomStorage.Normalize(room);
            Need(room.Validate(out _),"Invalid room snapshot.");
        }
        internal static Snapshot FromDocuments(RoomDocument room,ProgramMemoryDocument memory)
        {
            Need(room!=null&&memory!=null,"Missing snapshot documents.");
            var copy=room.Copy();copy.version=RoomDocument.CurrentVersion;
            var files=new[]{Utf8.GetBytes(JsonUtility.ToJson(copy)),memory.Encode()};
            for(int i=0;i<2;i++)Validate(i,files[i],true);return new Snapshot(files);
        }
        static string Root(string directory)
        {
            string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            Need(root.Length>Path.GetPathRoot(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Length,"Snapshots cannot own a drive root.");
            WorkspaceFileInventory.Parents(root);return root;
        }
        static byte[] Read(string path,int maximum)
        {
            string kind=WorkspaceFileInventory.Kind(path);if(kind=="absent")return null;
            Need(kind=="file","Snapshot path is not a regular file.");
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            Need(stream.Length>0&&stream.Length<=maximum,"Snapshot file exceeds its limit.");
            var bytes=new byte[(int)stream.Length];int offset=0;
            while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
            Need(stream.ReadByte()==-1,"Snapshot changed while reading.");return bytes;
        }
        static void Paths(string root,bool strictDocuments=true)
        {
            WorkspaceFileInventory.Parents(root);
            if(WorkspaceFileInventory.Kind(root)=="absent")return;
            if(strictDocuments){
            Need(!VersionedRoomFile<RoomDocument>.HasNewerFiles(root,"room",RoomDocument.CurrentVersion),"A newer room format remains; preserve it before recovery.");
            Need(WorkspaceFileInventory.Kind(Path.Combine(root,"room.v4.json.pending"))=="absent","An unfinished room save remains; preserve it before recovery.");
            foreach(string path in Directory.EnumerateFileSystemEntries(root,"program-memory.v*").Take(3))
                Need((Path.GetFileName(path)==ProgramMemoryStore.FileName||Path.GetFileName(path)==ProgramMemoryStore.FileName+".backup")&&WorkspaceFileInventory.Kind(path)=="file","Unrecognized or unfinished memory remains; preserve it before recovery.");
            }
            // Unknown versions and orphan staging files are retained, never guessed into a commit.
            foreach(string path in Directory.EnumerateFileSystemEntries(root,"room-snapshot.v*").Take(2))
                Need(Path.GetFileName(path)==FileName&&WorkspaceFileInventory.Kind(path)=="file","Unrecognized snapshot evidence; preserve it before recovery.");
            foreach(string name in Names.Append(FileName))
                Need(!Directory.EnumerateFileSystemEntries(root,name+".snapshot.*").Any(),"Unfinished snapshot staging remains; preserve it before recovery.");
        }
        static readonly object gateMapLock=new();
        static readonly Dictionary<string,WeakReference<object>> gates=new(Path.DirectorySeparatorChar=='\\'?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        sealed class Owner:IDisposable
        {
            object gate;readonly FileStream file;
            internal Owner(object gate,FileStream file){this.gate=gate;this.file=file;}
            public void Dispose(){var held=Interlocked.Exchange(ref gate,null);if(held==null)return;try{file?.Dispose();}finally{Monitor.Exit(held);}}
        }
        static IDisposable Own(string root,bool strictDocuments=true,bool wait=false,bool initialize=true,bool sharedRead=false)
        {
            object gate;
            lock(gateMapLock){
                if(!gates.TryGetValue(root,out var weak)||!weak.TryGetTarget(out gate)){
                    if(gates.Count>=64)foreach(string key in gates.Where(x=>!x.Value.TryGetTarget(out _)).Select(x=>x.Key).ToArray())gates.Remove(key);
                    gates[root]=new WeakReference<object>(gate=new object());
                }
            }
            if(wait)Monitor.Enter(gate);else if(!Monitor.TryEnter(gate))throw new IOException("The room snapshot writer is busy.");
            try {
                Paths(root,strictDocuments);if(initialize){Directory.CreateDirectory(root);Paths(root,strictDocuments);}
                string path=Path.Combine(root,"room-snapshot.writer.lock");
                Need(WorkspaceFileInventory.Kind(path)!="directory","Snapshot writer path is unavailable.");
                return new Owner(gate,initialize?new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None):WorkspaceFileInventory.Kind(path)=="file"?new FileStream(path,FileMode.Open,FileAccess.Read,sharedRead?FileShare.Read:FileShare.None):null);
            }catch{Monitor.Exit(gate);throw;}
        }
        // Startup is the only ordinary path allowed to recover. Read-only inspection and
        // ordinary saves must not replace caches or overwrite an unconfirmed paired save.
        internal static IDisposable Enter(string directory,bool recover=false,bool wait=true,bool initialize=true)
        {
            string root=Root(directory);var owner=Own(root,strictDocuments:false,wait:wait,initialize:initialize);
            try {
                if(recover)RecoverOwned(root,null);
                else Need(WorkspaceFileInventory.Kind(Path.Combine(root,FileName))=="absent","An interrupted room/memory snapshot needs recovery before using saved data.");
                return owner;
            }catch{owner.Dispose();throw;}
        }
        // Read-only maintenance must not initialize a lock file or recover an inactive room.
        // Existing host path ownership excludes outside writers when no lock exists yet.
        // Read sharing permits inventory hashing and nested read-only checks while the
        // existing file handle still excludes exclusive writers and startup recovery.
        internal static IDisposable Inspect(string directory,bool wait=true)
        {
            string root=Root(directory);var owner=Own(root,strictDocuments:false,wait:wait,initialize:false,sharedRead:true);
            try {
                Need(WorkspaceFileInventory.Kind(Path.Combine(root,FileName))=="absent","An interrupted room/memory snapshot needs recovery before inspecting saved data.");
                return owner;
            }catch{owner.Dispose();throw;}
        }
        static Snapshot Current(string root)
        {
            var bytes=Names.Select((name,i)=>Read(Path.Combine(root,name),Limits[i])).ToArray();
            for(int i=0;i<2;i++){
                Validate(i,bytes[i]);var backup=Read(Path.Combine(root,Names[i]+".backup"),Limits[i]);Validate(i,backup);
                Need(bytes[i]!=null||backup==null,"Only a retained document remains; recover it explicitly.");
            }
            return new Snapshot(bytes);
        }
        static byte[] Encode(Intent intent)
        {
            JArray Files(Snapshot value)=>new(Enumerable.Range(0,2).Select(i=>value.Read(i) is byte[] b?(JToken)new JValue(Convert.ToBase64String(b)):JValue.CreateNull()));
            var bytes=Utf8.GetBytes(new JObject{["version"]=3,["id"]=intent.Id,["phase"]=intent.Phase,["before"]=Files(intent.Before),["after"]=Files(intent.After),["backups"]=new JArray(intent.Backups)}.ToString(Formatting.None));
            Need(bytes.Length<=JournalLimit,"Snapshot intent exceeds its limit.");intent.Wire=bytes;return bytes;
        }
        static Intent Decode(byte[] bytes)
        {
            var root=Json(bytes,4);
            Need(Exact(root,"version","id","phase","before","after","backups")&&root["version"]?.Type==JTokenType.Integer&&(int)root["version"]==3,"Unsupported snapshot intent.");
            Need(root["id"]?.Type==JTokenType.String&&ProgramMemoryDocument.Id((string)root["id"]),"Invalid snapshot identity.");
            Need(root["phase"]?.Type==JTokenType.String&&((string)root["phase"]=="prepared"||(string)root["phase"]=="committed"),"Invalid snapshot phase.");
            Snapshot Files(string key,bool required){
                Need(root[key] is JArray a&&a.Count==2,"Invalid snapshot pair.");var result=new byte[2][];
                for(int i=0;i<2;i++){
                    var token=root[key][i];
                    if(token.Type!=JTokenType.Null){
                        Need(token.Type==JTokenType.String&&((string)token).Length<=4*((Limits[i]+2)/3),"Invalid snapshot payload.");
                        result[i]=Convert.FromBase64String((string)token);
                    }
                    Validate(i,result[i],required);
                }
                return new Snapshot(result);
            }
            Need(root["backups"] is JArray b&&b.Count==2&&b.All(x=>x.Type==JTokenType.String&&((string)x=="absent"||((string)x).Length==64&&((string)x).All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'))),"Invalid retained snapshot identities.");
            return new Intent{Id=(string)root["id"],Phase=(string)root["phase"],Before=Files("before",false),After=Files("after",true),Backups=root["backups"].Values<string>().ToArray(),Wire=bytes};
        }
        static void Write(string root,string name,byte[] bytes)
        {
            WorkspaceFileInventory.Parents(root);string target=Path.Combine(root,name);
            Need(WorkspaceFileInventory.Kind(target)!="directory","Snapshot target is unavailable.");
            if(bytes==null){if(WorkspaceFileInventory.Kind(target)=="file")File.Delete(target);return;}
            string staged=target+".snapshot."+Guid.NewGuid().ToString("N");
            try {
                using(var file=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                WorkspaceFileInventory.Parents(root);string kind=WorkspaceFileInventory.Kind(target);
                Need(kind!="directory","Snapshot target changed.");
                if(kind=="file")File.Replace(staged,target,null);else File.Move(staged,target);
            }finally{if(WorkspaceFileInventory.Kind(staged)=="file")File.Delete(staged);}
        }
        static void CheckOwned(string root,Intent intent)
        {
            bool committed=intent.Phase=="committed";
            Paths(root);Need(Fingerprint(Read(Path.Combine(root,FileName),JournalLimit))==Fingerprint(intent.Wire),"Snapshot intent changed; files are preserved.");
            // Validate every primary/backup before changing any of them. Interrupted recovery
            // can then repeat safely; an outside edit is never rolled back or overwritten.
            for(int i=0;i<2;i++){
                string current=Fingerprint(Read(Path.Combine(root,Names[i]),Limits[i]));
                Need(current==Fingerprint(intent.Before.Read(i))||current==Fingerprint(intent.After.Read(i)),"Snapshot primary changed outside its transaction; files are preserved.");
                string backup=Fingerprint(Read(Path.Combine(root,Names[i]+".backup"),Limits[i]));
                Need(backup==intent.Backups[i]||committed&&backup==Fingerprint(intent.Before.Read(i)),"Snapshot backup changed outside its transaction; files are preserved.");
            }
        }
        static Recovery RecoverOwned(string root,Action<string> fault)
        {
            var bytes=Read(Path.Combine(root,FileName),JournalLimit);if(bytes==null)return null;
            var intent=Decode(bytes);bool committed=intent.Phase=="committed";
            CheckOwned(root,intent);
            var chosen=committed?intent.After:intent.Before;
            for(int i=0;i<2;i++){CheckOwned(root,intent);if(Fingerprint(Read(Path.Combine(root,Names[i]),Limits[i]))!=Fingerprint(chosen.Read(i)))Write(root,Names[i],chosen.Read(i));fault?.Invoke(i==0?"recovered-room":"recovered-memory");}
            if(committed)for(int i=0;i<2;i++){
                CheckOwned(root,intent);
                if(intent.Before.Read(i) is byte[] previous)Write(root,Names[i]+".backup",previous);
                fault?.Invoke(i==0?"room-backup":"memory-backup");
            }
            CheckOwned(root,intent);File.Delete(Path.Combine(root,FileName));return new Recovery(intent.Id,committed);
        }
        // Failure inspection preserves unrelated unfinished ordinary saves and never recovers.
        internal static Snapshot InspectSnapshot(string directory){using var owner=Inspect(directory);return Current(Root(directory));}
        internal static Snapshot Capture(string directory)=>Capture(directory,out _);
        internal static Snapshot Capture(string directory,out Recovery recovery,Action<string> fault=null,bool wait=false)
        {
            string root=Root(directory);using var owner=Own(root,wait:wait);recovery=RecoverOwned(root,fault);return Current(root);
        }
        internal static string Publish(string directory,Snapshot expected,Snapshot candidate,Action<string> fault=null,bool wait=false)
        {
            Need(expected!=null&&candidate!=null,"Missing paired snapshot.");for(int i=0;i<2;i++)Validate(i,candidate.Read(i),true);
            string root=Root(directory);using var owner=Own(root,wait:wait);RecoverOwned(root,null);
            var current=Current(root);Need(current.Identity==expected.Identity,"Saved room or memory changed; inspect before keeping this snapshot.");
            var intent=new Intent{Id=Guid.NewGuid().ToString("N"),Phase="prepared",Before=current,After=candidate,Backups=Names.Select((name,i)=>Fingerprint(Read(Path.Combine(root,name+".backup"),Limits[i]))).ToArray()};
            fault?.Invoke("before-journal");Paths(root);
            Need(Current(root).Identity==expected.Identity&&WorkspaceFileInventory.Kind(Path.Combine(root,FileName))=="absent"&&Names.Select((name,i)=>Fingerprint(Read(Path.Combine(root,name+".backup"),Limits[i]))).SequenceEqual(intent.Backups),"Saved snapshot changed before publication.");
            Write(root,FileName,Encode(intent));fault?.Invoke("prepared");
            CheckOwned(root,intent);Write(root,Names[0],candidate.Read(0));fault?.Invoke("room");
            CheckOwned(root,intent);Write(root,Names[1],candidate.Read(1));fault?.Invoke("memory");
            CheckOwned(root,intent);intent.Phase="committed";Write(root,FileName,Encode(intent));fault?.Invoke("committed");
            RecoverOwned(root,fault);return intent.Id;
        }
    }
}

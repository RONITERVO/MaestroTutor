// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Rules
{
    public sealed partial class InvocationReceipts
    {
        string recoveryId,lastRecoveredId;
        string RecoveryMarker=>Path.Combine(Path.GetDirectoryName(path),"action-recovery.pending.json");
        static readonly Regex JournalName=new(@"^action-receipts\.v[0-9]+\.json(?:\.(?:pending|backup|bak|unreadable))?\z");
        const long ArchiveLimit=128L*1024*1024,TransactionLimit=16L*1024*1024;
        const string FreshJournal="{\"version\":1,\"entries\":[]}";
        public JObject RecoveryView=>Error==null?null:new JObject {
            ["id"]=recoveryId,["status"]="Recovery stops active one-off actions, archives their old history and starts a fresh journal. Old uncertain actions are never replayed."
        };
        void Fail(string message) {Error=message;recoveryId??=Guid.NewGuid().ToString("N");}
        public bool CanRecover(string id,out string status)
        {
            if(Error==null&&id==lastRecoveredId&&id!=null){status="Action history was already recovered. Nothing was replayed.";return true;}
            status="Recovery request is stale. Inspect the current storage status.";
            return Error!=null&&id==recoveryId;
        }
        static void Ordinary(string value)
        {
            if((File.GetAttributes(value)&FileAttributes.ReparsePoint)!=0)throw new IOException("Recovery cannot follow linked paths");
        }
        static byte[] Digest(string value) {using var stream=File.OpenRead(value);using var sha=SHA256.Create();return sha.ComputeHash(stream);}
        static bool Same(string a,string b)=>new FileInfo(a).Length==new FileInfo(b).Length&&Digest(a).SequenceEqual(Digest(b));
        static void Durable(string value,byte[] bytes)
        {
            using var stream=new FileStream(value,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
        }
        static void Preserve(string source,string destination)
        {
            Ordinary(source);
            if(File.Exists(destination)){Ordinary(destination);if(!Same(source,destination))throw new IOException("Recovery evidence changed");return;}
            string staging=destination+".copying";
            if(File.Exists(staging)){Ordinary(staging);File.Delete(staging);}
            using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var output=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None)){input.CopyTo(output);output.Flush(true);}
            File.Move(staging,destination);
        }
        static long ArchiveBytes(string root,out int count)
        {
            count=0;if(!Directory.Exists(root))return 0;Ordinary(root);long total=0;
            foreach(var folder in Directory.GetDirectories(root)){
                Ordinary(folder);if(Directory.GetDirectories(folder).Length!=0)throw new IOException("Unexpected archive layout");
                foreach(var file in Directory.GetFiles(folder)){Ordinary(file);total+=new FileInfo(file).Length;if(++count>512||total>ArchiveLimit)throw new IOException("Recovery archive is full");}
            }
            if(Directory.GetFiles(root).Length!=0)throw new IOException("Unexpected archive layout");
            return total;
        }
        // Explicit recovery is resumable. The durable marker stays until every original
        // file is preserved and the new empty journal is committed. Startup never
        // falls back to an older history while this transaction is incomplete.
        public bool Recover(string id,out string status)
        {
            if(!CanRecover(id,out status))return false;
            if(Error==null)return true;
            try {
                string directory=Path.GetDirectoryName(path),archives=Path.Combine(directory,"action-receipt-archives");
                Directory.CreateDirectory(directory);Ordinary(directory);long used=ArchiveBytes(archives,out int archiveCount);
                JObject plan=null;string archive=null;
                if(File.Exists(RecoveryMarker)){
                    Ordinary(RecoveryMarker);
                    try {
                        if(new FileInfo(RecoveryMarker).Length<=8192){
                            var candidate=JObject.Parse(File.ReadAllText(RecoveryMarker,Encoding.UTF8));
                            if(candidate.Count==3&&candidate["version"]?.Type==JTokenType.Integer&&(int?)candidate["version"]==1&&Id(candidate["id"])&&((string)candidate["id"]).Length==32&&candidate["files"] is JArray list&&list.Count<=16&&
                                list.All(x=>x.Type==JTokenType.String&&JournalName.IsMatch((string)x))&&list.Values<string>().Distinct().Count()==list.Count){
                                plan=candidate;archive=Path.Combine(archives,(string)plan["id"]);
                            }
                        }
                    }catch(Exception ex) when(StorageFailure(ex)){/* Preserve malformed metadata as evidence below. */}
                }
                var present=Directory.GetFileSystemEntries(directory).Where(x=>JournalName.IsMatch(Path.GetFileName(x))).ToArray();
                foreach(var file in present){Ordinary(file);if(Directory.Exists(file))throw new IOException("A journal path is a directory");}
                long size=present.Sum(x=>new FileInfo(x).Length),markerBytes=plan==null&&File.Exists(RecoveryMarker)?new FileInfo(RecoveryMarker).Length:0;
                if(present.Length>16||size+markerBytes>TransactionLimit||plan==null&&(used+size+markerBytes+2*1024*1024>ArchiveLimit||archiveCount+present.Length+3>512||
                    Directory.Exists(archives)&&Directory.GetDirectories(archives).Length>=128))throw new IOException("Recovery archive capacity exceeded");
                if(plan==null){
                    archive=Path.Combine(archives,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(archive);Ordinary(archives);Ordinary(archive);
                    if(File.Exists(RecoveryMarker))Preserve(RecoveryMarker,Path.Combine(archive,"previous-marker.json"));
                    plan=new JObject {["version"]=1,["id"]=Path.GetFileName(archive),["files"]=new JArray(present.Select(Path.GetFileName))};
                    string staging=RecoveryMarker+".writing";
                    if(File.Exists(staging)){Ordinary(staging);File.Delete(staging);}
                    Durable(staging,Encoding.UTF8.GetBytes(plan.ToString(Newtonsoft.Json.Formatting.None)));
                    if(File.Exists(RecoveryMarker))Maestro.Quest.Persistence.FilePublication.Replace(staging,RecoveryMarker,null);else File.Move(staging,RecoveryMarker);
                }
                Directory.CreateDirectory(archive);Ordinary(archives);Ordinary(archive);
                var names=((JArray)plan["files"]).Values<string>().ToArray();
                long additional=present.Where(file=>!File.Exists(Path.Combine(archive,Path.GetFileName(file)))).Sum(file=>{
                    string staging=Path.Combine(archive,Path.GetFileName(file))+".copying";
                    if(File.Exists(staging))Ordinary(staging);
                    return Math.Max(0,new FileInfo(file).Length-(File.Exists(staging)?new FileInfo(staging).Length:0));
                });
                int extraFiles=present.Count(file=>!File.Exists(Path.Combine(archive,Path.GetFileName(file)))&&!File.Exists(Path.Combine(archive,Path.GetFileName(file))+".copying"))+
                    (File.Exists(Path.Combine(archive,"session.json"))||File.Exists(Path.Combine(archive,"session.json.writing"))?0:1)+(File.Exists(Path.Combine(archive,"fresh.json"))?0:1)+(markerBytes>0?1:0);
                if(archiveCount+extraFiles>512||used+markerBytes+additional+(File.Exists(Path.Combine(archive,"session.json"))?0:1024*1024)+8192>ArchiveLimit)throw new IOException("Recovery archive capacity exceeded");
                if(present.Any(x=>Path.GetFileName(x)!=Path.GetFileName(path)&&!names.Contains(Path.GetFileName(x))))throw new IOException("Journal files changed during recovery");
                string session=Path.Combine(archive,"session.json");
                if(!File.Exists(session)){
                    string temporary=session+".writing";if(File.Exists(temporary)){Ordinary(temporary);File.Delete(temporary);}
                    Durable(temporary,Encoding.UTF8.GetBytes(new JArray(entries.Select(x=>x.DeepClone())).ToString(Newtonsoft.Json.Formatting.None)));
                    File.Move(temporary,session);
                }
                else Ordinary(session);
                // First preserve every source; no journal file is removed before this loop completes.
                foreach(string name in names){
                    string source=Path.Combine(directory,name),saved=Path.Combine(archive,name);
                    if(File.Exists(source)){
                        if(source==path&&File.Exists(saved)&&File.ReadAllText(source,Encoding.UTF8)==FreshJournal)continue;
                        Preserve(source,saved);
                    }else if(!File.Exists(saved))throw new IOException("Recovery source and archive are both missing");
                    else Ordinary(saved);
                }
                foreach(string name in names.Where(x=>x!=Path.GetFileName(path))){
                    string source=Path.Combine(directory,name);
                    if(File.Exists(source)){if(!Same(source,Path.Combine(archive,name)))throw new IOException("Journal changed during recovery");File.Delete(source);}
                }
                if(File.Exists(path)&&names.Contains(Path.GetFileName(path))&&File.ReadAllText(path,Encoding.UTF8)!=FreshJournal&&!Same(path,Path.Combine(archive,Path.GetFileName(path))))throw new IOException("Primary journal changed during recovery");
                if(File.Exists(path)&&!names.Contains(Path.GetFileName(path))&&File.ReadAllText(path,Encoding.UTF8)!=FreshJournal)throw new IOException("Unexpected primary journal");
                string fresh=Path.Combine(archive,"fresh.json");
                if(File.Exists(fresh)){Ordinary(fresh);File.Delete(fresh);}
                Durable(fresh,Encoding.UTF8.GetBytes(FreshJournal));
                if(File.Exists(path))Maestro.Quest.Persistence.FilePublication.Replace(fresh,path,null);else File.Move(fresh,path);
                File.Delete(RecoveryMarker);
                entries.Clear();next=Guid.NewGuid().ToString("N");Error=null;lastRecoveredId=id;recoveryId=null;
                status="Action history recovered. Previous evidence was archived; no old actions were replayed.";return true;
            }catch(Exception ex) when(StorageFailure(ex)){
                Fail("Action history recovery could not finish. One-off actions remain stopped, new starts are disabled and previous evidence is retained. Check storage and retry recovery.");
                status=Error;return false;
            }
        }
    }
}

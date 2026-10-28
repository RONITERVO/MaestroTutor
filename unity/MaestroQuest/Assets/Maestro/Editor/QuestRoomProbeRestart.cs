// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Editor
{
    /// <summary>Read-only admission for an explicitly owned, checkpointed process restart.</summary>
    public sealed class QuestRoomProbeRestart
    {
        public string WorkspaceDirectory { get; }
        public string RestartOf { get; }
        QuestRoomProbeRestart(string workspace,string restartOf){WorkspaceDirectory=workspace;RestartOf=restartOf;}
        public static QuestRoomProbeRestart Resolve(string directory,JObject owner)
        {
            Need(Path.IsPathRooted(directory),"The probe directory must be absolute.");
            directory=Path.GetFullPath(directory);
            Need(Version(owner)&&GuidId(owner,"id"),"Invalid probe owner.");
            bool restart=owner.Property("restartOf")!=null;
            if(!restart){
                if(owner.Property("nativeRestart")!=null)Need(owner["nativeRestart"].Type==JTokenType.Boolean,"Invalid native restart flag.");
                if(Enabled(owner,"nativeRestart"))Need(Enabled(owner,"firstLearner")&&Enabled(owner,"liveContinuity"),"Native restart requires the production Live learner mode.");
                return new QuestRoomProbeRestart(Path.Combine(directory,"workspace"),"");
            }
            Need(Enabled(owner,"nativeRestart")&&Enabled(owner,"firstLearner")&&Enabled(owner,"liveContinuity")&&GuidId(owner,"restartOf"),"Invalid restart child owner.");
            var parent=Directory.GetParent(directory)?.FullName;
            Need(parent!=null&&SamePath(directory,Path.Combine(parent,"restart")),"Restart is permitted only in the direct restart child.");
            Need((string)owner["id"]!=(string)owner["restartOf"],"A restart needs a fresh owner identity.");
            Plain(directory);Plain(parent);
            foreach(var name in new[]{"ready.json","state.json","terminal.json","workspace","learner-resume.json"})
                Need(!Exists(Path.Combine(directory,name)),"The restart child must have fresh outputs and no copied workspace.");
            string workspace=Path.Combine(parent,"workspace");
            Need(Directory.Exists(workspace),"The original workspace is missing.");Plain(workspace);
            Need(!Exists(Path.Combine(parent,"terminal.json")),"A checkpointed interruption cannot have a source terminal receipt.");
            var sourceOwner=Read(parent,"owner.json");
            Need(Version(sourceOwner)&&GuidId(sourceOwner,"id")&&(string)sourceOwner["id"]==(string)owner["restartOf"]&&sourceOwner.Property("restartOf")==null,"Wrong original probe owner.");
            Need(Enabled(sourceOwner,"nativeRestart")&&Enabled(sourceOwner,"firstLearner")&&Enabled(sourceOwner,"liveContinuity"),"The source did not opt into native restart.");
            var receipt=Read(parent,"restart.json");
            Need(Version(receipt)&&Text(receipt,"id")==Text(sourceOwner,"id")&&Text(receipt,"method")=="terminate-after-checkpoint","Invalid restart receipt.");
            Need(SamePath(Text(receipt,"workspace"),workspace),"Restart must use the exact original workspace.");
            var source=receipt["source"] as JObject;var resumed=receipt["resumed"] as JObject;
            Need(source!=null&&resumed!=null,"Missing restart process identities.");
            Need(Text(source,"id")==Text(sourceOwner,"id")&&Positive(source,"processId")&&Positive(source,"startedAt")&&Positive(source,"exitedAt")&&Integer(source,"exitedAt")>=Integer(source,"startedAt"),"Invalid source process lifetime.");
            Need(source["exitCode"]?.Type==JTokenType.Integer&&source["terminalExisted"]?.Type==JTokenType.Boolean&&!(bool)source["terminalExisted"],"Missing interrupted process outcome.");
            Need(Text(resumed,"id")==Text(owner,"id")&&SamePath(Text(resumed,"directory"),directory),"Wrong resumed owner or directory.");
            var ready=Read(parent,"ready.json");
            Need(Version(ready)&&Text(ready,"id")==Text(sourceOwner,"id")&&Positive(ready,"processId")&&Positive(ready,"processStartedAt")&&Integer(ready,"processId")==Integer(source,"processId")&&Integer(ready,"processStartedAt")==Integer(source,"startedAt"),"The original ready receipt does not attest this process.");
            Need(SamePath(Text(ready,"workspaceDirectory"),workspace)&&Text(ready,"restartOf")=="","The source ready receipt is not the initial workspace owner.");
            byte[] bytes=Bytes(parent,"restart-request.json",8192);
            using var digest=SHA256.Create();
            string hash=BitConverter.ToString(digest.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            Need(Text(receipt,"requestSha256")==hash,"The restart request changed after checkpointing.");
            var request=Parse(bytes);
            Need(Version(request)&&Text(request,"id")==Text(sourceOwner,"id")&&Text(request,"operation")=="restart","Wrong restart request owner or operation.");
            foreach(var key in new[]{"clientId","nativeSession","appleId","treeId"})Need(GuidId(request,key),"Missing restart checkpoint identity.");
            Need(Text(request,"appleId")!=Text(request,"treeId"),"Independent tree and apple identities are required.");
            var checkpoint=request["checkpoint"] as JObject;
            Need(checkpoint!=null&&checkpoint.Properties().Count()==2&&Clean(checkpoint["room"] as JObject)&&Clean(checkpoint["behaviours"] as JObject),"Restart requires both durable stores to be clean and unheld.");
            return new QuestRoomProbeRestart(workspace,Text(sourceOwner,"id"));
        }
        static bool Clean(JObject store)=>store!=null&&store.Properties().Count()==5&&Text(store,"phase")=="idle"&&Disabled(store,"pending")&&Disabled(store,"dirty")&&Disabled(store,"dispatchHeld")&&Text(store,"error")=="";
        static bool Enabled(JObject value,string key)=>value?[key]?.Type==JTokenType.Boolean&&(bool)value[key];
        static bool Disabled(JObject value,string key)=>value?[key]?.Type==JTokenType.Boolean&&!(bool)value[key];
        static bool Version(JObject value)=>value?["version"]?.Type==JTokenType.Integer&&Integer(value,"version")==1;
        static bool GuidId(JObject value,string key)=>value?[key]?.Type==JTokenType.String&&Guid.TryParseExact((string)value[key],"N",out var id)&&id.ToString("N")== (string)value[key];
                static bool Positive(JObject value,string key)=>value?[key]?.Type==JTokenType.Integer&&Integer(value,key)>0;
        static long Integer(JObject value,string key)=>(long)value[key];
        static string Text(JObject value,string key)=>value?[key]?.Type==JTokenType.String?(string)value[key]:null;
        static bool SamePath(string first,string second)=>!string.IsNullOrEmpty(first)&&Path.IsPathRooted(first)&&string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
        static bool Exists(string path)=>File.Exists(path)||Directory.Exists(path);
        static void Plain(string path)=>Need((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"Restart ownership paths must not be symbolic links or reparse points.");
        static JObject Parse(byte[] bytes)=>JObject.Parse(new UTF8Encoding(false,true).GetString(bytes),new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
        static JObject Read(string parent,string name)=>Parse(Bytes(parent,name,65536));
        static byte[] Bytes(string parent,string name,int maximum)
        {
            string path=Path.Combine(parent,name);Need(File.Exists(path),"Missing restart evidence: "+name);Plain(path);
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            Need(stream.Length>0&&stream.Length<=maximum,"Restart evidence exceeds its bounded size: "+name);
            var bytes=new byte[(int)stream.Length];int offset=0,read;
            while(offset<bytes.Length&&(read=stream.Read(bytes,offset,bytes.Length-offset))>0)offset+=read;
            Need(offset==bytes.Length&&stream.ReadByte()==-1,"Restart evidence changed during admission.");return bytes;
        }
        static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    }
}

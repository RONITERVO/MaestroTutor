// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Rules
{
    /// <summary>Write-ahead evidence, never a playback queue. Issued IDs are consumed
    /// before effects; an evicted/unissued ID cannot become a new invocation.</summary>
    public sealed class InvocationReceipts
    {
        readonly string path;
        readonly List<JObject> entries=new();
        string next=Guid.NewGuid().ToString("N");
        public string Error { get; private set; }
        public string NextId=>Error==null?next:null;
        public InvocationReceipts(string directory)
        {
            path=Path.Combine(directory,"action-receipts.v1.json");
            try {
                if(Directory.Exists(directory)&&Directory.GetFiles(directory,"action-receipts.v*.json").Any(p=>!string.Equals(Path.GetFullPath(p),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Newer action receipt format");
                if(!File.Exists(path))return;
                if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("Receipt limit");
                using var reader=new JsonTextReader(new StringReader(File.ReadAllText(path,Encoding.UTF8))) {MaxDepth=48,DateParseHandling=DateParseHandling.None};
                var root=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                if(reader.Read()||root.Count!=2||(int?)root["version"]!=1||root["entries"] is not JArray data||data.Count>24)
                    throw new InvalidDataException("Receipt format");
                foreach(var token in data) {
                    if(token is not JObject item||!Valid(item)||entries.Any(x=>(string)x["id"]==(string)item["id"]))
                        throw new InvalidDataException("Receipt record");
                    entries.Add((JObject)item.DeepClone());
                }
                if(entries.Count(Active)>8||entries.Count(x=>!Active(x))>16)throw new InvalidDataException("Receipt count limit");
                foreach(var item in entries.Where(Active).ToArray()) {
                    entries.Remove(item);entries.Add(item);
                    item["phase"]="interrupted";
                    item["status"]="The app restarted before a final result was saved. Some effects may have happened; this action was not replayed.";
                }
                Trim();Save();
            } catch(Exception ex) when(StorageFailure(ex)) {entries.Clear();Error="Action receipt storage cannot be read safely. New actions are disabled; existing outcomes are unknown.";}
        }
        static bool StorageFailure(Exception ex)=>ex is InvalidDataException||ex is IOException||ex is UnauthorizedAccessException||ex is JsonException||ex is ArgumentException||ex is InvalidCastException||ex is OverflowException||ex is System.Security.SecurityException;
        static bool Id(JToken value)=>value?.Type==JTokenType.String&&System.Text.RegularExpressions.Regex.IsMatch((string)value,"^[a-f0-9]{32}$");
        static bool Active(JObject item)=>(string)item["phase"] is "preparing" or "running";
        static bool Valid(JObject item)=>item.Count==7&&Id(item["id"])&&RoomCapabilityCatalog.ValidCall(item["call"] as JObject)
            &&JToken.DeepEquals(item["capability"],item["call"]["id"])&&JToken.DeepEquals(item["version"],item["call"]["version"])
            &&item["resources"] is JArray resources&&resources.Count<=16
            &&JToken.DeepEquals(new JArray(RoomExecutions.Resources(new JObject {["operation"]="start",["call"]=item["call"].DeepClone()}).OrderBy(x=>x)),new JArray(resources.Values<string>().OrderBy(x=>x)))
            &&item["status"]?.Type==JTokenType.String&&((string)item["status"]).Length<=2048
            &&new[]{"preparing","running","completed","cancelled","failed","interrupted"}.Contains((string)item["phase"]);
        public JObject Find(string id)=>entries.FirstOrDefault(x=>(string)x["id"]==id)?.DeepClone() as JObject;
        void Trim()
        {
            while(entries.Count(x=>!Active(x))>16)entries.Remove(entries.First(x=>!Active(x)));
        }
        bool Save()
        {
            if(Error!=null)return false;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var bytes=Encoding.UTF8.GetBytes(new JObject {["version"]=1,["entries"]=new JArray(entries.Select(x=>x.DeepClone()))}.ToString(Formatting.None));
                if(bytes.Length>1024*1024)throw new InvalidDataException("Receipt limit");
                string pending=path+".pending";
                using(var stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)) {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);
                return true;
            } catch(Exception ex) when(StorageFailure(ex)) {
                Error="Action receipt storage failed. New actions are disabled; unsaved outcomes may be uncertain after restart.";return false;
            }
        }
        public bool Reserve(string id,JObject call,string[] resources,out string error)
        {
            error=Error;if(error!=null)return false;
            if(id!=next) {error="This action ID is expired or was never issued. Inspect its outcome; do not replay it.";return false;}
            if(entries.Count(Active)>=8) {error="All action receipt slots are in use";return false;}
            entries.Add(new JObject {["id"]=id,["capability"]=call["id"].DeepClone(),["version"]=call["version"].DeepClone(),["resources"]=new JArray(resources),
                ["phase"]="preparing",["status"]="Action accepted; completion has not been recorded",["call"]=call.DeepClone()});
            next=Guid.NewGuid().ToString("N");Trim();
            if(Save())return true;
            // No native effect was authorized. Keep honest in-process evidence even
            // when the atomic write may have reached disk before its error.
            var item=entries.Last();item["phase"]="failed";item["status"]="Action did not start because its receipt could not be saved";Trim();
            error=Error;return false;
        }
        public void Update(JObject detail)
        {
            if(detail==null)return;
            int index=entries.FindIndex(x=>(string)x["id"]==(string)detail["id"]);
            if(index<0)return;
            entries.RemoveAt(index);entries.Add((JObject)detail.DeepClone());Trim();Save();
        }
        public JObject Observe(string selectedId,Func<string,JObject> live)
        {
            var values=entries.Select(x=>live((string)x["id"])??(JObject)x.DeepClone()).ToArray();
            static JObject Summary(JObject value) {var result=(JObject)value.DeepClone();result.Remove("call");return result;}
            return new JObject {
                ["selected"]=values.FirstOrDefault(x=>(string)x["id"]==selectedId)?.DeepClone()??JValue.CreateNull(),
                ["running"]=new JArray(values.Where(Active).Select(Summary)),
                ["outcomes"]=new JArray(values.Where(x=>!Active(x)).Select(Summary)),
                ["nextRunId"]=NextId,["storageError"]=Error
            };
        }
    }
}

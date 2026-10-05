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
    public sealed partial class InvocationReceipts
    {
        readonly string path;
        readonly Action<string,string,string> publish;
        readonly List<JObject> entries=new();
        string next=Guid.NewGuid().ToString("N");
        public string Error { get; private set; }
        public string NextId=>Error==null?next:null;
        public InvocationReceipts(string directory):this(directory,Maestro.Quest.Persistence.FilePublication.Replace) {}
        internal InvocationReceipts(string directory,Action<string,string,string> publish)
        {
            this.publish=publish??throw new ArgumentNullException(nameof(publish));
            path=Path.Combine(directory,"action-receipts.v1.json");
            try {
                if(File.Exists(RecoveryMarker))throw new InvalidDataException("Interrupted receipt recovery");
                if(Directory.Exists(directory)&&Directory.GetFiles(directory,"action-receipts.v*").Any(p=>JournalName.IsMatch(Path.GetFileName(p))&&!string.Equals(Path.GetFullPath(p),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)&&!Path.GetFileName(p).StartsWith("action-receipts.v1.json.",StringComparison.Ordinal)))
                    throw new InvalidDataException("Newer action receipt format");
                if(!File.Exists(path))return;
                if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("Receipt limit");
                using var reader=new JsonTextReader(new StringReader(File.ReadAllText(path,Encoding.UTF8))) {MaxDepth=64,DateParseHandling=DateParseHandling.None};
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
            } catch(Exception ex) when(StorageFailure(ex)) {entries.Clear();Fail("Action receipt storage cannot be read safely. New actions are disabled; existing outcomes are unknown.");}
        }
        static bool StorageFailure(Exception ex)=>ex is InvalidDataException||ex is IOException||ex is UnauthorizedAccessException||ex is JsonException||ex is ArgumentException||ex is InvalidCastException||ex is OverflowException||ex is System.Security.SecurityException;
        static bool Id(JToken value)=>value?.Type==JTokenType.String&&System.Text.RegularExpressions.Regex.IsMatch((string)value,"^[a-f0-9]{32}$");
        static bool Active(JObject item)=>(string)item["phase"] is "preparing" or "running";
        static bool Valid(JObject item)=>(item.Count==7||item.Count==8&&item.ContainsKey("output"))&&
            (!item.ContainsKey("output")||(string)item["phase"]=="completed"&&Maestro.Quest.Programs.BehaviourCatalog.Action((string)item["capability"]) is var definition&&definition!=null&&
                (int?)item["version"]==definition.Version&&Maestro.Quest.Programs.CapabilityArguments.Validate(item["output"],definition.OutputSchema,out _))&&Id(item["id"])&&RoomCapabilityCatalog.ValidCall(item["call"] as JObject)
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
            string phase="stage";
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var bytes=Encoding.UTF8.GetBytes(new JObject {["version"]=1,["entries"]=new JArray(entries.Select(x=>x.DeepClone()))}.ToString(Formatting.None));
                if(bytes.Length>1024*1024)throw new InvalidDataException("Receipt limit");
                string pending=path+".pending";
                using(var stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)) {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                phase="publish";
                if(File.Exists(path))publish(pending,path,null);else File.Move(pending,path);
                return true;
            } catch(Exception ex) when(StorageFailure(ex)) {
                UnityEngine.Debug.LogWarning($"Maestro action receipt save failed during {phase} ({ex.GetType().Name}, 0x{ex.HResult:X8})");
                Fail("Action receipt storage failed. New actions are disabled; unsaved outcomes may be uncertain after restart.");return false;
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
        public JObject Observe(string selectedId,Func<string,bool,JObject> live)
        {
            JToken selected=JValue.CreateNull();var active=new JArray();var outcomes=new JArray();
            foreach(var entry in entries) {
                string id=(string)entry["id"];bool detail=id==selectedId;
                var value=live(id,detail)??entry;
                if(detail)selected=value.DeepClone();
                // Calls can contain complete creation recipes. Summaries never expose
                // them, so project the visible fields instead of cloning then removing.
                var summary=new JObject();
                foreach(var property in value.Properties())if(property.Name!="call")summary.Add(property.Name,property.Value.DeepClone());
                (Active(value)?active:outcomes).Add(summary);
            }
            return new JObject {
                ["selected"]=selected,["running"]=active,["outcomes"]=outcomes,
                ["nextRunId"]=NextId,["storageError"]=Error,["recovery"]=RecoveryView
            };
        }
    }
}

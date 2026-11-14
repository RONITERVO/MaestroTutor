// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Maestro.Quest.Creation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Persistence
{
    [Serializable] public sealed class WorldPackSlot {public string slot,id;}
    [Serializable] public sealed class WorldPackResourceSlot {public string kind,slot,id;}
    [Serializable] public sealed class WorldPackInstance
    {
        public string instanceId,packId,sourceHash,entryId;
        public int packVersion;
        public WorldPackSlot[] objects=Array.Empty<WorldPackSlot>();
        public WorldPackResourceSlot[] resources=Array.Empty<WorldPackResourceSlot>();
    }
    /// <summary>Historical origin, not object ownership or a live dependency. Accepted generations
    /// retain these immutable document bytes through ordinary edits, including deletion of mapped
    /// objects/resources. DTOs are detached; only strict Encode publishes their canonical form.</summary>
    [Serializable] public sealed class WorldPackDocument
    {
        public const string FileName="world-packs.v1.json";
        public const int MaximumBytes=1024*1024,MaximumInstances=16,MaximumObjects=256,MaximumResources=128;
        public int version=1;
        public string worldId;
        public WorldPackInstance[] instances=Array.Empty<WorldPackInstance>();
        static readonly UTF8Encoding Utf8=new(false,true);
        static readonly Regex ObjectSlot=new("^[a-zA-Z][a-zA-Z0-9_]{0,23}\\z",RegexOptions.CultureInvariant);
        static readonly Regex Slug=new("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*\\z",RegexOptions.CultureInvariant);
        static readonly Regex PackId=new("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)*\\z",RegexOptions.CultureInvariant);
        static InvalidDataException Invalid(string message)=>new(message);
        static void Need(bool condition,string message){if(!condition)throw Invalid(message);}
        static bool Hex(string value,int length)=>value?.Length==length&&value.All(c=>c>='a'&&c<='f'||c>='0'&&c<='9');
        static bool Name(string value)=>value!=null&&value.Length<=64&&Slug.IsMatch(value);
        static bool Exact(JObject value,params string[] fields)=>value!=null&&value.Count==fields.Length&&fields.All(value.ContainsKey);
        static string Text(JToken token){Need(token?.Type==JTokenType.String,"Expected world-pack text.");return (string)token;}
        static int Integer(JToken token){Need(token?.Type==JTokenType.Integer,"Expected world-pack integer.");long value=(long)token;Need(value>0&&value<=int.MaxValue,"Invalid world-pack integer.");return (int)value;}
        static int ResourceLimit(string kind)=>kind switch {
            "appearance"=>RoomAppearance.MaximumDefinitions,"audio"=>RoomAudioDefinition.MaximumSources,
            "environment"=>RoomEnvironmentProfile.MaximumProfiles,"visibility"=>RoomVisibilityLayer.MaximumLayers,
            _=>throw Invalid("Unknown world-pack resource kind.")
        };
        void Validate()
        {
            Need(version==1&&Hex(worldId,32)&&instances!=null&&instances.Length<=MaximumInstances,"Invalid world-pack document.");
            var instanceIds=new HashSet<string>(StringComparer.Ordinal);var objectIds=new HashSet<string>(StringComparer.Ordinal);
            var resourceIds=new HashSet<string>(StringComparer.Ordinal);var resourcesByKind=new Dictionary<string,int>(StringComparer.Ordinal);int objectCount=0;
            foreach(var entry in instances){
                Need(entry!=null&&Hex(entry.instanceId,32)&&instanceIds.Add(entry.instanceId),"Invalid or duplicate world-pack instance identity.");
                Need(entry.packId!=null&&entry.packId.Length<=96&&PackId.IsMatch(entry.packId)&&entry.packVersion>0&&Hex(entry.sourceHash,64)&&Name(entry.entryId),"Invalid world-pack source identity.");
                Need(entry.objects!=null&&entry.objects.Length<=MaximumObjects&&entry.resources!=null&&entry.resources.Length<=MaximumResources,"Invalid world-pack mappings.");
                var slots=new HashSet<string>(StringComparer.Ordinal);
                foreach(var item in entry.objects){
                    Need(++objectCount<=MaximumObjects&&item!=null&&item.slot!=null&&ObjectSlot.IsMatch(item.slot)&&slots.Add(item.slot)&&Hex(item.id,32)&&objectIds.Add(item.id),"Invalid, duplicate or excessive world-pack object mapping.");
                }
                slots.Clear();
                foreach(var item in entry.resources){
                    Need(item!=null&&Hex(item.slot,32)&&Hex(item.id,32),"Invalid world-pack resource mapping.");
                    int limit=ResourceLimit(item.kind),count=resourcesByKind.TryGetValue(item.kind,out var prior)?prior+1:1;
                    Need(count<=limit&&slots.Add(item.kind+"/"+item.slot)&&resourceIds.Add(item.kind+"/"+item.id),"Duplicate or excessive world-pack resource mapping.");resourcesByKind[item.kind]=count;
                }
            }
        }
        public void ValidateWorld(string acceptedWorldId)
        {
            Validate();Need(Hex(acceptedWorldId,32)&&worldId==acceptedWorldId,"World-pack provenance belongs to a different world.");
        }
        public byte[] Encode()
        {
            Validate();var json=new JObject {["version"]=version,["worldId"]=worldId,["instances"]=new JArray(instances.Select(entry=>new JObject {
                ["instanceId"]=entry.instanceId,["packId"]=entry.packId,["packVersion"]=entry.packVersion,["sourceHash"]=entry.sourceHash,["entryId"]=entry.entryId,
                ["objects"]=new JArray(entry.objects.Select(item=>new JObject {["slot"]=item.slot,["id"]=item.id})),
                ["resources"]=new JArray(entry.resources.Select(item=>new JObject {["kind"]=item.kind,["slot"]=item.slot,["id"]=item.id}))
            }))};
            var bytes=Utf8.GetBytes(json.ToString(Formatting.None));Need(bytes.Length<=MaximumBytes,"World-pack document exceeds its byte limit.");return bytes;
        }
        public static WorldPackDocument Decode(byte[] bytes)
        {
            Need(bytes!=null&&bytes.Length>0&&bytes.Length<=MaximumBytes,"Invalid world-pack document size.");
            try {
                using var reader=new JsonTextReader(new StringReader(Utf8.GetString(bytes))){MaxDepth=8,DateParseHandling=DateParseHandling.None};
                var root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                Need(!reader.Read()&&Exact(root,"version","worldId","instances")&&Integer(root["version"])==1,"Unsupported world-pack document.");
                Need(root["instances"] is JArray list&&list.Count<=MaximumInstances,"Invalid world-pack instance list.");
                var result=new WorldPackDocument{worldId=Text(root["worldId"])};var entries=new List<WorldPackInstance>();
                foreach(var token in (JArray)root["instances"]){
                    Need(token is JObject entry&&Exact(entry,"instanceId","packId","packVersion","sourceHash","entryId","objects","resources"),"Invalid world-pack instance fields.");
                    Need(token["objects"] is JArray objects&&objects.Count<=MaximumObjects&&token["resources"] is JArray resources&&resources.Count<=MaximumResources,"Invalid world-pack mapping lists.");
                    var item=new WorldPackInstance{instanceId=Text(token["instanceId"]),packId=Text(token["packId"]),packVersion=Integer(token["packVersion"]),sourceHash=Text(token["sourceHash"]),entryId=Text(token["entryId"])};
                    item.objects=((JArray)token["objects"]).Select(value=>{
                        Need(value is JObject slot&&Exact(slot,"slot","id"),"Invalid world-pack object fields.");return new WorldPackSlot{slot=Text(value["slot"]),id=Text(value["id"])};
                    }).ToArray();
                    item.resources=((JArray)token["resources"]).Select(value=>{
                        Need(value is JObject slot&&Exact(slot,"kind","slot","id"),"Invalid world-pack resource fields.");return new WorldPackResourceSlot{kind=Text(value["kind"]),slot=Text(value["slot"]),id=Text(value["id"])};
                    }).ToArray();entries.Add(item);
                }
                result.instances=entries.ToArray();result.Validate();return result;
            }catch(Exception error)when(error is JsonException||error is DecoderFallbackException||error is OverflowException){throw new InvalidDataException("Invalid world-pack document encoding.",error);}
        }
        // No mutable owner or repair/default path: absence is optional, malformed/future evidence
        // is preserved and refused. Callers validate the returned document against accepted room ID.
        internal static byte[] ReadOptional(string directory)
        {
            WorkspaceFileInventory.Parents(directory);string kind=WorkspaceFileInventory.Kind(directory);
            if(kind=="absent")return null;Need(kind=="directory","World-pack storage is not a directory.");
            var paths=Directory.EnumerateFileSystemEntries(directory,"world-packs.*").Take(2).ToArray();
            if(paths.Length==0)return null;
            Need(paths.Length==1&&Path.GetFileName(paths[0])==FileName&&WorkspaceFileInventory.Kind(paths[0])=="file","Unrecognized world-pack evidence; preserve it before recovery.");
            byte[] bytes=WorkspaceLibraryCapture.ReadDocument(paths[0],MaximumBytes);_=Decode(bytes);return bytes;
        }
    }
}

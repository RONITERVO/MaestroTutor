// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Persistence
{
    /// <summary>Immutable bundled authoring sources, not a live scene or an installation service.
    /// Version 1 admits closed native construction data only; no asset downloads or executable DSL.</summary>
    public static class WorldPackCatalog
    {
        public const int MaximumBytes=512*1024,MaximumEntries=16;
        static readonly UTF8Encoding Utf8=new(false,true);
        public sealed class Entry
        {
            readonly byte[] bytes;readonly JObject creation;
            public string Id {get;} public int Version {get;} public string Hash {get;}
            public string EntryId {get;} public string Name {get;} public string Description {get;}
            public IReadOnlyList<string> Features {get;}
            internal Entry(byte[] source,JObject value,string hash) {
                bytes=(byte[])source.Clone();creation=(JObject)value["creation"].DeepClone();
                Id=(string)value["packId"];Version=(int)value["packVersion"];Hash=hash;
                EntryId=(string)value["entryId"];Name=(string)value["name"];Description=(string)value["description"];
                Features=Array.AsReadOnly(((JArray)value["features"]).Values<string>().ToArray());
            }
            internal byte[] SourceBytes=>(byte[])bytes.Clone();
            internal JObject Creation=>(JObject)creation.DeepClone();
        }
        static IReadOnlyList<Entry> available;
        // Capture on the Unity owner thread before dispatching detached preparation to a worker.
        public static IReadOnlyList<Entry> Available=>available??=Load();
        static IReadOnlyList<Entry> Load() {
            var all=Resources.LoadAll<TextAsset>("WorldPacks").Select(asset=>{var bytes=asset.bytes;return Read(bytes,ModelLibrary.Hash(bytes));})
                .OrderBy(entry=>entry.Id,StringComparer.Ordinal).ThenBy(entry=>entry.Version).ToArray();
            if(all.Length<1||all.Length>MaximumEntries||all.Select(e=>e.Id+":"+e.Version).Distinct(StringComparer.Ordinal).Count()!=all.Length)
                throw Invalid("Bundled world-pack identities are empty, duplicated or excessive.");
            return Array.AsReadOnly(all);
        }
        public static bool TryGet(string id,int version,string hash,out Entry entry) {
            entry=Available.FirstOrDefault(e=>e.Id==id&&e.Version==version&&e.Hash==hash);return entry!=null;
        }
        static InvalidDataException Invalid(string message)=>new(message);
        static JObject Parse(byte[] bytes) {
            if(bytes==null||bytes.Length<1||bytes.Length>MaximumBytes)throw Invalid("World-pack source exceeds its bounded size.");
            using var reader=new JsonTextReader(new StringReader(Utf8.GetString(bytes))){MaxDepth=48,DateParseHandling=DateParseHandling.None};
            var value=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            if(reader.Read())throw Invalid("Extra world-pack source content.");return value;
        }
        static JObject BatchSchema() {
            var schema=new BatchCreationCapability().InputSchema;
            // The pack contains only the saved creation data. Loading mode belongs to a live action.
            var fields=(JObject)schema["properties"];
            foreach(var key in fields.Properties().Select(p=>p.Name).Except(new[]{"blueprint","position","rotation","scale"}).ToArray())fields.Remove(key);
            schema.Remove("format");return schema;
        }
        static JObject Schema(JObject batch)=>Object(new JObject {
            ["format"]=Choice("maestro-bundled-world"),["version"]=Number(1,1,true),
            ["packId"]=Text("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)*$",96),
            ["packVersion"]=Number(1,int.MaxValue,true),["entryId"]=Text("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$",64),
            ["name"]=Text("^.{1,80}$",80),["description"]=Text("^.{1,400}$",400),
            ["author"]=Text("^.{1,100}$",100),["license"]=Text("^.{1,100}$",100),
            ["features"]=List(Text("^[a-zA-Z][a-zA-Z0-9.]+$",96),1,128),
            // Unsupported dependencies fail explicitly instead of becoming a permissive missing-file preview.
            ["dependencies"]=List(Object(new JObject()),0,0),["assets"]=List(Object(new JObject()),0,0),["creation"]=batch
        });
        static void RemoveFormats(JObject schema) {
            schema.Remove("format");
            if(schema["properties"] is JObject properties)foreach(var field in properties.Properties())RemoveFormats((JObject)field.Value);
            if(schema["items"] is JObject item)RemoveFormats(item);
            if(schema["oneOf"] is JArray variants)foreach(JObject variant in variants)RemoveFormats(variant);
        }
        static void FeaturesUsed(JToken value,JObject schema,HashSet<string> result) {
            if(schema["x-features"] is JArray declared)foreach(string feature in declared.Values<string>())result.Add(feature);
            var selected=Resolve(schema,value);if(selected==null)throw Invalid("Unsupported world-pack source variant.");
            if(!ReferenceEquals(selected,schema)){FeaturesUsed(value,selected,result);return;}
            if(value is JObject obj&&schema["properties"] is JObject fields)foreach(var property in obj.Properties())FeaturesUsed(property.Value,(JObject)fields[property.Name],result);
            else if(value is JArray array&&schema["items"] is JObject item)foreach(var child in array)FeaturesUsed(child,item,result);
        }
        internal static string[] RequiredFeatures(JObject creation) {
            var features=new HashSet<string>(StringComparer.Ordinal);FeaturesUsed(creation,BatchSchema(),features);
            return features.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        static void ClosedNativeSources(JObject creation) {
            foreach(JObject piece in (JArray)creation["blueprint"]["pieces"]){
                var source=(JObject)piece["source"];string kind=(string)source["kind"];
                if(kind is not ("recipe" or "prototype"))throw Invalid("This pack version supports inline recipes and closed native prototypes only.");
                if(kind=="prototype"){
                    var prototype=(JObject)source["prototype"];
                    if((string)prototype["geometry"]?["kind"]=="model"||prototype["walking"]!=null)
                        throw Invalid("Bundled pack model and motion dependencies are not supported by this source version.");
                    if(prototype["appearanceBindings"] is JArray bindings&&bindings.Any(b=>(string)b["kind"]=="material"))
                        throw Invalid("Imported material dependencies are not supported by this source version.");
                }
            }
            var resources=creation["blueprint"]["resources"];
            if(resources?["audioSources"] is JArray audio&&audio.Any(a=>(string)a["kind"]!="tone"))throw Invalid("Only local tone definitions are supported by this pack version.");
            if(resources?["appearances"] is JArray appearances&&appearances.Any(a=>(string)a["style"]?["patternMode"]=="image"||!string.IsNullOrEmpty((string)a["style"]?["imageHash"])))
                throw Invalid("Image dependencies are not supported by this pack version.");
        }
        static void LocalClosure(JObject creation) {
            var pieces=(JArray)creation["blueprint"]["pieces"];var resources=creation["blueprint"]["resources"];
            IEnumerable<JToken> Prototypes()=>pieces.Select(p=>p["source"]["prototype"]).Where(p=>p!=null);
            void Exact(string field,IEnumerable<string> references) {
                var definitions=(resources?[field] as JArray??new JArray()).Select(d=>(string)d["id"]).ToArray();
                if(definitions.Distinct(StringComparer.Ordinal).Count()!=definitions.Length||!definitions.ToHashSet(StringComparer.Ordinal).SetEquals(references))
                    throw Invalid("World-pack resources must exactly close their typed local references: "+field);
            }
            Exact("appearances",Prototypes().SelectMany(p=>(p["appearanceBindings"] as JArray??new JArray()).Select(b=>(string)b["appearanceId"])));
            Exact("audioSources",Prototypes().SelectMany(p=>(p["audioEmitters"] as JArray??new JArray()).Select(e=>(string)e["source"])));
            Exact("environmentProfiles",Prototypes().Select(p=>(string)p["environmentProfile"]).Where(id=>!string.IsNullOrEmpty(id)));
            Exact("visibilityLayers",Prototypes().Select(p=>(string)p["visibilityLayer"]).Where(id=>!string.IsNullOrEmpty(id)));
        }
        internal static Entry Read(byte[] bytes,string expectedHash) {
            if(!ModelLibrary.ValidHash(expectedHash)||bytes==null||bytes.Length>MaximumBytes||ModelLibrary.Hash(bytes)!=expectedHash)throw Invalid("World-pack source hash does not match the selected exact bytes.");
            var value=Parse(bytes);var batch=BatchSchema();var schema=Schema(batch);var structural=(JObject)schema.DeepClone();RemoveFormats(structural);
            if(!CapabilityArguments.Validate(value,structural,out var error))throw Invalid(error);
            foreach(string key in new[]{"name","description","author","license"})
                if(((string)value[key]).Any(c=>c=='\u2028'||c=='\u2029'||char.IsSurrogate(c)))throw Invalid("World-pack labels must fit the compact observation contract.");
            var creation=(JObject)value["creation"];ClosedNativeSources(creation);LocalClosure(creation);
            var features=((JArray)value["features"]).Values<string>().ToArray();
            if(features.Distinct(StringComparer.Ordinal).Count()!=features.Length||!features.ToHashSet(StringComparer.Ordinal).SetEquals(RequiredFeatures(creation)))
                throw Invalid("World-pack features must exactly declare the supported creation vocabulary used by its source.");
            // Strict source/dependency/feature admission precedes canonical semantic validation, whose
            // private throwaway prototype identities are never retained or published as an instance.
            if(!CapabilityArguments.Validate(value,schema,out error)||!CreationBatch.ValidResourceWire(creation,out error)||!CreationBatch.Read(creation).blueprint.Validate(out error))throw Invalid(error);
            return new Entry(bytes,value,expectedHash);
        }
    }
}

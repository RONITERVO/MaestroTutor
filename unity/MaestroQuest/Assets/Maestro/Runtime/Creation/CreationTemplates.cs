// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Creation
{
    /// <summary>Trusted bundled examples. Identity hashes exact file bytes; every expansion is detached editable data.</summary>
    public static class CreationTemplates
    {
        public const string Feature="creationTemplates.v1";
        public sealed class Entry
        {
            readonly JObject source;
            public string Hash {get;}
            public string Id=>(string)source["id"];
            public string Name=>(string)source["name"];
            public JObject Source=>(JObject)source.DeepClone();
            public RoomRecipe Recipe=>JsonUtility.FromJson<RoomRecipe>(source["definition"]["recipe"].ToString());
            public DrawingSurface[] Surfaces {get {
                if(source["definition"]["surfaces"] is not JArray surfaces)return Array.Empty<DrawingSurface>();
                var result=JsonUtility.FromJson<RoomObjectData>(new JObject {["surfaces"]=surfaces.DeepClone()}.ToString()).surfaces;
                foreach(var surface in result)surface.strokes=Array.Empty<SurfaceStroke>();
                return result;
            }}
            public DrawingTip[] DrawingTips=>source["definition"]["drawingTips"] is JArray tips?JsonUtility.FromJson<RoomObjectData>(new JObject {["drawingTips"]=tips.DeepClone()}.ToString()).drawingTips:Array.Empty<DrawingTip>();
            public CollisionRecipe Collision=>JsonUtility.FromJson<CollisionRecipe>(source["definition"]["collision"].ToString());
            public ObjectPhysicsSettings Physics=>JsonUtility.FromJson<ObjectPhysicsSettings>(source["definition"]["physics"].ToString());
            internal Entry(byte[] bytes) {
                if(bytes==null||bytes.Length>131072)throw new InvalidOperationException("Invalid bundled template size");
                using var reader=new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(new UTF8Encoding(false,true).GetString(bytes))) {MaxDepth=24,DateParseHandling=Newtonsoft.Json.DateParseHandling.None};
                source=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                if(reader.Read()||!CapabilityArguments.Validate(source,Schema(),out _))throw new InvalidOperationException("Invalid bundled creation template");
                Hash=ModelLibrary.Hash(bytes);
            }
            public JObject Summary(int index,int total)=>new() {
                ["index"]=index,["total"]=total,["hash"]=Hash,["id"]=Id,["name"]=Name,["details"]=new JObject {["description"]=source["description"].DeepClone(),["tags"]=string.Join(", ",((JArray)source["tags"]).Values<string>()),["author"]=source["author"].DeepClone(),["license"]=source["license"].DeepClone()},
                ["cost"]=new JObject {["parts"]=Recipe.parts.Length,["tracks"]=Recipe.tracks.Length,["generatedVertices"]=RecipeLathe.VertexCost(Recipe),["collisionPieces"]=Math.Max(1,Collision.Pieces)},["physics"]=source["definition"]["physics"].DeepClone()
            };
        }
        static IReadOnlyList<Entry> entries;
        public static IReadOnlyList<Entry> All {
            get {
                if(entries!=null)return entries;
                var loaded=Resources.LoadAll<TextAsset>("Creation/Templates").Select(asset=>new Entry(asset.bytes)).OrderBy(entry=>entry.Id,StringComparer.Ordinal).ToArray();
                if(loaded.Length<1||loaded.Length>32||loaded.Select(entry=>entry.Id).Distinct().Count()!=loaded.Length||loaded.Select(entry=>entry.Hash).Distinct().Count()!=loaded.Length)throw new InvalidOperationException("Invalid bundled creation library");
                return entries=Array.AsReadOnly(loaded);
            }
        }
        public static Entry Find(string hash)=>All.FirstOrDefault(entry=>entry.Hash==hash);
        // Explicit complete schemas prevent JsonUtility from silently discarding mistyped fields.
        static JObject SurfaceSchema(){var s=DrawingSurfaceCapability.DefinitionSchema();((JObject)s["properties"])["id"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32);((JObject)s["properties"])["version"]=Number(1,1,true);((JArray)s["required"]).Add("id");((JArray)s["required"]).Add("version");return s;}
        static JObject TipSchema(){var s=DrawingTipCapability.DefinitionSchema();((JObject)s["properties"])["version"]=Number(1,1,true);((JArray)s["required"]).Add("version");return s;}
        internal static JObject Schema()=>Object(new JObject {
            ["format"]=Choice("maestro-creation-template"),["version"]=Number(1,1,true),["id"]=Text("^[a-z][a-z0-9-]{0,31}$",32),["name"]=Text("^.{1,80}$",80),
            ["description"]=Text("^.{1,128}$",128),["tags"]=List(Text("^[a-z][a-z0-9-]{0,23}$",24),1,8),["author"]=Text("^.{1,80}$",80),["license"]=Text("^.{1,64}$",64),
            ["definition"]=Object(new JObject {["version"]=Number(1,1,true),["recipe"]=RecipeSchema(),["collision"]=CollisionCapability.RecipeSchema(),["physics"]=PhysicsSettingsCapability.SettingsSchema(),["surfaces"]=List(SurfaceSchema(),0,4),["drawingTips"]=List(TipSchema(),0,1)},"surfaces","drawingTips")
        });
    }
}

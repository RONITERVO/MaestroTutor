// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Persistence
{
    /// <summary>Detached documents and bounded content-addressed payload readers. The caller must capture
    /// all documents at one coordinated boundary; this codec does not freeze a live room or its writers.</summary>
    public sealed class WorkspaceArchiveSnapshot
    {
        internal readonly Dictionary<string,byte[]> Documents;
        internal readonly Dictionary<string,Func<Stream>> Assets;
        internal readonly WorkspaceArchiveMetadata Metadata;
        public WorkspaceArchiveSnapshot(IReadOnlyDictionary<string,byte[]> documents,IReadOnlyDictionary<string,Func<Stream>> assets)
        {
            if(documents==null||assets==null)throw new ArgumentNullException();
            Documents=documents.ToDictionary(x=>x.Key,x=>(byte[])(x.Value??throw new InvalidDataException("Missing document bytes.")).Clone(),StringComparer.Ordinal);
            Assets=assets.ToDictionary(x=>x.Key,x=>x.Value??throw new InvalidDataException("Missing asset reader."),StringComparer.Ordinal);
            WorkspaceArchiveMetadata.CheckNames(Documents.Keys.Concat(Assets.Keys));
            if(Documents.Keys.Any(WorkspaceArchiveMetadata.IsAsset)||Assets.Keys.Any(x=>!WorkspaceArchiveMetadata.IsAsset(x)))throw new InvalidDataException("Invalid snapshot entry type.");
            Metadata=WorkspaceArchiveMetadata.Read(Documents,Assets.Keys);
        }
    }
    public sealed class WorkspaceArchiveSummary
    {
        public int Files {get;internal set;}
        public int Models {get;internal set;}
        public int Motions {get;internal set;}
        public int Sounds {get;internal set;}
        public int Images {get;internal set;}
        public string[] MissingImages {get;internal set;}=Array.Empty<string>();
        public int Modules {get;internal set;}
        public int UnavailablePrograms {get;internal set;}
        public long Bytes {get;internal set;}
        public string[] MissingModels {get;internal set;}=Array.Empty<string>();
        public string[] MissingSounds {get;internal set;}=Array.Empty<string>();
        public string[] MissingMotions {get;internal set;}=Array.Empty<string>();
        public string[] MissingControllerPrograms {get;internal set;}=Array.Empty<string>();
    }
    internal sealed class WorkspaceArchiveMetadata
    {
        internal static int DocumentVersion(string path)=>path==RoomStorage.FileName?RoomDocument.CurrentVersion:2;
        internal static readonly string[] Required={RoomStorage.FileName,"behaviours.v2.json","controls.v2.json","avatar-activities.v2.json","motions/motions.v2.json"};
        internal readonly Dictionary<string,MotionEntry> Motions=new(StringComparer.Ordinal);
        internal WorkspaceArchiveSummary Summary;
        internal RoomAudioDefinition[] SoundReferences=Array.Empty<RoomAudioDefinition>();
        static readonly UTF8Encoding Utf8=new(false,true);
        internal static bool IsModel(string path)=>path.StartsWith("models/",StringComparison.Ordinal)&&path.EndsWith(".glb",StringComparison.Ordinal);
        internal static bool IsMotion(string path)=>path.StartsWith("motions/",StringComparison.Ordinal)&&path.EndsWith(".motion.glb",StringComparison.Ordinal);
        internal static bool IsSound(string path)=>HasHash(path,"audio/",".wav");
        internal static bool IsImage(string path)=>HasHash(path,"images/",".image");
        internal static bool IsAsset(string path)=>IsModel(path)||IsMotion(path)||IsSound(path)||IsImage(path);
        internal static string HashName(string path)=>path.Substring(path.IndexOf('/')+1,64);
        static bool HasHash(string path,string prefix,string suffix)=>path.Length==prefix.Length+64+suffix.Length&&path.StartsWith(prefix,StringComparison.Ordinal)&&path.EndsWith(suffix,StringComparison.Ordinal)&&ModelLibrary.ValidHash(path.Substring(prefix.Length,64));
        internal static int Limit(string path)=>path switch {
            ProgramMemoryStore.FileName=>ProgramMemoryDocument.MaximumBytes,
            RoomStorage.FileName=>4*1024*1024,"behaviours.v2.json"=>512*1024,"controls.v2.json"=>8192,
            "avatar-activities.v2.json"=>256*1024,"motions/motions.v2.json"=>16*1024*1024,
            _ when HasHash(path,"images/",".image")=>SurfaceImage.MaximumBytes,
            _ when HasHash(path,"images/",".txt")=>1024,
            _ when HasHash(path,"audio/",".wav")=>WaveAudio.MaximumBytes,
            _ when HasHash(path,"audio/",".txt")=>1024,
            _ when HasHash(path,"models/",".glb")=>ModelInspection.MaximumBytes,
            _ when HasHash(path,"models/",".txt")=>128*1024,
            _ when HasHash(path,"motions/",".motion.glb")=>MotionPack.MaximumBytes,
            _ when HasHash(path,"program-modules.v1/",".json")=>ProgramModuleLibrary.MaximumBytes,
            _=>throw new InvalidDataException("Unknown or unsafe workspace archive path: "+path)
        };
        internal static void CheckNames(IEnumerable<string> values)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);foreach(var path in values){if(path==null||!names.Add(path))throw new InvalidDataException("Duplicate archive path.");_=Limit(path);}
            if(names.Count>WorkspaceArchive.MaximumEntries||Required.Any(x=>!names.Contains(x))||names.Count(IsImage)>ImageLibrary.MaximumFiles||names.Count(IsSound)>AudioLibrary.MaximumFiles||names.Count(IsModel)>32||names.Count(IsMotion)>MotionLibrary.MaximumEntries||names.Count(x=>x.StartsWith("program-modules.v1/",StringComparison.Ordinal))>ProgramModuleLibrary.MaximumEntries)throw new InvalidDataException("Missing or excessive workspace entries.");
            if(names.Any(x=>HasHash(x,"images/",".txt")&&!names.Contains(x.Substring(0,x.Length-4)+".image")))throw new InvalidDataException("Image information has no matching asset.");
            if(names.Any(x=>HasHash(x,"audio/",".txt")&&!names.Contains(x.Substring(0,x.Length-4)+".wav")))throw new InvalidDataException("Sound information has no matching asset.");
            if(names.Any(x=>x.StartsWith("models/",StringComparison.Ordinal)&&x.EndsWith(".txt",StringComparison.Ordinal)&&!names.Contains(x.Substring(0,x.Length-4)+".glb")))throw new InvalidDataException("Model information has no matching asset.");
        }
        static JObject Json(byte[] bytes,int maximum)
        {
            if(bytes==null||bytes.Length<1||bytes.Length>maximum)throw new InvalidDataException("Invalid workspace document size.");
            using var reader=new JsonTextReader(new StringReader(Utf8.GetString(bytes))) {MaxDepth=48,DateParseHandling=DateParseHandling.None};
            var json=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            if(reader.Read())throw new InvalidDataException("Extra workspace document content.");return json;
        }
        static T Document<T>(Dictionary<string,byte[]> documents,string path,params string[] fields)
        {
            var bytes=documents[path];var json=Json(bytes,Limit(path));
            if(json.Count!=fields.Length||fields.Any(x=>!json.ContainsKey(x))||json["version"]?.Type!=JTokenType.Integer||(int)json["version"]!=DocumentVersion(path))throw new InvalidDataException("Unsupported workspace document: "+path);
            if(path==RoomStorage.FileName&&(!RoomViewpoint.ValidWire(json)||!RoomWorldIdentity.ValidWire(json)||!RoomEnvironmentProfile.ValidWire(json)||!RoomAppearance.ValidWire(json)||!RoomVisibilityLayer.ValidWire(json)||!RoomLighting.ValidWire(json)||!RoomWorldTime.ValidWire(json)||!RoomWeather.ValidWire(json)||!RoomAudioDefinition.ValidWire(json)||!RoomWindow.ValidWire(json)))throw new InvalidDataException("Invalid workspace viewpoint or world identity.");
            return JsonUtility.FromJson<T>(Utf8.GetString(bytes));
        }
        internal static WorkspaceArchiveMetadata Read(Dictionary<string,byte[]> documents,IEnumerable<string> assetNames)
        {
            var assets=assetNames.ToHashSet(StringComparer.Ordinal);CheckNames(documents.Keys.Concat(assets));
            foreach(var pair in documents){if(pair.Value.Length<1||pair.Value.Length>Limit(pair.Key))throw new InvalidDataException("Workspace document exceeds its limit.");}
            if(documents.TryGetValue(ProgramMemoryStore.FileName,out var memory))_=ProgramMemoryDocument.Decode(memory);
            var room=Document<RoomDocument>(documents,RoomStorage.FileName,"version","objects","structures","audioSources","viewpoint","world","environmentProfiles","appearances","visibilityLayers","lighting","worldTime","weather");RoomStorage.Normalize(room);
            if(!room.Validate(out var issue))throw new InvalidDataException(issue);
            var rules=Document<RuleDocument>(documents,"behaviours.v2.json","version","sequences","bindings","buttons");
            var ruleJson=Json(documents["behaviours.v2.json"],Limit("behaviours.v2.json"));
            if(ruleJson["sequences"] is not JArray sequences||!sequences.All(RuleSequence.ValidWire)||!rules.Validate(out issue,true))throw new InvalidDataException("Invalid behaviour snapshot.");
            var controls=Document<ControllerPreferences>(documents,"controls.v2.json","version","avatarStick","userStick","deadZone","userSpeed","buttons");
            if(!controls.Validate())throw new InvalidDataException("Invalid controller snapshot.");
            var activities=Document<AvatarActivityDocument>(documents,"avatar-activities.v2.json","version","avatars");
            if(!activities.Valid())throw new InvalidDataException("Invalid avatar activity snapshot.");
            var motions=MotionLibrary.DecodeSnapshot(documents["motions/motions.v2.json"]);var result=new WorkspaceArchiveMetadata();
            foreach(var entry in motions.entries.Where(x=>!x.removed))result.Motions.Add("motions/"+entry.hash+".motion.glb",entry);
            if(!assets.Where(IsMotion).ToHashSet(StringComparer.Ordinal).SetEquals(result.Motions.Keys))throw new InvalidDataException("Motion downloads do not match their catalogue; keep removed entries without payloads.");
            foreach(var pair in documents.Where(x=>x.Key.StartsWith("program-modules.v1/",StringComparison.Ordinal)))ProgramModuleLibrary.ImportDefinition(HashName(pair.Key),Json(pair.Value,Limit(pair.Key)));
            foreach(var pair in documents.Where(x=>x.Key.StartsWith("models/",StringComparison.Ordinal)))_=Utf8.GetString(pair.Value);
            foreach(var pair in documents.Where(x=>x.Key.StartsWith("audio/",StringComparison.Ordinal)))_=Utf8.GetString(pair.Value);
            foreach(var pair in documents.Where(x=>x.Key.StartsWith("images/",StringComparison.Ordinal)))_=Utf8.GetString(pair.Value);
            result.SoundReferences=room.audioSources.Where(s=>s.kind=="clip").ToArray();
            // Includes literal source definitions captured in construction/program modules;
            // dynamically computed identities cannot be predicted, so capture all private clips.
            var soundHashes=result.SoundReferences.Select(s=>s.assetHash).Concat(documents.Where(p=>p.Key.StartsWith("program-modules.v1/",StringComparison.Ordinal)||p.Key=="behaviours.v2.json").SelectMany(p=>Json(p.Value,Limit(p.Key)).Descendants().OfType<JProperty>().Where(x=>x.Name=="assetHash"&&x.Value.Type==JTokenType.String&&ModelLibrary.ValidHash((string)x.Value)).Select(x=>(string)x.Value))).Distinct().ToArray();
            var imageHashes=room.appearances.Select(a=>a.style.imageHash).Concat(documents.Where(p=>p.Key.StartsWith("program-modules.v1/",StringComparison.Ordinal)||p.Key=="behaviours.v2.json").SelectMany(p=>Json(p.Value,Limit(p.Key)).Descendants().OfType<JProperty>().Where(x=>x.Name=="imageHash"&&x.Value.Type==JTokenType.String).Select(x=>(string)x.Value))).Where(ModelLibrary.ValidHash).Distinct().ToArray();
            var modelReferences=room.objects.Select(x=>x.modelHash).Concat(activities.avatars.Select(x=>x.modelHash)).Where(ModelLibrary.ValidHash).Distinct().ToArray();
            var motionReferences=room.objects.Select(x=>x.walkMotionId).Concat(activities.avatars.SelectMany(x=>x.roles).SelectMany(x=>x.choices).Select(x=>x.motionId)).Where(x=>!string.IsNullOrEmpty(x)).Distinct();
            var availableMotions=motions.entries.Where(x=>!x.removed).Select(x=>x.id).ToHashSet();var sequenceIds=rules.sequences.Select(x=>x.id).ToHashSet();
            result.Summary=new WorkspaceArchiveSummary {Files=documents.Count+assets.Count,Models=assets.Count(IsModel),Images=assets.Count(IsImage),MissingImages=imageHashes.Where(hash=>!assets.Contains("images/"+hash+".image")).ToArray(),Sounds=assets.Count(IsSound),Motions=assets.Count(IsMotion),Modules=documents.Keys.Count(x=>x.StartsWith("program-modules.v1/",StringComparison.Ordinal)),UnavailablePrograms=rules.sequences.Count(x=>rules.ProgramError(x)!=null),
                MissingSounds=soundHashes.Where(hash=>!assets.Contains("audio/"+hash+".wav")).ToArray(),MissingModels=modelReferences.Where(hash=>!assets.Contains("models/"+hash+".glb")).ToArray(),MissingMotions=motionReferences.Where(id=>!availableMotions.Contains(id)).ToArray(),MissingControllerPrograms=controls.buttons.Where(x=>x.command==ControllerCommand.Sequence&&!sequenceIds.Contains(x.sequenceId)).Select(x=>x.sequenceId).Distinct().ToArray()};
            return result;
        }
        internal void ValidateAsset(string path,byte[] bytes)
        {
            string hash=HashName(path);if(ModelLibrary.Hash(bytes)!=hash)throw new InvalidDataException("Asset content does not match its identity: "+path);
            if(IsImage(path)){_=SurfaceImage.Inspect(bytes);return;}
            if(IsSound(path)){var info=WaveAudio.Inspect(bytes);if(SoundReferences.Any(s=>s.assetHash==hash&&Math.Abs(s.seconds-info.Seconds)>.0001))throw new InvalidDataException("Saved sound duration differs from its exact WAV asset.");return;}
            if(IsModel(path)){ModelInspection.Inspect(bytes);return;}
            if(!Motions.TryGetValue(path,out var entry))throw new InvalidDataException("Motion metadata is missing.");
            var pack=MotionPack.Read(bytes);
            if(pack.Hash!=entry.hash||pack.RigHash!=entry.rigHash||pack.Bytes.Length!=entry.bytes||pack.CurveValues!=entry.curveValues||Math.Abs(pack.Duration-entry.duration)>.0001f)throw new InvalidDataException("Motion metadata differs from the exact payload.");
        }
    }
}

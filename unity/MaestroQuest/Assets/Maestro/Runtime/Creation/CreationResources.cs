// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>Closed, typed module-local definitions. IDs are local symbols,
    /// never permission to reuse or overwrite a definition in the destination.</summary>
    [Serializable] public sealed class CreationResources
    {
        public int version=1;
        public RoomAppearance[] appearances=Array.Empty<RoomAppearance>();
        public RoomAudioDefinition[] audioSources=Array.Empty<RoomAudioDefinition>();
        public RoomEnvironmentProfile[] environmentProfiles=Array.Empty<RoomEnvironmentProfile>();
        public RoomVisibilityLayer[] visibilityLayers=Array.Empty<RoomVisibilityLayer>();
        // JsonUtility materializes a missing inline class using its defaults.
        // Only internal old-format roundtrips accept this empty shell; Read
        // rejects any explicit old-format resource field at the public boundary.
        internal bool EmptySerializationShell=>version==1&&appearances!=null&&audioSources!=null&&environmentProfiles!=null&&visibilityLayers!=null&&!Any;
        internal bool Any=>(appearances?.Length??0)+(audioSources?.Length??0)+(environmentProfiles?.Length??0)+(visibilityLayers?.Length??0)>0;
        internal static bool Uses(RoomObjectData value)=>value.appearanceBindings.Length>0||value.audioEmitters.Length>0||value.environmentProfile!=""||value.visibilityLayer!="";
        internal static bool ValidatePrototype(RoomObjectData value,out string error) {
            error="Use bounded, valid local dependency references";
            if(value.appearanceBindings==null||value.audioEmitters==null||value.environmentProfile==null||value.visibilityLayer==null||value.visibilityLayer!=""&&!RoomWorldIdentity.Id(value.visibilityLayer)||
                value.environmentProfile!=""&&!RoomWorldIdentity.Id(value.environmentProfile)||value.audioEmitters.Length>4||value.audioEmitters.Any(e=>e==null||!RoomWorldIdentity.Id(e.source)))return false;
            var ids=value.appearanceBindings.Where(b=>b!=null).Select(b=>b.appearanceId).ToHashSet(StringComparer.Ordinal);
            if(!RoomAppearance.ValidateBindings(value,ids,out error))return false;
            var sounds=value.audioEmitters.Select(e=>e.source).ToHashSet(StringComparer.Ordinal);var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var emitter in value.audioEmitters){error="Sound emitter IDs must be distinct on their object";if(!keys.Add(emitter.id)||!emitter.Validate(value,sounds,out error))return false;}
            error=null;return true;
        }
        internal bool Validate(RoomObjectData[] objects,out string error) {
            error="Use version 1 or 2 of a complete construction resource bundle";
            if(version is not (1 or 2)||appearances==null||audioSources==null||environmentProfiles==null||visibilityLayers==null||version<2&&(visibilityLayers.Length>0||objects.Any(o=>o.visibilityLayer!="")))return false;
            if(!RoomAppearance.ValidateCollection(appearances,objects,RoomDocument.CurrentVersion,out error)||
                !RoomAudioDefinition.ValidateCollection(audioSources,objects,RoomDocument.CurrentVersion,out error)||
                !RoomEnvironmentProfile.ValidateCollection(environmentProfiles,objects,RoomDocument.CurrentVersion,out error)||
                !RoomVisibilityLayer.ValidateCollection(visibilityLayers,objects,RoomDocument.CurrentVersion,out error))return false;
            error="Bundle exactly the definitions referenced by this construction, including dormant bindings";
            if(!appearances.Select(a=>a.id).ToHashSet().SetEquals(objects.SelectMany(o=>o.appearanceBindings).Select(b=>b.appearanceId))||
                !audioSources.Select(a=>a.id).ToHashSet().SetEquals(objects.SelectMany(o=>o.audioEmitters).Select(e=>e.source))||
                !environmentProfiles.Select(p=>p.id).ToHashSet().SetEquals(objects.Select(o=>o.environmentProfile).Where(id=>id!=""))||
                !visibilityLayers.Select(p=>p.id).ToHashSet().SetEquals(objects.Select(o=>o.visibilityLayer).Where(id=>id!="")))return false;
            error=null;return true;
        }
        static Dictionary<string,string> Map(IEnumerable<string> ids,bool fresh)=>ids.Distinct(StringComparer.Ordinal).Select((id,i)=>(id,next:fresh?Guid.NewGuid().ToString("N"):(i+1).ToString("x32"))).ToDictionary(p=>p.id,p=>p.next,StringComparer.Ordinal);
        static void Bind(RoomObjectData[] objects,Dictionary<string,string> styles,Dictionary<string,string> sounds,Dictionary<string,string> profiles,Dictionary<string,string> layers) {
            foreach(var obj in objects) {
                foreach(var b in obj.appearanceBindings)b.appearanceId=styles[b.appearanceId];
                foreach(var e in obj.audioEmitters)e.source=sounds[e.source];
                if(obj.environmentProfile!="")obj.environmentProfile=profiles[obj.environmentProfile];
                if(obj.visibilityLayer!="")obj.visibilityLayer=layers[obj.visibilityLayer];
            }
        }
        CreationResources Remap(RoomObjectData[] objects,Dictionary<string,string> styles,Dictionary<string,string> sounds,Dictionary<string,string> profiles,Dictionary<string,string> layers) {
            var result=new CreationResources {version=layers.Count>0?2:1,
                appearances=styles.Select(p=>{var a=appearances.Single(x=>x.id==p.Key).Copy();a.id=p.Value;return a;}).ToArray(),
                audioSources=sounds.Select(p=>{var a=audioSources.Single(x=>x.id==p.Key).Copy();a.id=p.Value;return a;}).ToArray(),
                environmentProfiles=profiles.Select(p=>{var a=environmentProfiles.Single(x=>x.id==p.Key).Copy();a.id=p.Value;return a;}).ToArray(),
                visibilityLayers=layers.Select(p=>{var a=visibilityLayers.Single(x=>x.id==p.Key).Copy();a.id=p.Value;return a;}).ToArray()};
            Bind(objects,styles,sounds,profiles,layers);return result;
        }
        internal CreationResources Instantiate(RoomObjectData[] objects)=>Remap(objects,Map(appearances.Select(a=>a.id),true),Map(audioSources.Select(a=>a.id),true),Map(environmentProfiles.Select(p=>p.id),true),Map(visibilityLayers.Select(p=>p.id),true));
        internal static CreationResources Capture(RoomObjectData[] objects,RoomDocument room) {
            var source=new CreationResources{appearances=room.appearances,audioSources=room.audioSources,environmentProfiles=room.environmentProfiles,visibilityLayers=room.visibilityLayers};
            return source.Remap(objects,Map(objects.SelectMany(o=>o.appearanceBindings.OrderBy(b=>b.Key,StringComparer.Ordinal)).Select(b=>b.appearanceId),false),
                Map(objects.SelectMany(o=>o.audioEmitters.OrderBy(e=>e.id,StringComparer.Ordinal)).Select(e=>e.source),false),Map(objects.Select(o=>o.environmentProfile).Where(id=>id!=""),false),Map(objects.Select(o=>o.visibilityLayer).Where(id=>id!=""),false));
        }
    }
}

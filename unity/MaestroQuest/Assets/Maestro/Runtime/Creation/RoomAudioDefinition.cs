// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Saved source data, never a connection, credential or playback handle.</summary>
    [Serializable] public sealed class RoomAudioDefinition
    {
        public const int MaximumSources=32;
        public int version=1;
        public string id,name="",kind="tone",wave="sine";
        public float frequency=440,endFrequency=440,seconds=.25f,attack=.01f,release=.04f;
        public int seed=1;
        public RoomAudioDefinition Copy()=>(RoomAudioDefinition)MemberwiseClone();
        public bool Validate(out string error)
        {
            error="An audio source needs version 1, a stable ID and a readable name";
            if(version!=1||!Guid.TryParseExact(id,"N",out _)||name==null||name.Length>80||name.Any(char.IsControl))return false;
            error="This audio source requires a supported tone recipe";
            if(kind!="tone"||wave is not ("sine" or "triangle" or "noise"))return false;
            error="Tone duration, frequency or envelope is outside its supported range";
            if(!Range(frequency,20,10000)||!Range(endFrequency,20,10000)||!Range(seconds,.03f,30)||!Range(attack,.005f,2)||!Range(release,.005f,2)||(double)attack+release>seconds+1e-7||seed<1)return false;
            error=null;return true;
        }
        internal static bool Range(float value,float min,float max)=>float.IsFinite(value)&&value>=min&&value<=max;
        internal static bool ValidateCollection(RoomAudioDefinition[] sources,RoomObjectData[] objects,int version,out string error)
        {
            error="Audio definitions require the current room format";
            if(version<21&&((sources?.Length??0)>0||objects.Any(x=>(x.audioEmitters?.Length??0)>0)))return false;
            error="This room has invalid, duplicate or excessive audio sources";
            if(sources==null||sources.Length>MaximumSources)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var s in sources){error="Audio source IDs must be distinct";if(s==null||!ids.Add(s.id)||!s.Validate(out error))return false;}
            int count=0;
            foreach(var obj in objects) {
                var emitters=obj.audioEmitters??Array.Empty<RoomAudioEmitter>();count+=emitters.Length;
                error="Keep at most four sound emitters per object and 32 in the room";
                if(emitters.Length>4||count>32)return false;
                var keys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var e in emitters){error="Sound emitter IDs must be distinct on their object";if(e==null||!keys.Add(e.id)||!e.Validate(obj,ids,out error))return false;}
            }
            error=null;return true;
        }
    }
    [Serializable] public sealed class RoomAudioEmitter
    {
        public int version=1;
        public string id="sound",source,part="",joint="",role="effects";
        public Vector3 position;
        public bool spatial=true;
        public float gain=.25f,minDistance=.5f,maxDistance=15;
        public RoomAudioEmitter Copy()=>(RoomAudioEmitter)MemberwiseClone();
        internal bool Validate(RoomObjectData owner,HashSet<string> sources,out string error)
        {
            error="An emitter needs version 1, a stable local ID and an existing audio source";
            if(version!=1||id==null||!Regex.IsMatch(id,"^[a-zA-Z][a-zA-Z0-9_]{0,23}$")||source==null||!sources.Contains(source))return false;
            error="The sound attachment must name an existing recipe part or a Maestro joint";
            if(part==null||joint==null||part.Length>0&&(joint.Length>0||owner.recipe?.parts.Any(p=>p.id==part)!=true))return false;
            if(joint.Length>0&&(owner.kind!=RoomObjectKind.Maestro||!Enum.TryParse<PoseJoint>(joint,out var bone)||!Enum.IsDefined(typeof(PoseJoint),bone)||bone.ToString()!=joint))return false;
            error="Sound gain, distance, role or local position is invalid";
            if(!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>100||!RoomAudioDefinition.Range(gain,0,1)||!RoomAudioDefinition.Range(minDistance,.1f,10)||!RoomAudioDefinition.Range(maxDistance,minDistance,50)||role is not ("effects" or "media" or "ambience"))return false;
            error=null;return true;
        }
    }
    public sealed class AudioDefinitionEdits
    {
        public RoomAudioDefinition[] Replacements=Array.Empty<RoomAudioDefinition>();
        public string[] Removals=Array.Empty<string>();
        public bool Validate(out string error)
        {
            error="Audio edits need distinct valid definitions and removals";
            if(Replacements==null||Removals==null||Replacements.Length>32||Removals.Length>32)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var value in Replacements){error="Audio edits need distinct valid definitions";if(value==null||!ids.Add(value.id)||!value.Validate(out error))return false;}
            foreach(var id in Removals){error="Audio removals must be distinct and separate from replacements";if(!Guid.TryParseExact(id,"N",out _)||!ids.Add(id))return false;}
            error=null;return true;
        }
        internal RoomAudioDefinition[] Apply(RoomAudioDefinition[] current)
        {
            var values=current.ToDictionary(x=>x.id,x=>x.Copy(),StringComparer.Ordinal);
            foreach(var id in Removals)values.Remove(id);
            foreach(var value in Replacements)values[value.id]=value.Copy();
            return values.Values.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
        }
    }
    public sealed partial class RoomJournal
    {
        readonly Dictionary<string,RoomAudioDefinition> audioSources=new(StringComparer.Ordinal);
        readonly Dictionary<string,int> audioRevisions=new(StringComparer.Ordinal);
        public int AudioRevision(string id)=>id!=null&&audioRevisions.TryGetValue(id,out var value)?value:0;
        public RoomAudioDefinition ReadAudio(string id)=>id!=null&&audioSources.TryGetValue(id,out var value)?value.Copy():null;
        internal RoomAudioEmitter ReadAudioEmitter(string target,string key)=>items.TryGetValue(target,out var item)?item.audioEmitters?.FirstOrDefault(e=>e.id==key)?.Copy():null;
        public RoomAudioDefinition[] AudioSnapshot()=>audioSources.Values.OrderBy(x=>x.id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray();
        void SetAudio(RoomAudioDefinition[] before,RoomAudioDefinition[] after)
        {
            foreach(var s in before){audioSources.Remove(s.id);audioRevisions.Remove(s.id);}
            foreach(var s in after){audioSources[s.id]=s.Copy();audioRevisions[s.id]=clock.Next++;}
        }
        [Serializable] sealed class AudioDelta { public RoomAudioDefinition[] audioSources; }
        static bool EquivalentAudio(RoomAudioDefinition[] a,RoomAudioDefinition[] b)=>JsonUtility.ToJson(new AudioDelta {audioSources=a.OrderBy(x=>x.id).ToArray()})==JsonUtility.ToJson(new AudioDelta {audioSources=b.OrderBy(x=>x.id).ToArray()});
    }
}

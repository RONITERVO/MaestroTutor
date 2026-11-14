// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        public RoomAudioDefinition[] AudioSources()=>journal.AudioSnapshot();
        public RoomAudioDefinition ReadAudio(string id)=>journal.ReadAudio(id);
        internal RoomAudioEmitter ReadAudioEmitter(string target,string key)=>journal.ReadAudioEmitter(target,key);
        public int AudioRevision(string id)=>journal.AudioRevision(id);
        internal bool CanEditAudio(string id,int revision,RoomAudioDefinition definition,out string error)
        {
            if(!CanEditStructures(out error))return false;
            error="The sound changed or is missing; read its current source revision";
            var current=ReadAudio(id);
            if(!Guid.TryParseExact(id,"N",out _)||AudioRevision(id)!=revision||definition==null&&current==null)return false;
            if(definition!=null) {
                error="The sound identity must match the edited source";
                if(definition.id!=id||!definition.Validate(out error))return false;
                error="The room already has the maximum number of audio sources";
                if(current==null&&journal.AudioCount>=RoomAudioDefinition.MaximumSources)return false;
            } else if(journal.AudioReferenced(id)){error="Reassign or remove the emitters before removing their sound source";return false;}
            error=null;return true;
        }
        internal bool CanEditAudioEmitter(string target,int revision,string key,RoomAudioEmitter emitter,out string error)
        {
            if(!CanEditObject(target,false,out error))return false;
            if(!new CapabilityContext(this,GetComponent<AnimationWorkshop>()).Target(new JObject{["target"]=target},out _,out error))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; read its current sound emitter revision";return false;}
            return journal.CanEditAudioEmitter(target,key,emitter,out error);
        }
        internal bool BeginAudio(string id,int revision,RoomAudioDefinition definition,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=definition?.Copy();
            return BeginPreparedEdit(()=>CanEditAudio(id,revision,captured,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{
                    edit=null;
                    return PrepareAudio(id,revision,captured,out var edits,out issue)&&
                        journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out edit,out issue,audioEdits:edits);
                },captured==null?"Sound removed":"Sound saved",out pending,out error,checkpoint);
        }
        internal bool BeginAudioEmitter(string target,int revision,string key,RoomAudioEmitter emitter,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=emitter?.Copy();
            return BeginContentEdit(()=>CanEditAudioEmitter(target,revision,key,captured,out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{
                    bool ready=PrepareAudioEmitter(target,revision,key,captured,out var data,out issue);
                    values=ready?new[]{data}:null;return ready;
                },Array.Empty<string>(),captured==null?"Sound emitter removed":"Sound emitter saved",out pending,out error,checkpoint);
        }
        internal bool PrepareAudio(string id,int revision,RoomAudioDefinition definition,out AudioDefinitionEdits edits,out string error)
        {
            edits=null;if(!CanEditAudio(id,revision,definition,out error))return false;
            edits=definition==null?new AudioDefinitionEdits {Removals=new[]{id}}:new AudioDefinitionEdits {Replacements=new[]{definition.Copy()}};
            if(!edits.Validate(out error))return false;
            var candidate=Snapshot();candidate.audioSources=edits.Apply(candidate.audioSources);
            return candidate.Validate(out error);
        }
        internal bool EditAudio(string id,int revision,RoomAudioDefinition definition,out string error)
        {
            if(!PrepareAudio(id,revision,definition,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),definition==null?"Sound removed":"Sound saved",false,out error,audioEdits:edits);
        }
        internal bool PrepareAudioEmitter(string target,int revision,string key,RoomAudioEmitter emitter,out RoomObjectData data,out string error)
        {
            data=null;if(!CanEditAudioEmitter(target,revision,key,emitter,out error))return false;
            data=Pose(Read(target),Find(target).transform);
            var current=data.audioEmitters??Array.Empty<RoomAudioEmitter>();
            data.audioEmitters=current.Where(e=>e.id!=key).Concat(emitter==null?Array.Empty<RoomAudioEmitter>():new[]{emitter.Copy()}).OrderBy(e=>e.id,StringComparer.Ordinal).ToArray();
            var candidate=Snapshot();var next=data;candidate.objects=candidate.objects.Select(x=>x.id==target?next:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditAudioEmitter(string target,int revision,string key,RoomAudioEmitter emitter,out string error)
        {
            if(!PrepareAudioEmitter(target,revision,key,emitter,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),emitter==null?"Sound emitter removed":"Sound emitter saved",false,out error);
        }
    }
}

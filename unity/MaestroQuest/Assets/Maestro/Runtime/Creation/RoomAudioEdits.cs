// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        public RoomAudioDefinition[] AudioSources()=>journal.AudioSnapshot();
        public RoomAudioDefinition ReadAudio(string id)=>journal.ReadAudio(id);
        internal RoomAudioEmitter ReadAudioEmitter(string target,string key)=>journal.ReadAudioEmitter(target,key);
        public int AudioRevision(string id)=>journal.AudioRevision(id);
        internal bool PrepareAudio(string id,int revision,RoomAudioDefinition definition,out AudioDefinitionEdits edits,out string error)
        {
            edits=null;if(!CanEditStructures(out error))return false;
            if(AudioRevision(id)!=revision||definition==null&&ReadAudio(id)==null){error="The sound changed or is missing; read its current source revision";return false;}
            if(definition!=null&&definition.id!=id){error="The sound identity must match the edited source";return false;}
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
            data=null;if(!CanEditObject(target,false,out error))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; read its current sound emitter revision";return false;}
            if(emitter!=null&&emitter.id!=key){error="The emitter identity must match its edited key";return false;}
            data=Pose(Read(target),Find(target).transform);
            var current=data.audioEmitters??Array.Empty<RoomAudioEmitter>();
            if(emitter==null&&!current.Any(e=>e.id==key)){error="This sound emitter no longer exists";return false;}
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

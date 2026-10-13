// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Workspace-owned audio instances. Definitions never contain this runtime state.</summary>
    [DefaultExecutionOrder(260)]
    public sealed partial class WorldAudio:MonoBehaviour
    {
        internal const int MaximumVoices=AudioReservations.MaximumOwners;
        readonly Dictionary<string,AudioReservations.Entry> decoded=new(StringComparer.Ordinal);
        readonly Dictionary<string,Instance> instances=new(StringComparer.Ordinal);
        RoomEditor editor;
        bool paused,focused=true;
        internal static WorldAudio For(RoomEditor value)
        {
            var result=value.GetComponent<WorldAudio>()??value.gameObject.AddComponent<WorldAudio>();result.editor=value;return result;
        }
        internal sealed class Instance
        {
            internal string Id,Target,Emitter,Source,Session,Definition,CacheKey,Error;
            internal int SourceRevision,ObjectRevision;
            internal long Written;
            internal double ProgressAt;internal long Played;
            internal long Sequence;
            internal RoomItem Item;
            internal RoomAudioEmitter Attachment;
            internal PcmOutputLease Renderer;
            internal AudioSource Output => Renderer?.Source;
            internal SpeechPcmStream Stream => Renderer?.Stream;
            internal AudioPcmAsset Asset;
            internal short[] Samples=>Asset?.ResidentSamples;
            internal AudioReservations.Lease Resource;
            internal short[] Producer=new short[SpeechPcmStream.MaxChunk];
            internal bool Complete,Closed;
            internal double Cursor;
            internal bool Loop,Paused,RoomOwned;
            internal float Gain;
            internal int Revision;
            internal string Phase="preparing";
            internal readonly Queue<Maestro.Quest.Programs.AudioInstanceSample> Notices=new();
        }
        internal bool Available(out string error)
        {
            error="Room audio is paused or unavailable";
            if(!editor||!editor.isActiveAndEnabled||!isActiveAndEnabled||paused||!focused||editor.RuntimeGate.Held)return false;
            error="The room audio voice budget is full; stop or wait for another sound";
            PruneDecoded();if(instances.Count>=MaximumVoices||!AudioReservations.VoiceAvailable)return false;
            error=null;return true;
        }
        internal bool Begin(string target,string emitter,out Instance voice,out string error,bool loop=false)
        {
            voice=null;if(!Available(out error))return false;
            var item=editor.Find(target);var config=editor.ReadAudioEmitter(target,emitter);
            var definition=config==null?null:editor.ReadAudio(config.source);
            error="The sound emitter or source is missing";
            if(!item||!item.isActiveAndEnabled||definition==null)return false;
            if(instances.Values.Any(v=>v.Target==target&&v.Emitter==emitter)){error="This sound emitter is already playing";return false;}
            if(!definition.Validate(out error)||!Anchor(item,config,out _,out _,out error))return false;
            var v=new Instance {Id=Guid.NewGuid().ToString("N"),Target=target,Emitter=emitter,Source=definition.id,SourceRevision=editor.AudioRevision(definition.id),ObjectRevision=editor.ObjectRevision(target),ProgressAt=Time.realtimeSinceStartupAsDouble,Session=editor.TemporarySessionId,Item=item,Attachment=config.Copy(),Definition=JsonUtility.ToJson(config),CacheKey=JsonUtility.ToJson(definition),Loop=loop,Gain=config.gain};
            try {
                decoded.TryGetValue(v.CacheKey,out var cached);var recipe=definition.Copy();
                v.Resource=AudioReservations.AcquireSource(cached,recipe,new RoomResourceOwner(editor.WorldIdentity,target,"audio"),v.Id,emitter,v.SourceRevision,
                    cancel=>recipe.kind=="clip"?editor.Sounds.PrepareAsync(recipe.assetHash,recipe.seconds,cancel):Task.Run(()=>{var floats=AudioTone.Render(recipe,cancel);var pcm=new short[floats.Length];for(int i=0;i<pcm.Length;i++){if((i&1023)==0)cancel.ThrowIfCancellationRequested();pcm[i]=(short)Math.Round(floats[i]*short.MaxValue);}return AudioPcmAsset.Resident(pcm);},cancel));
                decoded[v.CacheKey]=v.Resource.Resource;instances.Add(v.Id,v);Note(v);voice=v;error=null;return true;
            } catch(Exception e){Close(v,e.Message);error="Could not prepare room sound: "+e.Message;return false;}
        }
        void PruneDecoded()
        {
            foreach(var pair in decoded.Where(p=>p.Value.Retiring).ToArray())decoded.Remove(pair.Key);
        }
        bool OpenRenderer(Instance v)
        {
            if(!decoded.TryGetValue(v.CacheKey,out var asset)){Close(v,"The decoded source is unavailable");return false;}
            if(!asset.Pending.IsCompleted)return false;
            if(asset.Pending.IsCanceled||asset.Pending.IsFaulted){_=asset.Pending.Exception;Close(v,"The sound could not be decoded");return false;}
            try {
                v.Asset=asset.Pending.Result;var config=v.Attachment;
                v.ProgressAt=Time.realtimeSinceStartupAsDouble;
                v.Renderer=PcmOutputLease.Open(transform,"World sound "+v.Emitter,
                    new SpeechPcmStream(AudioSettings.dspTime+.1,PcmOutputLease.DeviceTail),
                    config.spatial,v.Gain,96,config.minDistance,config.maxDistance);
                Pump(v);if(v.Closed||!Follow(v))return false;v.Renderer.Start();return true;
            } catch(Exception e){Close(v,"Could not start room sound: "+e.Message);return false;}
        }
        void Pump(Instance v)
        {
            try{PumpAvailable(v);}catch(Exception){Close(v,"The sound source could not supply its next samples");}
        }
        void PumpAvailable(Instance v)
        {
            var state=v.Stream.Read(AudioSettings.dspTime);
            if(state.Faulted||state.Closed){Close(v,"Audio output stopped consuming samples");return;}
            while((v.Loop||v.Written<v.Asset.Frames)&&state.Submitted-state.Played<24000*7) {
                int count=v.Loop?v.Producer.Length:(int)Math.Min(v.Producer.Length,v.Asset.Frames-v.Written);
                int source=(int)(v.Written%v.Asset.Frames);
                count=v.Asset.Read(source,v.Producer,Math.Min(count,v.Asset.Frames-source));
                if(count==0)break; // Pending disk/decoder work is not EOF.
                if(!v.Stream.TryWrite(v.Sequence+1,v.Producer,count,AudioSettings.dspTime,out var error)){Close(v,error);return;}
                v.Sequence++;v.Written+=count;state=v.Stream.Read(AudioSettings.dspTime);
            }
            if(state.Played!=v.Played){v.Played=state.Played;v.ProgressAt=Time.realtimeSinceStartupAsDouble;}
            else if(Time.realtimeSinceStartupAsDouble-v.ProgressAt>3){Close(v,"Audio output did not consume its queued samples");return;}
            v.Cursor=state.Played/(double)AudioTone.SampleRate;
            if(v.Phase=="preparing"&&state.Played>0){v.Phase="playing";Note(v);}
            if(!v.Loop&&v.Written==v.Asset.Frames&&!state.InputComplete) {
                if(!v.Stream.TryComplete(v.Sequence,out var error)){Close(v,error);return;}
                state=v.Stream.Read(AudioSettings.dspTime);
            }
            if(v.Renderer.IsDrained(AudioSettings.dspTime)){v.Complete=true;Close(v,null);}
        }
        internal void Tick(Instance v)
        {
            if(v.Closed)return;
            if(!editor||editor.TemporarySessionId!=v.Session||editor.RuntimeGate.Held||editor.Find(v.Target)!=v.Item||!v.Item||!v.Item.isActiveAndEnabled){Cancel(v,"The room or sound target became unavailable");return;}
            int revision=editor.ObjectRevision(v.Target);
            if(revision!=v.ObjectRevision){var current=editor.ReadAudioEmitter(v.Target,v.Emitter);if(current==null||JsonUtility.ToJson(current)!=v.Definition){Cancel(v,"The sound emitter changed or was removed");return;}v.ObjectRevision=revision;}
            if(v.Stream==null){OpenRenderer(v);return;}
            if(v.Renderer?.Available!=true){Close(v,"The audio renderer became unavailable");return;}
            if(!Follow(v))return;
            if(v.Paused){var state=v.Stream.Read(AudioSettings.dspTime);if(state.Faulted||state.Closed){Close(v,"The paused audio renderer failed");return;}v.Cursor=state.Played/(double)AudioTone.SampleRate;return;}
            Pump(v);
        }
        bool Follow(Instance v)
        {
            if(!Anchor(v.Item,v.Attachment,out var position,out var rotation,out var error)){Close(v,error);return false;}
            v.Renderer.SetPose(position,rotation);return true;
        }
        static bool Anchor(RoomItem item,RoomAudioEmitter emitter,out Vector3 position,out Quaternion rotation,out string error)
        {
            position=default;rotation=Quaternion.identity;error="The sound attachment is unavailable";if(!item)return false;
            var root=item.transform;Transform anchor=root;
            if(emitter.part.Length>0)anchor=item.GetComponent<RecipeObject>()?.Part(emitter.part);
            if(emitter.joint.Length>0) {
                var rig=item.GetComponent<AvatarPoseRig>();var joint=Enum.Parse<PoseJoint>(emitter.joint);anchor=rig?rig.Bone(joint):null;
                var canonical=rig?rig.CanonicalBone(joint):null;if(!anchor||!canonical)return false;
                rotation=canonical.rotation;position=anchor.position+rotation*Vector3.Scale(root.lossyScale,emitter.position);
            } else {if(!anchor)return false;rotation=anchor.rotation;position=anchor.TransformPoint(emitter.position);}
            error=null;return true;
        }
        // Pending, playing and paused voices all retain their emitting object.
        internal string[] RetainedTargets()=>instances.Values.Where(v=>!v.Closed).Select(v=>v.Target).Distinct(StringComparer.Ordinal).ToArray();
        internal Instance Current(string target,string emitter)=>instances.Values.FirstOrDefault(v=>v.Target==target&&v.Emitter==emitter);
        internal void Cancel(Instance v,string reason="Sound playback was cancelled")=>Close(v,reason,"cancelled");
        internal void Close(Instance v,string error="Sound playback was cancelled",string phase=null)
        {
            if(v.Closed)return;if(v.Stream!=null)v.Cursor=v.Stream.Read(AudioSettings.dspTime).Played/(double)AudioTone.SampleRate;
            v.Closed=true;v.Error=error;v.Phase=phase??(v.Complete?"completed":"failed");Note(v);v.Renderer?.Dispose();
            var resource=v.Resource?.Resource;v.Resource?.Dispose();v.Resource=null;
            if(resource!=null&&resource.Retiring&&decoded.TryGetValue(v.CacheKey,out var cached)&&ReferenceEquals(resource,cached))decoded.Remove(v.CacheKey);
            v.Asset=null;v.Producer=null;v.Renderer=null;v.Item=null;v.Attachment=null;instances.Remove(v.Id);Remember(v);
        }
        void CloseAll(string reason){foreach(var voice in instances.Values.ToArray())Cancel(voice,reason);}
        void LateUpdate(){foreach(var voice in instances.Values.ToArray())Tick(voice);PruneDecoded();}
        void OnEnable()=>AudioSettings.OnAudioConfigurationChanged+=AudioChanged;
        void OnDisable(){AudioSettings.OnAudioConfigurationChanged-=AudioChanged;CloseAll("Room audio was disabled");decoded.Clear();}
        void AudioChanged(bool _)=>CloseAll("The audio device changed; start the sound again");
        void OnApplicationPause(bool value){paused=value;if(value)CloseAll("Room audio was suspended");}
        void OnApplicationFocus(bool value){focused=value;if(!value)CloseAll("Room audio lost focus");}
    }
}

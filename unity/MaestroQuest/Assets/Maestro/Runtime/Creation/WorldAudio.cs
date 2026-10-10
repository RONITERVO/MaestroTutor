// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
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
            internal AudioSource Output;
            internal AudioClip Carrier;
            internal SpeechPcmStream Stream;
            internal SpeechPcmFilter Filter;
            internal short[] Samples;
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
                v.Resource=AudioReservations.Acquire(cached,recipe,new RoomResourceOwner(editor.WorldIdentity,target,"audio"),v.Id,emitter,v.SourceRevision,
                    cancel=>recipe.kind=="clip"?editor.Sounds.DecodeAsync(recipe.assetHash,cancel,recipe.seconds):Task.Run(()=>{var floats=AudioTone.Render(recipe,cancel);var pcm=new short[floats.Length];for(int i=0;i<pcm.Length;i++){if((i&1023)==0)cancel.ThrowIfCancellationRequested();pcm[i]=(short)Math.Round(floats[i]*short.MaxValue);}return pcm;},cancel));
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
                v.Samples=asset.Pending.Result;var config=v.Attachment;
                var child=new GameObject("World sound "+v.Emitter);child.transform.SetParent(transform,false);
                v.Output=child.AddComponent<AudioSource>();var source=v.Output;
                source.playOnAwake=false;source.loop=true;source.volume=v.Gain;source.dopplerLevel=0;source.priority=96;
                source.spatialBlend=config.spatial?1:0;source.minDistance=config.minDistance;source.maxDistance=config.maxDistance;source.rolloffMode=AudioRolloffMode.Logarithmic;
                if(config.spatial)SpeechSpatializer.Configure(source);
                AudioSettings.GetDSPBufferSize(out var length,out var buffers);
                int rate=AudioSettings.outputSampleRate;if(rate<24000||rate>192000)throw new InvalidOperationException("The audio output rate is unsupported");
                // Reuse the bounded mono PCM transport and actual DSP consumption receipts.
                v.ProgressAt=Time.realtimeSinceStartupAsDouble;
                v.Stream=new SpeechPcmStream(AudioSettings.dspTime+.1,Math.Max(.02,(double)length*Math.Max(1,buffers)/rate+.02));
                v.Carrier=AudioClip.Create("World sound carrier",1024,1,AudioTone.SampleRate,false);source.clip=v.Carrier;
                v.Filter=child.AddComponent<SpeechPcmFilter>();v.Filter.Bind(v.Stream,rate);
                Pump(v);if(v.Closed||!Follow(v))return false;source.Play();return true;
            } catch(Exception e){Close(v,"Could not start room sound: "+e.Message);return false;}
        }
        void Pump(Instance v)
        {
            var state=v.Stream.Read(AudioSettings.dspTime);
            if(state.Faulted){Close(v,"Audio output stopped consuming samples");return;}
            while((v.Loop||v.Written<v.Samples.Length)&&state.Submitted-state.Played<24000*7) {
                int count=v.Loop?v.Producer.Length:(int)Math.Min(v.Producer.Length,v.Samples.Length-v.Written);
                for(int at=0;at<count;){int source=(int)((v.Written+at)%v.Samples.Length),take=Math.Min(count-at,v.Samples.Length-source);Array.Copy(v.Samples,source,v.Producer,at,take);at+=take;}
                if(!v.Stream.TryWrite(v.Sequence+1,v.Producer,count,AudioSettings.dspTime,out var error)){Close(v,error);return;}
                v.Sequence++;v.Written+=count;state=v.Stream.Read(AudioSettings.dspTime);
            }
            if(state.Played!=v.Played){v.Played=state.Played;v.ProgressAt=Time.realtimeSinceStartupAsDouble;}
            else if(Time.realtimeSinceStartupAsDouble-v.ProgressAt>3){Close(v,"Audio output did not consume its queued samples");return;}
            v.Cursor=state.Played/(double)AudioTone.SampleRate;
            if(v.Phase=="preparing"&&state.Played>0){v.Phase="playing";Note(v);}
            if(!v.Loop&&v.Written==v.Samples.Length&&state.Played==v.Samples.Length){v.Complete=true;Close(v,null);}
        }
        internal void Tick(Instance v)
        {
            if(v.Closed)return;
            if(!editor||editor.TemporarySessionId!=v.Session||editor.RuntimeGate.Held||editor.Find(v.Target)!=v.Item||!v.Item||!v.Item.isActiveAndEnabled){Cancel(v,"The room or sound target became unavailable");return;}
            int revision=editor.ObjectRevision(v.Target);
            if(revision!=v.ObjectRevision){var current=editor.ReadAudioEmitter(v.Target,v.Emitter);if(current==null||JsonUtility.ToJson(current)!=v.Definition){Cancel(v,"The sound emitter changed or was removed");return;}v.ObjectRevision=revision;}
            if(v.Stream==null){OpenRenderer(v);return;}
            if(!v.Output||!v.Filter){Close(v,"The audio renderer became unavailable");return;}
            if(!Follow(v))return;
            if(v.Paused){var state=v.Stream.Read(AudioSettings.dspTime);if(state.Faulted){Close(v,"The paused audio renderer failed");return;}v.Cursor=state.Played/(double)AudioTone.SampleRate;return;}
            Pump(v);
        }
        bool Follow(Instance v)
        {
            if(!Anchor(v.Item,v.Attachment,out var position,out var rotation,out var error)){Close(v,error);return false;}
            v.Output.transform.SetPositionAndRotation(position,rotation);return true;
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
            v.Closed=true;v.Error=error;v.Phase=phase??(v.Complete?"completed":"failed");Note(v);v.Stream?.Close();v.Filter?.Stop();
            if(v.Output){v.Output.Stop();v.Output.clip=null;Destroy(v.Output.gameObject);}if(v.Carrier)Destroy(v.Carrier);
            var resource=v.Resource?.Resource;v.Resource?.Dispose();v.Resource=null;
            if(resource!=null&&resource.Retiring&&decoded.TryGetValue(v.CacheKey,out var cached)&&ReferenceEquals(resource,cached))decoded.Remove(v.CacheKey);
            v.Samples=null;v.Producer=null;v.Stream=null;v.Filter=null;v.Output=null;v.Carrier=null;v.Item=null;v.Attachment=null;instances.Remove(v.Id);Remember(v);
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

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Admission survives the component that requested a decode. A retiring task
    // keeps its reservation until it drains, even when its workspace is destroyed.
    internal static class AudioReservations
    {
        internal const int MaximumSources=8,MaximumOwners=8;
        internal const long MaximumSourceBytes=(30L*AudioTone.SampleRate+4)*sizeof(short),MaximumPcmBytes=MaximumSources*MaximumSourceBytes;
        static readonly object gate=new();
        static readonly List<Entry> entries=new();
        internal sealed class Entry
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),World,Region,Source,Kind,AssetHash,DefinitionHash;
            internal readonly long ReservedBytes;
            internal readonly CancellationTokenSource Cancel=new();
            internal readonly List<Lease> Owners=new();
            internal Task<short[]> Pending;
            internal volatile bool Retiring;
            internal Entry(RoomAudioDefinition definition,RoomResourceOwner owner,string hash)
            {
                World=owner.World;Region=owner.Region;Source=definition.id;Kind=definition.kind;AssetHash=definition.assetHash??"";DefinitionHash=hash;
                // Imported duration is compared within 0.0001 s before decoding.
                // Four extra samples cover that tolerance and float rounding.
                ReservedBytes=(checked((long)Math.Ceiling((double)definition.seconds*AudioTone.SampleRate))+4)*sizeof(short);
            }
        }
        internal sealed class Lease:IDisposable
        {
            internal Entry Resource;
            internal readonly RoomResourceOwner Owner;
            internal readonly string Instance,Emitter;
            internal readonly int SourceRevision;
            internal Lease(Entry value,RoomResourceOwner owner,string instance,string emitter,int revision){Resource=value;Owner=owner;Instance=instance;Emitter=emitter;SourceRevision=revision;}
            public void Dispose()
            {
                Entry retiring=null;Task<short[]> pending=null;
                lock(gate){
                    var value=Resource;if(value==null)return;Resource=null;value.Owners.Remove(this);
                    if(value.Owners.Count==0){value.Retiring=true;retiring=value;pending=value.Pending;}
                }
                if(retiring==null)return;
                try{retiring.Cancel.Cancel();}finally{_ = Drain(retiring,pending);}
            }
        }
        internal static bool VoiceAvailable {get{lock(gate)return entries.Sum(e=>e.Owners.Count)<MaximumOwners;}}
        internal static Lease Acquire(Entry cached,RoomAudioDefinition definition,RoomResourceOwner owner,string instance,string emitter,int revision,Func<CancellationToken,Task<short[]>> loader)
        {
            if(definition==null||!definition.Validate(out _)||owner==null||owner.Role!="audio"||owner.World.Length==0||owner.Region.Length==0||owner.Target.Length==0||
                !Guid.TryParseExact(instance,"N",out _)||string.IsNullOrEmpty(emitter)||emitter.Length>24||revision<1||loader==null)throw new ArgumentException("Invalid audio resource owner or source");
            string hash=ModelLibrary.Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(definition)));
            lock(gate){
                if(entries.Sum(e=>e.Owners.Count)>=MaximumOwners)throw new InvalidOperationException("The shared room audio voice budget is full; stop or wait for another sound");
                if(entries.Any(e=>e.Owners.Any(v=>v.Instance==instance)))throw new InvalidOperationException("This audio instance already owns a resource");
                var entry=cached;
                if(entry!=null&&(entry.Retiring||!entries.Contains(entry)))throw new InvalidOperationException("The earlier sound preparation is stopping; wait for it to finish");
                if(entry!=null&&(entry.World!=owner.World||entry.Region!=owner.Region||entry.DefinitionHash!=hash))throw new InvalidOperationException("A sound resource cannot be reassigned to another world or source");
                if(entry==null){
                    entry=new Entry(definition,owner,hash);
                    if(entries.Count>=MaximumSources||entry.ReservedBytes>MaximumPcmBytes-entries.Sum(e=>e.ReservedBytes)){
                        entry.Cancel.Dispose();throw new InvalidOperationException("The shared sound decoder budget is full; wait for earlier preparations to finish");
                    }
                    entries.Add(entry);entry.Pending=Prepare(loader,entry.Cancel.Token,entry.ReservedBytes);_ = Observe(entry.Pending);
                }
                var lease=new Lease(entry,owner,instance,emitter,revision);entry.Owners.Add(lease);return lease;
            }
        }
        static async Task<short[]> Prepare(Func<CancellationToken,Task<short[]>> loader,CancellationToken cancel,long reserved)
        {
            var samples=await loader(cancel).ConfigureAwait(false);
            if(samples==null||samples.Length==0||samples.LongLength*sizeof(short)>reserved)throw new InvalidOperationException("Decoded sound exceeds its admitted PCM budget");
            return samples;
        }
        static async Task Observe(Task task){try{await task.ConfigureAwait(false);}catch(Exception){}}
        static async Task Drain(Entry entry,Task pending)
        {
            try{await pending.ConfigureAwait(false);}catch(Exception){}
            finally{lock(gate){entries.Remove(entry);entry.Pending=null;entry.Cancel.Dispose();}}
        }
        internal static JObject ObserveBudget()
        {
            lock(gate)return new JObject{["sources"]=entries.Count,["owners"]=entries.Sum(e=>e.Owners.Count),["retiring"]=entries.Count(e=>e.Retiring),
                ["reservedPcmBytes"]=entries.Sum(e=>e.ReservedBytes),["readyPcmBytes"]=entries.Sum(ReadyBytes),["sourceLimit"]=MaximumSources,["ownerLimit"]=MaximumOwners,["pcmByteLimit"]=MaximumPcmBytes};
        }
        static long ReadyBytes(Entry entry)=>entry.Pending?.Status==TaskStatus.RanToCompletion?entry.Pending.Result.LongLength*sizeof(short):0;
        internal static JObject ObserveReservation(int index)
        {
            lock(gate){if(index<0||index>=entries.Count)return null;var e=entries[index];
                string state=e.Retiring?"retiring":e.Pending.IsCanceled||e.Pending.IsFaulted?"failed":e.Pending.IsCompleted?"ready":"loading";
                return new JObject{["reservationId"]=e.Id,["worldId"]=e.World,["regionId"]=e.Region,["sourceId"]=e.Source,["kind"]=e.Kind,["assetHash"]=e.AssetHash,["definitionHash"]=e.DefinitionHash,
                    ["state"]=state,["owners"]=e.Owners.Count,["reservedPcmBytes"]=e.ReservedBytes,["readyPcmBytes"]=ReadyBytes(e)};
            }
        }
        internal static JObject ObserveOwner(string reservationId,int index)
        {
            lock(gate){var e=entries.FirstOrDefault(e=>e.Id==reservationId);if(e==null||index<0||index>=e.Owners.Count)return null;var v=e.Owners[index];
                return new JObject{["reservationId"]=e.Id,["instanceId"]=v.Instance,["worldId"]=v.Owner.World,["regionId"]=v.Owner.Region,["target"]=v.Owner.Target,["role"]=v.Owner.Role,["emitter"]=v.Emitter,["sourceId"]=e.Source,["sourceRevision"]=v.SourceRevision};
            }
        }
    }
}

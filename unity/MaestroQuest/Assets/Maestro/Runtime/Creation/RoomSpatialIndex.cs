// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
namespace Maestro.Quest.Creation
{
    // One room owns this disposable, value-only index. Saved membership is still
    // authoritative; publication is keyed by detached geometry, never task order.
    // Tick is main-thread/cooperative. At most one bounded byte inspection runs
    // in the background, with no native allocations or bundled-file publication.
    internal sealed class RoomSpatialIndex:IDisposable
    {
        sealed class Entry {internal string Id,Source,ModelHash;internal RoomObjectData Data;internal RoomSpatialBounds Bounds;internal bool Ready;}
        readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
        readonly Dictionary<string,ModelAssetBounds> inspected=new(StringComparer.Ordinal);
        readonly Queue<Entry> pending=new();
        readonly Func<string,CancellationToken,Task<ModelAssetBounds>> inspect;
        readonly Func<string,ModelAssetBounds?> cached;
        readonly CancellationTokenSource lifetime=new();
        Task<ModelAssetBounds> reading;string readingHash;bool ended;
        internal int Count=>entries.Count;
        readonly RoomSpatialChanges changes=new();
        internal long Generation=>changes.Version;
        internal bool ChangesAffect(long since,HashSet<string> targets)=>changes.Affects(since,targets);
        internal bool Pending=>!ended&&(pending.Count>0||reading!=null);
        internal RoomSpatialIndex(ModelLibrary library):this(library.PrepareRestBoundsAsync,hash=>library.TryReadBounds(hash,out var value,true)?value:null){}
        internal RoomSpatialIndex(Func<string,CancellationToken,Task<ModelAssetBounds>> inspect,Func<string,ModelAssetBounds?> cached=null)
        {this.inspect=inspect??throw new ArgumentNullException(nameof(inspect));this.cached=cached??(_=>null);}
        internal void Synchronize(RoomDocument document)
        {
            if(ended)throw new ObjectDisposedException(nameof(RoomSpatialIndex));
            var active=new HashSet<string>();var hashes=new HashSet<string>();pending.Clear();
            foreach(var data in document.objects){
                active.Add(data.id);if(data.kind==RoomObjectKind.ImportedModel)hashes.Add(data.modelHash);
                string source=RoomEditor.SpatialSource(data.Copy());
                if(!entries.TryGetValue(data.id,out var entry)||entry.Source!=source){entry=new(){Id=data.id,Source=source,Data=data.Copy(),ModelHash=data.kind==RoomObjectKind.ImportedModel?data.modelHash:null};entries[data.id]=entry;changes.Record(data.id);}
                if(entry.Ready&&!entry.Bounds.Visual.Known&&data.kind==RoomObjectKind.ImportedModel&&cached(data.modelHash)?.Known==true){entry.Ready=false;changes.Record(data.id);}
                if(!entry.Ready){entry.Data??=data.Copy();pending.Enqueue(entry);}
            }
            foreach(var id in entries.Keys.Where(id=>!active.Contains(id)).ToArray()){entries.Remove(id);changes.Record(id);}
            foreach(var hash in inspected.Keys.Where(hash=>!hashes.Contains(hash)).ToArray())inspected.Remove(hash);
        }
        internal bool TryRead(string target,string source,out RoomSpatialBounds bounds)
        {
            bounds=RoomSpatialBounds.Unknown;
            if(ended||!entries.TryGetValue(target,out var entry)||!entry.Ready||entry.Source!=source)return false;
            bounds=entry.Bounds;return true;
        }
        internal void Tick()
        {
            if(ended)return;
            if(reading?.IsCompleted==true){
                // A cancelled/replaced index is never ticked again. Observe every
                // task exception even if the object's reference was removed.
                ModelAssetBounds value;
                try{value=reading.GetAwaiter().GetResult();}catch(Exception){value=ModelAssetBounds.Unknown("The model could not be inspected for spatial preparation.");}
                if(entries.Values.Any(e=>e.ModelHash==readingHash))inspected[readingHash]=value;
                reading=null;readingHash=null;
            }
            int count=Math.Min(RoomPreparationBudget.MaximumStepsPerFrame,pending.Count);
            long started=Stopwatch.GetTimestamp();
            for(int i=0;i<count;i++){
                if(i>0&&(Stopwatch.GetTimestamp()-started)/(double)Stopwatch.Frequency>=.002)break;
                var entry=pending.Dequeue();ModelAssetBounds? asset=null;
                if(entry.Data.kind==RoomObjectKind.ImportedModel){
                    string hash=entry.Data.modelHash;asset=cached(hash);
                    if(!asset.HasValue&&inspected.TryGetValue(hash,out var value))asset=value;
                    if(!asset.HasValue){
                        if(reading==null){readingHash=hash;try{reading=inspect(hash,lifetime.Token);}catch(Exception){reading=Task.FromResult(ModelAssetBounds.Unknown("The model could not be inspected for spatial preparation."));}}
                        pending.Enqueue(entry);continue;
                    }
                }
                try{entry.Bounds=RoomAuthoredBounds.Derive(entry.Data,asset);}catch(Exception){entry.Bounds=RoomSpatialBounds.Unknown;}
                entry.Ready=true;entry.Data=null;changes.Record(entry.Id);
            }
        }
        public void Dispose()
        {
            if(ended)return;ended=true;lifetime.Cancel();entries.Clear();pending.Clear();inspected.Clear();
            // Inspection can finish its current bounded validation unit after
            // cancellation. No continuation captures the editor or publishes it.
            if(reading!=null)_=reading.ContinueWith(t=>{_ = t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
            reading=null;lifetime.Dispose();
        }
    }
}

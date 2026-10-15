// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace Maestro.Quest.Imports
{
    public sealed partial class ModelLibrary
    {
        const int MaximumBoundsEntries=32;
        readonly object boundsGate=new();
        readonly SemaphoreSlim boundsPreparation=new(1,1);
        readonly Dictionary<string,(ModelAssetBounds Static,ModelAssetBounds Resting,IReadOnlyList<ModelClipMetadata> Clips)> assetBounds=new(StringComparer.Ordinal);
        readonly List<string> boundsOrder=new();
        // Pure read: a cache miss never opens a file, materializes the included
        // avatar, reserves native capacity or queues asynchronous work.
        internal bool TryReadBounds(string hash,out ModelAssetBounds result,bool rest=false)
        {
            result=default;if(!ValidHash(hash))return false;
            lock(boundsGate){if(!assetBounds.TryGetValue(hash,out var value))return false;result=rest?value.Resting:value.Static;return true;}
        }
        internal bool TryReadClips(string hash,out IReadOnlyList<ModelClipMetadata> clips)
        {
            clips=null;if(!ValidHash(hash))return false;
            lock(boundsGate){if(!assetBounds.TryGetValue(hash,out var value))return false;clips=value.Clips;return true;}
        }
        // Explicit owned preparation, not a fact read. Included content is read
        // in place; unlike native first-use import this never writes a local copy.
        internal Task<ModelAssetBounds> PrepareBoundsAsync(string hash,CancellationToken cancellation)=>PrepareBoundsAsync(hash,cancellation,false);
        internal Task<ModelAssetBounds> PrepareRestBoundsAsync(string hash,CancellationToken cancellation)=>PrepareBoundsAsync(hash,cancellation,true);
        async Task<ModelAssetBounds> PrepareBoundsAsync(string hash,CancellationToken cancellation,bool rest)
        {
            cancellation.ThrowIfCancellationRequested();
            if(TryReadBounds(hash,out var ready,rest))return ready;
            // A disabled room can be reopened before its cancelled validation
            // unit finishes. The library keeps one inspection slot across owners.
            await boundsPreparation.WaitAsync(cancellation).ConfigureAwait(false);
            try{return await Task.Run(()=>{
                cancellation.ThrowIfCancellationRequested();
                if(TryReadBounds(hash,out var cached,rest))return cached;
                if(!ValidHash(hash))throw new ModelImportException("This room has an invalid model reference.");
                ModelAsset asset;
                if(included!=null&&hash==included.Hash&&!File.Exists(Path.Combine(directory,hash+".glb")))asset=included.Read();
                else asset=ReadLocalCore(hash,cancellation);
                cancellation.ThrowIfCancellationRequested();RememberBounds(asset);
                return rest?asset.Inspection.RestBounds:asset.Inspection.AssetBounds;
            },cancellation).ConfigureAwait(false);}finally{boundsPreparation.Release();}
        }
        void RememberBounds(ModelAsset verified)
        {
            lock(boundsGate){
                boundsOrder.Remove(verified.Hash);boundsOrder.Add(verified.Hash);
                assetBounds[verified.Hash]=(verified.Inspection.AssetBounds,verified.Inspection.RestBounds,verified.Inspection.AnimationClips);
                if(boundsOrder.Count>MaximumBoundsEntries){assetBounds.Remove(boundsOrder[0]);boundsOrder.RemoveAt(0);}
            }
        }
        void ForgetBounds(string hash)
        {
            if(hash==null)return;
            lock(boundsGate){assetBounds.Remove(hash);boundsOrder.Remove(hash);}
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
namespace Maestro.Quest.Imports
{
    public sealed partial class ModelLibrary
    {
        const int MaximumBoundsEntries=32;
        readonly object boundsGate=new();
        readonly Dictionary<string,ModelAssetBounds> assetBounds=new(StringComparer.Ordinal);
        readonly List<string> boundsOrder=new();
        // Pure read: a cache miss never opens a file, materializes the included
        // avatar, reserves native capacity or queues asynchronous work.
        internal bool TryReadBounds(string hash,out ModelAssetBounds result)
        {
            result=default;if(!ValidHash(hash))return false;
            lock(boundsGate)return assetBounds.TryGetValue(hash,out result);
        }
        void RememberBounds(ModelAsset verified)
        {
            lock(boundsGate){
                boundsOrder.Remove(verified.Hash);boundsOrder.Add(verified.Hash);
                assetBounds[verified.Hash]=verified.Inspection.AssetBounds;
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

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Imports
{
    // Immutable derived values, never an authored room document or native asset.
    public readonly struct ModelAssetBounds
    {
        public const int DerivationVersion=1;
        public bool Known {get;}
        public bool HasBounds {get;}
        public Bounds SourceBounds {get;}
        public string Reason {get;}
        internal float Margin {get;}
        internal ModelAssetBounds(bool known,bool hasBounds,Bounds bounds,string reason,float margin=0)
        {Known=known;HasBounds=hasBounds;SourceBounds=bounds;Reason=reason;Margin=margin;}
        internal static ModelAssetBounds Unknown(string reason)=>new(false,false,default,reason);
    }
}

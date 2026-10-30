// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Ordered positive wet spans of one finite ray. The terrain bound is two
    // triangles per visited cell, at most 2*16+3 cells. Reused by the query owner.
    internal sealed class LiquidIntervals {
        internal const int Maximum=2*(2*16+3);
        readonly Vector2[] values=new Vector2[Maximum];
        internal int Count {get;private set;}
        internal bool Complete {get;private set;}=true;
        internal Vector2 this[int index]=>index>=0&&index<Count?values[index]:throw new ArgumentOutOfRangeException(nameof(index));
        internal void Clear(){Count=0;Complete=true;}
        internal void Fail(){Count=0;Complete=false;}
        internal void Add(float begin,float end){
            if(!Complete)return;
            if(!float.IsFinite(begin)||!float.IsFinite(end)||begin<0||end>1||end<begin){Fail();return;}
            if(end==begin)return;
            if(Count>0){
                var last=values[Count-1];
                if(begin<last.x){Fail();return;}
                if(begin<=last.y){values[Count-1]=new Vector2(last.x,Mathf.Max(last.y,end));return;}
            }
            if(Count==Maximum){Fail();return;}
            values[Count++]=new Vector2(begin,end);
        }
    }
}

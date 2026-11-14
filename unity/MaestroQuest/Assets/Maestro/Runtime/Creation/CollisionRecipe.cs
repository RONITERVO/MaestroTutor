// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class CollisionShape
    {
        public string id,shape="box";
        public Vector3 position,size=Vector3.one*.1f;
        public Quaternion rotation=Quaternion.identity;
        public float innerRadius;
        public int segments;
    }
    /// <summary>Independent, editable root-local collision proxies; no visual mesh or animation inference.</summary>
    [Serializable] public sealed class CollisionRecipe
    {
        public const string Feature="collisionShapes.v1";
        public const int MaximumShapes=16,MaximumPieces=64,MaximumRoomPieces=512;
        public int version=1;
        public CollisionShape[] shapes=Array.Empty<CollisionShape>();
        public CollisionRecipe Copy()=>JsonUtility.FromJson<CollisionRecipe>(JsonUtility.ToJson(this));
        public int Pieces=>shapes.Sum(s=>s.shape=="ring"?s.segments:1);
        public static int ReservedPieces(RoomObjectData data)=>data.IsBuiltIn?0:Math.Max(1,data.collision?.Pieces??0);
        public bool Validate(out string error) {
            error="Use up to 16 uniquely named collision shapes, with at most 64 convex pieces";
            if(version!=1||shapes==null||shapes.Length>MaximumShapes||shapes.Any(s=>!Valid(s))||shapes.Select(s=>s.id).Distinct().Count()!=shapes.Length||Pieces>MaximumPieces)return false;
            error=null;return true;
        }
        static bool Valid(CollisionShape s) {
            if(s==null||!RoomRecipe.ValidId(s.id)||!new[]{"box","sphere","cylinder","ring"}.Contains(s.shape)||!RoomRecipe.Finite(s.position)||!RoomRecipe.Finite(s.size)||!MotionFrame.ValidRotation(s.rotation)||!float.IsFinite(s.innerRadius))return false;
            if(s.position.magnitude>2||s.size.x<.005f||s.size.y<.005f||s.size.z<.005f||s.size.x>2||s.size.y>2||s.size.z>2||s.position.magnitude+s.size.magnitude*.5f>3)return false;
            if(s.shape=="sphere"&&(s.size.x!=s.size.y||s.size.x!=s.size.z))return false;
            if(s.shape=="box"||s.shape=="sphere")return s.segments==0&&s.innerRadius==0;
            if(s.segments<8||s.segments>24)return false;
            return s.shape=="cylinder"?s.innerRadius==0:s.innerRadius>=.05f&&s.innerRadius<=.45f&&Mathf.Min(s.size.x,s.size.z)*(.5f-s.innerRadius)>=.005f-1e-7f;
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class SurfaceStroke
    {
        public string id;
        public Color color=Color.white;
        public float radius=.003f;
        public Vector3[] points=Array.Empty<Vector3>();
        public SurfaceStroke Copy()=>new(){id=id,color=color,radius=radius,points=points?.ToArray()};
    }
    /// <summary>An explicit planar drawing patch. Coordinates belong to an object or stable recipe part, never its collision proxy.</summary>
    [Serializable] public sealed class DrawingSurface
    {
        public const int MaximumPerObject=4,MaximumRoomSurfaces=64,MaximumStrokes=32,MaximumRoomStrokes=128,MaximumPoints=512;
        public int version=1;
        public string id,part="";
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public float width=.5f,height=.35f;
        public bool enabled=true;
        public SurfaceStroke[] strokes=Array.Empty<SurfaceStroke>();
        public DrawingSurface Copy()=>new(){version=version,id=id,part=part,position=position,rotation=rotation,width=width,height=height,enabled=enabled,strokes=strokes?.Select(s=>s?.Copy()).ToArray()};
        public static bool Name(string value)=>value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[a-zA-Z][a-zA-Z0-9_]{0,31}$");
        public bool Validate(RoomObjectData owner,out string error)
        {
            error="A drawing surface has invalid geometry, identity or stroke data";
            if(version!=1||!Name(id)||part==null||part!=""&&(owner.recipe?.parts==null||!owner.recipe.parts.Any(p=>p.id==part))||!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>100||!MotionFrame.ValidRotation(rotation)||!float.IsFinite(width)||!float.IsFinite(height)||width<.02f||width>4||height<.02f||height>4||strokes==null||strokes.Length>MaximumStrokes)return false;
            if(strokes.Any(s=>s==null||!Guid.TryParseExact(s.id,"N",out _))||strokes.Select(s=>s.id).Distinct().Count()!=strokes.Length)return false;
            foreach(var s in strokes) {
                var c=s.color;
                if(!RoomDocument.ValidateDrawing(s.points,s.radius,out _)||s.points.Length>MaximumPoints||!Unit(c.r)||!Unit(c.g)||!Unit(c.b)||c.a!=1||s.points.Any(p=>Mathf.Abs(p.z)>.000001f||Mathf.Abs(p.x)+s.radius>width*.5f+.000001f||Mathf.Abs(p.y)+s.radius>height*.5f+.000001f))return false;
            }
            error=null;return true;
        }
        static bool Unit(float n)=>float.IsFinite(n)&&n>=0&&n<=1;
        public static bool ValidateCollection(RoomObjectData owner,out string error)
        {
            error=null;var all=owner.surfaces??Array.Empty<DrawingSurface>();
            if(all.Length>MaximumPerObject||owner.IsBuiltIn&&all.Length>0||all.Any(s=>s==null)||all.Select(s=>s.id).Distinct().Count()!=all.Length){error="Choose at most four unique drawing surfaces on a user-created object";return false;}
            foreach(var s in all)if(!s.Validate(owner,out error))return false;return true;
        }
        public static int PointCount(RoomObjectData owner)=>(owner.surfaces??Array.Empty<DrawingSurface>()).Sum(s=>s.strokes.Sum(x=>x.points.Length));
        public static int StrokeCount(RoomObjectData owner)=>(owner.surfaces??Array.Empty<DrawingSurface>()).Sum(s=>s.strokes.Length);
    }
}

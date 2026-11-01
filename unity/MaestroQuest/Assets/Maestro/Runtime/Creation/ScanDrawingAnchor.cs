// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Only exact Meta identities are saved. No cached scan mesh or guessed replacement anchor.</summary>
    [Serializable] public sealed class ScanDrawingAnchor
    {
        public int version=1;
        public string roomId,anchorId;
        public float x,y,angle;
        public const float Offset=.006f;
        public ScanDrawingAnchor Copy()=>new(){version=version,roomId=roomId,anchorId=anchorId,x=x,y=y,angle=angle};
        internal static bool Id(string s)=>Guid.TryParseExact(s,"N",out var id)&&id!=Guid.Empty&&id.ToString("N")==s;
        public static bool Has(RoomObjectData data)=>(data?.scanAnchors?.Length??0)>0;
        public bool Validate()=>version==1&&Id(roomId)&&Id(anchorId)&&float.IsFinite(x)&&float.IsFinite(y)&&Mathf.Abs(x)<=25&&Mathf.Abs(y)<=25&&float.IsFinite(angle)&&Mathf.Abs(angle)<=180;
        internal static bool ValidateOwner(RoomObjectData data,out string error)
        {
            error=null;if(!Has(data))return true;
            error="A scanned ink layer needs one exact anchor and one root plane; use its layer/ink controls instead of object movement or physics";
            var surfaces=data.surfaces;
            if(data.kind!=RoomObjectKind.Drawing||data.scanAnchors.Length!=1||data.scanAnchors[0]==null||!data.scanAnchors[0].Validate()||data.points==null||data.points.Length!=0||surfaces==null||surfaces.Length!=1)return false;
            var s=surfaces[0];if(s==null||s.id!="Canvas"||s.part!=""||s.Kind!="plane"||s.position!=Vector3.zero||s.rotation!=Quaternion.identity||data.position!=Vector3.zero||data.rotation!=Quaternion.identity||data.scale!=1||data.physics!=ItemPhysics.Fixed||data.collisionShape!=ItemCollider.Automatic||data.collision!=null||data.motion!=null||data.recipe!=null)return false;
            if((data.drawingTips?.Length??0)>0||(data.connections?.Length??0)>0||(data.snapPoints?.Length??0)>0||(data.containers?.Length??0)>0||(data.heightFields?.Length??0)>0||(data.sculptTips?.Length??0)>0||(data.materialStores?.Length??0)>0)return false;
            error=null;return true;
        }
        internal Vector2[] Corners(float width,float height)
        {
            var rotation=Quaternion.AngleAxis(angle,Vector3.forward);var corners=new Vector2[4];int i=0;
            foreach(var p in new[]{new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(width/2,height/2,0),new Vector3(-width/2,height/2,0)}){var v=rotation*p;corners[i++]=new Vector2(x+v.x,y+v.y);}return corners;
        }
        internal bool Fits(ScannedSurface surface,float width,float height)
        {
            if(surface==null||!surface.Plane.HasValue||!float.IsFinite(width)||!float.IsFinite(height)||width<=0||height<=0)return false;var corners=Corners(width,height);var rect=surface.Plane.Value;
            if(corners.Any(p=>p.x<rect.xMin-.00001f||p.x>rect.xMax+.00001f||p.y<rect.yMin-.00001f||p.y>rect.yMax+.00001f))return false;
            var polygon=surface.Boundary;if(polygon==null||polygon.Length==0)return true;
            if(corners.Any(p=>!Inside(p,polygon)))return false;
            for(int i=0;i<4;i++)if(!EdgeInside(corners[i],corners[(i+1)%4],polygon))return false;
            return true;
        }
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        // Split at every boundary intersection, including vertices and collinear edges.
        // Testing only corners/proper crossings misses a concave notch entered at a vertex.
        static bool EdgeInside(Vector2 a,Vector2 b,Vector2[] polygon)
        {
            var cuts=new List<float>{0,1};var edge=b-a;float length=edge.sqrMagnitude;
            for(int i=0;i<polygon.Length;i++){
                var c=polygon[i];var d=polygon[(i+1)%polygon.Length];var other=d-c;
                float denominator=edge.x*other.y-edge.y*other.x;
                if(Mathf.Abs(denominator)>1e-10f){
                    var offset=c-a;float t=(offset.x*other.y-offset.y*other.x)/denominator;
                    float u=(offset.x*edge.y-offset.y*edge.x)/denominator;
                    if(t>=0&&t<=1&&u>=-1e-6f&&u<=1+1e-6f)cuts.Add(t);
                }else if(Mathf.Abs(Cross(a,b,c))<1e-8f){
                    cuts.Add(Mathf.Clamp01(Vector2.Dot(c-a,edge)/length));cuts.Add(Mathf.Clamp01(Vector2.Dot(d-a,edge)/length));
                }
            }
            cuts.Sort();for(int i=1;i<cuts.Count;i++)if(!Inside(a+edge*((cuts[i-1]+cuts[i])*.5f),polygon))return false;
            return true;
        }
        static bool Inside(Vector2 p,Vector2[] polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++){
                var a=polygon[j];var b=polygon[i];var ab=b-a;float t=ab.sqrMagnitude==0?0:Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);if((p-(a+t*ab)).sqrMagnitude<.0000000001f)return true;
                if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }return inside;
        }
    }
}

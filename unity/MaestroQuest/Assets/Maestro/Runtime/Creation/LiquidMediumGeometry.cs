// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // One geometric interpretation of the live measured quantity; no extra inventory.
    internal readonly partial struct LiquidMediumGeometry {
        internal readonly string Id;internal readonly RoomItem Item;internal readonly RoomContainer Contents;
        internal readonly Rigidbody Body;
        readonly TerrainLiquidGeometry terrain;readonly Vector3 terrainNormal;readonly float terrainLevel;
        internal readonly float Level,Volume;internal readonly Vector3 Up;readonly Matrix4x4 inverse;
        internal LiquidMediumGeometry(string id,RoomItem item,RoomContainer contents,Vector3 up,Rigidbody body){
            Id=id;Item=item;Contents=contents;Up=up;Body=body;
            var t=item.transform;float scale=Mathf.Abs(t.lossyScale.x);
            var rotation=t.rotation*contents.frame.rotation;terrain=RoomTerrainReservoir.Native(contents,t);terrainNormal=Quaternion.Inverse(rotation)*up;
            inverse=(t.localToWorldMatrix*Matrix4x4.TRS(contents.frame.position,contents.frame.rotation,Vector3.one)).inverse;
            var centre=t.TransformPoint(contents.frame.position+contents.frame.rotation*(Vector3.up*contents.height*.5f));
            float localLevel=ContainerFlowGeometry.Level(contents,terrainNormal,contents.amountMl/contents.capacityMl,terrain);terrainLevel=localLevel+terrainNormal.y*contents.height*.5f;
            Level=Vector3.Dot(centre,up)+localLevel*scale;
            Volume=(float)(terrain?.Volume??contents.FootprintArea*contents.height)*scale*scale*scale;
        }
        // Clip the whole swept upright body against the convex liquid cavity.
        // The cylinder uses sixteen circumscribed planes (at most 2% wider at a
        // corner). Expanded planes conservatively include the body footprint:
        // a narrow cavity cannot be skipped between movement frames.
        internal bool Sweep(Vector3 from,Vector3 to,float radius,float height,out float depth,out Vector3 contact)=>Sweep(from,to,radius,height,out depth,out contact,out _);
        internal bool Sweep(Vector3 from,Vector3 to,float radius,float height,out float depth,out Vector3 contact,out Vector3 foot){
            depth=0;contact=foot=default;if(Contents.amountMl<=0)return false;
            if(terrain!=null){bool touched=terrain.Sweep(inverse,from,to,radius,height,terrainNormal,terrainLevel,Up,Level,out depth,out foot);contact=foot+Vector3.up*Mathf.Clamp(depth*.5f,.001f,height);return touched;}
            var a=inverse.MultiplyPoint3x4(from);var d=inverse.MultiplyVector(to-from);float enter=0,exit=1;
            for(int i=0;i<PlaneCount;i++){LocalPlane(i,out var normal,out float limit);if(!Plane(normal,limit,a,d,radius,height,ref enter,ref exit))return false;}
            // The liquid surface stays perpendicular to gravity, including tilted vessels.
            float support=Support(Up,radius,height);
            if(!Clip(Vector3.Dot(Up,from),Vector3.Dot(Up,to-from),Level+support,ref enter,ref exit))return false;
            foot=Vector3.Lerp(from,to,Vector3.Dot(Up,to-from)<0?exit:enter);
            depth=Mathf.Max(0,Level-Vector3.Dot(Up,foot));
            contact=foot+Vector3.up*Mathf.Clamp(depth*.5f,.001f,height);
            return depth>.0001f;
        }
        // Egress never grants a dry actor permission to enter water, or a
        // submerged actor permission to walk beyond its wading depth. The whole
        // segment must stay no deeper than its starting contact. On level ground
        // it must approach a closest expanded cavity boundary, not cross the pool
        // toward a farther shore. Movement callers still check ground and solids.
        internal bool ShallowExit(Vector3 from,Vector3 to,float radius,float height,float maximumDepth,float sweptDepth){
            float epsilon=Mathf.Max(1e-6f,Mathf.Min(.001f,height*.001f));
            if(!Sweep(from,from,radius,height,out float startDepth,out _)||
                sweptDepth>maximumDepth+epsilon||sweptDepth>startDepth+epsilon)return false;
            var change=to-from;
            if(Vector3.Dot(Up,change)>epsilon)return true;
            var local=inverse.MultiplyPoint3x4(from);float closest=float.PositiveInfinity,progress=float.NegativeInfinity;
            for(int i=0;i<PlaneCount;i++){
                LocalPlane(i,out var normal,out float limit);var worldNormal=inverse.transpose.MultiplyVector(normal);
                float horizontal=new Vector2(worldNormal.x,worldNormal.z).magnitude;if(horizontal<1e-6f)continue;
                float distance=(limit+Support(worldNormal,radius,height)-Vector3.Dot(normal,local))/horizontal;
                float outward=Vector3.Dot(worldNormal,change)/horizontal;
                if(distance<closest-epsilon){closest=distance;progress=outward;}
                else if(distance<=closest+epsilon)progress=Mathf.Max(progress,outward);
            }
            return progress>1e-6f;
        }
        int PlaneCount=>Contents.IsRectangular?6:18;
        void LocalPlane(int index,out Vector3 normal,out float limit){
            if(index<2){normal=index==0?Vector3.down:Vector3.up;limit=index==0?0:Contents.height;return;}
            if(Contents.IsRectangular){normal=index switch{2=>Vector3.right,3=>Vector3.left,4=>Vector3.forward,_=>Vector3.back};limit=index<4?Contents.rectangle.width*.5f:Contents.rectangle.depth*.5f;return;}
            float angle=(index-2)*Mathf.PI/8;normal=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));limit=Contents.radius;
        }
        internal static float Support(Vector3 normal,float radius,float height)=>radius*Mathf.Sqrt(normal.x*normal.x+normal.z*normal.z)-Mathf.Min(0,normal.y*height);
        // Ordinary cavities use Sweep’s expanded half-spaces at a foot height.
        // Terrain routing conservatively uses the outer reservoir footprint;
        // actual segment admission still clips against its wet terrain triangles.
        internal void RouteLines(float radius,float height,float footY,System.Collections.Generic.List<Vector3> lines){
            lines.Clear();var foot=new Vector3(0,footY,0);var local=inverse.MultiplyPoint3x4(foot);
            for(int i=0;i<PlaneCount;i++){
                LocalPlane(i,out var normal,out float limit);var worldNormal=inverse.transpose.MultiplyVector(normal);
                lines.Add(new Vector3(worldNormal.x,worldNormal.z,limit+Support(worldNormal,radius,height)-Vector3.Dot(normal,local)));
            }
            lines.Add(new Vector3(Up.x,Up.z,Level+Support(Up,radius,height)-Vector3.Dot(Up,foot)));
        }
        bool Plane(Vector3 normal,float limit,Vector3 a,Vector3 d,float radius,float height,ref float enter,ref float exit){
            var worldNormal=inverse.transpose.MultiplyVector(normal);
            float support=Support(worldNormal,radius,height);
            return Clip(Vector3.Dot(normal,a),Vector3.Dot(normal,d),limit+support,ref enter,ref exit);
        }
        internal static bool Clip(float at,float change,float limit,ref float enter,ref float exit){
            if(Mathf.Abs(change)<1e-8f)return at<=limit;
            float t=(limit-at)/change;if(change>0)exit=Mathf.Min(exit,t);else enter=Mathf.Max(enter,t);
            return enter<=exit;
        }
        // Constant-time contact with the measured free surface or an already
        // wet origin. This deliberately excludes the swept wet-volume fallback
        // used by rain, so prepared stream segments never scan terrain cells.
        internal bool TryFreeSurfaceContact(Vector3 from,Vector3 to,out Vector3 point,out float fraction)=>
            FreeSurfaceContact(from,to,true,out point,out fraction);
        bool FreeSurfaceContact(Vector3 from,Vector3 to,bool toleratePlaneOrigin,out Vector3 point,out float fraction){
            point=default;fraction=0;
            if(Contents==null||Contents.amountMl<=0||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to))return false;
            float before=Vector3.Dot(from,Up)-Level,after=Vector3.Dot(to,Up)-Level;
            if(!float.IsFinite(before)||!float.IsFinite(after))return false;
            // A full receiver's world-space mouth can invert one float step
            // above its local ceiling. Only an origin within one micrometre of
            // the measured plane gets vertical cavity roundoff tolerance; the
            // finite horizontal footprint and terrain proof stay exact.
            if(toleratePlaneOrigin&&Mathf.Abs(before)<=.000001f){
                var onPlane=from-Up*before;var at=inverse.MultiplyPoint3x4(onPlane);
                if(at.y>=-.00001f&&at.y<=Contents.height+.00001f&&Contents.ContainsHorizontal(at)&&(terrain==null||terrain.Contains(at))){point=onPlane;return true;}
            }
            if(before<=0&&ContainsAtOrBelowSurface(from)){point=from;return true;}
            if(before<=0||after>0)return false;
            fraction=before/(before-after);if(!float.IsFinite(fraction))return false;
            point=Vector3.LerpUnclamped(from,to,fraction);var local=inverse.MultiplyPoint3x4(point);
            return local.y>=-.0001f&&local.y<=Contents.height+.0001f&&Contents.ContainsHorizontal(local)&&(terrain==null||terrain.Contains(local));
        }
        // Presentation accepts a descending endpoint on the plane. An origin
        // already in/on this finite medium, or entry through a submerged side,
        // is suppression rather than a new free-surface impact. Keep the
        // through-crossing contact contract below unchanged.
        internal bool FirstSurfaceContact(Vector3 from,Vector3 to,out Vector3 point,out float fraction,out bool submerged){
            point=default;fraction=0;submerged=false;
            if(Contents.amountMl<=0||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to))return false;
            float before=Vector3.Dot(from,Up)-Level,after=Vector3.Dot(to,Up)-Level;
            if(!float.IsFinite(before)||!float.IsFinite(after))return false;
            if(FreeSurfaceContact(from,to,false,out point,out fraction)){submerged=before<=0;return true;}
            if(!FirstWetEntry(from,to,out fraction))return false;
            point=Vector3.LerpUnclamped(from,to,fraction);submerged=true;return true;
        }
        bool FirstWetEntry(Vector3 from,Vector3 to,out float fraction){
            bool found=MeasureSegment(from,to,out var path);fraction=path.Enter;return found;
        }
        internal bool MeasureSegment(Vector3 from,Vector3 to,out LiquidSegment path){
            path=default;
            if(Contents==null||Contents.amountMl<=0||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to))return false;
            var a=inverse.MultiplyPoint3x4(from);var d=inverse.MultiplyVector(to-from);
            if(!RoomRecipe.Finite(a)||!RoomRecipe.Finite(d))return false;
            if(terrain!=null)return terrain.MeasureSegment(a,a+d,terrainNormal,terrainLevel,out path);
            float enter=0,exit=1;
            if(!Clip(-a.y,-d.y,0,ref enter,ref exit)||!Clip(a.y,d.y,Contents.height,ref enter,ref exit)||
                !Clip(Vector3.Dot(from,Up),Vector3.Dot(to-from,Up),Level,ref enter,ref exit))return false;
            if(Contents.IsRectangular){
                if(!Clip(a.x,d.x,Contents.rectangle.width*.5f,ref enter,ref exit)||!Clip(-a.x,-d.x,Contents.rectangle.width*.5f,ref enter,ref exit)||
                    !Clip(a.z,d.z,Contents.rectangle.depth*.5f,ref enter,ref exit)||!Clip(-a.z,-d.z,Contents.rectangle.depth*.5f,ref enter,ref exit))return false;
            }else{
                // Exact circle interval; the circumscribed body sweep is too
                // generous for a zero-radius cosmetic point near the rim.
                double aa=(double)d.x*d.x+(double)d.z*d.z;
                double bb=2*((double)a.x*d.x+(double)a.z*d.z);
                double cc=(double)a.x*a.x+(double)a.z*a.z-(double)Contents.radius*Contents.radius;
                if(aa==0){if(cc>0)return false;}
                else{
                    double discriminant=bb*bb-4*aa*cc;if(discriminant<0)return false;
                    double root=System.Math.Sqrt(discriminant);
                    enter=Mathf.Max(enter,(float)((-bb-root)/(2*aa)));exit=Mathf.Min(exit,(float)((-bb+root)/(2*aa)));
                    if(enter>exit)return false;
                }
            }
            path=new LiquidSegment(enter,exit,(double)exit-enter);return true;
        }
        internal bool ContainsAtOrBelowSurface(Vector3 point){
            if(Contents.amountMl<=0||!RoomRecipe.Finite(point)||Vector3.Dot(point,Up)>Level)return false;
            var local=inverse.MultiplyPoint3x4(point);
            return local.y>=0&&local.y<=Contents.height&&Contents.ContainsHorizontal(local)&&(terrain==null||terrain.Contains(local));
        }
        internal bool CrossesSurface(Vector3 from,Vector3 to,out Vector3 point)=>CrossesSurface(from,to,out point,out _);
        internal bool CrossesSurface(Vector3 from,Vector3 to,out Vector3 point,out float fraction){
            point=default;fraction=0;float before=Vector3.Dot(from,Up)-Level,after=Vector3.Dot(to,Up)-Level;
            if(before*after>=0||Mathf.Abs(before)<1e-6f||Mathf.Abs(after)<1e-6f)return false;
            fraction=before/(before-after);point=Vector3.LerpUnclamped(from,to,fraction);
            var local=inverse.MultiplyPoint3x4(point);
            return Contents.amountMl>0&&local.y>=-.0001f&&local.y<=Contents.height+.0001f&&Contents.ContainsHorizontal(local)&&(terrain==null||terrain.Contains(local));
        }
        internal bool Sample(Vector3 point,out float depth){
            depth=Level-Vector3.Dot(point,Up);var local=inverse.MultiplyPoint3x4(point);
            return Contents.amountMl>0&&depth>0&&local.y>=0&&local.y<=Contents.height&&Contents.ContainsHorizontal(local)&&(terrain==null||terrain.Contains(local));
        }
    }
}

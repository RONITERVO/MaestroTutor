// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Geometry only: the caller's prepared aperture/solid proof owns admission.
    // No quantity copy, transfer, physics query or terrain-cell traversal.
    internal readonly struct LiquidStreamGeometry
    {
        const float BoundaryTolerance=.00001f;
        internal readonly LiquidMediumGeometry Medium;
        readonly RoomContainer contents;
        readonly Matrix4x4 inverse;
        readonly TerrainLiquidGeometry terrain;
        readonly Collider terrainBed;
        readonly bool valid;
        internal LiquidStreamGeometry(RoomContainer live,RoomItem item,Vector3 up)
        {
            Medium=default;contents=live;inverse=default;terrain=null;terrainBed=null;valid=false;
            if(live==null||!item||!RoomRecipe.Finite(up)||!float.IsFinite(up.sqrMagnitude)||Mathf.Abs(up.sqrMagnitude-1)>.001f)return;
            var placement=item.transform.localToWorldMatrix*Matrix4x4.TRS(live.frame.position,live.frame.rotation,Vector3.one);
            if(!float.IsFinite(placement.determinant)||Mathf.Abs(placement.determinant)<1e-12f)return;
            terrain=RoomTerrainReservoir.Native(live,item.transform);
            if(live.TerrainFloor&&terrain==null)return;
            terrainBed=terrain!=null?item.GetComponent<HeightFieldView>().Collision:null;
            inverse=placement.inverse;
            Medium=new LiquidMediumGeometry("",item,live,up,null);valid=true;
        }
        internal bool IsTerrainBed(Collider collider)=>valid&&terrainBed&&collider==terrainBed&&terrainBed.enabled&&terrainBed.gameObject.activeInHierarchy;
        internal bool TryTerrainDeposition(Collider collider,Vector3 point)
        {
            if(!IsTerrainBed(collider)||!RoomRecipe.Finite(point))return false;
            var local=inverse.MultiplyPoint3x4(point);
            return ContainsCavity(local,BoundaryTolerance)&&TerrainContainsBoundary(local);
        }
        bool ContainsCavity(Vector3 local,float tolerance)
        {
            if(!RoomRecipe.Finite(local)||local.y < -tolerance||local.y>contents.height+tolerance)return false;
            return contents.IsRectangular
                ? Mathf.Abs(local.x)<=contents.rectangle.width*.5f+tolerance&&Mathf.Abs(local.z)<=contents.rectangle.depth*.5f+tolerance
                : (double)local.x*local.x+(double)local.z*local.z<=(double)(contents.radius+tolerance)*(contents.radius+tolerance);
        }
        bool TerrainContainsBoundary(Vector3 local)
        {
            // Only compensate boundary roundoff. A logical floor beneath the
            // actual bed, or a hit on the collider's underside, stays invalid.
            local.x=Mathf.Clamp(local.x,-contents.rectangle.width*.5f,contents.rectangle.width*.5f);
            local.z=Mathf.Clamp(local.z,-contents.rectangle.depth*.5f,contents.rectangle.depth*.5f);
            local.y=Mathf.Clamp(local.y+BoundaryTolerance,0,contents.height);
            return terrain.Contains(local);
        }
        internal bool TryDeposition(Vector3 from,Vector3 to,out Vector3 point,out float fraction,out bool boundary,out float cavityExit)
        {
            point=default;fraction=0;boundary=false;cavityExit=float.PositiveInfinity;
            if(!valid||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!float.IsFinite((to-from).sqrMagnitude))return false;
            bool wet=Medium.TryFreeSurfaceContact(from,to,out point,out fraction);
            bool hasExit=TryCavityExit(from,to,out var edge,out float edgeFraction);
            if(hasExit)cavityExit=edgeFraction;
            if(!hasExit||wet&&fraction<=edgeFraction)return wet;
            point=edge;fraction=edgeFraction;boundary=true;return true;
        }
        bool TryCavityExit(Vector3 from,Vector3 to,out Vector3 point,out float fraction)
        {
            point=default;fraction=0;var a=inverse.MultiplyPoint3x4(from);var d=inverse.MultiplyVector(to-from);
            // The segment must start at/in this already selected cavity. Small
            // local tolerance absorbs inverse-transform roundoff at its mouth.
            if(!ContainsCavity(a,BoundaryTolerance))return false;
            float enter=float.NegativeInfinity,exit=float.PositiveInfinity;
            if(!LiquidMediumGeometry.Clip(-a.y,-d.y,0,ref enter,ref exit)||!LiquidMediumGeometry.Clip(a.y,d.y,contents.height,ref enter,ref exit))return false;
            if(contents.IsRectangular){
                if(!LiquidMediumGeometry.Clip(a.x,d.x,contents.rectangle.width*.5f,ref enter,ref exit)||!LiquidMediumGeometry.Clip(-a.x,-d.x,contents.rectangle.width*.5f,ref enter,ref exit)||
                    !LiquidMediumGeometry.Clip(a.z,d.z,contents.rectangle.depth*.5f,ref enter,ref exit)||!LiquidMediumGeometry.Clip(-a.z,-d.z,contents.rectangle.depth*.5f,ref enter,ref exit))return false;
            }else{
                double aa=(double)d.x*d.x+(double)d.z*d.z,bb=2*((double)a.x*d.x+(double)a.z*d.z);
                double cc=(double)a.x*a.x+(double)a.z*a.z-(double)contents.radius*contents.radius;
                if(aa==0){if(cc>0)return false;}
                else{
                    double discriminant=bb*bb-4*aa*cc;if(discriminant<0)return false;
                    double root=System.Math.Sqrt(discriminant);
                    enter=Mathf.Max(enter,(float)((-bb-root)/(2*aa)));exit=Mathf.Min(exit,(float)((-bb+root)/(2*aa)));
                    if(enter>exit)return false;
                }
            }
            if(!float.IsFinite(exit)||exit<0||exit>1)return false;
            if(terrain!=null&&!TerrainContainsBoundary(a+d*exit))return false;
            fraction=exit;point=Vector3.LerpUnclamped(from,to,exit);return true;
        }
    }
}

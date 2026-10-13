// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Maestro.Quest.Interaction {
    public sealed partial class RoomNavigation {
        NavMeshPath routeScratch;RoomRouteSearch routeSearch;
        uint routeGeometry;string routeMode;float routeDepth;bool routeChecksBody;Func<Collider,bool> followObstacle,routeObstacle;
        internal bool FollowRoutePending=>routeSearch?.Pending==true||routeInputs?.Pending==true;
        internal string FollowRouteStatus {get;private set;}
        internal void CancelFollowRoute(){routeSearch=null;solidRouteBodies.Clear();FollowRouteStatus=null;followObstacle=null;ReleaseRouteInputs();}
        void ReleaseFollowRoute(){CancelFollowRoute();routeScratch?.ClearCorners();routeScratch=null;routeObstacle=null;routeMode=null;routeDepth=0;routeGeometry=0;}
        bool RouteObstacle(Collider c)=>!CurrentGround.Contains(c)&&followObstacle(c);
        bool RouteSegment(Vector3 from,Vector3 to,out string liquid,out Vector3 foot,bool checkBody=true,bool checkWater=true){
            liquid=null;foot=from;
            if(!RouteInputsReady(from,to,.15f,out var inputError)){WaterBlocker=inputError;return false;}
            if(checkWater&&actor&&actor.WaterTraversal.mode!="ignore"){
                var editor=actor.GetComponentInParent<Creation.RoomEditor>();
                if(!editor||!editor.Liquids){WaterBlocker="Water traversal is unavailable";return false;}
                if(!editor.Liquids.CheckTraversal(actor,from,to,radius,height,out var result)){liquid=result.BodyId;foot=result.Foot;WaterBlocker=result.Reason;return false;}
            }
            if(!checkBody)return true;
            int mask=world.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),actor);
            bool clear=groundMotor.ClearExact(from,to,radius,height,mask,routeObstacle??=RouteObstacle);
            TraversalBlocker=clear?null:groundMotor.Blocker;
            if(!clear&&TraversalBlocker){liquid=RememberRouteSolid(TraversalBlocker);foot=from;}return clear;
        }
        bool ValidateRouteSegment(Vector3 from,Vector3 to)=>RouteSegment(from,to,out _,out _);
        bool RouteEdge(Vector3 from,Vector3 to,out Vector3[] points,out string liquid,out Vector3 foot)=>RouteEdge(from,to,out points,out liquid,out foot,true);
        bool RouteEdge(Vector3 from,Vector3 to,out Vector3[] points,out string liquid,out Vector3 foot,bool checkBody){
            points=null;liquid=null;foot=from;
            if(!RouteInputsReady(from,to,.5f,out var inputError)){WaterBlocker=inputError;return false;}
            routeScratch??=new NavMeshPath();
            if(!NavMesh.CalculatePath(from,to,Filter,routeScratch)||routeScratch.status!=NavMeshPathStatus.PathComplete)return false;
            var corners=routeScratch.corners;if(corners.Length<2||corners.Length>RoomRouteSearch.MaximumCorners)return false;
            for(int i=0;i<corners.Length;i++){
                if(!RouteInputsReady(corners[i],corners[i],.15f,out var cornerError)){WaterBlocker=cornerError;return false;}
                if(!CurrentGround.Sample(corners[i],.15f,.15f,out var support)||!GroundPosition(support.point))return false;
                corners[i]=support.point;
            }
            for(int i=1;i<corners.Length;i++)if(!RouteSegment(corners[i-1],corners[i],out liquid,out foot,checkBody))return false;
            points=corners;return true;
        }
        void RouteFootprint(string id,Vector3 reference,List<Vector3> points){
            if(SolidRouteFootprint(id,reference,points))return;
            var editor=actor?actor.GetComponentInParent<Creation.RoomEditor>():null;
            if(editor&&editor.Liquids)editor.Liquids.RouteFootprint(id,radius+.06f,height,reference.y,points);
        }
        bool RouteSample(Vector3 point,out Vector3 floor){
            floor=default;
            if(!RouteInputsReady(point,point,2,out var inputError)){WaterBlocker=inputError;return false;}
            return CurrentGround.Sample(point,2,2,out var support)&&SampleInstalled(support.point,.15f,out floor);
        }
        internal bool FollowRoute(Vector3 from,Vector3 to,Func<Collider,bool> obstacle,out Vector3[] points,bool avoidSolids=false){
            points=Array.Empty<Vector3>();WaterBlocker=null;TraversalBlocker=null;
            if(obstacle==null||!Ready||PathsPending){CancelFollowRoute();FollowRouteStatus="Waiting for accepted walking surfaces";return false;}
            BeginRouteInputs(to);
            if(routeSearch!=null&&(routeGeometry!=SurfaceRevision||routeChecksBody!=avoidSolids||!SolidRouteBodiesCurrent()||routeMode!=actor?.WaterTraversal.mode||routeDepth!=(actor?.WaterTraversal.maxDepthMetres??0)||
                (routeSearch.From-from).sqrMagnitude>.01f||(routeSearch.To-to).sqrMagnitude>.04f))CancelFollowRoute();
            followObstacle=obstacle;
            if(routeSearch==null){
                solidRouteBodies.Clear();
                if(!RouteInputsReady(from,to,.5f,out var inputError)){FollowRouteStatus=inputError;return false;}
                if(!SampleInstalled(from,.25f,out var start)||!SampleInstalled(to,.5f,out var end)){FollowRouteStatus="No supported walking destination is available";return false;}
                // No route can repair occupied destination body space. Reject it
                // before expanding candidates; water admission remains path-relative.
                if(avoidSolids&&!RouteSegment(end,end,out _,out _,checkWater:false)){
                    FollowRouteStatus=routeInputs?.Status??"The walking destination is occupied";return false;
                }
                // Keep ordinary following incremental: a distant prop must not freeze the
                // actor before it can approach. Every actual step still sweeps its body.
                // Finite travel and a blocked follower additionally plan around solids.
                // Every detour edge checks both current water and body clearance.
                if(RouteEdge(start,end,out points,out string liquid,out var foot,avoidSolids)){FollowRouteStatus=null;return true;}
                points=Array.Empty<Vector3>();
                if(IncompleteRouteInputs())return false;
                if(string.IsNullOrEmpty(liquid)){FollowRouteStatus=WaterBlocker??"No connected clear route is available";return false;}
                routeSearch=new(start,end,liquid,foot,RouteEdge,RouteFootprint,RouteSample,ValidateRouteSegment,()=>routeInputs?.Status==null);
                routeGeometry=SurfaceRevision;routeChecksBody=avoidSolids;routeMode=actor?.WaterTraversal.mode;routeDepth=actor?.WaterTraversal.maxDepthMetres??0;
            }
            routeSearch.Tick();
            if(IncompleteRouteInputs())return false;
            if(routeSearch.Result!=null){points=routeSearch.Result;routeSearch=null;FollowRouteStatus=null;WaterBlocker=null;return true;}
            if(routeSearch.Failure!=null){FollowRouteStatus=routeSearch.Failure;WaterBlocker=FollowRouteStatus;routeSearch=null;return false;}
            FollowRouteStatus="Finding a walking route around obstacles or water";return false;
        }
    }
}

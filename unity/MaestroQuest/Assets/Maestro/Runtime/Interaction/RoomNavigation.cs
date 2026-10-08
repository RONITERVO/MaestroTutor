// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.AI;

namespace Maestro.Quest.Interaction
{
    /// <summary>Owned navigation built from accepted scanned and authored collision surfaces.</summary>
    public sealed partial class RoomNavigation : MonoBehaviour
    {
        RoomPhysicsWorld world;
        RoomItem actor;
        Transform virtualFrame;
        internal void SetVirtualFrame(Transform value){virtualFrame=value;observedFrame=false;Clear();}
        NavMeshData data;
        NavMeshDataInstance installed;
        int agentType = -1;
        float radius, height;
        NavMeshQueryFilter Filter => new() { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        RoomNavigationGeometry accepted=new(), candidate=new();
        bool configured,observedFrame;
        Matrix4x4 lastFrame;
        float frameChangedAt;
        int frameChangedOn;
        // Relative real/authored geometry needs a new route map, but a continuously
        // moving world must not synchronously bake one every input frame.
        internal const float FrameSettleSeconds=.15f;
        internal bool PathsPending {get;private set;}
        RoomGroundQuery CurrentGround => PathsPending?candidate.Ground:accepted.Ground;
        static readonly Func<Collider,bool> IgnoreObstacle=_=>false;
        readonly RoomGroundMotor groundMotor=new();
        Func<Vector3,bool> groundPosition;
        internal Collider TraversalBlocker {get;private set;}
        internal string WaterBlocker {get;private set;}
        bool WaterStep(Vector3 from,Vector3 to,float bodyHeight){
            WaterBlocker=null;
            if(!actor||actor.WaterTraversal.mode=="ignore")return true;
            var editor=actor.GetComponentInParent<Creation.RoomEditor>();
            if(!editor||!editor.Liquids){WaterBlocker="Water traversal is unavailable";return false;}
            bool allowed=editor.Liquids.CheckTraversal(actor,from,to,radius,bodyHeight,out var result);
            if(!allowed)WaterBlocker=result.Reason;return allowed;
        }
        internal uint SurfaceRevision { get; private set; }
        internal uint BuildRevision { get; private set; }
        Vector3 installedPosition;
        Quaternion installedRotation;
        public bool Ready => configured && RefreshGeometry(out _);
        public void Initialize(RoomPhysicsWorld value) { world = value; world.Changed += RoomChanged; }
        void RoomChanged() { if (!world.EnvironmentReady(actor)) Clear(); }
        public bool Prepare(float bodyRadius, float bodyHeight, out string error,RoomItem target=null)
        {
            error = null;
            if(actor!=target){Clear();actor=target;}
            if (!world || !world.Running || !world.EnvironmentReady(actor)) { error = "Prepare the selected ground and Start physics before walking"; return false; }
            if(!float.IsFinite(bodyRadius)||!float.IsFinite(bodyHeight)||bodyRadius<=0||bodyHeight<bodyRadius*2) { error="Choose a valid walking body";return false; }
            if(!configured||Mathf.Abs(radius-bodyRadius)>=.005f||Mathf.Abs(height-bodyHeight)>=.01f)Clear();
            radius=bodyRadius;height=bodyHeight;configured=true;
            return RefreshGeometry(out error);
        }
        bool RefreshGeometry(out string error)
        {
            error="Room navigation needs active physics and accepted surfaces";
            if(!world||!world.Running||!world.EnvironmentReady(actor)){Clear();return false;}
            bool frameMoved=false;
            if(virtualFrame) {
                var frame=world.transform.worldToLocalMatrix*virtualFrame.localToWorldMatrix;
                frameMoved=observedFrame&&!frame.Equals(lastFrame);
                if(frameMoved){frameChangedAt=Time.unscaledTime;frameChangedOn=Time.frameCount;}
                lastFrame=frame;observedFrame=true;
            }
            Physics.SyncTransforms();
            if(!candidate.Capture(world.transform,world.IncludesRealRoom(actor),!world.IncludesRealRoom(actor)&&virtualFrame?virtualFrame:world.transform)) { Clear();error="No accepted scanned or authored floor is available for walking";return false; }
            if(data&&installed.valid&&candidate.Same(accepted)){
                Install(candidate.Position,candidate.Rotation);error=null;return installed.valid;
            }
            if(PathsPending||data&&frameMoved) {
                // Retire the old route immediately. Direct traversal still uses
                // the freshly captured accepted colliders and swept body checks.
                Clear();PathsPending=true;
                if(Time.frameCount-frameChangedOn<2||Time.unscaledTime-frameChangedAt<FrameSettleSeconds){error=null;return true;}
            }
            // A mesh edit or root move must never reuse a stale route. Build from
            // exactly the collider geometry captured for this accepted revision.
            Clear();
            var bounds=candidate.Bounds;bounds.Expand(.2f);
            var settings = NavMesh.CreateSettings(); agentType = settings.agentTypeID;
            settings.agentRadius = radius + .025f; settings.agentHeight = height;
            settings.agentClimb = RoomGroundQuery.MaximumStep; settings.agentSlope = RoomGroundQuery.MaximumSlope;
            settings.overrideVoxelSize = true; settings.voxelSize = Mathf.Clamp(radius/4,.025f,.08f);
            settings.minRegionArea = .1f;
            data = NavMeshBuilder.BuildNavMeshData(settings,candidate.BuildSources,bounds,Vector3.zero,Quaternion.identity);
            if (!data) { Clear(); error = "No walking area could be built from the accepted surfaces"; return false; }
            Install(candidate.Position,candidate.Rotation); BuildRevision++;
            (accepted,candidate)=(candidate,accepted);
            error=installed.valid?null:"The accepted walking area could not be installed";
            return installed.valid;
        }
        void Install(Vector3 position,Quaternion rotation)
        {
            if(installed.valid&&installedPosition.Equals(position)&&installedRotation.Equals(rotation))return;
            if(installed.valid)installed.Remove();
            installed=NavMesh.AddNavMeshData(data,position,rotation);
            installedPosition=position;installedRotation=rotation;SurfaceRevision++;
        }
        public bool Sample(Vector3 point, float maximumDistance, out Vector3 floor)
        {
            floor = default;
            if(!Ready||!Creation.RoomRecipe.Finite(point)||!float.IsFinite(maximumDistance)||maximumDistance<0)return false;
            if(PathsPending) {
                if(!CurrentGround.Sample(point,maximumDistance,maximumDistance,out var current)||
                    !world.ContainsSimulation(current.point+Vector3.up*.1f,actor))return false;
                floor=current.point;return true;
            }
            return SampleInstalled(point,maximumDistance,out floor);
        }
        bool SampleInstalled(Vector3 point,float maximumDistance,out Vector3 floor){
            floor=default;
            if (!NavMesh.SamplePosition(point,out var hit,maximumDistance,Filter) || !world.ContainsSimulation(hit.position + Vector3.up*.1f,actor)) return false;
            // NavMesh is a route approximation. Foot placement uses the accepted
            // collider itself, including its current revision and rendered height.
            if(!accepted.Ground.Sample(hit.position,.08f,.08f,out var support)||Vector3.Distance(point,support.point)>maximumDistance+.001f)return false;
            floor=support.point;return true;
        }
        public bool DirectStep(Vector3 from,Vector3 to,out Vector3 floor)
        {
            floor=default;
            if(!Ready)return false;
            if(PathsPending)return groundMotor.Travel(CurrentGround,from,to-from,radius,height,0,IgnoreObstacle,
                groundPosition??=GroundPosition,out floor,out _);
            return Sample(from,.1f,out var start) && Sample(to,.08f,out floor) &&
                Vector3.Distance(new Vector3(to.x,floor.y,to.z),floor) < .025f && !NavMesh.Raycast(start,floor,out _,Filter);
        }
        bool GroundPosition(Vector3 value)=>world&&world.ContainsSimulation(value+Vector3.up*.1f,actor);
        internal bool Traverse(Vector3 from,Vector3 to,Func<Collider,bool> obstacle,out Vector3 floor,out string error)
        {
            TraversalBlocker=null;WaterBlocker=null;floor=default;error="No connected accepted ground is available for this step";
            if(!Ready||!PathsPending&&!DirectStep(from,to,out _))return false;
            int mask=world.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),actor);
            bool moved=groundMotor.Travel(CurrentGround,from,to-from,radius,height,mask,obstacle,groundPosition??=GroundPosition,out floor,out error);
            if(!moved)TraversalBlocker=groundMotor.Blocker;
            else if(!WaterStep(from,floor,height)){error=WaterBlocker;return false;}
            return moved;
        }
        internal bool ClearAuthoredStep(Vector3 from,Vector3 to,float extraHeight,Func<Collider,bool> obstacle)
        {
            WaterBlocker=null;int mask=world.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),actor);
            bool clear=groundMotor.ClearExact(from,to,radius,height+extraHeight,mask,obstacle);TraversalBlocker=clear?null:groundMotor.Blocker;
            return clear&&WaterStep(from,to,height);
        }
        public bool Path(Vector3 from, Vector3 to, NavMeshPath path) {
            WaterBlocker=null;
            if(!Ready||PathsPending||!Sample(from,.25f,out var start)||!Sample(to,.5f,out var end)||
                !NavMesh.CalculatePath(start,end,Filter,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            var corners=path.corners;
            for(int i=1;i<corners.Length;i++)if(!WaterStep(corners[i-1],corners[i],height))return false;
            return true;
        }
        void Clear()
        {
            PathsPending=false;CancelFollowRoute();
            if (installed.valid) { installed.Remove(); SurfaceRevision++; }
            ArtResources.Release(data); data = null;
            if (agentType != -1) NavMesh.RemoveSettings(agentType);
            agentType = -1;
        }
        void OnDestroy() { if (world) world.Changed -= RoomChanged; Clear(); }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
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
        RoomNavigationMap map;
        NavMeshData data=>map?.Data;
        int agentType=>map?.AgentType??-1;
        readonly string resourceOwner=Guid.NewGuid().ToString("N");
        uint resourceGeneration;
        internal string NavigationIssue {get;private set;}
        float radius, height;
        NavMeshQueryFilter Filter => new() { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        RoomNavigationGeometry accepted=new(), candidate=new(), building=new();
        RoomNavigationBuild pendingBuild;
        bool configured,observedFrame,actorBound;
        Matrix4x4 lastFrame;
        float frameChangedAt;
        int frameChangedOn=-1;
        // Relative real/authored geometry needs a new route map, but a continuously
        // moving world must not synchronously bake one every input frame.
        internal const float FrameSettleSeconds=.15f;
        internal bool PathsPending {get;private set;}
        RoomGroundQuery CurrentGround => PathsPending?candidate.Ground:accepted.Ground;
        static readonly Func<Collider,bool> IgnoreObstacle=_=>false;
        Func<Collider,bool> structuralObstacle;
        bool StructuralObstacle(Collider collision){
            int mask=world.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),actor);
            if((mask&(1<<collision.gameObject.layer))==0)return false;
            var editor=InputEditor;
            return RoomWalkingObstacle.Blocks(collision,actor?actor.transform:null,editor?editor.Identity(actor):null);
        }
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
        public bool Ready => configured && RefreshGeometry(out _);
        // A bound actor that was destroyed is not an intentionally actorless query.
        bool ActorAvailable=>!actorBound||actor&&actor.isActiveAndEnabled&&world&&actor.transform.IsChildOf(world.transform);
        bool ContextOwned=>isActiveAndEnabled&&world&&world.SimulationActive&&ActorAvailable;
        bool ContextAvailable=>ContextOwned&&world.EnvironmentReady(actor);
        public void Initialize(RoomPhysicsWorld value)
        {
            if(world==value)return;
            if(world){world.Changed-=RoomChanged;world.UnregisterNavigation(this);}
            ResetBinding();world=value;
            if(world&&isActiveAndEnabled){world.Changed+=RoomChanged;world.RegisterNavigation(this);}
        }
        void RoomChanged() { if (!ContextOwned) Clear(); }
        public bool Prepare(float bodyRadius, float bodyHeight, out string error,RoomItem target=null)
        {
            error = null;
            if(!isActiveAndEnabled){Clear();error="Room navigation is disabled; return to the active world";return false;}
            bool bound=!ReferenceEquals(target,null);
            if(!ReferenceEquals(actor,target)||actorBound!=bound){Clear();actor=target;actorBound=bound;}
            if(!ActorAvailable){Clear();error="The walking actor is unavailable in this world";return false;}
            if(!RoomEnvironmentBinding.RestorationReady(actor)){Clear();error=actor.GetComponent<RoomEnvironmentBinding>().RestorationReason;return false;}
            if(!ContextOwned||world.IncludesRealRoom(actor)&&!world.EnvironmentReady(actor)){Clear();error="Prepare the selected ground and Start physics before walking";return false;}
            if(!float.IsFinite(bodyRadius)||!float.IsFinite(bodyHeight)||bodyRadius<=0||bodyHeight<bodyRadius*2) { error="Choose a valid walking body";return false; }
            if(!configured||Mathf.Abs(radius-bodyRadius)>=.005f||Mathf.Abs(height-bodyHeight)>=.01f)Clear();
            radius=bodyRadius;height=bodyHeight;configured=true;
            if(RefreshGeometry(out error))return true;
            if(NavigationIssue!=null){error=NavigationIssue;return false;}
            return InitialInputsNeeded(out error);
        }
        bool RefreshGeometry(out string error)
        {
            NavigationIssue=null;error="Room navigation needs active physics and accepted surfaces";
            if(!ContextOwned){Clear();return false;}
            if(!RoomEnvironmentBinding.RestorationReady(actor)){ReleaseMap();candidate.Clear();error=actor.GetComponent<RoomEnvironmentBinding>().RestorationReason;return false;}
            if(!world.EnvironmentReady(actor)){ReleaseMap();candidate.Clear();return false;}
            bool frameMoved=false;
            if(virtualFrame) {
                var frame=world.transform.worldToLocalMatrix*virtualFrame.localToWorldMatrix;
                frameMoved=observedFrame&&!frame.Equals(lastFrame);
                if(frameMoved){frameChangedAt=Time.unscaledTime;frameChangedOn=Time.frameCount;}
                lastFrame=frame;observedFrame=true;
            }
            Physics.SyncTransforms();
            if(!candidate.Capture(world.transform,world.IncludesRealRoom(actor),!world.IncludesRealRoom(actor)&&virtualFrame?virtualFrame:world.transform,structuralObstacle??=StructuralObstacle)) { Clear();error="No accepted scanned or authored floor is available for walking";return false; }
            StartingGeometryAccepted();
            if(data&&map.Installed&&candidate.Same(accepted)){
                accepted.RefreshGroundCapture(candidate);
                if(!Install(candidate.Position,candidate.Rotation)){Clear();NavigationIssue=error="The accepted walking area could not be installed; retry after resources release";return false;}
                candidate.Clear();error=null;return true;
            }
            if(pendingBuild!=null){
                if(!candidate.Same(building))ReleaseMap();
                else if(!pendingBuild.Done){building.RefreshGroundCapture(candidate);error=null;return true;}
                else{
                    building.RefreshGroundCapture(candidate);
                    map=pendingBuild.Take();pendingBuild=null;building.Clear();
                    if(!Install(candidate.Position,candidate.Rotation)){Clear();NavigationIssue=error="The accepted walking area could not be installed; retry after resources release";return false;}
                    BuildRevision++;(accepted,candidate)=(candidate,accepted);candidate.Clear();PathsPending=false;
                    error=null;return true;
                }
            }
            // Old routes disappear before another step. Current accepted colliders
            // still support manual/exact traversal while asynchronous paths prepare.
            if(data)ReleaseMap();PathsPending=true;
            if(frameChangedOn>=0&&(Time.frameCount-frameChangedOn<2||Time.unscaledTime-frameChangedAt<FrameSettleSeconds)){error=null;return true;}
            try{
                string owner=resourceOwner+":"+world.GetInstanceID()+":"+(actor?actor.GetInstanceID():0)+":"+resourceGeneration;
                var admission=RoomNavigationBuild.TryStart(candidate,radius,height,owner,out pendingBuild,out var reason);
                if(admission==NavigationAdmission.Rejected){PathsPending=false;NavigationIssue=error=reason;return false;}
                if(admission==NavigationAdmission.WaitingForOperation)NavigationIssue=reason;
                if(admission==NavigationAdmission.Started){
                    (building,candidate)=(candidate,building);
                    candidate.Capture(world.transform,world.IncludesRealRoom(actor),!world.IncludesRealRoom(actor)&&virtualFrame?virtualFrame:world.transform,structuralObstacle??=StructuralObstacle);
                }
            }catch(Exception){Clear();NavigationIssue=error="The walking map could not be prepared; retry after changing the walking geometry";return false;}
            error=null;return true;
        }
        bool Install(Vector3 position,Quaternion rotation)
        {
            bool ready=map.TryInstall(position,rotation,out var changed);if(changed)SurfaceRevision++;return ready;
        }
        public bool Sample(Vector3 point, float maximumDistance, out Vector3 floor)
        {
            floor = default;
            if(!Ready||!Creation.RoomRecipe.Finite(point)||!float.IsFinite(maximumDistance)||maximumDistance<0||!SampleInputsReady(point,maximumDistance))return false;
            if(PathsPending) {
                if(!CurrentGround.Sample(point,maximumDistance,maximumDistance,out var current)||
                    !world.ContainsSimulation(current.point+Vector3.up*.1f,actor))return false;
                floor=current.point;return true;
            }
            return SampleInstalled(point,maximumDistance,out floor);
        }
        bool SampleInstalled(Vector3 point,float maximumDistance,out Vector3 floor){
            floor=default;
            if(!SampleInputsReady(point,maximumDistance))return false;
            if (!NavMesh.SamplePosition(point,out var hit,maximumDistance,Filter) || !world.ContainsSimulation(hit.position + Vector3.up*.1f,actor)) return false;
            // NavMesh is a route approximation. Foot placement uses the accepted
            // collider itself, including its current revision and rendered height.
            if(!accepted.Ground.Sample(hit.position,.08f,.08f,out var support)||Vector3.Distance(point,support.point)>maximumDistance+.001f)return false;
            floor=support.point;return true;
        }
        public bool DirectStep(Vector3 from,Vector3 to,out Vector3 floor)
        {
            floor=default;
            if(!Ready||!PassiveSegmentInputsReady(from,to,.1f,false))return false;
            if(PathsPending){
                RoomWalkingPreparation.Envelopes(from,to,radius,height,out var body,out var ground);
                return PassiveInputsReady(body,ground,false)&&groundMotor.Travel(CurrentGround,from,to-from,radius,height,0,IgnoreObstacle,
                    groundPosition??=GroundPosition,out floor,out _);
            }
            return Sample(from,.1f,out var start) && Sample(to,.08f,out floor) &&
                Vector3.Distance(new Vector3(to.x,floor.y,to.z),floor) < .025f && !NavMesh.Raycast(start,floor,out _,Filter);
        }
        // A proximity tolerance may skip a corner only when the resulting
        // segment remains on the map. Otherwise approach the actual corner first.
        internal int AdvanceCorner(Vector3 from,Vector3[] points,int next,float tolerance){
            while(next<points.Length&&Vector3.Distance(from,points[next])<tolerance){
                if(next+1<points.Length&&(from-points[next]).sqrMagnitude>1e-10f&&!DirectStep(from,points[next+1],out _))break;
                next++;
            }
            return next;
        }
        bool GroundPosition(Vector3 value)=>world&&world.ContainsSimulation(value+Vector3.up*.1f,actor);
        internal bool Traverse(Vector3 from,Vector3 to,Func<Collider,bool> obstacle,out Vector3 floor,out string error)
        {
            TraversalBlocker=null;WaterBlocker=null;floor=default;error="No connected accepted ground is available for this step";
            if(!Ready){error=NavigationIssue??error;return false;}
            if(!StepInputsReady(from,to,0,out var inputError)){error=inputError;return false;}
            if(!PathsPending&&!DirectStep(from,to,out _))return false;
            int mask=world.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),actor);
            bool moved=groundMotor.Travel(CurrentGround,from,to-from,radius,height,mask,obstacle,groundPosition??=GroundPosition,out floor,out error);
            if(!moved)TraversalBlocker=groundMotor.Blocker;
            else if(!WaterStep(from,floor,height)){error=WaterBlocker;return false;}
            return moved;
        }
        internal bool ClearAuthoredStep(Vector3 from,Vector3 to,float extraHeight,Func<Collider,bool> obstacle)
        {
            WaterBlocker=null;TraversalBlocker=null;
            if(!Ready||obstacle==null)return false;
            if(!StepInputsReady(from,to,extraHeight,out var inputError)){WaterBlocker=inputError;return false;}
            int mask=world.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),actor);
            bool clear=groundMotor.ClearExact(from,to,radius,height+extraHeight,mask,obstacle);TraversalBlocker=clear?null:groundMotor.Blocker;
            return clear&&WaterStep(from,to,height);
        }
        public bool Path(Vector3 from, Vector3 to, NavMeshPath path) {
            WaterBlocker=null;
            if(!Ready||PathsPending||!Sample(from,.25f,out var start)||!Sample(to,.5f,out var end)||
                !NavMesh.CalculatePath(start,end,Filter,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            var corners=path.corners;
            for(int i=1;i<corners.Length;i++)if(!PassiveSegmentInputsReady(corners[i-1],corners[i],.15f,actor&&actor.WaterTraversal.mode!="ignore")||!WaterStep(corners[i-1],corners[i],height))return false;
            return true;
        }
        // Rebuilding keeps only the fresh candidate. Full retirement also drops
        // candidate discovery, ground, obstacle and route references immediately.
        void ReleaseMap()
        {
            PathsPending=false;ReleaseFollowRoute();groundMotor.Clear();TraversalBlocker=null;WaterBlocker=null;
            if(map?.Installed==true)SurfaceRevision++;
            pendingBuild?.Dispose();pendingBuild=null;building.Clear();
            map?.Dispose();map=null;accepted.Clear();resourceGeneration++;NavigationIssue=null;
        }
        void Clear(){ReleaseMovementInputs();ReleaseMap();candidate.Clear();observedFrame=false;frameChangedOn=-1;}
        internal void ReleaseActor(RoomItem target){if(actorBound&&ReferenceEquals(actor,target))ResetBinding();}
        void ResetBinding(){Clear();configured=false;actor=null;actorBound=false;}
        void LateUpdate(){
            if(!(data||PathsPending))return;
            if(!ContextOwned)Clear();
            else if(!world.EnvironmentReady(actor)){ReleaseMap();candidate.Clear();}
            else if(PathsPending)RefreshGeometry(out _);
        }
        void OnEnable(){if(world){world.Changed+=RoomChanged;world.RegisterNavigation(this);}}
        void OnDisable(){if(world){world.Changed-=RoomChanged;world.UnregisterNavigation(this);}ResetBinding();}
        void OnDestroy(){if(world){world.Changed-=RoomChanged;world.UnregisterNavigation(this);}ResetBinding();world=null;virtualFrame=null;}
    }
}

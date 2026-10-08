// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Maestro.Quest.Interaction
{
    /// <summary>Virtual-only presentation over a movable content frame; physical tracking is never moved.</summary>
    public sealed partial class VirtualRoomView : MonoBehaviour
    {
        Transform content,trackingOrigin;
        RoomWorldMotion motion;
        Vector3 entryPosition;Quaternion entryRotation;
        internal event Action Moved;
        public string MovementError {get;private set;}
        Camera viewer;
        ScannedRoom scan;
        RoomPhysicsWorld physics;
        ARCameraManager passthrough;
        readonly RoomGroundQuery ground = new();
        readonly RoomGroundMotor groundMotor = new();
        Func<Collider,bool> groundObstacle;
        Func<Vector3,bool> groundPosition;
        readonly Collider[] overlaps = new Collider[32];
        public bool Active => BackdropOpacity == 1;
        public bool CanEnter=>isActiveAndEnabled&&motion!=null&&motion.Ready&&trackingOrigin&&viewer&&!viewer.transform.IsChildOf(content)&&(!scan||!scan.Busy);
        public void Initialize(Transform virtualContent,Transform physicalOrigin,Camera camera,ScannedRoom scanned,RoomPhysicsWorld world)
        { content=virtualContent;entryPosition=content.position;entryRotation=content.rotation;trackingOrigin=physicalOrigin;motion=new RoomWorldMotion(content,world);viewer=camera;scan=scanned;physics=world;
            passthrough=viewer.GetComponent<ARCameraManager>();world?.GetComponent<RoomNavigation>()?.SetVirtualFrame(content); }
        internal bool ResetWorkspaceFrame(out string error)=>motion.SetPose(entryPosition,entryRotation,out error);
        bool PhysicalView(out Vector3 foot,out float heading)
        {
            foot=default;heading=0;
            if(!viewer||!trackingOrigin||motion==null||!motion.Ready||viewer.transform.IsChildOf(content))return false;
            var head=viewer.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);
            foot=new Vector3(head.position.x,trackingOrigin.position.y,head.position.z);
            if(!RoomRecipe.Finite(foot)||!RoomRecipe.Finite(forward)||forward.sqrMagnitude<.0001f)return false;
            heading=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;return true;
        }
        internal bool ReadViewpoint(out RoomViewpoint value)
        {
            value=null;if(!PhysicalView(out var foot,out var heading))return false;
            var frame=new RoomFrame(content);
            value=new RoomViewpoint{active=true,position=frame.PointToRoom(foot),yaw=Mathf.DeltaAngle(0,heading-content.eulerAngles.y)};
            return value.Valid;
        }
        internal bool RestoreViewpoint(RoomViewpoint value,out string error)
        {
            error="A valid tracked view and saved location are needed to restore the world";
            if(value==null||!value.Valid||!value.active||!PhysicalView(out var foot,out var heading))return false;
            var rotation=Quaternion.AngleAxis(heading-value.yaw,Vector3.up);
            return motion.SetPose(foot-rotation*value.position,rotation,out error);
        }
        internal bool CanPlaceViewpoint(RoomViewpoint value,out string error)
        {
            error="Wait for an active tracked view and an idle room scan";
            if(!CanEnter||!PhysicalView(out var physicalFoot,out _)||value==null||!value.Valid||!value.active)return false;
            Physics.SyncTransforms();
            var destination=new RoomFrame(content).PointToWorld(value.position);
            float height=Mathf.Clamp(viewer.transform.position.y-physicalFoot.y,.65f,2.2f),radius=.2f;
            int mask=(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment);
            int count=Physics.OverlapCapsuleNonAlloc(destination+Vector3.up*(radius+.04f),destination+Vector3.up*(height-radius),radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            // Saturation is unknown clearance, never evidence of an empty destination.
            if(count==overlaps.Length){error="The destination is too crowded to verify clearance";return false;}
            for(int i=0;i<count;i++){
                var hit=overlaps[i];
                if(hit&&hit.transform.IsChildOf(content)&&hit.attachedRigidbody?.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame!=true){
                    error="The requested world location is occupied; choose a clear place";return false;
                }
            }
            ground.Capture(content);
            if(Active&&!ground.Support(destination,radius,.025f,.025f,out _,out _,out _)){error="Virtual placement needs accepted ground at the destination";return false;}
            error=null;return true;
        }
        internal bool PrepareViewpoint(RoomViewpoint value,out string error)
        {
            if(!CanPlaceViewpoint(value,out error)||!PhysicalView(out var foot,out var heading))return false;
            var rotation=Quaternion.AngleAxis(heading-value.yaw,Vector3.up);
            return motion.PreparePose(foot-rotation*value.position,rotation,out error);
        }
        internal void ApplyPreparedViewpoint()=>motion.ApplyPreparedPose();
        internal Vector3 WorldPosition=>content.position;
        internal Quaternion WorldRotation=>content.rotation;
        public bool Enter() => SetPresentation(1,false);
        public void Exit() => ResetPresentation();
        bool WalkingObstacle(Collider value)
        {
            if(!value||!value.transform.IsChildOf(content))return false;
            var body=value.attachedRigidbody;
            return !body||(body.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame!=true&&body.GetComponent<RoomItem>()?.Grab?.isSelected!=true);
        }
        bool WalkingPosition(Vector3 point)=>RoomViewpoint.ValidPosition(new RoomFrame(content).PointToRoom(point));
        public bool Move(Vector3 delta)
        {
            MovementError=null;
            if(!Active||!float.IsFinite(delta.sqrMagnitude)||delta.sqrMagnitude>.01f)return false;
            if(!PhysicalView(out var foot,out _)){MovementError="Movement needs a valid tracked physical view";return false;}
            if(!RoomViewpoint.ValidPosition(new RoomFrame(content).PointToRoom(foot))){MovementError="Movement needs a view within supported world coordinates";return false;}
            delta.y=0;if(delta.sqrMagnitude<.00000001f)return true;
            Physics.SyncTransforms();ground.Capture(content);
            float height=Mathf.Clamp(viewer.transform.position.y-foot.y,.65f,2.2f);
            int mask=(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment);
            if(!groundMotor.Travel(ground,foot,delta,.2f,height,mask,groundObstacle??=WalkingObstacle,groundPosition??=WalkingPosition,out var destination,out var error)){
                MovementError=error;return false;
            }
            bool moved=motion.SetPose(content.position-(destination-foot),content.rotation,out error);
            MovementError=error;if(moved)Moved?.Invoke();return moved;
        }
        public bool Turn(float degrees)
        {
            MovementError=null;if(!Active||(degrees!=-30&&degrees!=30))return false;
            if(!ReadViewpoint(out _)){MovementError="Turning needs a view within supported saved-world coordinates";return false;}
            var turn=Quaternion.AngleAxis(-degrees,Vector3.up);var pivot=viewer.transform.position;
            bool moved=motion.SetPose(pivot+turn*(content.position-pivot),turn*content.rotation,out var error);MovementError=error;if(moved)Moved?.Invoke();return moved;
        }
        void OnDisable() => Exit();
        void OnDestroy() => Exit();
    }
}

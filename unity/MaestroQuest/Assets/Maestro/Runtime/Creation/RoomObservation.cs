// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Capture one camera's actual projection for a dependency read. A stereo
    // view is the union of both eyes; object centres and Renderer.isVisible
    // cannot prove whether this room's observer can see owned geometry.
    internal sealed class RoomObservationView
    {
        readonly Plane[][] frusta;
        readonly int mask;
        readonly bool valid;
        internal RoomObservationView(Camera camera)
        {
            mask=camera.cullingMask;
            try {
                if(camera.stereoEnabled&&camera.stereoTargetEye!=StereoTargetEyeMask.None){
                    var eyes=new List<Plane[]>();
                    if((camera.stereoTargetEye&StereoTargetEyeMask.Left)!=0)eyes.Add(Planes(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left)*camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left)));
                    if((camera.stereoTargetEye&StereoTargetEyeMask.Right)!=0)eyes.Add(Planes(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right)*camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right)));
                    frusta=eyes.ToArray();
                }else frusta=new[]{Planes(camera.projectionMatrix*camera.worldToCameraMatrix)};
                valid=frusta.Length>0;
                foreach(var planes in frusta)foreach(var plane in planes)
                    valid&=RoomRecipe.Finite(plane.normal)&&float.IsFinite(plane.distance)&&plane.normal.sqrMagnitude>.5f;
            }catch(UnityException){valid=false;}
        }
        static Plane[] Planes(Matrix4x4 matrix)
        {
            for(int i=0;i<16;i++)if(!float.IsFinite(matrix[i]))throw new UnityException("Observation projection is unavailable");
            return GeometryUtility.CalculateFrustumPlanes(matrix);
        }
        internal bool Includes(Renderer renderer)
        {
            if((mask&(1<<renderer.gameObject.layer))==0)return false;
            var bounds=renderer.bounds;
            // Unknown geometry/projection cannot be used as evidence to retire.
            if(!valid||!RoomRecipe.Finite(bounds.center)||!RoomRecipe.Finite(bounds.extents))return true;
            foreach(var planes in frusta)if(GeometryUtility.TestPlanesAABB(planes,bounds))return true;
            return false;
        }
    }
    public sealed partial class RoomEditor
    {
        sealed class ObservationCamera:IDisposable
        {
            RoomEditor editor;
            internal readonly Camera Camera;
            internal ObservationCamera(RoomEditor owner,Camera camera){editor=owner;Camera=camera;}
            public void Dispose(){if(ReferenceEquals(editor,null))return;editor.observationCameras.Remove(this);editor=null;}
        }
        readonly HashSet<ObservationCamera> observationCameras=new();
        readonly List<Renderer> observationRenderers=new();
        // Manual capture cameras can be disabled components: Camera.Render is
        // synchronous, and the caller owns this lease through its render/readback.
        internal IDisposable ObserveNativeCamera(Camera camera)
        {
            if(!camera)throw new ArgumentNullException(nameof(camera));
            var lease=new ObservationCamera(this,camera);observationCameras.Add(lease);return lease;
        }
        internal void RetainObservedNative(RoomRetentionGraph graph)
        {
            if(!isActiveAndEnabled)return;
            var cameras=new HashSet<Camera>();
            var viewer=Viewer?Viewer.GetComponent<Camera>():null;
            if(viewer&&viewer.isActiveAndEnabled)cameras.Add(viewer);
            foreach(var lease in observationCameras)if(lease.Camera&&lease.Camera.gameObject.activeInHierarchy)cameras.Add(lease.Camera);
            if(cameras.Count==0)return;
            var views=new List<RoomObservationView>();foreach(var camera in cameras)views.Add(new RoomObservationView(camera));
            foreach(var pair in objects){
                var item=pair.Value;if(!item||!item.isActiveAndEnabled||!graph.Contains(pair.Key))continue;
                item.GetComponentsInChildren(false,observationRenderers);
                foreach(var renderer in observationRenderers){
                    if(!renderer||!renderer.enabled||renderer.forceRenderingOff)continue;
                    bool observed=false;foreach(var view in views)if(view.Includes(renderer)){observed=true;break;}
                    if(observed){graph.Retain(pair.Key,RoomRetentionReason.Observation);break;}
                }
            }
            observationRenderers.Clear();
        }
    }
}

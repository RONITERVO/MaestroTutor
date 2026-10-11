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
        internal RoomObservationView(Camera camera,int? sourceLayers=null)
        {
            mask=sourceLayers??camera.cullingMask;
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
            return Includes(renderer.bounds);
        }
        internal bool Includes(RoomSpatialBounds spatial)
        {
            if((mask&spatial.VisualLayers)==0)return false;
            if(!spatial.Visual.Known)return true;
            return spatial.Visual.HasBounds&&Includes(spatial.Visual.Bounds);
        }
        bool Includes(Bounds bounds)
        {
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
            internal readonly Camera Camera;internal readonly int? SourceLayers;
            internal ObservationCamera(RoomEditor owner,Camera camera,int? sourceLayers){editor=owner;Camera=camera;SourceLayers=sourceLayers;}
            public void Dispose(){if(ReferenceEquals(editor,null))return;editor.observationCameras.Remove(this);editor=null;}
        }
        readonly HashSet<ObservationCamera> observationCameras=new();
        readonly List<Renderer> observationRenderers=new();
        // Manual capture cameras can be disabled components: Camera.Render is
        // synchronous, and the caller owns this lease through its render/readback.
        internal IDisposable ObserveNativeCamera(Camera camera,int? sourceLayers=null)
        {
            if(!camera)throw new ArgumentNullException(nameof(camera));
            var lease=new ObservationCamera(this,camera,sourceLayers);observationCameras.Add(lease);return lease;
        }
        internal void RetainObservedNative(RoomRetentionGraph graph)
        {
            if(!isActiveAndEnabled)return;
            var views=new List<RoomObservationView>();
            var viewer=Viewer?Viewer.GetComponent<Camera>():null;
            if(viewer&&viewer.isActiveAndEnabled)views.Add(new RoomObservationView(viewer));
            foreach(var lease in observationCameras)if(lease.Camera&&lease.Camera.gameObject.activeInHierarchy)views.Add(new RoomObservationView(lease.Camera,lease.SourceLayers));
            if(views.Count==0)return;
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
            foreach(var pair in dormantNative){
                if(!graph.Contains(pair.Key))continue;
                var spatial=Frame.Valid?ReadDormantSpatial(pair.Key,pair.Value).Transform(transform.localToWorldMatrix):RoomSpatialBounds.Unknown;
                foreach(var view in views)if(view.Includes(spatial)){graph.Retain(pair.Key,RoomRetentionReason.Observation);break;}
            }
        }
    }
}

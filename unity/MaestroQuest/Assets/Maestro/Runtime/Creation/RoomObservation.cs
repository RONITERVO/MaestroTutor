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
        readonly Vector3 origin;readonly float translationMargin,angularChord;
        internal RoomObservationView(Camera camera,int? sourceLayers=null,float translation=0,float angle=0,bool uncertain=false)
        {
            mask=sourceLayers??camera.cullingMask;origin=camera.transform.position;
            translationMargin=translation;angularChord=2*Mathf.Sin(Mathf.Clamp(angle,0,180)*Mathf.Deg2Rad*.5f);
            try {
                if(camera.stereoEnabled&&camera.stereoTargetEye!=StereoTargetEyeMask.None){
                    var eyes=new List<Plane[]>();
                    if((camera.stereoTargetEye&StereoTargetEyeMask.Left)!=0)eyes.Add(Planes(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left)*camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left)));
                    if((camera.stereoTargetEye&StereoTargetEyeMask.Right)!=0)eyes.Add(Planes(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right)*camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right)));
                    frusta=eyes.ToArray();
                }else frusta=new[]{Planes(camera.projectionMatrix*camera.worldToCameraMatrix)};
                valid=!uncertain&&frusta.Length>0&&RoomRecipe.Finite(origin)&&float.IsFinite(translationMargin)&&translationMargin>=0&&float.IsFinite(angularChord);
                foreach(var planes in frusta)foreach(var plane in planes)
                    valid&=RoomRecipe.Finite(plane.normal)&&float.IsFinite(plane.distance)&&plane.normal.sqrMagnitude>.5f;
            }catch(UnityException){valid=false;}
        }
        static Plane[] Planes(Matrix4x4 matrix)
        {
            for(int i=0;i<16;i++)if(!float.IsFinite(matrix[i]))throw new UnityException("Observation projection is unavailable");
            return GeometryUtility.CalculateFrustumPlanes(matrix);
        }
        internal bool Reliable=>valid;
        internal bool Includes(Renderer renderer)
        {
            if((mask&(1<<renderer.gameObject.layer))==0)return false;
            if(renderer is SkinnedMeshRenderer){
                if(!renderer.TryGetComponent<Imports.ImportedSkinBounds>(out var skin))return true;
                var value=skin.Read(Matrix4x4.identity);
                return !value.Known||value.HasBounds&&Includes(value.Bounds);
            }
            return Includes(renderer.bounds);
        }
        internal bool Includes(RoomSpatialBounds spatial)
        {
            if((mask&spatial.VisualLayers)==0)return false;
            if(!spatial.Visual.Known)return true;
            return spatial.Visual.HasBounds&&Includes(spatial.Visual.Bounds);
        }
        // A branch encloses both leaf centres and leaf extents. Twice its
        // diagonal bounds the leaf-dependent prediction margin; using the
        // ordinary leaf margin here could incorrectly prune a swept view.
        internal bool MayInclude(RoomSpatialExtent envelope)=>!envelope.Known||envelope.HasBounds&&Includes(envelope.Bounds,true);
        bool Includes(Bounds bounds,bool branch=false)
        {
            // Unknown geometry/projection cannot be used as evidence to retire.
            if(!valid||!RoomRecipe.Finite(bounds.center)||!RoomRecipe.Finite(bounds.extents))return true;
            // Rotating a point by at most theta moves it by at most
            // 2*r*sin(theta/2). Expanding each actual eye's planes by this
            // distance plus translation contains the entire swept view, not
            // merely endpoint frusta. It also supports orthographic cameras.
            float margin=translationMargin+angularChord*(Vector3.Distance(origin,bounds.center)+(branch?2:1)*bounds.extents.magnitude);
            if(!float.IsFinite(margin))return true;
            foreach(var planes in frusta){
                bool inside=true;
                foreach(var plane in planes){
                    var normal=plane.normal;
                    float radius=Mathf.Abs(normal.x)*bounds.extents.x+Mathf.Abs(normal.y)*bounds.extents.y+Mathf.Abs(normal.z)*bounds.extents.z;
                    if(plane.GetDistanceToPoint(bounds.center)+radius < -margin){inside=false;break;}
                }
                if(inside)return true;
            }
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
            VisitObservedNative(views,id=>graph.Retain(id,RoomRetentionReason.Observation));
            foreach(var target in LightViewDependencies(views))graph.Retain(target,RoomRetentionReason.Lighting);
            RetainViewerDemand(graph);
        }
        void VisitObservedNative(IReadOnlyList<RoomObservationView> views,Action<string> retain,bool includePreparedEnvelope=false,bool includeDormant=true)
        {
            foreach(var pair in objects){
                var item=pair.Value;if(!item||!item.isActiveAndEnabled)continue;
                if(includePreparedEnvelope){
                    // Predictive retention uses the envelope that would be used
                    // to reload this root, including disabled/rest geometry.
                    // Exact rendered observation below keeps its existing meaning.
                    var spatial=RoomSpatialBounds.Unknown;
                    if(Frame.Read(item.transform,out var position,out var rotation,out var scale)){
                        spatial=RoomSpatialBounds.CaptureVisual(item,journal.ImportedEntity(pair.Key),observationRenderers)
                            .Transform(Matrix4x4.TRS(position,rotation,Vector3.one*scale)).Transform(transform.localToWorldMatrix);
                    }
                    foreach(var view in views)if(view.Includes(spatial)){retain(pair.Key);break;}
                    continue;
                }
                item.GetComponentsInChildren(false,observationRenderers);
                foreach(var renderer in observationRenderers){
                    if(!renderer||!renderer.enabled||renderer.forceRenderingOff)continue;
                    bool observed=false;foreach(var view in views)if(view.Includes(renderer)){observed=true;break;}
                    if(observed){retain(pair.Key);break;}
                }
            }
            observationRenderers.Clear();
            if(!includeDormant)return;
            foreach(var candidate in QueryDormantView(views)){
                var spatial=Frame.Valid?candidate.Spatial.Transform(transform.localToWorldMatrix):RoomSpatialBounds.Unknown;
                foreach(var view in views)if(view.Includes(spatial)){retain(candidate.Id);break;}
            }
        }
    }
}

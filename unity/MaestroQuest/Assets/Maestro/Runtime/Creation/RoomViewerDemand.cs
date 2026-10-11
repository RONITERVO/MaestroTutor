// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal const double ObservationGrace=2;
        readonly RoomObservationPrediction viewerPrediction=new();
        readonly Dictionary<string,double> viewerDemandUntil=new(System.StringComparer.Ordinal);
        Camera sampledViewer;RoomJournal observationJournal;double viewerSampleTime=double.NaN;
        System.Func<bool> viewerTracked;
        internal void ConfigureObservationTracking(System.Func<bool> tracked){viewerTracked=tracked;ResetViewerDemand();}
        bool ViewerTrackingReady=>!Ownership.Suspended&&(viewerTracked?.Invoke()??true);
        Camera ActiveViewerCamera(){var camera=Viewer?Viewer.GetComponent<Camera>():null;return camera&&camera.isActiveAndEnabled?camera:null;}
        void LateUpdate()=>TickViewerDemand(Time.unscaledTimeAsDouble);
        internal void TickViewerDemand(double now)
        {
            var camera=ActiveViewerCamera();
            if(!isActiveAndEnabled||journal==null||RuntimeGate.Held||!camera||!double.IsFinite(now)){ResetViewerDemand();return;}
            if(!ReferenceEquals(observationJournal,journal)||now<viewerSampleTime){ResetViewerDemand();observationJournal=journal;}
            viewerSampleTime=now;
            if(sampledViewer!=camera){viewerPrediction.Reset();sampledViewer=camera;}
            if(ViewerTrackingReady)viewerPrediction.Sample(camera.transform.position,camera.transform.rotation,now);else viewerPrediction.Reset();
            var view=PredictedViewerView(now);
            VisitObservedNative(new[]{view},id=>viewerDemandUntil[id]=now+ObservationGrace);
            foreach(var id in viewerDemandUntil.Keys.Where(id=>!HasSavedObject(id)||viewerDemandUntil[id]<=now).ToArray())viewerDemandUntil.Remove(id);
        }
        RoomObservationView PredictedViewerView(double now)
        {
            bool known=viewerPrediction.Read(now,out var distance,out var angle);
            return new RoomObservationView(sampledViewer,translation:distance,angle:angle,uncertain:!known||!ViewerTrackingReady);
        }
        void RetainViewerDemand(RoomRetentionGraph graph)
        {
            // One-off image captures use their exact scoped lease only; they do
            // not acquire a two-second display demand after the image is done.
            var camera=ActiveViewerCamera();if(!camera||RuntimeGate.Held)return;
            if(!sampledViewer||camera!=sampledViewer||!ReferenceEquals(observationJournal,journal)){
                // Startup, viewer replacement or a journal boundary can be read
                // before LateUpdate. No motion sample is evidence of no demand.
                VisitObservedNative(new[]{new RoomObservationView(camera,uncertain:true)},id=>graph.Retain(id,RoomRetentionReason.ApproachingView));return;
            }
            double now=Time.unscaledTimeAsDouble;
            VisitObservedNative(new[]{PredictedViewerView(now)},id=>graph.Retain(id,RoomRetentionReason.ApproachingView));
            foreach(var pair in viewerDemandUntil)if(pair.Value>now)graph.Retain(pair.Key,RoomRetentionReason.RecentObservation);
        }
        void ResetViewerDemand(){sampledViewer=null;observationJournal=null;viewerSampleTime=double.NaN;viewerPrediction.Reset();viewerDemandUntil.Clear();}
    }
}

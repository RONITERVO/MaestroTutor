// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        sealed class DisplayDemand
        {
            internal int Revision,Priority;internal double Until;
            internal readonly List<ObservationCamera> Captures=new();
        }
        readonly Dictionary<string,DisplayDemand> viewerDisplayDemand=new(StringComparer.Ordinal);
        readonly Dictionary<string,DisplayDemand> captureDisplayDemand=new(StringComparer.Ordinal);
        Camera displayViewer;
        bool DisplayViewerCurrent=>displayViewer&&displayViewer==ActiveViewerCamera()&&(viewerTracked?.Invoke()??true);
        void ResetNativeDisplayDemand(){displayViewer=null;viewerDisplayDemand.Clear();captureDisplayDemand.Clear();}
        void RefreshNativeDisplayDemand(double now)
        {
            // Retention deliberately treats missing metadata and uncertain tracking
            // as demand. Admission must not mistake that for a view of every area.
            if(dormantNative.Count==0||!DisplayReady||!Frame.Valid){ResetNativeDisplayDemand();return;}
            RefreshDormantDisplayDemand(now);
        }
        void RefreshDormantDisplayDemand(double now)
        {
            // Keep captured demand lambdas out of the idle gate: the compiler creates
            // their closure at method entry, even when an early return skips the work.
            var camera=(viewerTracked?.Invoke()??true)?ActiveViewerCamera():null;
            RoomObservationView direct=camera?new RoomObservationView(camera):null,predicted=null;
            if(direct?.Reliable!=true){camera=null;direct=null;}
            if(displayViewer!=camera){viewerDisplayDemand.Clear();displayViewer=camera;}
            foreach(var demand in viewerDisplayDemand.Values)demand.Priority=3;
            foreach(var demand in captureDisplayDemand.Values)demand.Captures.Clear();
            if(camera&&sampledViewer==camera&&ReferenceEquals(observationJournal,journal)&&viewerPrediction.Read(now,out var distance,out var angle))
                predicted=new RoomObservationView(camera,translation:distance,angle:angle);
            var captures=new List<(ObservationCamera owner,RoomObservationView view)>();
            foreach(var lease in observationCameras)if(lease.Camera&&lease.Camera.gameObject.activeInHierarchy){
                var view=new RoomObservationView(lease.Camera,lease.SourceLayers);if(view.Reliable)captures.Add((lease,view));
            }
            var directLights=direct!=null?NativeDisplayDependencies(direct):new HashSet<string>();
            var predictedLights=predicted!=null?NativeDisplayDependencies(predicted):new HashSet<string>();
            var captureLights=new Dictionary<ObservationCamera,HashSet<string>>();
            foreach(var capture in captures)captureLights[capture.owner]=NativeDisplayDependencies(capture.view);
            var views=new List<RoomObservationView>();if(direct!=null)views.Add(direct);if(predicted!=null)views.Add(predicted);
            foreach(var capture in captures)views.Add(capture.view);
            var required=new HashSet<string>(directLights,StringComparer.Ordinal);required.UnionWith(predictedLights);
            foreach(var lights in captureLights.Values)required.UnionWith(lights);
            foreach(var candidate in QueryDormantView(views,required)){
                if(dormantNative[candidate.Id].Phase==NativeEntityPhase.Failed)continue;
                var spatial=candidate.Spatial;bool prepared=candidate.Prepared,placed=candidate.Placed;
                // A missing exact scan plane cannot display this ink. Keep facts
                // unknown for retention, but do not acquire a useless hidden view.
                if(!prepared||!placed){viewerDisplayDemand.Remove(candidate.Id);continue;}
                spatial=spatial.Transform(transform.localToWorldMatrix);int revision=ObjectRevision(candidate.Id);
                int priority=(directLights.Contains(candidate.Id)||direct?.Includes(spatial)==true)?(spatial.Visual.Known?0:2):(predictedLights.Contains(candidate.Id)||predicted?.Includes(spatial)==true)?1:4;
                if(priority<4){
                    if(!viewerDisplayDemand.TryGetValue(candidate.Id,out var demand))viewerDisplayDemand[candidate.Id]=demand=new();
                    demand.Revision=revision;demand.Priority=priority;demand.Until=now+ObservationGrace;
                }
                foreach(var capture in captures)if(captureLights[capture.owner].Contains(candidate.Id)||capture.view.Includes(spatial)){
                    if(!captureDisplayDemand.TryGetValue(candidate.Id,out var demand))captureDisplayDemand[candidate.Id]=demand=new();
                    demand.Revision=revision;demand.Priority=spatial.Visual.Known?0:2;demand.Captures.Add(capture.owner);
                }
            }
            foreach(var id in viewerDisplayDemand.Keys.Where(id=>!NativeEntityDormant(id)||!HasSavedObject(id)||viewerDisplayDemand[id].Revision!=ObjectRevision(id)||viewerDisplayDemand[id].Until<=now).ToArray())viewerDisplayDemand.Remove(id);
            foreach(var id in captureDisplayDemand.Keys.Where(id=>captureDisplayDemand[id].Captures.Count==0).ToArray())captureDisplayDemand.Remove(id);
        }
        HashSet<string> NativeDisplayDependencies(RoomObservationView view)
        {
            // An accepted membership/connection edit can couple an already visible
            // resident to a dormant peer. The same authored closure used for action
            // acquisition must also reach that peer through ordinary viewing.
            var required=LightViewDependencies(new[]{view});
            VisitObservedNative(new[]{view},id=>required.Add(id),includeDormant:false);
            return journal.RetentionTopology.Closure(required).ToHashSet(StringComparer.Ordinal);
        }
        int NativeDisplayPriority(string id,double now)
        {
            int priority=4,revision=ObjectRevision(id);
            if(DisplayViewerCurrent&&viewerDisplayDemand.TryGetValue(id,out var demand)&&demand.Revision==revision&&demand.Until>now)priority=demand.Priority;
            if(captureDisplayDemand.TryGetValue(id,out demand)&&demand.Revision==revision&&demand.Captures.Any(lease=>observationCameras.Contains(lease)&&lease.Camera&&lease.Camera.gameObject.activeInHierarchy))priority=Math.Min(priority,demand.Priority);
            return priority;
        }
    }
}

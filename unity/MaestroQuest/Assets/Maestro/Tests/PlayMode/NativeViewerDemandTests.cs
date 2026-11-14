// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        void SampleViewerDemand(){double now=Time.unscaledTimeAsDouble;editor.TickViewerDemand(now-.02);editor.TickViewerDemand(now-.01);}
        [UnityTest] public IEnumerator ViewerDemandCannotRetireAnUnsampledOrReplacementViewerArea()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.position=new Vector3(0,0,-3);var camera=NativeObservationCamera();
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True);Assert.That(editor.RetireNativeArea(area,out _),Is.False);
            SampleViewerDemand();Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);
            var replacement=NativeObservationCamera();Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True);Assert.That(editor.RetireNativeArea(area,out _),Is.False);
            replacement.enabled=false;camera.enabled=false;yield return null;
        }
        [UnityTest] public IEnumerator ViewerDemandAnticipatesOutsideViewAndSharesReasonsWithoutChangingSavedState()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.position=new Vector3(2.5f,0,3);NativeObservationCamera();
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;SampleViewerDemand();
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True);
            Assert.That(editor.RetireNativeArea(area,out _),Is.False);
            for(int i=0;i<3;i++){
                Assert.That(RegionFacts.Retention().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=area},out var fact),Is.True);
                Assert.That(((JObject)fact.Value)["reasons"].Values<string>(),Does.Contain("approachingView"));
            }
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));yield return null;
        }
        [UnityTest] public IEnumerator ViewerDemandGracePreventsTurnAwayChurnThenReleasesWithoutReadsRefreshingIt()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);var camera=NativeObservationCamera();SampleViewerDemand();
            camera.transform.rotation=Quaternion.Euler(0,180,0);SampleViewerDemand();
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.RecentObservation),Is.True);
            Assert.That(editor.RetireNativeArea(area,out _),Is.False);
            double end=Time.unscaledTimeAsDouble+RoomEditor.ObservationGrace+.1;
            while(Time.unscaledTimeAsDouble<end){editor.ObserveRegionRetention(area);yield return null;}
            Assert.That(RetainedFor(id,RoomRetentionReason.RecentObservation),Is.False);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
        }
        [UnityTest] public IEnumerator ViewerDemandIncludesDormantGeometryWithoutCreatingNativeObjects()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.position=new Vector3(2.5f,0,3);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            NativeObservationCamera();SampleViewerDemand();Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True);Assert.That(editor.Find(id),Is.Null);
            Assert.That((bool)editor.ObserveRegionRetention(area)["unloadingSupported"],Is.False);
        }
        [UnityTest] public IEnumerator ViewerDemandIsFrameOwnedAndResetsAcrossEditorAndCameraLifecycle()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(2.5f,0,3);var camera=NativeObservationCamera();
            yield return null;yield return null;Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True,"No inspection is required to sample the viewer");
            camera.enabled=false;Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);yield return null;
            camera.enabled=true;camera.transform.rotation=Quaternion.Euler(0,180,0);SampleViewerDemand();
            editor.enabled=false;editor.enabled=true;Assert.That(RetainedFor(id,RoomRetentionReason.RecentObservation),Is.False);
            root.GetComponent<RoomInteraction>().Viewer=null;Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);
        }
        [UnityTest] public IEnumerator ViewerDemandKeepsCaptureLeasesScopedAndHonoursCurrentCullingLayers()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);var capture=NativeObservationCamera(false);
            using(editor.ObserveNativeCamera(capture)){yield return null;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);}
            Assert.That(RetainedFor(id,RoomRetentionReason.RecentObservation),Is.False);
            var camera=NativeObservationCamera();camera.cullingMask=0;SampleViewerDemand();Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.RecentObservation),Is.False);
        }
        [UnityTest] public IEnumerator ViewerDemandTreatsAnUntrackedFrozenPoseAsUnknownUntilFreshTrackingReturns()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,-3);NativeObservationCamera();
            bool tracked=true;editor.ConfigureObservationTracking(()=>tracked);SampleViewerDemand();Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);
            tracked=false;Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True,"Tracking loss is observed even between frame samples");SampleViewerDemand();
            tracked=true;Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True,"A frozen transform cannot serve as tracked motion history");SampleViewerDemand();Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator ViewerDemandTrackingJumpIsUnknownUntilFreshSamplesAndClockRollbackClearsGrace()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,-3);var camera=NativeObservationCamera();
            double now=Time.unscaledTimeAsDouble;editor.TickViewerDemand(now-.02);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True,"First sample is unknown");
            editor.TickViewerDemand(now-.01);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.False);
            camera.transform.position=Vector3.right*20;editor.TickViewerDemand(now);Assert.That(RetainedFor(id,RoomRetentionReason.ApproachingView),Is.True,"Relocation is not extrapolated motion");
            editor.TickViewerDemand(now-10);Assert.That(RetainedFor(id,RoomRetentionReason.RecentObservation),Is.False,"A backwards clock must not preserve future grace deadlines");yield return null;
        }
    }
}

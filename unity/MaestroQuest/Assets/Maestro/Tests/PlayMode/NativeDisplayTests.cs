// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        IEnumerator DisplayAppears(string id)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(!editor.Find(id)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.Find(id),Is.Not.Null,"Observed saved object did not reappear");
        }
        IEnumerator DisplayFrames(int count=4){for(int i=0;i<count;i++)yield return null;}
        string RetireDisplayBlock()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);return id;
        }
        string RetireDisplayGroup()
        {
            var(area,ids)=NativePreparationGroup();
            for(int i=0;i<ids.Length;i++)editor.Find(ids[i]).transform.position=new Vector3(0,0,3+i*.02f);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);return ids[0];
        }
        [UnityTest] public IEnumerator NativeDisplayReappearsWithoutLoadingHiddenIndependentAreaOrEditingTheWorld()
        {
            string id=RetireDisplayBlock();string other=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;
            string otherArea=NativeAreaFor(other);editor.Find(other).transform.position=new Vector3(0,0,-3);
            Assert.That(editor.RetireNativeArea(otherArea,out var error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;NativeObservationCamera();
            yield return DisplayAppears(id);yield return DisplayFrames();
            Assert.That(editor.Find(other),Is.Null);Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(new Vector3(0,0,3)));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator NativeDisplayTurningTowardAreaLoadsItWithoutAnExplicitAction()
        {
            string id=RetireDisplayBlock();yield return null;var camera=NativeObservationCamera();camera.transform.rotation=Quaternion.Euler(0,180,0);
            yield return DisplayFrames();Assert.That(editor.Find(id),Is.Null);camera.transform.rotation=Quaternion.identity;
            yield return DisplayAppears(id);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator NativeDisplayFocusLossCancelsAndReturnCanReacquire()
        {
            string id=RetireDisplayGroup();yield return null;NativeObservationCamera();editor.TickNativeDisplay();
            Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Preparing));editor.SendMessage("OnApplicationFocus",false);
            yield return DisplayFrames();Assert.That(editor.Find(id),Is.Null);editor.SendMessage("OnApplicationFocus",true);yield return DisplayAppears(id);
        }
        [UnityTest] public IEnumerator NativeDisplayCaptureNeedsOwnedLeaseAndDisposalCancelsItsPendingLoad()
        {
            string id=RetireDisplayGroup();yield return null;var camera=NativeObservationCamera(false);
            yield return DisplayFrames();Assert.That(editor.Find(id),Is.Null,"An unowned camera cannot request loading");
            var lease=editor.ObserveNativeCamera(camera);editor.TickNativeDisplay();Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Preparing));lease.Dispose();
            yield return DisplayFrames();Assert.That(editor.Find(id),Is.Null,"Disposed capture cannot publish late content");
            using(editor.ObserveNativeCamera(camera)){yield return DisplayAppears(id);}
        }
        [UnityTest] public IEnumerator NativeDisplayRequiresTrackedValidProjectionAndMatchingLayers()
        {
            string id=RetireDisplayBlock();yield return null;var camera=NativeObservationCamera();bool tracked=false;editor.ConfigureObservationTracking(()=>tracked);
            yield return DisplayFrames();Assert.That(editor.Find(id),Is.Null);tracked=true;camera.cullingMask=0;
            yield return DisplayFrames();Assert.That(editor.Find(id),Is.Null);camera.cullingMask=-1;camera.projectionMatrix=Matrix4x4.zero;
            editor.TickNativeDisplay();Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Dormant));camera.ResetProjectionMatrix();yield return DisplayAppears(id);
        }
        [UnityTest] public IEnumerator NativeDisplayPendingMetadataIsRetentionButNotLoadingEvidence()
        {
            string id=NewStroke(),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var changed=editor.Read(id);changed.radius*=2;changed.position=new Vector3(0,0,3);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);NativeObservationCamera();
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            editor.TickNativeDisplay();Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Dormant));
            yield return DisplayAppears(id);Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.True);
        }
        [UnityTest] public IEnumerator NativeDisplayCompletedUnknownGeometryIsNeverTreatedAsEmpty()
        {
            // The retained envelope must remain conservative even if a runtime
            // renderer cannot be reduced to a static authored envelope.
            block.gameObject.AddComponent<SkinnedMeshRenderer>();string id=RetireDisplayBlock();yield return null;
            Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.False);var camera=NativeObservationCamera();camera.transform.rotation=Quaternion.Euler(0,180,0);
            yield return DisplayAppears(id);
        }
        [UnityTest] public IEnumerator NativeDisplayDisabledRenderersDoNotEraseRetainedGeometry()
        {
            foreach(var renderer in block.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            string id=RetireDisplayBlock();yield return null;var bounds=editor.ReadSpatialBounds(id,out _);Assert.That(bounds.Visual.Known,Is.True);Assert.That(bounds.Visual.HasBounds,Is.True);
            NativeObservationCamera();yield return DisplayAppears(id);
        }
        [UnityTest] public IEnumerator NativeDisplayTimeoutDoesNotRetryEveryFrameWhileStillVisible()
        {
            string id=RetireDisplayGroup();yield return null;NativeObservationCamera();editor.TickNativeDisplay();
            editor.TickNativeDisplay(Time.unscaledTimeAsDouble+RoomEditor.NativeDisplayTimeout+1);yield return DisplayFrames(8);
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeDisplayPending,Is.False);Assert.That(editor.Status,Does.Contain("timed out"));
            var retry=editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None);yield return NativeAreaCompletion(retry);Assert.That(editor.Find(id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator NativeDisplayExpiredViewDemandCancelsPreparationAndCanReturn()
        {
            string id=RetireDisplayGroup();yield return null;var camera=NativeObservationCamera();editor.TickNativeDisplay();
            camera.transform.rotation=Quaternion.Euler(0,180,0);editor.TickNativeDisplay(Time.unscaledTimeAsDouble+RoomEditor.ObservationGrace+.1);
            yield return DisplayFrames();Assert.That(editor.Find(id),Is.Null);camera.transform.rotation=Quaternion.identity;yield return DisplayAppears(id);
        }
        [UnityTest] public IEnumerator NativeDisplayIdleRoomDoesNotAllocatePerFrameDemandWork()
        {
            NativeObservationCamera();for(int i=0;i<5;i++)editor.TickNativeDisplay();
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<512;i++)editor.TickNativeDisplay();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(allocated,Is.LessThan(32768),"A fully resident room must not build admission views or queries every frame");yield return null;
        }
        [UnityTest] public IEnumerator NativeDisplayRepeatedDormantObservationDoesNotCopyOrHashAuthoredGeometry()
        {
            string id=NewStroke(),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var changed=editor.Read(id);changed.radius*=2;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            for(int i=0;i<40&&!(bool)editor.ObserveAuthoredBounds(id)["prepared"];i++)yield return null;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.True);
            for(int i=0;i<5;i++)editor.ReadSpatialBounds(id,out _);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<512;i++)editor.ReadSpatialBounds(id,out _);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(allocated,Is.LessThan(32768),"A passive dormant read must not clone and serialize authored data on every frame");
            var bounds=editor.ReadSpatialBounds(id,out _);changed=editor.Read(id);changed.position+=Vector3.right*2;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Bounds.center.x,Is.EqualTo(bounds.Visual.Bounds.center.x+2).Within(.001f));Assert.That(editor.Find(id),Is.Null);
        }
    }
    public sealed partial class RoomEditorTests
    {
        [UnityTest] public IEnumerator NativeDisplayStartupPrioritizesPreparedVisibleAreaThenCompletesOtherAreas()
        {
            var saved=StartupMany();var creations=saved.objects.Where(x=>!x.IsBuiltIn).ToArray();
            foreach(var item in creations)item.position=new Vector3(0,0,-3);var visible=creations.Last();visible.position=new Vector3(0,0,3);
            saved.regions=creations.Select(x=>new RoomRegion{id=Guid.NewGuid().ToString("N"),name="Area",members=new[]{x.id}}).ToArray();
            yield return ReloadForStartup(saved,suspend:true);
            for(int i=0;i<40&&creations.Any(x=>!(bool)editor.ObserveAuthoredBounds(x.id)["prepared"]);i++)yield return null;
            Assert.That(creations.All(x=>(bool)editor.ObserveAuthoredBounds(x.id)["prepared"]),Is.True);
            var cameraObject=new GameObject("Startup viewer",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);var camera=cameraObject.GetComponent<Camera>();camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.fieldOfView=60;camera.aspect=1;camera.stereoTargetEye=StereoTargetEyeMask.None;
            root.GetComponent<RoomInteraction>().Viewer=camera.transform;editor.SendMessage("OnApplicationFocus",true);editor.TickNativeDisplay();
            Assert.That(editor.NativePhase(visible.id),Is.EqualTo(NativeEntityPhase.Preparing).Or.EqualTo(NativeEntityPhase.Resident));Assert.That(editor.NativePhase(creations.First().id),Is.EqualTo(NativeEntityPhase.Dormant));
            yield return StartupFinished();Assert.That(creations.All(x=>editor.Find(x.id)),Is.True);Assert.That(physics.Running,Is.False);
        }
    }
}

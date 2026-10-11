// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
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
        Camera NativeObservationCamera(bool viewer=true)
        {
            var go=new GameObject("Owned observation camera",typeof(Camera));go.transform.SetParent(root.transform,false);
            var camera=go.GetComponent<Camera>();camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.fieldOfView=60;camera.aspect=1;camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.enabled=viewer;if(viewer)root.GetComponent<RoomInteraction>().Viewer=camera.transform;return camera;
        }
        [UnityTest] public IEnumerator NativeObservationPreventsRetirementOfVisibleNativeGeometryAndReleasesWhenTurnedAway()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);var camera=NativeObservationCamera();
            var saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.False);Assert.That(editor.Find(id),Is.EqualTo(block));
            Assert.That(RegionFacts.Retention().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=area},out var fact),Is.True);
            Assert.That(((JObject)fact.Value)["reasons"].Values<string>(),Does.Contain("observation"));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            camera.transform.rotation=Quaternion.Euler(0,180,0);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            camera.transform.rotation=Quaternion.identity;
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True,"Retained dormant geometry contributes demand without loading");
            Assert.That(editor.Find(id),Is.Null,"A dependency read cannot instantiate dormant saved content");Assert.That(editor.Read(id),Is.Not.Null);
            Assert.That((bool)editor.ObserveRegionRetention(area)["unloadingSupported"],Is.False);
        }
        [UnityTest] public IEnumerator NativeObservationTestsBoundsInsteadOfOriginAndFollowsNativeTransformChanges()
        {
            string id=editor.Identity(block);NativeAreaFor(id);NativeObservationCamera();block.transform.position=new Vector3(10,0,3);
            var renderer=block.GetComponentsInChildren<Renderer>().First();renderer.transform.localScale=new Vector3(21,.13f,.13f);
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True,"A long object intersects the view although its centre is outside");
            renderer.transform.localScale=Vector3.one*.13f;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            block.transform.position=new Vector3(0,0,3);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator NativeObservationUsesOwnedCameraLayersAndVisibleRendererState()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);var camera=NativeObservationCamera();
            var renderer=block.GetComponentsInChildren<Renderer>().First();renderer.gameObject.layer=17;camera.cullingMask=1<<18;
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            camera.cullingMask=1<<17;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            renderer.enabled=false;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);renderer.enabled=true;
            renderer.forceRenderingOff=true;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);renderer.forceRenderingOff=false;
            camera.enabled=false;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);camera.enabled=true;
            renderer.gameObject.SetActive(false);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator NativeObservationSupportsOrthographicProjectionAndClipPlanes()
        {
            string id=editor.Identity(block);NativeAreaFor(id);var camera=NativeObservationCamera();camera.orthographic=true;camera.orthographicSize=1;
            block.transform.position=new Vector3(0,0,3);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            block.transform.position=new Vector3(2,0,3);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            block.transform.position=new Vector3(0,0,11);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            block.transform.position=new Vector3(0,0,-1);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator NativeObservationIgnoresUnownedCamerasAndIndependentCaptureLeasesReleaseSeparately()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);var camera=NativeObservationCamera(false);camera.enabled=true;
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False,"No global scene-camera search");camera.enabled=false;
            var first=editor.ObserveNativeCamera(camera);var second=editor.ObserveNativeCamera(camera);
            try {
                Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True,"Manual Camera.Render uses a disabled component");
                first.Dispose();first.Dispose();Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
                second.Dispose();Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            }finally{first.Dispose();second.Dispose();}
            yield return null;
        }
        [UnityTest] public IEnumerator NativeObservationCombinesOwnedViewsAndDropsDestroyedOrInactiveCaptureCameras()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);var main=NativeObservationCamera();main.transform.rotation=Quaternion.Euler(0,180,0);
            var capture=NativeObservationCamera(false);using var lease=editor.ObserveNativeCamera(capture);
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);capture.gameObject.SetActive(false);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            capture.gameObject.SetActive(true);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            UnityEngine.Object.Destroy(capture.gameObject);yield return null;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
        }
        [UnityTest] public IEnumerator NativeObservationPropagatesThroughAreaMembershipWithoutHoldingAnIndependentArea()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);
            var peers=editor.Snapshot().objects.Where(v=>!v.IsBuiltIn&&v.id!=id).Select(v=>v.id).ToArray();Assert.That(peers.Length,Is.GreaterThanOrEqualTo(2));
            Assert.That(AssignRegion(peers[0],area,out var error),Is.True,error);NativeAreaFor(peers[1]);
            block.transform.position=new Vector3(0,0,3);editor.Find(peers[0]).transform.position=new Vector3(0,0,-3);editor.Find(peers[1]).transform.position=new Vector3(0,0,-4);NativeObservationCamera();
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);Assert.That(RetainedFor(peers[0],RoomRetentionReason.Observation),Is.True);
            Assert.That(RetainedFor(peers[1],RoomRetentionReason.Observation),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator NativeObservationTracksWorldRelocationAndEditorLifecycle()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);var camera=NativeObservationCamera();
            root.transform.SetPositionAndRotation(new Vector3(20,4,-10),Quaternion.Euler(0,65,0));Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            editor.enabled=false;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);editor.enabled=true;
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            root.GetComponent<RoomInteraction>().Viewer=null;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator NativeObservationActualSnapshotHoldsItsOwnedViewOnlyThroughTheRender()
        {
            string id=editor.Identity(block);NativeAreaFor(id);block.transform.position=new Vector3(0,0,3);
            var viewer=new GameObject("Pose without a rendering camera");viewer.transform.SetParent(root.transform,false);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
            bool captured=false,retained=false;
            void BeforeCull(Camera camera){if(camera.name!="Virtual room snapshot")return;captured=true;retained=RetainedFor(id,RoomRetentionReason.Observation);}
            Camera.onPreCull+=BeforeCull;
            try {
                Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
                Assert.That(editor.CaptureCameraView(out _,out var error),Is.True,error);
                Assert.That(captured,Is.True,"The actual native capture camera must render");Assert.That(retained,Is.True);
                Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False,"Completed capture must release its observation lease");
            }finally{Camera.onPreCull-=BeforeCull;}
            yield return null;
        }
    }
}

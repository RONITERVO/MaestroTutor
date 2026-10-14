// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        IEnumerator AutomaticAreaUntil(Func<bool> done,string message){
            double end=Time.realtimeSinceStartupAsDouble+10;
            while(!done()&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.That(done(),Is.True,message);
        }
        (string id,string area,Camera camera) AutomaticAreaScene(){
            string id=editor.Identity(block),area=NativeAreaFor(id);
            block.transform.position=new Vector3(0,0,3);
            var camera=NativeObservationCamera();editor.ConfigureNativeRetirement();return(id,area,camera);
        }
        [UnityTest]public IEnumerator AutomaticAreaTurnAwayReleasesNativeResourcesAndViewRestoresExactSavedIdentity(){
            var(id,area,camera)=AutomaticAreaScene();string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.ObjectRevision(id);
            yield return null;yield return null;Assert.That(editor.Find(id),Is.SameAs(block));
            camera.transform.rotation=Quaternion.Euler(0,180,0);yield return new WaitForSecondsRealtime(.3f);
            Assert.That(editor.Find(id),Is.SameAs(block),"Recent viewing prevents immediate unload");
            yield return AutomaticAreaUntil(()=>!editor.Find(id),"Unobserved idle area did not retire");
            Assert.That(editor.Read(id),Is.Not.Null);Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            Assert.That(RegionFacts.Retention().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=area},out var value),Is.True);
            Assert.That((bool)((JObject)value.Value)["unloadingSupported"],Is.True);
            Assert.That((string)((JObject)value.Value)["retirementState"],Is.EqualTo("dormant"));
            camera.transform.rotation=Quaternion.identity;
            yield return AutomaticAreaUntil(()=>editor.Find(id),"Viewing the area did not restore its native object");
            Assert.That(editor.Find(id).transform.position,Is.EqualTo(new Vector3(0,0,3)));Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));Assert.That(physics.Running,Is.False);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator AutomaticAreaTrackingLossAndDisabledCameraNeverAuthorizeUnloading(){
            var(id,_,camera)=AutomaticAreaScene();camera.transform.rotation=Quaternion.Euler(0,180,0);bool tracked=false;editor.ConfigureObservationTracking(()=>tracked);
            yield return new WaitForSecondsRealtime(2.7f);Assert.That(editor.Find(id),Is.SameAs(block));
            tracked=true;camera.enabled=false;yield return new WaitForSecondsRealtime(2.7f);Assert.That(editor.Find(id),Is.SameAs(block));
            camera.enabled=true;yield return AutomaticAreaUntil(()=>!editor.Find(id),"Fresh reliable tracking did not allow retirement");
        }
        [UnityTest]public IEnumerator AutomaticAreaCaptureLeaseKeepsOffscreenContentUntilReadbackEnds(){
            var(id,_,camera)=AutomaticAreaScene();camera.transform.rotation=Quaternion.Euler(0,180,0);
            var capture=NativeObservationCamera(false);
            using(editor.ObserveNativeCamera(capture)){
                yield return new WaitForSecondsRealtime(2.7f);Assert.That(editor.Find(id),Is.SameAs(block));
            }
            yield return AutomaticAreaUntil(()=>!editor.Find(id),"Finished capture retained an unused area");
        }
        [UnityTest]public IEnumerator AutomaticAreaOwnershipAndRunningBodyRetainContentButRestartLeavesUnusedAreaCold(){
            var(id,_,camera)=AutomaticAreaScene();camera.transform.rotation=Quaternion.Euler(0,180,0);
            Assert.That(editor.Ownership.TryAcquire("area-test","Pose",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(id,"wholeTarget")},null,out var lease,out var error),Is.True,error);
            using(lease){yield return new WaitForSecondsRealtime(2.7f);Assert.That(editor.Find(id),Is.SameAs(block));}
            Assert.That(editor.SetItemPhysics(id,new(){mode="solid",shape="box",mass=1}),Is.True);
            physics.SetSurfaces(true,"Aligned fixture floor");Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var initialRun,out error),Is.True,error);yield return NativeActionDone(initialRun);Assert.That(physics.Running,Is.True);
            yield return new WaitForSecondsRealtime(1);Assert.That(editor.Find(id),Is.Not.Null);
            physics.PausePhysics();yield return AutomaticAreaUntil(()=>!editor.Find(id),"Idle area stayed resident after simulation paused");
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out error),Is.True,error);yield return NativeActionDone(run);
            Assert.That(physics.Running,Is.True);Assert.That(editor.Find(id),Is.Null);yield return new WaitForSecondsRealtime(.8f);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest]public IEnumerator AutomaticAreaKeepsOffscreenLightingAndCastersButRetiresUnrelatedArea(){
            var(lamp,caster,distant)=LightObservationScene();LightObservationCamera();editor.ConfigureNativeRetirement();
            yield return AutomaticAreaUntil(()=>!editor.Find(distant),"Unrelated distant area stayed resident");
            Assert.That(editor.Find(lamp),Is.Not.Null);Assert.That(editor.Find(caster),Is.Not.Null);
            Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.True);Assert.That(RetainedFor(caster,RoomRetentionReason.Lighting),Is.True);
        }
        [UnityTest]public IEnumerator AutomaticAreaLosingViewDuringPreparationPreservesEveryNativeObject(){
            var(id,area,audio)=CpuRetirementArea();editor.Find(id).transform.position=new Vector3(0,0,-3);
            var camera=NativeObservationCamera();var holder=HoldAcousticContext(audio);var original=editor.Find(id);string saved=JsonUtility.ToJson(editor.Snapshot());
            var otherClaims=editor.Snapshot().objects.Where(x=>!x.IsBuiltIn&&x.id!=id).Select(x=>new BehaviourCatalog.Claim(x.id,"wholeTarget")).ToArray();
            Assert.That(editor.Ownership.TryAcquire("cancel-isolation","Keep unrelated inputs",RoomActorRole.Control,otherClaims,null,out var unrelated,out var error),Is.True,error);
            using var keepUnrelated=unrelated;
            try{
                editor.ConfigureNativeRetirement();
                yield return AutomaticAreaUntil(()=>audio.RetainedGeometryCount>0&&(string)editor.ObserveRegionRetention(area)["retirementState"]=="preparing","Retirement never prepared owned geometry");
                camera.enabled=false;
                yield return AutomaticAreaUntil(()=>audio.RetainedGeometryCount==0,"Cancelled retirement retained its geometry reservation");
                Assert.That(editor.Find(id),Is.SameAs(original));Assert.That(original.isActiveAndEnabled,Is.True);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            }finally{editor.ConfigureNativeRetirement(false);UnityEngine.Object.Destroy(holder);}
        }
        [UnityTest]public IEnumerator AutomaticAreaImmediateReenableWaitsForCancelledPreparationToDrain(){
            var(id,area,audio)=CpuRetirementArea();editor.Find(id).transform.position=new Vector3(0,0,-3);NativeObservationCamera();
            var holder=HoldAcousticContext(audio);
            var claims=editor.Snapshot().objects.Where(x=>!x.IsBuiltIn&&x.id!=id).Select(x=>new BehaviourCatalog.Claim(x.id,"wholeTarget")).ToArray();
            Assert.That(editor.Ownership.TryAcquire("restart-isolation","Keep unrelated inputs",RoomActorRole.Control,claims,null,out var unrelated,out var error),Is.True,error);
            using var keepUnrelated=unrelated;
            try{
                editor.ConfigureNativeRetirement();
                yield return AutomaticAreaUntil(()=>audio.RetainedGeometryCount>0&&(string)editor.ObserveRegionRetention(area)["retirementState"]=="preparing","Retirement never prepared owned geometry");
                editor.ConfigureNativeRetirement(false);editor.ConfigureNativeRetirement();
                double end=Time.realtimeSinceStartupAsDouble+10;
                while(editor.Find(id)&&Time.realtimeSinceStartupAsDouble<end){
                    Assert.That(audio.RetainedGeometryCount,Is.LessThanOrEqualTo(16),"A cancelled and a new retirement must not own duplicate geometry reservations");
                    Assert.That((string)editor.ObserveRegionRetention(area)["retirementState"],Is.Not.EqualTo("failed"));yield return null;
                }
                Assert.That(editor.Find(id),Is.Null);Assert.That(audio.RetainedGeometryCount,Is.EqualTo(16));
            }finally{editor.ConfigureNativeRetirement(false);UnityEngine.Object.Destroy(holder);}
        }
        [UnityTest]public IEnumerator AutomaticAreaFailureIsObservableDoesNotSpinAndChangedInputCanRetry(){
            var(id,area,audio)=CpuRetirementArea();editor.Find(id).transform.position=new Vector3(0,0,-3);NativeObservationCamera();
            var holder=HoldAcousticContext(audio);var original=editor.Find(id);var surface=original.GetComponentsInChildren<AcousticSurface>().Last();var valid=surface.Mesh;var invalid=new Mesh();
            try{
                surface.Bind(invalid);editor.ConfigureNativeRetirement();
                yield return AutomaticAreaUntil(()=>(string)editor.ObserveRegionRetention(area)["retirementState"]=="failed","Invalid native input did not report retirement failure");
                Assert.That(editor.Find(id),Is.SameAs(original));Assert.That((string)editor.ObserveRegionRetention(area)["retirementIssue"],Is.Not.Empty);
                var failureFact=RegionFacts.Retention();
                Assert.That(failureFact.TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=area},out var failureValue),Is.True,"Failure must remain readable through the shared catalog");
                var boundedReport=(JObject)failureValue.Value;
                boundedReport["retirementIssue"]=RoomEditor.NativeRetirementIssueText("\n"+new string('"',256));
                Assert.That(((string)boundedReport["retirementIssue"]).Length,Is.EqualTo(128));
                Assert.That(((string)boundedReport["retirementIssue"]).Any(char.IsControl),Is.False);
                Assert.DoesNotThrow(()=>ProgramValue.Literal(boundedReport,failureFact.Type),"Escaped and long errors must remain valid program facts");
                surface.Bind(valid);yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(editor.Find(id),Is.SameAs(original),"An unchanged failed area must not retry every frame");
                var changed=editor.Read(id);changed.name="Repaired area geometry";changed.position=new Vector3(0,0,-3);
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out var error),Is.True,error);
                yield return AutomaticAreaUntil(()=>!editor.Find(id),"Changed input did not allow a fresh retirement attempt");
                Assert.That(editor.Read(id).name,Is.EqualTo(changed.name));
            }finally{editor.ConfigureNativeRetirement(false);if(surface)surface.Bind(valid);UnityEngine.Object.Destroy(invalid);UnityEngine.Object.Destroy(holder);}
        }
    }
}

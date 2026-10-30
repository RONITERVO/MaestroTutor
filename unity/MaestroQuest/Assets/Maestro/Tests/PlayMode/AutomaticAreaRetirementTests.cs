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
        (string near,string far,Camera camera) AutomaticHomeScene(){
            string near=editor.Identity(block);
            foreach(var value in editor.Snapshot().objects.Where(x=>!x.IsBuiltIn&&x.id!=near))NativeAreaFor(value.id);
            block.transform.position=new Vector3(0,0,3);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Distant home object",new Vector3(0,0,-4),1,Color.blue,out var far,out var error),Is.True,error);
            var camera=NativeObservationCamera();return(near,far,camera);
        }
        [UnityTest]public IEnumerator AutomaticNamedIndependentAreaRetiresOneMemberAndTogetherEditRestoresItsGroup(){
            var(near,far,camera)=AutomaticHomeScene();string area=NewRegion();
            Assert.That(AssignRegion(near,area,out var error),Is.True,error);Assert.That(AssignRegion(far,area,out error),Is.True,error);
            var value=editor.ReadRegion(area);value.loading="independent";Assert.That(editor.EditRegion(value,area,editor.RegionRevision(area),value.members,out error),Is.True,error);
            string saved=JsonUtility.ToJson(editor.Snapshot());editor.ConfigureNativeRetirement();
            yield return AutomaticAreaUntil(()=>!editor.Find(far),"Independent named area kept an unobserved peer resident");
            Assert.That(editor.Find(near),Is.SameAs(block));var state=editor.ObserveRegionRetention(area);
            Assert.That((int)state["memberCount"],Is.EqualTo(2));Assert.That((int)state["residentCount"],Is.EqualTo(1));Assert.That((int)state["retainedCount"],Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            value=editor.ReadRegion(area);value.loading="together";Assert.That(editor.EditRegion(value,area,editor.RegionRevision(area),value.members,out error),Is.True,error);
            yield return AutomaticAreaUntil(()=>editor.Find(far),"A newly coupled dormant peer was not restored for its observed group");
            camera.transform.rotation=Quaternion.Euler(0,90,0);
            yield return AutomaticAreaUntil(()=>!editor.Find(near)&&!editor.Find(far),"Together area did not retire atomically");
            camera.transform.rotation=Quaternion.identity;
            yield return AutomaticAreaUntil(()=>editor.Find(near)&&editor.Find(far),"Viewing one together member did not restore both");
        }
        [UnityTest]public IEnumerator AutomaticIndependentAreasStillLoadPhysicalPeersAcrossTheirBoundary(){
            var(near,far,camera)=AutomaticHomeScene();string a=NewRegion(),b=NewRegion();
            Assert.That(AssignRegion(near,a,out var error),Is.True,error);Assert.That(AssignRegion(far,b,out error),Is.True,error);
            foreach(var id in new[]{a,b}){var area=editor.ReadRegion(id);area.loading="independent";Assert.That(editor.EditRegion(area,id,editor.RegionRevision(id),area.members,out error),Is.True,error);}
            Assert.That(editor.SetItemPhysics(near,new(){mode="fixed",shape="box",mass=1}),Is.True);
            Assert.That(editor.SetItemPhysics(far,new(){mode="solid",shape="box",mass=1}),Is.True);
            Assert.That(Connect(AttachCall(far,near),out error),Is.True,error);yield return SpatialSettingsCompletion.Wait(connectionExecutions);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Unrelated garden piece",new Vector3(4,0,-4),1,Color.red,out var loose,out error),Is.True,error);
            Assert.That(AssignRegion(loose,a,out error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());editor.ConfigureNativeRetirement();
            yield return AutomaticAreaUntil(()=>!editor.Find(loose),"Independent member was pinned by an unrelated physical assembly");
            Assert.That(editor.Find(near),Is.Not.Null);Assert.That(editor.Find(far),Is.Not.Null);
            camera.transform.rotation=Quaternion.Euler(0,90,0);
            yield return AutomaticAreaUntil(()=>!editor.Find(near)&&!editor.Find(far),"Cross-area assembly did not retire together");
            camera.transform.rotation=Quaternion.identity;
            yield return AutomaticAreaUntil(()=>editor.Find(near)&&editor.Find(far),"Cross-area physical peer did not restore with its observed endpoint");
            Assert.That(editor.Find(far).GetComponent<RoomConnectionView>(),Is.Not.Null);
            yield return AutomaticAreaUntil(()=>!editor.Find(loose),"Recent-view grace expired but an independent member stayed pinned to the physical assembly");
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator AutomaticHomeIndependentViewsRetireAndRestoreOnlyObservedObjects(){
            var(near,far,camera)=AutomaticHomeScene();string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            editor.ConfigureNativeRetirement();
            yield return AutomaticAreaUntil(()=>!editor.Find(far),"Unrelated home object remained loaded beside a visible object");
            Assert.That(editor.Find(near),Is.SameAs(block));Assert.That(editor.RegionFor(near),Is.Empty);Assert.That(editor.RegionFor(far),Is.Empty);
            var state=editor.ObserveRegionRetention("");Assert.That((int)state["memberCount"],Is.EqualTo(2));Assert.That((int)state["residentCount"],Is.EqualTo(1));Assert.That((int)state["retainedCount"],Is.EqualTo(1));
            camera.transform.rotation=Quaternion.Euler(0,180,0);
            yield return AutomaticAreaUntil(()=>editor.Find(far)&&!editor.Find(near),"Turning toward a dormant home object did not load it independently");
            Assert.That(editor.Find(far).transform.position,Is.EqualTo(new Vector3(0,0,-4)));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator AutomaticHomePhysicalConnectionKeepsBothEndsButNotUnrelatedContent(){
            var(near,far,camera)=AutomaticHomeScene();
            Assert.That(editor.SetItemPhysics(near,new(){mode="fixed",shape="box",mass=1}),Is.True);
            Assert.That(editor.SetItemPhysics(far,new(){mode="solid",shape="box",mass=1}),Is.True);
            Assert.That(Connect(AttachCall(far,near),out var error),Is.True,error);yield return SpatialSettingsCompletion.Wait(connectionExecutions);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Independent piece",new Vector3(4,0,-4),1,Color.red,out var loose,out error),Is.True,error);
            string saved=JsonUtility.ToJson(editor.Snapshot());editor.ConfigureNativeRetirement();
            yield return AutomaticAreaUntil(()=>!editor.Find(loose),"An unrelated home piece was pinned by a connection");
            Assert.That(editor.Find(near),Is.Not.Null);Assert.That(editor.Find(far),Is.Not.Null);
            camera.transform.rotation=Quaternion.Euler(0,90,0);
            double until=Time.realtimeSinceStartupAsDouble+10;while((editor.Find(near)||editor.Find(far))&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.That(!editor.Find(near)&&!editor.Find(far),Is.True,"Connected home demand: "+editor.ObserveRegionRetention("")+"; near="+editor.ReadRetention().Reasons(near)+"; far="+editor.ReadRetention().Reasons(far));
            camera.transform.rotation=Quaternion.identity;
            yield return AutomaticAreaUntil(()=>editor.Find(near)&&editor.Find(far),"Observing one endpoint did not restore both ends");
            Assert.That(editor.Find(far).GetComponent<RoomConnectionView>(),Is.Not.Null);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator AutomaticHomeActiveOwnerRetainsOnlyItsGroupUntilReleased(){
            var(near,far,_)=AutomaticHomeScene();
            Assert.That(editor.Ownership.TryAcquire("home-pin","Keep distant piece",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(far,"wholeTarget")},null,out var lease,out var error),Is.True,error);
            editor.ConfigureNativeRetirement();
            using(lease){yield return new WaitForSecondsRealtime(2.7f);Assert.That(editor.Find(far),Is.Not.Null);}
            yield return AutomaticAreaUntil(()=>!editor.Find(far),"Released home ownership kept unrelated content loaded");
            Assert.That(editor.Find(near),Is.Not.Null);
        }
        [UnityTest]public IEnumerator AutomaticHomeFailureDoesNotPreventAnotherGroupFromRetiring(){
            var(near,far,_)=AutomaticHomeScene();var(id,_,audio)=CpuRetirementArea();Assert.That(AssignRegion(id,"",out var error),Is.True,error);
            editor.Find(id).transform.position=new Vector3(0,0,-3);var holder=HoldAcousticContext(audio);var original=editor.Find(id);
            var surface=original.GetComponentsInChildren<AcousticSurface>().Last();var valid=surface.Mesh;var invalid=new Mesh();
            try{
                surface.Bind(invalid);editor.ConfigureNativeRetirement();
                yield return AutomaticAreaUntil(()=>!editor.Find(far)&&(string)editor.ObserveRegionRetention("")["retirementState"]=="failed","One failed home group blocked another group or hid its failure");
                Assert.That(editor.Find(near),Is.Not.Null);Assert.That(editor.Find(id),Is.SameAs(original));
                Assert.That((string)editor.ObserveRegionRetention("")["retirementIssue"],Is.Not.Empty);
                surface.Bind(valid);yield return new WaitForSecondsRealtime(1.2f);Assert.That(editor.Find(id),Is.SameAs(original));
                var changed=editor.Read(id);changed.name="Repaired home geometry";changed.position=new Vector3(0,0,-3);
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
                yield return AutomaticAreaUntil(()=>!editor.Find(id),"Repaired home group did not retry after its input changed");
                Assert.That((string)editor.ObserveRegionRetention("")["retirementIssue"],Is.Empty);Assert.That(editor.Find(near),Is.Not.Null);
            }finally{editor.ConfigureNativeRetirement(false);if(surface)surface.Bind(valid);UnityEngine.Object.Destroy(invalid);UnityEngine.Object.Destroy(holder);}
        }
        [UnityTest]public IEnumerator AutomaticHomeExplicitAreaPreparationRejectsNewMembers(){
            var(id,_,audio)=CpuRetirementArea();foreach(var value in editor.Snapshot().objects.Where(x=>!x.IsBuiltIn&&x.id!=id))NativeAreaFor(value.id);
            Assert.That(AssignRegion(id,"",out var error),Is.True,error);var original=editor.Find(id);var holder=HoldAcousticContext(audio);string peer=editor.Identity(block);
            try{
                var task=editor.RetireNativeArea("");yield return CpuReservationStarted(task,audio);
                Assert.That(AssignRegion(peer,"",out error),Is.True,error);
                yield return NativeAreaCompletion(task,false);Assert.That(editor.Find(id),Is.SameAs(original));Assert.That(editor.Find(peer),Is.SameAs(block));Assert.That(audio.RetainedGeometryCount,Is.Zero);
                yield return NativeAreaCompletion(editor.RetireNativeArea(""));Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Find(peer),Is.Null);
            }finally{UnityEngine.Object.Destroy(holder);}
        }
        [UnityTest]public IEnumerator AutomaticHomeTargetPreparationRejectsChangedDependencyGroup(){
            var(id,_,audio)=CpuRetirementArea();Assert.That(AssignRegion(id,"",out var error),Is.True,error);string peer=editor.Identity(block),other=NativeAreaFor(peer);var original=editor.Find(id);var holder=HoldAcousticContext(audio);
            try{
                var task=editor.RetireNativeTargets(new[]{id});yield return CpuReservationStarted(task,audio);
                Assert.That(AssignRegion(id,other,out error),Is.True,error);
                yield return NativeAreaCompletion(task,false);Assert.That(editor.Find(id),Is.SameAs(original));Assert.That(block.isActiveAndEnabled,Is.True);Assert.That(audio.RetainedGeometryCount,Is.Zero);
                yield return NativeAreaCompletion(editor.RetireNativeTargets(new[]{id}),false);
                yield return NativeAreaCompletion(editor.RetireNativeArea(other));Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Find(peer),Is.Null);
            }finally{UnityEngine.Object.Destroy(holder);}
        }
    }
}

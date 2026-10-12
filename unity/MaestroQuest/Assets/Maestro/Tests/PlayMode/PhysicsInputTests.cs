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
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string PhysicsInputBall(Vector3 position){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Physics input ball",position,.4f,Color.red,out var id,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="solid",shape="sphere",mass=1}),Is.True,editor.Status);
            return id;
        }
        JObject PhysicsInputFact(string id){
            Assert.That(BehaviourCatalog.TryRead("object.physics.inputs",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var result),Is.True);
            Assert.That(result.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(result.Value);
        }
        [UnityTest]public IEnumerator PhysicsInputsMissingUpperGroundFreezesBeforeGravityAndDoesNotBorrowScan(){
            string ground=AvailabilityTerrain(),ball=PhysicsInputBall(new(4,1,4));
            var item=editor.Find(ball);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            physics.SetSurfaces(true,"Lower scan aligned");physics.StartPhysics();Assert.That(body.isKinematic,Is.False);
            Assert.That(rigid.Launch(Vector3.right*2,Vector3.up),Is.True);
            int settled=0;rigid.Settled+=_=>settled++;editor.Find(ground).gameObject.SetActive(false);var position=body.position;
            Assert.That(rigid.TryReadMotion(out var available,out _,out _),Is.True);Assert.That(available,Is.False);
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("unavailable"));
            yield return new WaitForFixedUpdate();Assert.That(body.isKinematic,Is.True);Assert.That(body.useGravity,Is.False);
            Assert.That(Vector3.Distance(position,body.position),Is.LessThan(.001f));Assert.That(physics.Running,Is.True,"Local refusal does not pause unrelated actors");
            yield return new WaitForSeconds(.7f);Assert.That(settled,Is.Zero,"Unavailable input is not a settled event");
            editor.Find(ground).gameObject.SetActive(true);yield return new WaitForFixedUpdate();Assert.That(body.isKinematic,Is.True,"Failed input needs a fresh owner");
            physics.PausePhysics();physics.StartPhysics();Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));
        }
        [UnityTest]public IEnumerator PhysicsInputsOffsetMissingCavityPreventsGravityAndPartialBuoyancy(){
            var(pool,prop)=MediumFixture(.05f);physics.PausePhysics();
            var container=editor.Read(pool).containers[0];container.frame.position=new(-8,.2f,0);
            ContainerRun(new RoomAgentExecutor(editor),ContainerCall(pool,container));
            editor.Find(pool).transform.position+=Vector3.right*8;editor.Find(pool).GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
            physics.StartPhysics();var item=editor.Find(prop);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            Assert.That(body.isKinematic,Is.False);editor.Find(pool).gameObject.SetActive(false);var before=body.position;
            editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);
            Assert.That(rigid.TryReadMotion(out var available,out _,out _),Is.True);Assert.That(available,Is.False);
            yield return new WaitForFixedUpdate();Assert.That(body.isKinematic,Is.True);Assert.That(Vector3.Distance(before,body.position),Is.LessThan(.001f));
            Assert.That((string)PhysicsInputFact(prop)["state"],Is.EqualTo("unavailable"));
        }
        [UnityTest]public IEnumerator PhysicsInputsKnownDistantMissingObstacleOnlyBlocksAfterMovingIntoReach(){
            string ball=PhysicsInputBall(new(0,2,0));
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Distant obstacle",new(15,1,0),1,Color.white,out var far,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(far,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);NativeAreaFor(far);
            editor.Find(far).gameObject.SetActive(false);physics.SetSurfaces(true,"Aligned scan");physics.StartPhysics();
            var body=editor.Find(ball).GetComponent<Rigidbody>();Assert.That(body.isKinematic,Is.False);float height=body.position.y;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Assert.That(body.position.y,Is.LessThan(height));
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("ready"));
            Assert.That(PhysicsInputFact(ball)["dependencies"].Values<string>(),Does.Not.Contain(far));
            editor.Find(far).transform.position=body.position+Vector3.right*.3f;
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("unavailable"));
            yield return new WaitForFixedUpdate();Assert.That(body.isKinematic,Is.True,"A newly relevant missing obstacle invalidates the next step");
        }
        [UnityTest]public IEnumerator PhysicsInputsSleepingBodyRetainsSupportAndObservationNeverStartsOrSaves(){
            string ground=AvailabilityTerrain(),ball=PhysicsInputBall(new(4,.3f,4));NativeAreaFor(ground);NativeAreaFor(ball);
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("inactive"));Assert.That(physics.Running,Is.False);Assert.That(editor.NativeActivationPending,Is.False);
            physics.SetSurfaces(true,"Aligned scan");physics.StartPhysics();editor.Find(ball).GetComponent<Rigidbody>().Sleep();
            Assert.That(editor.ReadRetention().Reasons(ground)&RoomRetentionReason.Ownership,Is.Not.EqualTo(RoomRetentionReason.None));
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("ready"));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            physics.PausePhysics();Assert.That(editor.ReadRetention().Reasons(ground)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));yield return null;
        }
        [UnityTest]public IEnumerator PhysicsInputsPauseAnimationTeleportAndDisableReleaseTheirOwnDemand(){
            string ground=AvailabilityTerrain(),ball=PhysicsInputBall(new(4,1,4));NativeAreaFor(ground);NativeAreaFor(ball);
            var rigid=editor.Find(ball).GetComponent<RigidRoomItem>();var body=editor.Find(ball).GetComponent<Rigidbody>();
            physics.SetSurfaces(true,"Aligned scan");physics.StartPhysics();
            using var other=editor.DemandNativeEntities(new[]{ground},()=>new[]{ground},()=>true);
            Assert.That(rigid.Launch(Vector3.right*3,Vector3.up*2),Is.True);var owner=new object();rigid.SetAnimationOwner(owner,true);
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("inactive"));other.Dispose();
            Assert.That(editor.ReadRetention().Reasons(ground)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            rigid.SetAnimationOwner(owner,false);Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("ready"));
            Assert.That(rigid.Launch(Vector3.right*3,Vector3.zero),Is.True);rigid.Teleported();Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));
            physics.PausePhysics();Assert.That(editor.ReadRetention().Reasons(ground)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            physics.StartPhysics();rigid.enabled=false;physics.PausePhysics();physics.StartPhysics();Assert.That(body.isKinematic,Is.True);Assert.That(editor.ReadRetention().Reasons(ground)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));yield return null;
        }
        (string ground,string ball,RigidRoomItem rigid,Rigidbody body) PendingPhysicsInputFixture(){
            string ground=AvailabilityTerrain(),ball=PhysicsInputBall(new(4,1,4));NativeAreaFor(ball);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);
            // Isolate the per-body preparation contract. Production still keeps
            // whole-world admission; this fixture is not streaming acceptance.
            physics.ConfigureCollisionAdmission(editor,()=>null);
            physics.SetSurfaces(true,"Isolated local admission fixture");physics.StartPhysics();
            var rigid=editor.Find(ball).GetComponent<RigidRoomItem>();var body=editor.Find(ball).GetComponent<Rigidbody>();
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("pending"));
            // Supply motion at the boundary of the owned step, before PhysX runs.
            body.isKinematic=false;body.linearVelocity=Vector3.right*3;body.angularVelocity=Vector3.up*2;rigid.Refresh();
            Assert.That(body.isKinematic,Is.True);Assert.That((bool)PhysicsInputFact(ball)["momentumHeld"],Is.True);
            return(ground,ball,rigid,body);
        }
        [UnityTest]public IEnumerator PhysicsInputsIsolatedPreparationRetainsOnlyContinuouslyOwnedMomentum(){
            var(ground,ball,rigid,body)=PendingPhysicsInputFixture();
            var before=new RoomFrame(root.transform);uint revision=rigid.MotionRevision;
            root.transform.rotation=Quaternion.Euler(0,90,0);var after=new RoomFrame(root.transform);rigid.MoveFrame(before,after);Physics.SyncTransforms();
            Assert.That(rigid.MotionRevision,Is.EqualTo(revision),"A world-frame transfer is not a new physics owner");
            yield return NavigationLoadingDone();rigid.Refresh();
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(body.isKinematic,Is.False);
            Assert.That(editor.Frame.VectorToRoom(body.linearVelocity).x,Is.EqualTo(3).Within(.1f));Assert.That(body.angularVelocity.y,Is.EqualTo(2).Within(.1f));
            Assert.That((bool)PhysicsInputFact(ball)["momentumHeld"],Is.False);
        }
        [UnityTest]public IEnumerator PhysicsInputsIsolatedPauseCancelsPendingLoadAndForgetsMomentum(){
            var(ground,ball,rigid,body)=PendingPhysicsInputFixture();physics.PausePhysics();
            Assert.That((bool)PhysicsInputFact(ball)["momentumHeld"],Is.False);Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("inactive"));
            yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);Assert.That(body.isKinematic,Is.True);
            var loading=editor.ActivateNativeArea(editor.RegionFor(ground));yield return new WaitUntil(()=>loading.IsCompleted);Assert.That(loading.Result,Is.Null);
            physics.StartPhysics();Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));Assert.That(body.angularVelocity,Is.EqualTo(Vector3.zero));
        }
        [UnityTest]public IEnumerator PhysicsInputsFreeBodyOwnerDoesNotTakeOverAnActiveXriGrip(){
            float previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/72;
            try{
                string ball=PhysicsInputBall(new(4,1,4));var item=editor.Find(ball);
                physics.SetSurfaces(true,"Aligned scan");physics.StartPhysics();
                var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);
                for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
                Assert.That(item.Grab.isSelected,Is.True,"The synthetic hand must still select the ball");
                Assert.That(item.Grab.throwOnDetach,Is.True,"A new grip clears the previous teleport's cancelled-throw state");
                Assert.That(item.Grab.movementType,Is.EqualTo(XRBaseInteractable.MovementType.VelocityTracking));
                Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("inactive"));
                for(int i=0;i<12;i++){hand.transform.position+=Vector3.right*.025f;yield return new WaitForSeconds(.02f);}
                hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;
                manager.SelectExit((IXRSelectInteractor)hand,item.Grab);yield return null;yield return new WaitForFixedUpdate();
                Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("ready"));
                Assert.That(item.GetComponent<Rigidbody>().linearVelocity.x,Is.GreaterThan(.2f),"The real XRI throw must survive native input acquisition");
            }finally{Time.captureDeltaTime=previousDelta;}
        }
        [UnityTest]public IEnumerator PhysicsInputsAuthoringDuringSimulationWaitsForCompletePublication(){
            physics.SetSurfaces(true,"Aligned scan");physics.StartPhysics();
            string first=PhysicsInputBall(new(4,1,4)),second=PhysicsInputBall(new(4.5f,1,4));
            foreach(var id in new[]{first,second}){
                var rigid=editor.Find(id).GetComponent<RigidRoomItem>();
                Assert.That(rigid.Simulating,Is.True,PhysicsInputFact(id).ToString());
                Assert.That(rigid.Launch(Vector3.up,Vector3.zero),Is.True,PhysicsInputFact(id).ToString());
            }
            Assert.That(physics.Running,Is.True);yield return new WaitForFixedUpdate();
            Assert.That((string)PhysicsInputFact(first)["state"],Is.EqualTo("ready"));
        }
        [UnityTest]public IEnumerator PhysicsInputsFixedSurfacesStillPublishRealContacts(){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Contact platform",new(10,0,0),4,Color.white,out var floor,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(floor,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            var platform=editor.Find(floor);Physics.SyncTransforms();float top=platform.Grab.colliders[0].bounds.max.y;
            string ball=PhysicsInputBall(new(10,top+.4f,0));int contacts=0;
            platform.GetComponent<RigidRoomItem>().ContactStarted+=(_,other,_,_)=>{if(other.attachedRigidbody==editor.Find(ball).GetComponent<Rigidbody>())contacts++;};
            physics.SetSurfaces(true,"Aligned scan");physics.StartPhysics();
            for(int i=0;i<70&&contacts==0;i++)yield return new WaitForFixedUpdate();
            Assert.That(contacts,Is.GreaterThan(0),"Fixed objects still emit collision events for user programs");
        }
        [UnityTest]public IEnumerator PhysicsInputsSweepCoversOffsetRotationGravityAndRejectsUnknownShape(){
            string ball=PhysicsInputBall(new(4,2,4));var item=editor.Find(ball);var body=item.GetComponent<Rigidbody>();var sphere=item.GetComponentInChildren<SphereCollider>();
            Assert.That(sphere,Is.Not.Null);sphere.center=new(5,0,0);body.centerOfMass=Vector3.zero;
            Physics.SyncTransforms();Assert.That(RoomPhysicsSweep.Bounds(item,body,.02f,out var motion,out var support),Is.True);
            var center=item.transform.TransformPoint(sphere.center);float distance=Vector3.Distance(center,body.worldCenterOfMass);
            Assert.That(motion.Contains(body.worldCenterOfMass+Vector3.forward*distance),Is.True,"All orientations around the centre of mass are covered");
            Assert.That(motion.Contains(body.worldCenterOfMass+Vector3.right*(15*RoomPhysicsSweep.LookAhead)),Is.True);
            Assert.That(support.min.y,Is.LessThanOrEqualTo(motion.min.y-15.99f));
            sphere.enabled=false;Assert.That(RoomPhysicsSweep.Bounds(item,body,.02f,out _,out _),Is.False);yield return null;
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        IEnumerator ResidentModelAt(Vector3 position,Action<string> completed){
            var asset=ModelLibrary.Inspect("resident.glb",ModelFixture.Create());
            var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.IsNull(save.Exception);
            var load=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);
            yield return new WaitUntil(()=>load.IsCompleted);Assert.IsNull(load.Exception);
            var data=editor.Read(load.Result);data.position=position;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(data.id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            Assert.That(editor.Find(data.id).GetComponent<CreatedRoomObject>().ModelGeometryReady,Is.True);
            completed(data.id);
        }
        [UnityTest]public IEnumerator ResidentCollisionLossStopsNearbyGroupButIndependentPhysicsContinues(){
            string target=null;yield return ResidentModelAt(new(4,1,4),id=>target=id);
            var ids=PhysicsIslandChain();string free=PhysicsInputBall(new(-15,3,-10));StartIsland();yield return IslandActive(ids);
            foreach(var id in ids)Assert.That(editor.Find(id).GetComponent<RigidRoomItem>().Launch(Vector3.right,Vector3.zero),Is.True);
            var freeBody=editor.Find(free).GetComponent<Rigidbody>();
            Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Launch(Vector3.right*2,Vector3.zero),Is.True);
            editor.Find(target).GetComponent<CreatedRoomObject>().Model.Dispose();
            // Production pre-PhysX publication must stop affected actors without a
            // solver step against the replacement AABB or a room-wide pause.
            editor.RememberPhysicsStep();
            foreach(var id in ids){
                Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True,PhysicsInputFact(id).ToString());
                Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False);
            }
            Assert.That(physics.Running,Is.True);Assert.That(freeBody.isKinematic,Is.False);
            float x=freeBody.position.x;yield return new WaitForFixedUpdate();
            Assert.That(freeBody.position.x,Is.GreaterThan(x));Assert.That(editor.ResidentCollisionGuard(target).Shape.enabled,Is.True);
        }
        [UnityTest]public IEnumerator ResidentCollisionRepairNeedsFreshOwnershipAndDoesNotRestoreOldMomentum(){
            string target=null;yield return ResidentModelAt(new(4,1,4),id=>target=id);
            string actor=PhysicsInputBall(new(4,2,4)),free=PhysicsInputBall(new(-12,3,-12));
            StartIsland();var rigid=editor.Find(actor).GetComponent<RigidRoomItem>();var body=rigid.GetComponent<Rigidbody>();
            Assert.That(rigid.Launch(Vector3.right,Vector3.up),Is.True);
            var model=editor.Find(target).GetComponent<CreatedRoomObject>();model.enabled=false;editor.RememberPhysicsStep();
            Assert.That(body.isKinematic,Is.True);Assert.That((string)PhysicsInputFact(actor)["state"],Is.EqualTo("unavailable"));
            model.enabled=true;editor.RememberPhysicsStep();yield return new WaitForFixedUpdate();
            Assert.That(body.isKinematic,Is.True,"Repair does not grant a new physics owner");Assert.That(editor.ResidentCollisionGuard(target),Is.Null);
            Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Simulating,Is.True);
            physics.PausePhysics();physics.StartPhysics();Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));Assert.That(body.angularVelocity,Is.EqualTo(Vector3.zero));
        }
        [UnityTest]public IEnumerator ResidentCollisionMovableImportItselfStaysStoppedAfterRepair(){
            string target=null;yield return ResidentModelAt(new(4,3,4),id=>target=id);
            Assert.That(editor.SetItemPhysics(target,new(){mode="solid",shape="box",mass=1}),Is.True,editor.Status);
            StartIsland();var item=editor.Find(target);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            var model=item.GetComponent<CreatedRoomObject>();Assert.That(rigid.Launch(Vector3.right*2,Vector3.up),Is.True);
            model.enabled=false;editor.RememberPhysicsStep();Assert.That(body.isKinematic,Is.True);Assert.That(physics.Running,Is.True);
            Assert.That((string)PhysicsInputFact(target)["state"],Is.EqualTo("unavailable"));
            model.enabled=true;editor.RememberPhysicsStep();yield return new WaitForFixedUpdate();
            Assert.That(body.isKinematic,Is.True,"Repairing this object's collider is not a fresh owner");
            Assert.That((bool)PhysicsInputFact(target)["momentumHeld"],Is.False);
            physics.PausePhysics();physics.StartPhysics();Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));
        }
        [UnityTest]public IEnumerator ResidentCollisionReadsStayPassiveAndUseCurrentPlacementOnlyForMatchingGeometry(){
            string target=null;yield return ResidentModelAt(new(4,1,4),id=>target=id);
            var item=editor.Find(target);var model=item.GetComponent<CreatedRoomObject>();var before=editor.ReadCollisionBounds(target);
            model.enabled=false;int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            for(int i=0;i<4;i++){Assert.That(editor.ReadCollisionBounds(target).Known,Is.True);Assert.That((string)editor.ObserveSpatialBounds(target)["source"],Is.EqualTo("retained"));}
            Assert.That(editor.ResidentCollisionGuard(target),Is.Null,"Observation must not publish a guard");Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            item.transform.position+=Vector3.right*4;var moved=editor.ReadCollisionBounds(target);Assert.That(moved.Bounds.center.x,Is.EqualTo(before.Bounds.center.x+4).Within(.001f));
            item.transform.rotation=Quaternion.Euler(0,43,0);item.transform.localScale*=2;
            var transformed=editor.ReadCollisionBounds(target);Assert.That(transformed.Known,Is.True);editor.RefreshRegionCollision();
            Assert.That(editor.ResidentCollisionGuard(target).Shape.center,Is.EqualTo(transformed.Bounds.center));
            var data=editor.Read(target);data.modelGeometry=new(){scaleMode="source",metresPerUnit=2};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.ReadCollisionBounds(target).Known,Is.False,"Old extents must not certify changed native geometry");
            Assert.That(editor.ResidentCollisionGuard(target),Is.Null);Assert.That(physics.SimulationReady,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator ResidentCollisionMissingRootRemainsGlobalAndDisablingWorkspaceLeavesNoBarrier(){
            string target=null;yield return ResidentModelAt(new(4,1,4),id=>target=id);StartIsland();
            var model=editor.Find(target).GetComponent<CreatedRoomObject>();model.enabled=false;editor.RefreshRegionCollision();
            var guard=editor.ResidentCollisionGuard(target).Shape;Assert.That(physics.Running,Is.True);
            editor.enabled=false;Assert.That(!guard||!guard.enabled,Is.True);Assert.That(physics.Running,Is.False);
            editor.enabled=true;editor.RefreshRegionCollision();Assert.That(editor.ResidentCollisionGuard(target).Shape.enabled,Is.True);
            Assert.That(physics.SetRunning(true,out var error),Is.True,error);
            UnityEngine.Object.DestroyImmediate(editor.Find(target).gameObject);editor.RefreshRegionCollision();
            Assert.That(editor.ReadCollisionBounds(target).Known,Is.False);Assert.That(physics.Running,Is.False);Assert.That(editor.ResidentCollisionGuard(target),Is.Null);
            Assert.That(physics.SetRunning(true,out error),Is.False);Assert.That(editor.DeleteObject(target,out error),Is.True,error);Assert.That(editor.Read(target),Is.Null);Assert.That(physics.Running,Is.False,"Cleanup does not restart physics");Assert.That(physics.SetRunning(true,out error),Is.True,error);yield return null;
        }
        [UnityTest]public IEnumerator ResidentCollisionLocalNavigationInputsRejectTheModelWithoutInvalidatingUnrelatedGround(){
            string target=null;yield return ResidentModelAt(new(4,1,4),id=>target=id);StartIsland();
            var near=editor.ReadCollisionBounds(target).Bounds;near.Expand(.2f);
            var far=new Bounds(new(-10,1,-10),Vector3.one);
            editor.Find(target).GetComponent<CreatedRoomObject>().Model.Dispose();
            var nearIds=editor.SpatialInputTargets(near,near,false,null);var farIds=editor.SpatialInputTargets(far,far,false,null);
            Assert.That(nearIds,Does.Contain(target));Assert.That(farIds,Does.Not.Contain(target));
            Assert.That(editor.SpatialInputsReady(nearIds,near,near,false,null,out _),Is.EqualTo(RoomSpatialReadiness.Unavailable));
            Assert.That(editor.SpatialInputsReady(farIds,far,far,false,null,out _),Is.EqualTo(RoomSpatialReadiness.Ready));
            Assert.That(physics.Running,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator ResidentCollisionLossKeepsGrabAndAnimationOwnership(){
            string target=null;yield return ResidentModelAt(new(4,1,4),id=>target=id);
            string actor=PhysicsInputBall(new(4,2,4));StartIsland();
            var item=editor.Find(target);var model=item.GetComponent<CreatedRoomObject>();
            var animated=editor.Find(actor).GetComponent<RigidRoomItem>();animated.SetAnimationOwner(this,true);
            var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);
            Assert.That(item.Grab.isSelected,Is.True);model.enabled=false;editor.RememberPhysicsStep();
            Assert.That(item.Grab.isSelected,Is.True,"Geometry loss must not take the hand's selection");
            Assert.That(item.Grab.colliders.All(c=>!c||!c.enabled),Is.True,"Stale held colliders must not emit physical contacts");
            Assert.That(animated.AnimationOwned,Is.True);Assert.That((string)PhysicsInputFact(actor)["state"],Is.EqualTo("inactive"));
            yield return null;Assert.That(item.Grab.isSelected,Is.True);Assert.That(animated.AnimationOwned,Is.True);
            manager.SelectExit((IXRSelectInteractor)hand,item.Grab);model.enabled=true;editor.RefreshRegionCollision();
            Assert.That(item.Grab.colliders.Any(c=>c&&c.enabled),Is.True);Assert.That(editor.ResidentCollisionGuard(target),Is.Null);
            animated.SetAnimationOwner(this,false);Assert.That(physics.Running,Is.True);
        }
        [UnityTest]public IEnumerator ResidentCollisionNativeGuardImpactHasRecoveryProvenanceAndNoAuthoredContact(){
            string target=null;yield return ResidentModelAt(new(5,3,10),id=>target=id);
            var extent=editor.ReadCollisionBounds(target).Bounds;var position=extent.center;position.x=extent.min.x-6;
            string actor=PhysicsInputBall(position);StartIsland();
            editor.Find(target).GetComponent<CreatedRoomObject>().Model.Dispose();editor.RefreshRegionCollision();
            var item=editor.Find(actor);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            var counter=item.gameObject.AddComponent<DormantCollisionCounter>();int contacts=0;rigid.ContactStarted+=(_,__,___,____)=>contacts++;
            Assert.That(rigid.Launch(Vector3.right*15,Vector3.zero),Is.True);body.useGravity=false;editor.RememberPhysicsStep();
            var mode=Physics.simulationMode;
            try{
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.5f);
                Assert.That(counter.Contacts,Is.GreaterThan(0),"Exercise an unpredicted native impact beyond the normal prepared timestep");
                Assert.That(contacts,Is.Zero);Assert.That(body.isKinematic,Is.False,"Collision callbacks only queue identity");
                editor.ProcessDormantContacts();Assert.That(body.isKinematic,Is.True);Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.True);
                Assert.That((bool)PhysicsInputFact(actor)["momentumHeld"],Is.False);Assert.That(physics.Running,Is.True);
            }finally{Physics.simulationMode=mode;}
            yield return null;Assert.That(body.isKinematic,Is.True);Assert.That(contacts,Is.Zero);
        }
    }
}

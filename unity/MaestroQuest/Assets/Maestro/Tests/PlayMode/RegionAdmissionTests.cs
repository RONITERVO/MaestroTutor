// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [Test] public void RegionAdmissionReplacesDestroyedWorkspaceOwnerWithoutBorrowingItsRunIntent()
        {
            var shell=new GameObject("Collision authority shell");
            try {
                var first=new GameObject("Previous region");first.transform.SetParent(shell.transform,false);
                var next=new GameObject("Next region");next.transform.SetParent(shell.transform,false);
                var world=shell.AddComponent<RoomPhysicsWorld>();world.SetSurfaces(true,"Aligned test scan");
                world.ConfigureCollisionAdmission(first,()=>null);Assert.IsTrue(world.SetRunning(true,out var error),error);
                Assert.Throws<System.InvalidOperationException>(()=>world.ConfigureCollisionAdmission(next,()=>null));
                first.SetActive(false);Assert.Throws<System.InvalidOperationException>(()=>world.ConfigureCollisionAdmission(next,()=>null),"A disabled owner is still live and cannot be displaced");
                Object.DestroyImmediate(first);world.RefreshCollisionAdmission();Assert.IsFalse(world.SimulationReady);Assert.IsFalse(world.Running);
                string oldState=(string)world.ObserveSimulation()["stateId"];
                world.ConfigureCollisionAdmission(next,()=>null);
                Assert.IsTrue(world.SimulationReady);Assert.IsFalse(world.Running,"Replacing the retired owner must not resume its simulation intent");
                Assert.IsFalse(world.CanSetSimulation(oldState,true,out error));Assert.IsTrue(world.SetRunning(true,out error),error);
            }finally{Object.DestroyImmediate(shell);}
        }
        bool AdmissionCanOccupy(){using var query=new RoomEnvironmentQueries().Begin(physics);return query.CanOccupy(block.transform.position,block,null);}
        [UnityTest] public IEnumerator RegionAdmissionQueuedGeometryStopsPhysicsAndTraversalUntilExplicitRestart()
        {
            var asset=ModelLibrary.Inspect("region.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.IsNull(save.Exception);
            physics.SetSurfaces(true,"Aligned test scan");Assert.IsTrue(physics.SetRunning(true,out var error),error);
            var rigid=block.GetComponent<RigidRoomItem>();rigid.Configure(physics,ItemPhysics.Bouncy,.5f);var body=block.GetComponent<Rigidbody>();Assert.IsFalse(body.isKinematic);body.linearVelocity=Vector3.right*2;body.angularVelocity=Vector3.up;
            var barrier=new ModelLoadBarrier();var first=ResidencyModel();var load=first.LoadAsync(asset,barrier);string id=null;bool ran=true,allowed=true,occupied=true,frozen=false;string reason=null;
            try {
                yield return new WaitUntil(()=>barrier.Entered||load.IsCompleted);Assert.IsTrue(barrier.Entered);
                Assert.IsTrue(editor.CreateImportedModel(asset.Hash,out id,out error),error);
                yield return null;yield return null;
                ran=physics.Running;allowed=physics.SetRunning(true,out reason);occupied=AdmissionCanOccupy();frozen=body.isKinematic;
            }finally{barrier.Open();first.Dispose();}
            yield return new WaitUntil(()=>load.IsCompleted);Assert.IsNull(load.Exception);
            var view=editor.Find(id).GetComponent<CreatedRoomObject>();float deadline=Time.realtimeSinceStartup+5;
            while(!view.ModelGeometryReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(view.ModelGeometryReady,view.ModelStatus);
            Assert.IsFalse(ran,"Missing authored collision cannot keep the region running merely because a scan is ready");
            Assert.IsTrue(frozen,"Actual moving rigid bodies must stop before entering incomplete geometry");Assert.IsFalse(allowed,"Manual Start must refuse pending geometry");Assert.IsFalse(occupied,"Traversal cannot borrow the ready scan through incomplete region collision");Assert.That(reason,Does.Contain(id));
            Assert.IsFalse(physics.Running,"Finishing geometry must not silently restart physics");Assert.IsTrue(physics.SetRunning(true,out error),error);Assert.IsTrue(AdmissionCanOccupy());Assert.AreEqual(Vector3.zero,body.linearVelocity);Assert.AreEqual(Vector3.zero,body.angularVelocity);
        }
        [UnityTest] public IEnumerator RegionAdmissionMissingGeometryIsReportedAndRemovalRestoresReadinessWithoutRestart()
        {
            physics.SetSurfaces(true,"Aligned test scan");Assert.IsTrue(physics.SetRunning(true,out var error),error);
            Assert.IsTrue(editor.CreateImportedModel(new string('f',64),out var id,out error),error);
            var view=editor.Find(id).GetComponent<CreatedRoomObject>();float deadline=Time.realtimeSinceStartup+5;
            while(view.ModelStatus=="Loading local model…"&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;Assert.IsFalse(view.ModelGeometryReady);bool ran=physics.Running,allowed=physics.SetRunning(true,out var reason),occupied=AdmissionCanOccupy();
            Assert.IsTrue(editor.DeleteObject(id,out error),error);yield return null;
            Assert.IsFalse(ran);Assert.IsFalse(allowed);Assert.IsFalse(occupied);Assert.That(reason,Does.Contain(id));
            Assert.IsFalse(physics.Running);Assert.IsTrue(physics.SetRunning(true,out error),error);
        }
        [UnityTest] public IEnumerator RegionAdmissionPreparedImportsKeepMotionUntilAcceptedGeometryIsLost()
        {
            var asset=ModelLibrary.Inspect("ready.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.IsNull(save.Exception);
            physics.SetSurfaces(true,"Aligned test scan");Assert.IsTrue(physics.SetRunning(true,out var error),error);
            var barrier=new ModelLoadBarrier();var first=ResidencyModel();var load=first.LoadAsync(asset,barrier);System.Threading.Tasks.Task<string> placement=null;
            try {
                yield return new WaitUntil(()=>barrier.Entered||load.IsCompleted);Assert.IsTrue(barrier.Entered);
                placement=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return null;yield return null;
                Assert.IsTrue(physics.Running,"An unaccepted preparation or preview must not stop the accepted region");Assert.IsTrue(AdmissionCanOccupy());
            }finally{barrier.Open();first.Dispose();}
            yield return new WaitUntil(()=>load.IsCompleted&&placement.IsCompleted);Assert.IsNull(load.Exception);Assert.IsNull(placement.Exception);
            string id=placement.Result;var view=editor.Find(id).GetComponent<CreatedRoomObject>();Assert.IsTrue(view.ModelGeometryReady);Assert.IsTrue(physics.Running,"Ready geometry transfer cannot introduce a loading pause");
            var priorState=(string)physics.ObserveSimulation()["stateId"];view.Model.Dispose();Assert.IsTrue(physics.Running);Assert.IsFalse(physics.CanSetSimulation(priorState,true,out error));Assert.IsTrue(physics.Running,"Preflight cannot mutate simulation while reporting changed geometry");Assert.IsFalse(AdmissionCanOccupy(),"A captured scan must not outlive loss of required geometry");yield return null;
            Assert.IsFalse(physics.Running);Assert.IsFalse(physics.SetRunning(true,out error));Assert.That(error,Does.Contain(id));
            Assert.IsTrue(editor.DeleteObject(id,out error),error);Assert.IsFalse(physics.Running);Assert.IsTrue(physics.SetRunning(true,out error),error);
        }
        [UnityTest] public IEnumerator RegionAdmissionOwnerDisableCannotTurnBoundPhysicsIntoAnUnscopedWorld()
        {
            physics.SetSurfaces(true,"Aligned test scan");Assert.IsTrue(physics.SetRunning(true,out var error),error);
            editor.enabled=false;Assert.IsFalse(AdmissionCanOccupy());Assert.IsFalse(physics.SetRunning(true,out error));editor.enabled=true;yield return null;Assert.IsFalse(physics.Running);Assert.IsTrue(physics.SetRunning(true,out error),error);
        }
        [UnityTest] public IEnumerator RegionAdmissionRefusalIsVisibleThroughSharedFactsAndDoesNotDeleteSavedObjects()
        {
            physics.SetSurfaces(true,"Aligned test scan");var before=physics.ObserveSimulation();
            Assert.IsTrue(editor.CreateImportedModel(new string('e',64),out var id,out var error),error);yield return null;
            Assert.IsTrue(Maestro.Quest.Programs.BehaviourCatalog.TryRead("physics.simulation",1,null,new Maestro.Quest.Programs.BehaviourCatalog.FactContext(editor:editor),out var fact));
            var current=Newtonsoft.Json.Linq.JObject.FromObject(fact.Value);Assert.IsFalse((bool)current["ready"]);Assert.IsFalse((bool)current["canStart"]);Assert.That((string)current["reason"],Does.Contain(id));Assert.AreNotEqual((string)before["stateId"],(string)current["stateId"]);
            Assert.IsTrue((bool)physics.ObserveEnvironment()["scanReady"]);Assert.IsFalse((bool)physics.ObserveEnvironment()["ready"]);Assert.IsNotNull(editor.Read(id));
            Assert.IsFalse(physics.CanSetSimulation((string)before["stateId"],true,out error));Assert.IsTrue(editor.DeleteObject(id,out error),error);
            Assert.IsTrue((bool)physics.ObserveSimulation()["ready"]);Assert.IsFalse(physics.Running);
        }
        [UnityTest] public IEnumerator RegionAdmissionUnavailableCollisionRetiresAnAlreadyBuiltNavigationMap()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Accepted region floor";floor.transform.SetParent(root.transform,false);floor.transform.localPosition=Vector3.down*.1f;floor.transform.localScale=new Vector3(8,.2f,8);floor.layer=RoomPhysicsLayers.Environment;
            floor.AddComponent<RoomWalkableSurface>().Publish(floor.GetComponent<Collider>());Physics.SyncTransforms();
            physics.SetSurfaces(true,"Aligned test scan");Assert.IsTrue(physics.SetRunning(true,out var error),error);
            var navigation=root.AddComponent<RoomNavigation>();navigation.Initialize(physics);Assert.IsTrue(navigation.Prepare(.2f,1.7f,out error),error);Assert.IsTrue(navigation.Ready);
            var point=new Vector3(2,0,2);Assert.IsTrue(navigation.Sample(point,.2f,out _));
            Assert.IsTrue(editor.CreateImportedModel(new string('d',64),out var id,out error),error);
            Assert.IsFalse(navigation.Ready);Assert.IsFalse(navigation.Sample(point,.2f,out _));yield return null;
            Assert.IsTrue(editor.DeleteObject(id,out error),error);Assert.IsFalse(physics.Running);Assert.IsFalse(navigation.Ready);
            Assert.IsTrue(physics.SetRunning(true,out error),error);Assert.IsTrue(navigation.Prepare(.2f,1.7f,out error),error);Assert.IsTrue(navigation.Sample(point,.2f,out _));
        }
        [UnityTest] public IEnumerator RegionAdmissionVirtualGroundCannotMaskAnotherPendingCollisionDependency()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);floor.transform.localPosition=Vector3.down*.1f;floor.transform.localScale=new Vector3(8,.2f,8);floor.layer=RoomPhysicsLayers.Environment;
            floor.AddComponent<RoomWalkableSurface>().Publish(floor.GetComponent<Collider>());Physics.SyncTransforms();
            Assert.IsTrue(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out var error),error);Assert.IsTrue(physics.SetRunning(true,out error),error);
            Assert.IsTrue(editor.CreateImportedModel(new string('c',64),out var id,out error),error);
            Assert.IsTrue((bool)physics.ObserveEnvironment()["authoredReady"]);Assert.IsFalse(physics.SimulationReady);Assert.IsFalse(physics.CanSimulate(Vector3.up));Assert.IsFalse(AdmissionCanOccupy());
            Assert.IsTrue(editor.DeleteObject(id,out error),error);Assert.IsTrue(physics.SimulationReady);Assert.IsFalse(physics.Running);yield return null;
        }
        [UnityTest] public IEnumerator RegionAdmissionReadyReconciliationKeepsDynamicBodiesAdmitted()
        {
            string id=editor.Identity(block);Assert.IsTrue(editor.SetItemPhysics(id,new ObjectPhysicsSettings{mode="bouncy",shape="automatic",mass=.5f}));
            physics.SetSurfaces(true,"Aligned test scan");Assert.IsTrue(physics.SetRunning(true,out var error),error);var body=block.GetComponent<Rigidbody>();Assert.IsFalse(body.isKinematic);
            Assert.IsTrue(editor.PaintObject(id,Color.magenta,out error),error);
            Assert.IsTrue(physics.Running);Assert.IsFalse(body.isKinematic,"Reconciliation with ready geometry must not leave an ordinary dynamic body frozen");yield return null;
        }
    }
}

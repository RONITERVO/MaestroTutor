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
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        GameObject RestorationWall(Vector3 position){
            RoomPhysicsLayers.Configure();var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Restoration test wall";wall.transform.SetParent(root.transform,false);wall.transform.position=position;wall.layer=RoomPhysicsLayers.Scanned;
            physics.SetSurfaces(true,"Aligned");Physics.SyncTransforms();return wall;
        }
        JObject RecoveryArgs(string id){var state=EnvironmentRecoveryCapability.Observe(editor,id);return new JObject{["target"]=id,["revision"]=state["revision"].DeepClone(),["stateId"]=state["stateId"].DeepClone()};}
        RoomEnvironmentBinding RestoredBinding(string id)=>editor.Find(id).GetComponent<RoomEnvironmentBinding>();
        [UnityTest]public IEnumerator EnvironmentRecoveryPublishesBlockedViewWithoutChangingSavedPoseAndRequiresExplicitCheck(){
            string id=editor.Identity(block),area=NativeAreaFor(id);Vector3 pose=block.transform.position;
            yield return NativeAreaCompletion(editor.RetireNativeArea(area));string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            var wall=RestorationWall(pose);yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            var item=editor.Find(id);var binding=RestoredBinding(id);Assert.That(item.isActiveAndEnabled,Is.True);Assert.That(binding.RestorationHeld,Is.True);
            Assert.That(physics.EnvironmentReady(item),Is.False);Assert.That(physics.CanSimulate(pose,item),Is.False);
            Assert.That(item.transform.position,Is.EqualTo(pose));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.Revision,Is.EqualTo(revision));
            var observed=EnvironmentRecoveryCapability.Observe(editor,id);wall.transform.position+=Vector3.right*5;Physics.SyncTransforms();
            physics.SetSurfaces(true,"Ready again");for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            Assert.That(JToken.DeepEquals(EnvironmentRecoveryCapability.Observe(editor,id),observed),Is.True,"Availability and passive observation do not retry");
            var module=new EnvironmentRecoveryCapability();var args=RecoveryArgs(id);
            Assert.That(module.Start(new CapabilityContext(editor,animations),"repair",args,out var operation,out var error),Is.True,error);
            Assert.That((bool)operation.Result["held"],Is.False);Assert.That(binding.RestorationHeld,Is.False);Assert.That(physics.Running,Is.False);
            Assert.That(module.CanRun(new CapabilityContext(editor,animations),args,out _),Is.False,"A previous state token cannot be reused as new intent");
            Assert.That(item.transform.position,Is.EqualTo(pose));Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryFailedReceiptDoesNotRetryWhenWallLaterMoves(){
            string id=editor.Identity(block),area=NativeAreaFor(id);var pose=block.transform.position;
            yield return NativeAreaCompletion(editor.RetireNativeArea(area));var wall=RestorationWall(pose);yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            var executor=new RoomAgentExecutor(editor);string run=runtime.Scheduler.Receipts.NextId;
            var request=new RoomAgentRequest{version=2,conditions=new[]{new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}},commands=new[]{new RoomAgentCommand{action="execution",execution=new JObject{["operation"]="start",["runId"]=run,["call"]=new JObject{["id"]="object.environment.recheck",["version"]=1,["arguments"]=RecoveryArgs(id)}}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);yield return SpatialSettingsCompletion.Wait(executor.Executions);
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((bool)receipt["output"]["held"],Is.True);
            string state=RestoredBinding(id).RestorationState;wall.transform.position+=Vector3.right*5;Physics.SyncTransforms();
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return null;
            Assert.That(RestoredBinding(id).RestorationHeld,Is.True);Assert.That(RestoredBinding(id).RestorationState,Is.EqualTo(state));
            Assert.That(JToken.DeepEquals(runtime.Scheduler.Invocation(run),receipt),Is.True);
            Assert.That(EnvironmentRecoveryCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]=id},out _),Is.True);
        }
        [UnityTest]public IEnumerator EnvironmentRecoverySurvivesAnotherRetirementAfterObstacleDisappears(){
            string id=editor.Identity(block),area=NativeAreaFor(id);var pose=block.transform.position;
            yield return NativeAreaCompletion(editor.RetireNativeArea(area));var wall=RestorationWall(pose);yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(RestoredBinding(id).RestorationHeld,Is.True);string state=RestoredBinding(id).RestorationState;
            yield return NativeAreaCompletion(editor.RetireNativeArea(area));Assert.That(EnvironmentRecoveryCapability.Observe(editor,id),Is.Null);
            wall.transform.position+=Vector3.right*5;Physics.SyncTransforms();yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(RestoredBinding(id).RestorationHeld,Is.True);Assert.That(RestoredBinding(id).RestorationState,Is.Not.EqualTo(state));
            physics.StartPhysics();Assert.That(RestoredBinding(id).RestorationHeld,Is.False);physics.PausePhysics();
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryChecksWallThatMovesDuringPrivatePreparation(){
            var(area,ids)=NativePreparationGroup();var pose=editor.Find(ids[0]).transform.position;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;var wall=RestorationWall(pose+Vector3.right*20);
            var task=editor.ActivateNativeArea(area);yield return WaitForPrivateNativeCandidates(task);
            wall.transform.position=pose;Physics.SyncTransforms();yield return NativeAreaCompletion(task);
            Assert.That(RestoredBinding(ids[0]).RestorationHeld,Is.True);Assert.That(editor.Find(ids[0]).transform.position,Is.EqualTo(pose));
            Assert.That(ids.All(id=>editor.Find(id)&&editor.Find(id).isActiveAndEnabled),Is.True,"Failure to prove one collider does not discard editable geometry");
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryHonoursVirtualProfileWithoutReenablingScannedFloor(){
            string id=editor.Identity(block),area=NativeAreaFor(id),profile=NewEnvironment();
            Assert.That(AssignEnvironment(id,profile,out var error),Is.True,error);
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            var pose=editor.Find(id).transform.position;yield return NativeAreaCompletion(editor.RetireNativeArea(area));RestorationWall(pose);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(RestoredBinding(id).RestorationHeld,Is.False,"Fixed authored geometry does not need to stand on itself");
            Assert.That(physics.IncludesRealRoom(editor.Find(id)),Is.False);Assert.That(editor.Read(id).environmentProfile,Is.EqualTo(profile));
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryRefusesMissingGroundUntilExplicitStartAfterScanReturns(){
            string id=editor.Identity(block),area=NativeAreaFor(id);
            yield return NativeAreaCompletion(editor.RetireNativeArea(area));yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(RestoredBinding(id).RestorationHeld,Is.True);
            physics.SetSurfaces(true,"Aligned now");yield return null;Assert.That(RestoredBinding(id).RestorationHeld,Is.True);
            physics.StartPhysics();Assert.That(RestoredBinding(id).RestorationHeld,Is.False);physics.PausePhysics();
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryRejectsChangedSavedRevisionAndAnimationOwnership(){
            string id=editor.Identity(block);RestorationWall(block.transform.position);var binding=RestoredBinding(id);
            binding.BeginRestoration(false);binding.FinishRestoration();var old=RecoveryArgs(id);
            Assert.That(editor.PaintObject(id,Color.red,out var error),Is.True,error);
            var module=new EnvironmentRecoveryCapability();var context=new CapabilityContext(editor,animations);
            Assert.That(module.CanRun(context,old,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            var rigid=editor.Find(id).GetComponent<RigidRoomItem>();rigid.SetAnimationOwner(this,true);
            Assert.That(module.CanRun(context,RecoveryArgs(id),out error),Is.False);rigid.SetAnimationOwner(this,false);
            Assert.That(binding.RestorationHeld,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryGrabReleaseChecksCurrentShapesAndDoesNotReplayAThrow(){
            string id=editor.Identity(block);var wall=RestorationWall(block.transform.position);var binding=RestoredBinding(id);var rigid=block.GetComponent<RigidRoomItem>();
            rigid.Configure(physics,ItemPhysics.Solid,1);binding.BeginRestoration(false);binding.FinishRestoration();physics.StartPhysics();
            Assert.That(binding.RestorationHeld,Is.True);var hand=Hand(1,block.transform.position);
            manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);wall.transform.position+=Vector3.right*5;Physics.SyncTransforms();
            manager.SelectExit((IXRSelectInteractor)hand,block.Grab);
            Assert.That(binding.RestorationHeld,Is.False,binding.RestorationReason);Assert.That(block.Grab.throwOnDetach,Is.False);Assert.That(rigid.FlowMotion.Linear,Is.EqualTo(Vector3.zero));
            physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryCancelledGrabDoesNotAuthorizeResume(){
            string id=editor.Identity(block);var wall=RestorationWall(block.transform.position);var binding=RestoredBinding(id);
            binding.BeginRestoration(false);binding.FinishRestoration();var hand=Hand(1,block.transform.position);
            manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);wall.transform.position+=Vector3.right*5;Physics.SyncTransforms();
            manager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)block.Grab);
            Assert.That(binding.RestorationHeld,Is.True);Assert.That(block.Grab.throwOnDetach,Is.False);yield return null;
            Assert.That(binding.RestorationHeld,Is.True);physics.StartPhysics();Assert.That(binding.RestorationHeld,Is.False);physics.PausePhysics();
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryRepairRunsAtNativeCapacityWithoutCreatingOrLoadingObjects(){
            string id=editor.Identity(block);var wall=RestorationWall(block.transform.position);var binding=RestoredBinding(id);
            binding.BeginRestoration(false);binding.FinishRestoration();wall.transform.position+=Vector3.right*5;Physics.SyncTransforms();
            var leases=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
            try {
                int before=(int)NativeObjectResources.ObserveBudget()["instances"];var executor=new RoomAgentExecutor(editor);string run=runtime.Scheduler.Receipts.NextId;
                var request=new RoomAgentRequest{version=2,conditions=new[]{new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}},commands=new[]{new RoomAgentCommand{action="execution",execution=new JObject{["operation"]="start",["runId"]=run,["call"]=new JObject{["id"]="object.environment.recheck",["version"]=1,["arguments"]=RecoveryArgs(id)}}}}};
                Assert.That(executor.Execute(request,out var error,out _),Is.True,error);yield return SpatialSettingsCompletion.Wait(executor.Executions);
                var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((bool)receipt["output"]["held"],Is.False);
                Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(before));Assert.That(editor.NativeActivationPending,Is.False);
            } finally {foreach(var lease in leases)lease.Dispose();}
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryHeldObjectCanBeMovedOutOfTheWallWhileOtherPhysicsRuns(){
            string id=editor.Identity(block);RestorationWall(block.transform.position);var binding=RestoredBinding(id);var rigid=block.GetComponent<RigidRoomItem>();
            rigid.Configure(physics,ItemPhysics.Solid,1);binding.BeginRestoration(false);binding.FinishRestoration();physics.StartPhysics();
            var start=block.transform.position;var hand=Hand(1,start);manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            hand.transform.position+=Vector3.right*2;
            for(int i=0;i<12;i++)yield return null;
            Assert.That(Vector3.Distance(start,block.transform.position),Is.GreaterThan(1),"Recovery must not freeze the grab transformer");
            Assert.That(binding.RestorationHeld,Is.True,"Grabbing is repair, not permission to simulate");
            manager.SelectExit((IXRSelectInteractor)hand,block.Grab);Assert.That(binding.RestorationHeld,Is.False,binding.RestorationReason);
            Assert.That(rigid.FlowMotion.Linear,Is.EqualTo(Vector3.zero));physics.PausePhysics();
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryWaitsForCollisionGeometryAndReadinessDoesNotClearTheHold(){
            string id=editor.Identity(block);physics.SetSurfaces(true,"Aligned");var binding=RestoredBinding(id);var rigid=block.GetComponent<RigidRoomItem>();
            rigid.SetGeometryReady(false);binding.BeginRestoration(false);binding.FinishRestoration();
            Assert.That(binding.RestorationHeld,Is.True);Assert.That(binding.RestorationReason,Does.Contain("still loading"));
            Assert.That(new EnvironmentRecoveryCapability().CanRun(new CapabilityContext(editor,animations),RecoveryArgs(id),out var error),Is.False);Assert.That(error,Does.Contain("still loading"));
            rigid.SetGeometryReady(true);yield return null;Assert.That(binding.RestorationHeld,Is.True);
            Assert.That(new EnvironmentRecoveryCapability().Start(new CapabilityContext(editor,animations),"geometry-ready",RecoveryArgs(id),out var result,out error),Is.True,error);
            Assert.That((bool)result.Result["held"],Is.False);Assert.That(physics.Running,Is.False);
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryNavigationExplainsBothPhysicalAndVirtualHolds(){
            string id=editor.Identity(block);RestorationWall(block.transform.position);var binding=RestoredBinding(id);
            binding.BeginRestoration(false);binding.FinishRestoration();physics.StartPhysics();
            var navigation=root.AddComponent<RoomNavigation>();navigation.Initialize(physics);
            Assert.That(navigation.Prepare(.1f,1.6f,out var error,block),Is.False);Assert.That(error,Is.EqualTo(binding.RestorationReason));
            physics.PausePhysics();string profile=NewEnvironment();Assert.That(AssignEnvironment(id,profile,out error),Is.True,error);
            block.GetComponent<RigidRoomItem>().Configure(physics,ItemPhysics.Solid,1);physics.StartPhysics();
            Assert.That(binding.RestorationHeld,Is.True,"There is no accepted virtual ground");
            Assert.That(navigation.Prepare(.1f,1.6f,out error,block),Is.False);Assert.That(error,Is.EqualTo(binding.RestorationReason));
            physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator EnvironmentRecoveryAlreadyClearRetryPreservesMotionAndState(){
            string id=editor.Identity(block);physics.SetSurfaces(true,"Aligned");physics.StartPhysics();var rigid=block.GetComponent<RigidRoomItem>();rigid.Configure(physics,ItemPhysics.Solid,1);
            for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            Assert.That(rigid.Launch(Vector3.right,Vector3.zero),Is.True);var velocity=rigid.FlowMotion;var state=RecoveryArgs(id);
            Assert.That(new EnvironmentRecoveryCapability().Start(new CapabilityContext(editor,animations),"noop",state,out var result,out var error),Is.True,error);
            Assert.That((string)result.Result["stateId"],Is.EqualTo((string)state["stateId"]));Assert.That(rigid.FlowMotion.Linear,Is.EqualTo(velocity.Linear));physics.PausePhysics();
        }
    }
}

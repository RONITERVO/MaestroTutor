// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest]public IEnumerator DormantCollisionRetirementKeepsBoundsWithoutAnAuthoredOrSelectableObject(){
            RoomPhysicsLayers.Configure();string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.localPosition+=Vector3.up*.3f;
            var expected=editor.ReadCollisionBounds(id);string saved=JsonUtility.ToJson(editor.Read(id));int revision=editor.Revision;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);var guard=editor.DormantCollisionGuard(id);Assert.That(guard,Is.Not.Null);Assert.That(guard.Known&&guard.HasBounds,Is.True);Assert.That(guard.Shape.enabled,Is.True);
            Assert.That(guard.Shape.center,Is.EqualTo(expected.Bounds.center));Assert.That(Vector3.Distance(guard.Shape.size,expected.Bounds.size+Vector3.one*.004f),Is.LessThan(.00001f));
            Assert.That(guard.Shape.GetComponent<RoomItem>(),Is.Null);Assert.That(guard.Shape.GetComponent<Renderer>(),Is.Null);Assert.That(guard.Shape.gameObject.layer,Is.EqualTo(RoomPhysicsLayers.Pending));
            Assert.That((RoomPhysicsLayers.InteractionMask&(1<<RoomPhysicsLayers.Pending)),Is.Zero);Assert.That(Physics.GetIgnoreLayerCollision(RoomPhysicsLayers.Pending,RoomPhysicsLayers.Item),Is.False);Assert.That(Physics.GetIgnoreLayerCollision(RoomPhysicsLayers.Pending,RoomPhysicsLayers.Controller),Is.True);
            Physics.SyncTransforms();var b=guard.Shape.bounds;var ray=new Ray(b.center-Vector3.right*(b.extents.x+1),Vector3.right);
            Assert.That(Physics.Raycast(ray,out var hit,2+b.size.x,1<<RoomPhysicsLayers.Pending,QueryTriggerInteraction.Ignore),Is.True);Assert.That(hit.collider,Is.SameAs(guard.Shape));
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));yield return null;
        }
        [UnityTest]public IEnumerator DormantCollisionCancelledActivationKeepsGuardAndSuccessfulPublicationReplacesIt(){
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var guard=editor.DormantCollisionGuard(id);var collider=guard.Shape;using var cancel=new CancellationTokenSource();var task=editor.ActivateNativeArea(area,cancel.Token);cancel.Cancel();
            yield return NativeAreaCompletion(task,false);Assert.That(collider&&collider.enabled&&collider.gameObject.activeInHierarchy,Is.True);Assert.That(editor.Find(id),Is.Null);
            task=editor.ActivateNativeArea(area);if(!task.IsCompleted)Assert.That(collider.enabled,Is.True);yield return NativeAreaCompletion(task);
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.DormantCollisionGuard(id),Is.Null);Assert.That(guard.Shape,Is.Null);Assert.That(!collider||!collider.enabled,Is.True);
        }
        [UnityTest]public IEnumerator DormantCollisionFollowsAcceptedPlacementAndWorldFrameChanges(){
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var guard=editor.DormantCollisionGuard(id);var before=guard.Shape.center;var data=editor.Read(id);data.position+=Vector3.right*3;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);Assert.That(guard.Shape.center,Is.EqualTo(before+Vector3.right*3));Assert.That(editor.Find(id),Is.Null);
            root.transform.SetPositionAndRotation(new(2,1,-3),Quaternion.Euler(0,40,0));root.transform.localScale=Vector3.one*1.5f;Physics.SyncTransforms();
            var expected=editor.ReadCollisionBounds(id).Transform(root.transform.localToWorldMatrix).Bounds;Assert.That(guard.Shape.bounds.Contains(expected.min)&&guard.Shape.bounds.Contains(expected.max),Is.True);yield return null;
        }
        [UnityTest]public IEnumerator DormantCollisionDeletionAndUndoCannotLeaveTheOldBarrier(){
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;var guard=editor.DormantCollisionGuard(id);var shape=guard.Shape;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);Assert.That(editor.DormantCollisionGuard(id),Is.Null);Assert.That(!shape||!shape.enabled,Is.True);
            editor.Undo();Assert.That(editor.Find(id),Is.Not.Null);Assert.That(guard.Shape,Is.Null);yield return null;Assert.That((bool)shape,Is.False);
        }
        [UnityTest]public IEnumerator DormantCollisionUnknownAndKnownEmptyAreDifferentAndNeverReuseStaleBounds(){
            using var guard=new RoomCollisionGuard(editor,"test-extent");var value=new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=new Bounds(new(8,3,8),Vector3.one)};
            guard.Synchronize(value);var shape=guard.Shape;Assert.That(shape.enabled,Is.True);guard.Synchronize(default);Assert.That(guard.Known,Is.False);Assert.That(guard.HasBounds,Is.False);Assert.That(shape.enabled,Is.False);
            guard.Synchronize(RoomSpatialExtent.Empty);Assert.That(guard.Known,Is.True);Assert.That(guard.HasBounds,Is.False);Assert.That(shape.enabled,Is.False);
            guard.Synchronize(value);Assert.That(shape.enabled,Is.True);guard.Dispose();Assert.That(shape.enabled,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator DormantCollisionNativeBarrierBlocksBodiesWithoutPublishingFakeContacts(){
            RoomPhysicsLayers.Configure();using var guard=new RoomCollisionGuard(editor,"test-barrier");guard.Synchronize(new(){Known=true,HasBounds=true,Bounds=new Bounds(new(8,3,8),new(1,2,1))});
            var worldRoot=new GameObject("Isolated barrier physics");var projectile=new GameObject("Barrier impact body");var mode=Physics.simulationMode;
            try{
                var world=worldRoot.AddComponent<RoomPhysicsWorld>();world.SetSurfaces(true,"Synthetic aligned world");world.StartPhysics();projectile.layer=RoomPhysicsLayers.Item;projectile.transform.position=new(6.5f,3,8);
                var sphere=projectile.AddComponent<SphereCollider>();sphere.radius=.1f;var item=projectile.AddComponent<RoomItem>();item.Configure(new Collider[]{sphere});var rigid=projectile.AddComponent<RigidRoomItem>();rigid.Initialize(item);rigid.Configure(world,ItemPhysics.Solid,1);
                int semanticContacts=0;rigid.ContactStarted+=(_,__,___,____)=>semanticContacts++;var counter=projectile.AddComponent<DormantCollisionCounter>();Assert.That(rigid.Launch(Vector3.right*5,Vector3.zero),Is.True);var body=projectile.GetComponent<Rigidbody>();body.useGravity=false;
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();for(int tick=0;tick<20;tick++)Physics.Simulate(.02f);
                Assert.That(counter.Contacts,Is.GreaterThan(0),"Establish a real native barrier impact");Assert.That(body.position.x,Is.LessThan(7.5f));Assert.That(semanticContacts,Is.Zero,"A loading barrier is not an authored collision event");
            }finally{Physics.simulationMode=mode;UnityEngine.Object.Destroy(projectile);UnityEngine.Object.Destroy(worldRoot);}yield return null;
        }
        [UnityTest]public IEnumerator DormantCollisionQueriesIgnoreBarriersButStillSeeWorldGeometry(){
            using var guard=new RoomCollisionGuard(editor,"query-barrier");guard.Synchronize(new(){Known=true,HasBounds=true,Bounds=new Bounds(new(8,3,8),Vector3.one)});
            var obstacle=new GameObject("Actual query obstacle");
            try{
                obstacle.transform.position=new(11,3,8);var shape=obstacle.AddComponent<BoxCollider>();Physics.SyncTransforms();var ray=new Ray(new(6,3,8),Vector3.right);
                Assert.That(Physics.Raycast(ray,out var raw,7,~0,QueryTriggerInteraction.Ignore),Is.True);Assert.That(raw.collider,Is.SameAs(guard.Shape));
                Assert.That(RoomPointerHit.Raycast(ray,out var visible,7,~0,QueryTriggerInteraction.Ignore),Is.True);Assert.That(visible.collider,Is.SameAs(shape));
                var drawing=new DrawingSurfaceOcclusion();Assert.That(drawing.Clear(ray,4,editor.Find("book").transform,null),Is.True);Assert.That(drawing.Clear(ray,7,editor.Find("book").transform,null),Is.False);
                Assert.That(physics.CollisionMask(~0)&(1<<RoomPhysicsLayers.Pending),Is.Zero);Assert.That(physics.CollisionMask(~0)&(1<<RoomPhysicsLayers.Scanned),Is.Not.Zero);
                Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
                Assert.That(physics.CollisionMask(~0)&((1<<RoomPhysicsLayers.Pending)|(1<<RoomPhysicsLayers.Scanned)),Is.Zero);
                Assert.That(guard.Shape.enabled,Is.True,"Query filtering must leave the physical safeguard intact");
            }finally{UnityEngine.Object.Destroy(obstacle);}yield return null;
        }
        [UnityTest]public IEnumerator DormantCollisionGeometryEditDisablesStaleGuardUntilNativeGeometryReturns(){
            string id=RecipeTarget(),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var guard=editor.DormantCollisionGuard(id);Assert.That(guard.Known&&guard.HasBounds,Is.True);var shape=guard.Shape;var before=shape.size;
            var changed=editor.Read(id);changed.recipe.parts[0].size*=2;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);Assert.That(guard.Known,Is.False);Assert.That(shape.enabled,Is.False);
            float deadline=Time.realtimeSinceStartup+5;while(!(bool)editor.ObserveAuthoredBounds(id)["prepared"]&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.True);Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.True);
            Assert.That(guard.Known,Is.False,"Prepared visual bounds cannot substitute for accepted native collision");Assert.That(shape.enabled,Is.False);Assert.That(editor.Find(id),Is.Null);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(guard.Shape,Is.Null);var expected=editor.ReadCollisionBounds(id);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);var replacement=editor.DormantCollisionGuard(id);Assert.That(replacement.Known&&replacement.HasBounds&&replacement.Shape.enabled,Is.True);
            Assert.That(replacement.Shape.size,Is.Not.EqualTo(before));Assert.That(replacement.Shape.center,Is.EqualTo(expected.Bounds.center));Assert.That(Vector3.Distance(replacement.Shape.size,expected.Bounds.size+Vector3.one*.004f),Is.LessThan(.00001f));
        }
        [UnityTest]public IEnumerator DormantCollisionWorkspaceReplacementDisposesGuardsBeforePublishingOriginalRoom(){
            yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;var guard=editor.DormantCollisionGuard(id);var shape=guard.Shape;
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);
            var restored=editor.DormantCollisionGuard(id);Assert.That(restored,Is.Not.Null);Assert.That(restored,Is.Not.SameAs(guard));Assert.That(restored.Known,Is.False);Assert.That(restored.Shape,Is.Null);
            Assert.That(guard.Shape,Is.Null);Assert.That(!shape||!shape.enabled,Is.True);yield return null;Assert.That((bool)shape,Is.False);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.DormantCollisionGuard(id),Is.Null);
        }
        [UnityTest]public IEnumerator DormantCollisionAllowsOwnedLocalPreparationButCannotSurviveItsOwner(){
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);var guard=editor.DormantCollisionGuard(id);var shape=guard.Shape;
            physics.SetSurfaces(true,"Aligned scan");Assert.That(physics.SetRunning(true,out error),Is.True,error);
            UnityEngine.Object.Destroy(root);yield return null;Assert.That((bool)shape,Is.False);Assert.That(guard.Shape,Is.Null);
        }
    }
    internal sealed class DormantCollisionCounter:MonoBehaviour {
        internal int Contacts;
        void OnCollisionEnter(Collision collision){if(collision.collider.GetComponent<RoomCollisionProxy>())Contacts++;}
    }
}

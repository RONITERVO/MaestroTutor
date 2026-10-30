// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        IEnumerator GeometryReady(CapabilityOperation operation)
        {
            float end=Time.realtimeSinceStartup+20;
            while(true){var state=operation.State(out var error);Assert.AreNotEqual(RuleActionState.Failed,state,error);if(state==RuleActionState.Ready)yield break;Assert.Less(Time.realtimeSinceStartup,end,"Geometry completion timed out");yield return null;}
        }
        [UnityTest] public IEnumerator PendingGeometryRecipeCapturesQueuedPatchAndAcceptedRevision()
        {
            string target=RecipeTarget(true);var geometry=editor.Find(target).GetComponent<RecipeObject>();float duration=editor.Read(target).recipe.duration;
            var args=(JObject)RecipePatch(target)["arguments"];args["duration"]=duration+1;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.recipe.edit").Module;Assert.AreEqual("completion",module.Duration);
                Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"recipe",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(geometry.IsPlaying);Assert.AreEqual(duration,editor.Read(target).recipe.duration);
                args["duration"]=duration+5;blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(operation);
                int accepted=editor.ObjectRevision(target);Assert.IsFalse(geometry.IsPlaying);Assert.AreEqual(duration+1,editor.Read(target).recipe.duration);
                Assert.AreEqual(duration+1,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).recipe.duration,error);
                Assert.IsTrue(editor.PaintObject(target,Color.red,out error),error);Assert.Greater(editor.ObjectRevision(target),accepted);
                Assert.IsTrue(operation.Complete(out error),error);var result=operation.Result;Assert.AreEqual(accepted,(int)result["revision"]);Assert.AreEqual(duration+1,(float)result["duration"]);Assert.IsFalse((bool)result["playing"]);
                result["duration"]=99;Assert.AreEqual(duration+1,(float)operation.Result["duration"]);
            } finally {operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingGeometryRecipeCancellationPreservesPlayingGeometryAndFiles()
        {
            string target=RecipeTarget(true);var item=editor.Find(target);var geometry=item.GetComponent<RecipeObject>();var prior=geometry.Part("Head");string before=JsonUtility.ToJson(editor.Read(target));byte[] file=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            var args=(JObject)RecipePatch(target)["arguments"];args["duration"]=8;
            Assert.IsTrue(editor.BeginRecipeEdit(target,editor.ObjectRevision(target),args,out _,out var pending,out var error),error);
            yield return Prepared(pending);Assert.IsTrue(geometry.IsPlaying);Assert.AreSame(prior,geometry.Part("Head"));pending.Dispose();yield return Drained(pending);
            Assert.IsFalse(pending.Accepted);Assert.IsTrue(geometry.IsPlaying);Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(target)));Assert.AreEqual(file,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
        }
        [UnityTest] public IEnumerator PendingGeometryRecipeAcceptedPublicationFailureRetainsNewNativeGeometry()
        {
            string target=RecipeTarget(true);var geometry=editor.Find(target).GetComponent<RecipeObject>();float before=editor.Read(target).recipe.duration;byte[] file=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            var args=(JObject)RecipePatch(target)["arguments"];args["duration"]=before+1;using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginRecipeEdit(target,editor.ObjectRevision(target),args,out _,out var pending,out var error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected geometry publication failure");}}),error);
            try {
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);Assert.IsFalse(geometry.IsPlaying);Assert.AreEqual(before+1,editor.Read(target).recipe.duration);
                pause.Release();yield return Rejected(pending);Assert.IsFalse(pending.Published);Assert.AreEqual(file,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.AreEqual(before+1,editor.Read(target).recipe.duration);
                Assert.IsFalse(editor.BeginRecipeEdit(target,editor.ObjectRevision(target),args,out _,out _,out _));
            } finally {pause.Release();pending.Dispose();}
            yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingGeometryCollisionStopDrainsWithoutResettingMotionAndUndo()
        {
            string target=CollisionCup();var item=editor.Find(target);var body=item.GetComponent<Rigidbody>();
            Assert.IsTrue(editor.ConfigurePhysics(target,editor.ObjectRevision(target),new ObjectPhysicsSettings{mode="solid",shape="automatic",mass=.5f},out var error),error);
            physics.SetSurfaces(true,"Synthetic geometry continuity setup");physics.StartPhysics();Assert.IsFalse(body.isKinematic);
            var recipe=JsonUtility.FromJson<CollisionRecipe>(CollisionCall(target)["arguments"]["collision"].ToString());using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginCollisionEdit(target,editor.ObjectRevision(target),recipe,out _,out var pending,out error,pause.Check),error);recipe.shapes=Array.Empty<CollisionShape>();
            try {
                yield return Prepared(pending);Assert.AreEqual(1,item.Grab.colliders.Count);body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;
                Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);Assert.AreEqual(13,item.Grab.colliders.Count);Assert.AreEqual(Vector3.right,body.linearVelocity);Assert.AreEqual(Vector3.up,body.angularVelocity);Assert.IsFalse(body.isKinematic);
                var position=body.position;for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();Assert.Greater(Vector3.Distance(position,body.position),.001f);
                pending.Dispose();StringAssert.Contains("Saving is still pending",pending.InterruptionStatus);
            } finally {pause.Release();pending.Dispose();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.AreEqual(13,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).collision.Pieces,error);
            var now=body.position;var velocity=body.linearVelocity;var spin=body.angularVelocity;editor.Undo();Assert.LessOrEqual(Vector3.Distance(now,body.position),.000001f,"Collider mass recalculation must preserve the body position within one micrometre. Delta="+Vector3.Distance(now,body.position).ToString("R")+" before="+now.ToString("F9")+" after="+body.position.ToString("F9")+" transform="+item.transform.position.ToString("F9"));Assert.AreEqual(velocity,body.linearVelocity);Assert.AreEqual(spin,body.angularVelocity);Assert.AreEqual(1,item.Grab.colliders.Count);Assert.IsNull(editor.Read(target).collision);
        }
        [UnityTest] public IEnumerator PendingGeometryCollisionRejectsStaleQueuedTargetWithoutReplacingColliders()
        {
            string target=CollisionCup();var item=editor.Find(target);var prior=item.Grab.colliders.ToArray();var args=(JObject)CollisionCall(target)["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.collision.edit").Module.Start(new CapabilityContext(editor,animations),"collision",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(editor.PaintObject(target,Color.blue,out error),error);
                blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsEmpty(operation.Result);CollectionAssert.AreEqual(prior,item.Grab.colliders);Assert.IsNull(editor.Read(target).collision);
            } finally {operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingGeometryModelCancellationAndCapturedSettingsKeepExactReadiness()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var item=editor.Find(geometryTarget);var view=item.GetComponent<CreatedRoomObject>();var prior=item.Grab.colliders.Single();var settings=BuildingSettings();
            Assert.IsTrue(editor.BeginModelGeometry(geometryTarget,editor.ObjectRevision(geometryTarget),settings,out var cancelled,out var error),error);yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreSame(prior,item.Grab.colliders.Single());Assert.IsTrue(view.ModelGeometryReady);Assert.IsFalse(editor.Read(geometryTarget).modelGeometry.meshCollision);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out error),error);CapabilityOperation operation=null;var args=(JObject)GeometryCall(settings)["arguments"];
            try {
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.model.geometry.set").Module.Start(new CapabilityContext(editor,animations),"geometry",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);args["settings"]["metresPerUnit"]=10;
                blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(operation);Assert.IsTrue(view.ModelGeometryReady,view.ModelGeometryIssue);Assert.IsInstanceOf<MeshCollider>(item.Grab.colliders.Single());Assert.AreEqual(6,view.Model.LocalBounds.size.x,.001);
                int accepted=editor.ObjectRevision(geometryTarget);Assert.IsTrue(editor.PaintObject(geometryTarget,Color.green,out error),error);Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(accepted,(int)operation.Result["revision"]);
                Assert.IsTrue(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==geometryTarget).modelGeometry.walkable,error);
            } finally {operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingGeometryModelRefusesPhysicsStartedWhileWaiting()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var prior=editor.Find(geometryTarget).Grab.colliders.Single();var args=(JObject)GeometryCall(BuildingSettings())["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.model.geometry.set").Module.Start(new CapabilityContext(editor,animations),"geometry",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);physics.SetSurfaces(true,"Synthetic queued model physics setup");physics.StartPhysics();Assert.IsTrue(physics.Running);
                Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("Pause physics",error);Assert.IsEmpty(operation.Result);Assert.AreSame(prior,editor.Find(geometryTarget).Grab.colliders.Single());Assert.IsFalse(editor.Read(geometryTarget).modelGeometry.meshCollision);
            } finally {operation?.Stop(false);blocker.Dispose();physics.PausePhysics();}
            yield return Drained(blocker);
        }
        [UnityTest] public IEnumerator PendingGeometryModelUnloadedDuringAcceptanceCannotReportSuccess()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var view=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>();
            Assert.IsTrue(editor.BeginModelGeometry(geometryTarget,editor.ObjectRevision(geometryTarget),BuildingSettings(),out var pending,out var error),error);
            bool unloaded=false;Action callback=()=>{
                if(unloaded||!editor.Read(geometryTarget).modelGeometry.meshCollision||!view.ModelGeometryReady)return;
                unloaded=true;UnityEngine.Object.DestroyImmediate(view.Model);
            };
            try {
                yield return Prepared(pending);editor.Changed+=callback;
                yield return Rejected(pending);Assert.IsTrue(unloaded);Assert.IsTrue(pending.Accepted);
                Assert.IsFalse(pending.Complete(out error));StringAssert.Contains("native application is unavailable",error);
                Assert.IsFalse(view.ModelGeometryReady);Assert.IsTrue(editor.Read(geometryTarget).modelGeometry.meshCollision);
            } finally {editor.Changed-=callback;pending.Dispose();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            Assert.IsTrue(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==geometryTarget).modelGeometry.meshCollision,error);
            Assert.IsFalse(pending.Complete(out error),"Confirmed saving alone cannot turn failed native readiness into success");
        }
        [UnityTest] public IEnumerator PendingGeometryNativeQuotaRefusalKeepsPublishedModelAndCanRetry()
        {
            root.AddComponent<RuntimeDiagnostics>();yield return GeometryModel(BuildingModelFixture.Create());var item=editor.Find(geometryTarget);var view=item.GetComponent<CreatedRoomObject>();var prior=item.Grab.colliders.Single();var model=view.Model;
            byte[] file=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.ObjectRevision(geometryTarget);var leases=FillCollisionAdmission();RoomEditor.PendingPersistedEdit pending=null;
            try {
                Assert.IsTrue(editor.BeginModelGeometry(geometryTarget,revision,BuildingSettings(),out pending,out var error),error);yield return Prepared(pending);yield return Rejected(pending);
                Assert.IsFalse(pending.Accepted);Assert.AreEqual(revision,editor.ObjectRevision(geometryTarget));Assert.AreSame(model,view.Model);Assert.IsTrue(view.ModelGeometryReady);Assert.AreSame(prior,item.Grab.colliders.Single());Assert.AreEqual(file,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            } finally {pending?.Dispose();foreach(var lease in leases)lease.Retire();}
            if(pending!=null)yield return Drained(pending);yield return ContainerAction(new RoomAgentExecutor(editor),GeometryCall(BuildingSettings()));Assert.IsTrue(view.ModelGeometryReady,view.ModelGeometryIssue);Assert.IsInstanceOf<MeshCollider>(item.Grab.colliders.Single());
        }
    }
}

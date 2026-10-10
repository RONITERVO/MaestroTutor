// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator GeometryTransactionMixedEditRefusesNativeFailureBeforeAnySourceOrPoseChanges()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var model=editor.Read(geometryTarget);model.modelGeometry=new(){scaleMode="source",metresPerUnit=100};
            var moved=editor.Read(editor.Identity(block));moved.position+=Vector3.right;var before=JsonUtility.ToJson(editor.Snapshot());var position=block.transform.position;int revision=editor.Revision;
            Assert.IsFalse(editor.ApplyAgentEdit(revision,new[]{moved,model},Array.Empty<string>(),out var error),"A native model scale failure must reject the whole batch");
            StringAssert.Contains("12.5",error);Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));Assert.AreEqual(position,block.transform.position);Assert.AreEqual(revision,editor.Revision);
            yield return null;
        }
        [UnityTest] public IEnumerator GeometryTransactionUndoRefusalLeavesCurrentSourceAndHistoryAvailable()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var ex=new RoomAgentExecutor(editor);ContainerRun(ex,GeometryCall(BuildingSettings()));ContainerRun(ex,GeometryCall(new RoomModelGeometry()));
            var model=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>().Model;Assert.IsTrue(model.CollisionMeshes(out var filters,out _));foreach(var f in filters)f.sharedMesh.UploadMeshData(true);
            var before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;bool redo=editor.CanRedo;editor.Undo();
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()),"Unavailable native geometry must not consume Undo");Assert.AreEqual(revision,editor.Revision);Assert.IsTrue(editor.CanUndo);Assert.AreEqual(redo,editor.CanRedo);StringAssert.Contains("readable",editor.Status);yield return null;
        }
        [UnityTest] public IEnumerator GeometryTransactionRedoRefusalLeavesCurrentSourceAndHistoryAvailable()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var ex=new RoomAgentExecutor(editor);ContainerRun(ex,GeometryCall(BuildingSettings()));editor.Undo();
            var model=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>().Model;Assert.IsTrue(model.CollisionMeshes(out var filters,out _));foreach(var f in filters)f.sharedMesh.UploadMeshData(true);
            var before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;editor.Redo();
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()),"Unavailable native geometry must not consume Redo");Assert.AreEqual(revision,editor.Revision);Assert.IsTrue(editor.CanRedo);StringAssert.Contains("readable",editor.Status);yield return null;
        }
        [UnityTest] public IEnumerator GeometryTransactionPhysicsPauseGuardAppliesToMixedEditsUndoAndRedo()
        {
            yield return GeometryModel(BuildingModelFixture.Create());physics.SetSurfaces(true,"Synthetic transaction floor");physics.StartPhysics();Assert.IsTrue(physics.Running);
            var geometry=editor.Read(geometryTarget);geometry.modelGeometry=BuildingSettings();string original=JsonUtility.ToJson(editor.Snapshot());
            Assert.IsFalse(editor.ApplyAgentEdit(editor.Revision,new[]{geometry},Array.Empty<string>(),out var error));StringAssert.Contains("Pause physics",error);Assert.AreEqual(original,JsonUtility.ToJson(editor.Snapshot()));
            physics.PausePhysics();var ex=new RoomAgentExecutor(editor);ContainerRun(ex,GeometryCall(BuildingSettings()));ContainerRun(ex,GeometryCall(new RoomModelGeometry()));
            string fitted=JsonUtility.ToJson(editor.Snapshot());physics.StartPhysics();Assert.IsTrue(physics.Running);editor.Undo();StringAssert.Contains("Pause physics",editor.Status);Assert.AreEqual(fitted,JsonUtility.ToJson(editor.Snapshot()));Assert.IsTrue(editor.CanUndo);
            physics.PausePhysics();editor.Undo();string rigid=JsonUtility.ToJson(editor.Snapshot());Assert.IsTrue(editor.Read(geometryTarget).modelGeometry.meshCollision);
            physics.StartPhysics();Assert.IsTrue(physics.Running);editor.Redo();StringAssert.Contains("Pause physics",editor.Status);Assert.AreEqual(rigid,JsonUtility.ToJson(editor.Snapshot()));Assert.IsTrue(editor.CanRedo);
            physics.PausePhysics();editor.Redo();Assert.AreEqual(fitted,JsonUtility.ToJson(editor.Snapshot()));yield return null;
        }
        [UnityTest] public IEnumerator GeometryTransactionSuccessfulBatchPersistsAndUndoesAsOneEdit()
        {
            root.AddComponent<RuntimeDiagnostics>();yield return GeometryModel(BuildingModelFixture.Create());string original=JsonUtility.ToJson(editor.Snapshot());
            var geometry=editor.Read(geometryTarget);geometry.modelGeometry=BuildingSettings();var moved=editor.Read(editor.Identity(block));moved.position+=Vector3.right;
            Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{geometry,moved},Array.Empty<string>(),out var error),error);Assert.IsTrue(editor.TryFlush(out error),error);
            var owner=CollisionOwners(geometryTarget).Single();Assert.AreEqual("ready",(string)owner["phase"]);Assert.AreEqual(1,(int)owner["colliders"]);
            Assert.AreEqual(moved.position,editor.Read(moved.id).position);Assert.IsTrue(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==geometryTarget).modelGeometry.meshCollision);
            editor.Undo();Assert.AreEqual(original,JsonUtility.ToJson(editor.Snapshot()));Assert.IsTrue(editor.CanRedo);yield return null;Assert.IsEmpty(CollisionOwners(geometryTarget));
            editor.Redo();Assert.IsTrue(editor.TryFlush(out error),error);yield return null;
            var restored=CollisionOwners(geometryTarget).Single();Assert.AreEqual("ready",(string)restored["phase"]);Assert.AreNotEqual((string)owner["leaseId"],(string)restored["leaseId"]);Assert.AreEqual(moved.position,block.transform.localPosition);
            Assert.IsTrue(editor.Find(geometryTarget).GetComponent<CreatedRoomObject>().ModelGeometryReady);
        }
        [UnityTest] public IEnumerator GeometryTransactionMixedPreparationFailureReleasesCandidatesAndPreservesAcceptedOwners()
        {
            root.AddComponent<RuntimeDiagnostics>();yield return GeometryModel(BuildingModelFixture.Create());string first=geometryTarget;
            yield return GeometryModel(ModelFixture.Create(avatar:true));string second=geometryTarget;
            var a=editor.Read(first);a.modelGeometry=BuildingSettings();var b=editor.Read(second);b.modelGeometry=BuildingSettings();string before=JsonUtility.ToJson(editor.Snapshot());
            Assert.IsFalse(editor.ApplyAgentEdit(editor.Revision,new[]{a,b},Array.Empty<string>(),out _),"A skin cannot silently accept rigid collision in a batch");
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));yield return null;Assert.IsEmpty(CollisionOwners(first));Assert.IsEmpty(CollisionOwners(second));
            Assert.IsTrue(editor.Find(first).GetComponent<CreatedRoomObject>().ModelGeometryReady);Assert.IsTrue(editor.Find(second).GetComponent<CreatedRoomObject>().ModelGeometryReady);
        }
    }
}

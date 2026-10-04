// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Maestro.Quest.Tests
{
    public sealed class TemporaryRoomTests
    {
        GameObject root;
        string directory;
        RoomEditor editor;
        RoomInteraction room;
        RoomPhysicsWorld physics;
        string Primary=>Path.Combine(directory,"room.v15.json");
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroTemporaryTests-"+Guid.NewGuid().ToString("N"));
            root=new GameObject("Temporary room test");root.AddComponent<XRInteractionManager>();
            room=root.AddComponent<RoomInteraction>();physics=root.AddComponent<RoomPhysicsWorld>();
            var book=Included("book");var avatar=Included("maestro");
            editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,avatar,directory,physics);
            yield return null;
        }
        RoomItem Included(string name)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root.transform,false);
            var item=obj.AddComponent<RoomItem>();item.Configure(new[]{obj.GetComponent<Collider>()});room.Register(item);return item;
        }
        string Create(string name)
        {
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,name,new Vector3(.3f,1,.7f),1,Color.white,out var id,out var error),Is.True,error);
            return id;
        }
        RoomDocument Saved()=>new RoomStorage(directory).Load(out _);
        IEnumerator FinishSave()
        {
            float deadline=Time.realtimeSinceStartup+5;
            while(editor.TemporarySavePending && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.TemporarySavePending,Is.False,"Snapshot writer did not finish");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(root)UnityEngine.Object.Destroy(root);yield return null;
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        [UnityTest] public IEnumerator EditsPhysicsAutosavePauseAndDestroyCannotLeakTemporaryData()
        {
            var original=Create("Kept block");
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishSave();
            var bytes=File.ReadAllBytes(Primary);var saved=Saved();
            Assert.That(editor.BeginTemporaryRoom(out _),Is.False);
            // If a per-action save or lifecycle flush leaks through, this path
            // makes it fail. Temporary edits must remain fully usable anyway.
            Directory.CreateDirectory(Primary+".pending");
            var temp=Create("Temporary block");
            Assert.That(editor.MoveObject(original,new Vector3(.6f,1.2f,.8f),out error),Is.True,error);
            Assert.That(editor.PaintObject(original,Color.red,out error),Is.True,error);
            Assert.That(editor.AddDrawing(new[]{Vector3.zero,Vector3.right*.1f},Color.blue),Is.True);
            var replacement=editor.Read(original);replacement.name="Agent renamed";
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{replacement},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.SetItemPhysics(temp,new ObjectPhysicsSettings {mode="solid",shape="box",mass=2}),Is.True);
            physics.SetSurfaces(true,"Synthetic aligned scan");Assert.That(physics.SetRunning(true,out error),Is.True,error);
            editor.Find(temp).transform.localPosition=new Vector3(.4f,1.4f,.8f);
            editor.RememberPlacement(temp);
            yield return new WaitForSeconds(1.2f);
            editor.SendMessage("OnApplicationFocus",false);editor.SendMessage("OnApplicationPause",true);
            Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(bytes));
            UnityEngine.Object.Destroy(editor);yield return null;
            Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(bytes));
            Assert.That(Saved().objects.Length,Is.EqualTo(saved.objects.Length));
            Assert.That(Saved().objects.Single(x=>x.id==original).name,Is.EqualTo("Kept block"));
        }
        [UnityTest] public IEnumerator KeepCapturesOneSnapshotWithoutReplayingLaterEditsAndHasOneSavedUndo()
        {
            var existing=Create("Existing");
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishSave();
            var first=Create("First");var second=Create("Second");
            Assert.That(editor.PaintObject(existing,Color.blue,out error),Is.True,error);
            Assert.That(editor.DeleteObject(first,out error),Is.True,error);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);
            // Same frame: the writer owns a detached snapshot even if its task
            // has already returned before the next editor Update.
            var later=Create("Later");Assert.That(editor.PaintObject(second,Color.red,out error),Is.True,error);
            var before=editor.Find(second).transform.localPosition;
            yield return FinishSave();Assert.That(editor.TemporarySaveError,Is.Null);
            Assert.That(editor.TemporaryRoom,Is.True);Assert.That(editor.TemporarySaveRevision,Is.EqualTo(1));
            Assert.That(Saved().objects.Any(x=>x.id==later),Is.False);
            Assert.That(Saved().objects.Single(x=>x.id==second).color,Is.EqualTo(Color.white));
            Assert.That(editor.Read(second).color,Is.EqualTo(Color.red));
            Assert.That(editor.Find(second).transform.localPosition,Is.EqualTo(before));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.Find(later),Is.Null);Assert.That(editor.Read(second).color,Is.EqualTo(Color.white));
            editor.Undo();Assert.That(editor.Find(second),Is.Null);Assert.That(editor.Read(existing).color,Is.EqualTo(Color.white));
            Assert.That(editor.Find(first),Is.Null,"Created then removed within the group stays absent");
            editor.Undo();Assert.That(editor.Find(existing),Is.Null,"Older saved Undo history is preserved");
            editor.Redo();editor.Redo();Assert.That(editor.Read(existing).color,Is.EqualTo(Color.blue));
            Assert.That(editor.Read(second),Is.Not.Null);yield return null;
        }
        [UnityTest] public IEnumerator SaveFailureKeepsTheLiveForkAndCanBeRetriedWithoutNewObjects()
        {
            Create("Base");Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishSave();
            var bytes=File.ReadAllBytes(Primary);var id=Create("Keep me");
            Directory.CreateDirectory(Primary+".pending");
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return FinishSave();
            Assert.That(editor.TemporarySaveError,Is.Not.Null);Assert.That(editor.TemporarySaveRevision,Is.Zero);
            Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(bytes));Assert.That(editor.Find(id),Is.Not.Null);
            Assert.That(editor.PaintObject(id,Color.green,out error),Is.True,error);
            Directory.Delete(Primary+".pending");
            editor.SaveNow();yield return FinishSave();
            Assert.That(editor.TemporarySaveError,Is.Null);Assert.That(editor.TemporarySaveRevision,Is.EqualTo(1));
            Assert.That(Saved().objects.Count(x=>x.id==id),Is.EqualTo(1));
            Assert.That(Saved().objects.Single(x=>x.id==id).color,Is.EqualTo(Color.green));
        }
        [UnityTest] public IEnumerator DiscardStopsPhysicsRestoresPlacementsAndInvalidatesOldObservations()
        {
            var id=Create("Base");var before=editor.Read(id);int initial=editor.ObjectRevision(id);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishSave();
            Assert.That(editor.MoveObject(id,new Vector3(.5f,1.5f,.8f),out error),Is.True,error);
            int temporary=editor.ObjectRevision(id);Assert.That(temporary,Is.GreaterThan(initial));
            var removed=Create("Remove me");editor.Select(editor.Find(removed));
            physics.SetSurfaces(true,"Synthetic aligned scan");Assert.That(physics.SetRunning(true,out error),Is.True,error);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(physics.Running,Is.False);Assert.That(editor.TemporaryRoom,Is.False);Assert.That(editor.SelectedId,Is.Null);
            Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(before.position));
            Assert.That(editor.ObjectRevision(id),Is.GreaterThan(temporary));Assert.That(editor.Find(removed),Is.Null);
            Assert.That(editor.KeepTemporaryRoom(out _),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator HandOwnershipBlocksBoundaryChangesAndReleaseRemainsTemporary()
        {
            var id=Create("Hold me");var item=editor.Find(id);
            var hand=new GameObject("Hand");hand.SetActive(false);hand.transform.SetParent(root.transform,false);
            var ray=hand.AddComponent<XRRayInteractor>();ray.interactionManager=root.GetComponent<XRInteractionManager>();hand.SetActive(true);
            yield return null;
            var manager=root.GetComponent<XRInteractionManager>();manager.SelectEnter((IXRSelectInteractor)ray,item.Grab);
            Assert.That(editor.BeginTemporaryRoom(out _),Is.False);manager.SelectExit((IXRSelectInteractor)ray,item.Grab);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishSave();
            manager.SelectEnter((IXRSelectInteractor)ray,item.Grab);
            Assert.That(editor.KeepTemporaryRoom(out _),Is.False);Assert.That(editor.DiscardTemporaryRoom(out _),Is.False);
            manager.SelectExit((IXRSelectInteractor)ray,item.Grab);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);yield return null;
        }
        [UnityTest] public IEnumerator FailedBaselineKeepsTheForkUntilExplicitDiscardAndRestoresPriorUndo()
        {
            var id=Create("Persisted first");var bytes=File.ReadAllBytes(Primary);
            // A dirty placement must survive failure/discard, not revert to the older disk file.
            editor.Find(id).transform.localPosition=Vector3.up*2;editor.RememberPlacement(id);
            Directory.CreateDirectory(Primary+".pending");
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);var temp=Create("Trial during baseline");
            yield return FinishSave();Assert.That(editor.TemporarySaveError,Does.Contain("Starting room was not saved"));
            Assert.That(editor.TemporaryRoom,Is.True);Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.Find(temp),Is.Not.Null);
            Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(bytes));
            Directory.Delete(Primary+".pending");
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.Find(temp),Is.Null);Assert.That(editor.Read(id).position,Is.EqualTo(Vector3.up*2));
            editor.SendMessage("OnApplicationPause",true);
            Assert.That(Saved().objects.Single(x=>x.id==id).position,Is.EqualTo(Vector3.up*2));
            editor.Undo();Assert.That(editor.Find(id),Is.Null,"Previous saved Undo remains available");
        }
        [UnityTest] public IEnumerator SavedAndHistoricalMotionReferencesRemainProtectedAcrossTheFork()
        {
            string previous=Guid.NewGuid().ToString("N"),saved=Guid.NewGuid().ToString("N"),live=Guid.NewGuid().ToString("N");
            void Assign(string id) {
                var data=editor.Read("maestro");data.walkMotionId=id;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var issue),Is.True,issue);
            }
            Assign(previous);Assign(saved);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishSave();Assign(live);
            Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(live));
            Assert.That(editor.UsesMotion(saved),Is.True,"Discard still needs the base's motion");
            Assert.That(editor.UsesMotion(live),Is.True);
            Assert.That(editor.HistoricalMotion(previous),Is.True,"The base's earlier Undo must still protect its motion");
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);
            for(int i=0;i<40;i++)Assign(Guid.NewGuid().ToString("N"));
            Assert.That(editor.HistoricalMotion(live),Is.False,"Evict the in-flight snapshot's motion from local Undo");
            Assert.That(editor.UsesMotion(live),Is.True,"The pending snapshot must retain its own referenced assets");
            yield return FinishSave();
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(live));
            editor.Undo();Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(saved));
            editor.Undo();Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(previous));
        }
        [UnityTest] public IEnumerator PauseFinishesOnlyTheDispatchedSnapshotAndOrdinaryCreationStillSaves()
        {
            var baseId=Create("Ordinary");Assert.That(Saved().objects.Any(x=>x.id==baseId),Is.True);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishSave();var kept=Create("Keep on pause");
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);var late=Create("Do not keep on pause");
            editor.SendMessage("OnApplicationPause",true);
            Assert.That(editor.TemporarySavePending,Is.False);Assert.That(editor.TemporarySaveError,Is.Null);
            Assert.That(Saved().objects.Any(x=>x.id==kept),Is.True);Assert.That(Saved().objects.Any(x=>x.id==late),Is.False);
            Assert.That(editor.Find(late),Is.Not.Null);yield return null;
        }
    }
}

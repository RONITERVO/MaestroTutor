// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using Unity.Profiling;
namespace Maestro.Quest.Tests
{
    public sealed class RoomObjectObservationTests
    {
        const string DrawingId="dddddddddddddddddddddddddddddddd";
        static RoomJournal Journal(int points=2) => new(new RoomDocument {version=RoomDocument.CurrentVersion,objects=new[] {
            new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro},
            new RoomObjectData {id="book",kind=RoomObjectKind.Book},
            new RoomObjectData {id=DrawingId,name="My drawing",kind=RoomObjectKind.Drawing,physics=ItemPhysics.Bouncy,collisionShape=ItemCollider.Sphere,
                mass=2,scale=1.5f,color=Color.cyan,position=Vector3.up,points=Enumerable.Range(0,points).Select(i=>Vector3.right*(i/(float)(points-1))).ToArray()}
        }});
        [Test] public void ObservationIsDetachedAndTracksEditsUndoAndLivePlacementRevisions()
        {
            var journal=Journal();var before=JsonUtility.ToJson(journal.Snapshot());var values=journal.ObserveObjects();
            Assert.That(values.Select(x=>x.id),Is.EqualTo(new[]{"book",DrawingId,"maestro"}));
            var drawing=values[1];Assert.That(drawing.name,Is.EqualTo("My drawing"));Assert.That(drawing.kind,Is.EqualTo("Drawing"));
            Assert.That(drawing.position,Is.EqualTo(Vector3.up));Assert.That(drawing.scale,Is.EqualTo(1.5f));Assert.That(drawing.color,Is.EqualTo(Color.cyan));
            Assert.That(drawing.physics.mode,Is.EqualTo("bouncy"));Assert.That(drawing.physics.shape,Is.EqualTo("sphere"));Assert.That(drawing.physics.mass,Is.EqualTo(2));
            Assert.That(drawing.movement,Is.Null);Assert.That(values[2].movement.distance,Is.EqualTo(1.3f));Assert.That(values[2].movement.speed,Is.EqualTo(.65f));
            int revision=drawing.objectRevision;drawing.name="Changed outside";drawing.physics.mass=20;values[2].movement.speed=1;values[0]=null;
            Assert.That(JsonUtility.ToJson(journal.Snapshot()),Is.EqualTo(before),"Observers cannot edit saved state or history");
            Assert.That(journal.UpdatePlacement(DrawingId,Vector3.right,Quaternion.identity),Is.True);
            var moved=journal.ObserveObjects()[1];Assert.That(moved.position,Is.EqualTo(Vector3.right));Assert.That(moved.objectRevision,Is.GreaterThan(revision));
            var edited=journal.Read(DrawingId);edited.name="Edited";Assert.That(journal.Apply(new[]{edited},Array.Empty<string>(),out var error),Is.True,error);
            var after=journal.ObserveObjects()[1];Assert.That(after.name,Is.EqualTo("Edited"));Assert.That(after.objectRevision,Is.GreaterThan(moved.objectRevision));
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.ObserveObjects()[1].name,Is.EqualTo("My drawing"));
            Assert.That(journal.ObserveObjects()[1].objectRevision,Is.GreaterThan(after.objectRevision));
            Assert.That(journal.Redo(),Is.True);Assert.That(journal.ObserveObjects()[1].name,Is.EqualTo("Edited"));
        }
        static RoomJournal RecipeJournal(int parts)
        {
            var document=Journal().Snapshot();var item=document.objects.Single(x=>x.id==DrawingId);
            item.kind=RoomObjectKind.Assembly;item.points=null;
            item.recipe=new RoomRecipe {parts=Enumerable.Range(0,parts).Select(i=>new RecipePart {id="part_"+i}).ToArray()};
            return new RoomJournal(document);
        }
        static long Allocations(RoomJournal journal)
        {
            for(int i=0;i<8;i++)journal.ObserveObjects();
            using var recorder=new ProfilerRecorder(ProfilerCategory.Memory,"GC.Alloc",1,
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame|ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid,Is.True,"Allocation measurement must be available");
            recorder.Start();for(int i=0;i<32;i++)journal.ObserveObjects();recorder.Stop();
            Assert.That(recorder.Count,Is.GreaterThan(0),"An unsupported counter must not silently pass");
            return recorder.GetSample(0).Count/32;
        }
        [Test] public void ObjectListAllocationsDoNotGrowWithRecipeDetail()
        {
            var small=RecipeJournal(1);var detailed=RecipeJournal(32);
            long smallCount=Allocations(small),detailedCount=Allocations(detailed);
            TestContext.WriteLine($"Object list allocations: {smallCount} (1 part), {detailedCount} (32 parts)");
            Assert.That(smallCount,Is.GreaterThan(0));
            Assert.That(detailedCount-smallCount,Is.LessThanOrEqualTo(2),"The object list must not copy hidden recipe geometry");
        }
    }
}

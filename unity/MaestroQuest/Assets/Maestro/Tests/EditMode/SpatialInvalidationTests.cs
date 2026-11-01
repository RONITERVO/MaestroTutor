// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class SpatialInvalidationTests {
        static readonly string Cold=new('a',32),Live=new('b',32);
        static HashSet<string> Targets(params string[] ids)=>new(ids,StringComparer.Ordinal);
        static RoomDocument Document()=>new(){version=1,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=Cold,kind=RoomObjectKind.Block},new RoomObjectData{id=Live,kind=RoomObjectKind.Block}}};
        static RoomSpatialDirectoryState State(RoomJournal journal,RoomSpatialIndex index=null)=>new(journal,index,0,new[]{Cold});
        static RoomSpatialIndex Index()=>new((h,c)=>Task.FromResult(ModelAssetBounds.Unknown("Fixture")));
        static void Drain(RoomSpatialIndex index){for(int i=0;i<100&&index.Pending;i++)index.Tick();Assert.That(index.Pending,Is.False);}
        [Test] public void SpatialChangeWindowPreservesExactBoundaryAndRejectsLostHistory(){
            var changes=new RoomSpatialChanges();var targets=Targets(Cold);
            for(int i=0;i<RoomSpatialChanges.Capacity;i++)changes.Record(Live);
            Assert.That(changes.Affects(0,targets),Is.False);changes.Record(Live);
            Assert.That(changes.Affects(0,targets),Is.True,"Lost history cannot establish irrelevance");
            Assert.That(changes.Affects(1,targets),Is.False);long now=changes.Version;changes.Record(Cold);
            Assert.That(changes.Affects(now,targets),Is.True);Assert.That(changes.Affects(now,Targets(Live)),Is.False);
            Assert.That(changes.Affects(-1,targets),Is.True);Assert.That(changes.Affects(changes.Version+1,targets),Is.True);
            Assert.That(changes.Affects(0,Targets()),Is.False);Assert.Throws<ArgumentException>(()=>changes.Record(null));
        }
        [Test] public void SpatialDirectoryConsumesLivePlacementWithoutLosingAColdChange(){
            var journal=new RoomJournal(Document());var current=State(journal);var lagging=State(journal);var stamp=journal.SpatialCatalogStamp;
            for(int i=0;i<300;i++){
                Assert.That(journal.UpdatePlacement(Live,Vector3.right*(i%2==0?.1f:.2f),Quaternion.identity,true),Is.True);
                Assert.That(current.Current(journal,null,0),Is.True);
            }
            Assert.That(journal.SpatialCatalogStamp,Is.EqualTo(stamp));Assert.That(lagging.Current(journal,null,0),Is.False);
            Assert.That(journal.UpdatePlacement(Cold,Vector3.up,Quaternion.identity,true),Is.True);Assert.That(current.Current(journal,null,0),Is.False);
        }
        [Test] public void SpatialDirectoryIgnoresWorldSettingsButRejectsAuthoredChangesUndoAndMembership(){
            var journal=new RoomJournal(Document());var state=State(journal);var settings=journal.Lighting;settings.sunIntensity=.8f;
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,lighting:settings),Is.True,error);
            Assert.That(state.Current(journal,null,0),Is.True);journal.Undo();Assert.That(state.Current(journal,null,0),Is.True);
            var item=journal.Read(Live);item.color=Color.blue;
            Assert.That(journal.Apply(new[]{item},Array.Empty<string>(),out error),Is.True,error);Assert.That(state.Current(journal,null,0),Is.False,"All authored object fields remain conservative");
            state=State(journal);journal.Undo();Assert.That(state.Current(journal,null,0),Is.False);
            state=State(journal);Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[]{Cold},out error),Is.True,error);Assert.That(state.Current(journal,null,0),Is.False);
            state=State(journal);journal.Undo();Assert.That(state.Current(journal,null,0),Is.False);
        }
        [Test] public void SpatialDirectoryKeepsForkIdentityResidencyAndMetadataOwnership(){
            var journal=new RoomJournal(Document());var state=State(journal);var fork=journal.Fork();
            Assert.That(fork.UpdatePlacement(Cold,Vector3.up,Quaternion.identity),Is.True);
            Assert.That(state.Current(journal,null,0),Is.True,"A fork's shared observation clock is not the parent's geometry");
            Assert.That(state.Current(fork,null,0),Is.False);Assert.That(state.Current(journal,null,1),Is.False);
            using var index=Index();Assert.That(state.Current(journal,index,0),Is.False);
        }
        [Test] public void SpatialMetadataPublishesOnlyChangedIdentitiesAndNoOpSynchronizationStaysCurrent(){
            var document=Document();using var index=Index();index.Synchronize(document);Drain(index);
            long before=index.Generation;index.Synchronize(document.Copy());Assert.That(index.Generation,Is.EqualTo(before));
            document.objects[3].color=Color.blue;index.Synchronize(document);
            Assert.That(index.ChangesAffect(before,Targets(Cold)),Is.False);Assert.That(index.ChangesAffect(before,Targets(Live)),Is.True);
            before=index.Generation;Drain(index);Assert.That(index.ChangesAffect(before,Targets(Cold)),Is.False);Assert.That(index.ChangesAffect(before,Targets(Live)),Is.True);
            before=index.Generation;document.objects[2].color=Color.red;index.Synchronize(document);Assert.That(index.ChangesAffect(before,Targets(Cold)),Is.True);
            Drain(index);before=index.Generation;document.objects=Array.FindAll(document.objects,x=>x.id!=Cold);index.Synchronize(document);Assert.That(index.ChangesAffect(before,Targets(Cold)),Is.True);
        }
        [Test] public void SpatialDirectoryConsumesUnrelatedMetadataButRejectsRequiredPublication(){
            var document=Document();var journal=new RoomJournal(document);using var index=Index();index.Synchronize(document);Drain(index);var state=State(journal,index);
            document.objects[3].color=Color.blue;index.Synchronize(document);Assert.That(state.Current(journal,index,0),Is.True);Drain(index);Assert.That(state.Current(journal,index,0),Is.True);
            document.objects[2].color=Color.red;index.Synchronize(document);Assert.That(state.Current(journal,index,0),Is.False);
            state=State(journal,index);Drain(index);Assert.That(state.Current(journal,index,0),Is.False);
        }
        [Test] public void SpatialIdentityCollectionValidatesEmptyAndLostWindowsAndOwnsItsResults(){
            var changes=new RoomSpatialChanges();
            Assert.That(changes.TryCollect(0,out var empty,out var end),Is.True);Assert.That(empty,Is.Empty);Assert.That(end,Is.Zero);
            Assert.That(changes.TryCollect(-1,out _,out _),Is.False);Assert.That(changes.TryCollect(1,out _,out _),Is.False);
            for(int i=0;i<RoomSpatialChanges.Capacity;i++)changes.Record(i%3==0?Cold:Live);
            Assert.That(changes.TryCollect(0,out var exact,out end),Is.True);CollectionAssert.AreEqual(new[]{Cold,Live},exact);Assert.That(end,Is.EqualTo(RoomSpatialChanges.Capacity));
            exact[0]="caller mutation";Assert.That(changes.TryCollect(0,out exact,out _),Is.True);CollectionAssert.AreEqual(new[]{Cold,Live},exact);
            changes.Record(Live);Assert.That(changes.TryCollect(0,out _,out _),Is.False,"An empty prior directory cannot excuse missing history");
            Assert.That(changes.TryCollect(1,out _,out _),Is.True);Assert.That(changes.TryCollect(changes.Version,out empty,out end),Is.True);Assert.That(empty,Is.Empty);Assert.That(end,Is.EqualTo(changes.Version));
            Assert.That(changes.TryCollect(changes.Version+1,out _,out _),Is.False);
        }
        [Test] public void SpatialIdentityCollectionDoesNotLetFastConsumersConsumeSlowHistory(){
            var changes=new RoomSpatialChanges();long fast=0;
            for(int i=0;i<RoomSpatialChanges.Capacity+1;i++){
                changes.Record(Live);Assert.That(changes.TryCollect(fast,out var ids,out fast),Is.True);CollectionAssert.AreEqual(new[]{Live},ids);
            }
            Assert.That(changes.TryCollect(0,out _,out _),Is.False);changes.Record(Cold);
            Assert.That(changes.TryCollect(fast,out var final,out _),Is.True);CollectionAssert.AreEqual(new[]{Cold},final);
        }
        [Test] public void SpatialAuthoredDeltasUnionInsertRemoveMultiEditUndoAndRedoWithoutWorldSettings(){
            var journal=new RoomJournal(Document());long cursor=journal.SpatialAuthoredVersion;
            string inserted=new('c',32);var replacement=journal.Read(Live);replacement.name="Changed live name";
            var added=new RoomObjectData{id=inserted,kind=RoomObjectKind.Block};
            Assert.That(journal.Apply(new[]{replacement,added},new[]{Cold},out var error),Is.True,error);
            Assert.That(journal.TrySpatialAuthoredChanges(cursor,out var ids,out cursor),Is.True);CollectionAssert.AreEqual(new[]{Cold,Live,inserted},ids);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.TrySpatialAuthoredChanges(cursor,out ids,out cursor),Is.True);CollectionAssert.AreEqual(new[]{Cold,Live,inserted},ids);
            Assert.That(journal.Read(inserted),Is.Null);Assert.That(journal.Read(Cold),Is.Not.Null);
            Assert.That(journal.Redo(),Is.True);Assert.That(journal.TrySpatialAuthoredChanges(cursor,out ids,out cursor),Is.True);CollectionAssert.AreEqual(new[]{Cold,Live,inserted},ids);
            var lighting=journal.Lighting;lighting.sunIntensity=.75f;
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,lighting:lighting),Is.True,error);
            Assert.That(journal.TrySpatialAuthoredChanges(cursor,out ids,out var unchanged),Is.True);Assert.That(ids,Is.Empty);Assert.That(unchanged,Is.EqualTo(cursor));
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.TrySpatialAuthoredChanges(cursor,out ids,out _),Is.True);Assert.That(ids,Is.Empty);
        }
        [Test] public void SpatialAuthoredAndPlacementWindowsHaveIndependentCursorsAndInvalidEditsPublishNothing(){
            var journal=new RoomJournal(Document());long authored=journal.SpatialAuthoredVersion,placement=journal.SpatialPlacementVersion;
            Assert.That(journal.UpdatePlacement(Cold,Vector3.up,Quaternion.identity),Is.True);
            Assert.That(journal.TrySpatialPlacementChanges(placement,out var moved,out placement),Is.True);CollectionAssert.AreEqual(new[]{Cold},moved);
            Assert.That(journal.TrySpatialAuthoredChanges(authored,out var ids,out _),Is.True);Assert.That(ids,Is.Empty);
            var value=journal.Read(Live);value.color=Color.blue;Assert.That(journal.Apply(new[]{value},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(journal.TrySpatialAuthoredChanges(authored,out ids,out authored),Is.True);CollectionAssert.AreEqual(new[]{Live},ids);
            Assert.That(journal.TrySpatialPlacementChanges(placement,out moved,out _),Is.True);Assert.That(moved,Is.Empty);
            value.position=new(float.NaN,0,0);Assert.That(journal.Apply(new[]{value},Array.Empty<string>(),out _),Is.False);
            Assert.That(journal.TrySpatialAuthoredChanges(authored,out ids,out var now),Is.True);Assert.That(ids,Is.Empty);Assert.That(now,Is.EqualTo(authored));
            Assert.That(journal.TrySpatialPlacementChanges(-1,out _,out _),Is.False);Assert.That(journal.TrySpatialAuthoredChanges(authored+1,out _,out _),Is.False);
        }
        [Test] public void SpatialPublicationDeltaContainsCurrentIdentityAndCannotResurrectDeletedPendingMetadata(){
            var completion=new TaskCompletionSource<ModelAssetBounds>();using var index=new RoomSpatialIndex((h,c)=>completion.Task);
            var old=new RoomObjectData{id=Cold,kind=RoomObjectKind.ImportedModel,modelHash=new string('1',64)};
            index.Synchronize(new RoomDocument{objects=new[]{old}});index.Tick();long cursor=index.Generation;
            var current=old.Copy();current.id=Live;index.Synchronize(new RoomDocument{objects=new[]{current}});
            Assert.That(index.TryChanges(cursor,out var ids,out cursor),Is.True);CollectionAssert.AreEqual(new[]{Cold,Live},ids);
            completion.SetResult(ModelAssetBounds.Unknown("Fixture unavailable"));Drain(index);
            Assert.That(index.TryChanges(cursor,out ids,out _),Is.True);CollectionAssert.AreEqual(new[]{Live},ids);
            Assert.That(index.TryRead(Cold,RoomEditor.SpatialSource(old.Copy()),out _),Is.False);
            Assert.That(index.TryRead(Live,RoomEditor.SpatialSource(current.Copy()),out var result),Is.True);Assert.That(result.Collision.Known,Is.False);
        }
        [Test] public void SpatialGroundProjectionMatchesFullDirectoryAcrossAcceptedClassificationChanges(){
            var journal=new RoomJournal(Document());
            void Same(){
                var expected=journal.Snapshot().objects.Where(x=>!x.IsBuiltIn&&x.physics==ItemPhysics.Fixed&&((x.heightFields?.Length??0)>0||x.kind==RoomObjectKind.ImportedModel&&x.modelGeometry.walkable&&x.modelGeometry.meshCollision)).Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                CollectionAssert.AreEqual(expected,journal.GroundCandidates().Select(x=>x.target));
                foreach(var item in journal.Snapshot().objects){bool ground=journal.GroundCandidate(item.id,out var projected);Assert.That(ground,Is.EqualTo(expected.Contains(item.id)),item.id);if(ground){Assert.That(projected.target,Is.EqualTo(item.id));Assert.That(projected.terrain,Is.EqualTo(item.heightFields.Length>0));Assert.That(projected.imported,Is.EqualTo(item.kind==RoomObjectKind.ImportedModel&&item.modelGeometry.walkable&&item.modelGeometry.meshCollision));}}
                Assert.That(journal.GroundCandidate(new string('d',32),out _),Is.False);
            }
            Same();var terrain=journal.Read(Cold);terrain.physics=ItemPhysics.Fixed;terrain.heightFields=new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
            Assert.That(journal.Apply(new[]{terrain},Array.Empty<string>(),out var error),Is.True,error);Same();
            var imported=new RoomObjectData{id=new string('c',32),kind=RoomObjectKind.ImportedModel,modelHash=new string('1',64),physics=ItemPhysics.Fixed,modelGeometry=new(){meshCollision=true,walkable=true}};
            Assert.That(journal.Apply(new[]{imported},Array.Empty<string>(),out error),Is.True,error);Same();
            var invalid=imported.Copy();invalid.modelGeometry.meshCollision=false;
            Assert.That(journal.Apply(new[]{invalid},Array.Empty<string>(),out _),Is.False,"Walkable without mesh collision is not an accepted authored state");Same();
            invalid=imported.Copy();invalid.physics=ItemPhysics.Solid;
            Assert.That(journal.Apply(new[]{invalid},Array.Empty<string>(),out _),Is.False,"A dynamic mesh collider is not accepted");Same();
            invalid=journal.Read("maestro");invalid.heightFields=terrain.heightFields;
            Assert.That(journal.Apply(new[]{invalid},Array.Empty<string>(),out _),Is.False,"Built-in avatars cannot become height-field supports");Same();
            terrain.physics=ItemPhysics.Solid;terrain.heightFields=Array.Empty<RoomHeightField>();imported.modelGeometry.walkable=false;
            Assert.That(journal.Apply(new[]{terrain,imported},Array.Empty<string>(),out error),Is.True,error);Same();
            imported.modelGeometry.meshCollision=false;Assert.That(journal.Apply(new[]{imported},Array.Empty<string>(),out error),Is.True,error);Same();
            imported.physics=ItemPhysics.Solid;Assert.That(journal.Apply(new[]{imported},Array.Empty<string>(),out error),Is.True,error);Same();
            Assert.That(journal.Undo(),Is.True);Same();Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[]{Cold,imported.id},out error),Is.True,error);Same();
        }
    }
}

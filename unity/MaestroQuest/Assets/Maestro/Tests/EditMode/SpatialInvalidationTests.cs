// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
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
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomRetentionGraphTests
    {
        static string Id(char c)=>new(c,32);
        static RoomObjectData Item(char c,string peer=null,bool enabled=true)=>new(){id=Id(c),kind=RoomObjectKind.Block,connections=peer==null?Array.Empty<RoomConnection>():new[]{new RoomConnection{connected=peer,enabled=enabled}}};
        static RoomRegion Area(char c,params char[] members)=>new(){id=Id(c),name="Area",members=members.Select(Id).ToArray()};
        static RoomDocument Document(params RoomObjectData[] items)=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}.Concat(items).ToArray()};
        static long Allocations(Action query)
        {
            using var recorder=new ProfilerRecorder(ProfilerCategory.Memory,"GC.Alloc",1,ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame|ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid,Is.True,"Allocation recorder must be available");
            recorder.Start();for(int i=0;i<100;i++)query();recorder.Stop();
            Assert.That(recorder.Count,Is.GreaterThan(0),"Unsupported allocation counters must not pass silently");
            long count=recorder.GetSample(0).Count;Assert.That(count,Is.GreaterThan(0));return count;
        }
        [Test] public void IndependentAreasKeepPhysicalConnectionsButNotUnrelatedMembers()
        {
            var a=Area('a','1','2');a.loading="independent";var b=Area('b','3','4');b.loading="independent";
            var graph=new RoomRetentionGraph(new[]{Item('1',Id('3')),Item('2'),Item('3'),Item('4')},new[]{a,b});
            graph.Retain(Id('1'),RoomRetentionReason.Held);
            Assert.That(graph.Closure(new[]{Id('1')}),Is.EqualTo(new[]{Id('1'),Id('3')}));
            Assert.That(graph.Reasons(Id('3')),Is.EqualTo(RoomRetentionReason.Held));
            foreach(char id in "24")Assert.That(graph.Reasons(Id(id)),Is.EqualTo(RoomRetentionReason.None));
            Assert.That(graph.Members(a.id),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Assert.That(graph.Closure(graph.Members(a.id)),Is.EqualTo(new[]{Id('1'),Id('2'),Id('3')}),"Explicit whole-area requests still include every member and physical peer");
        }
        [Test] public void LoadingPolicyEditsInvalidateTopologyAndUndoWithoutRewritingOlderObservations()
        {
            var journal=new RoomJournal(Document(Item('1'),Item('2')));var area=Area('a','1','2');Regions(journal,area);
            var old=journal.RetentionTopology;int revision=journal.RegionRevision(area.id);var fork=journal.Fork();
            area.loading="independent";Regions(fork,area);
            Assert.That(journal.RetentionTopology,Is.SameAs(old));Assert.That(Closure(fork,'1'),Is.EqualTo(new[]{Id('1')}));
            Assert.That(journal.ApplySnapshot(fork.Snapshot(),out var error),Is.True,error);
            Assert.That(journal.RegionRevision(area.id),Is.GreaterThan(revision));Assert.That(journal.RetentionTopology,Is.Not.SameAs(old));
            Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1')}));Assert.That(old.Closure(new[]{Id('1')}),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Assert.That(journal.Undo(),Is.True);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Assert.That(journal.Redo(),Is.True);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1')}));
            Assert.That(new RoomJournal(journal.Snapshot()).ReadRegion(area.id).Loading,Is.EqualTo("independent"));
        }
        [Test] public void WarmJournalRetentionQueriesAvoidRebuildingAuthoredTopology()
        {
            var data=Document(Enumerable.Range(1,RoomDocument.MaximumObjects).Select(i=>new RoomObjectData{id=i.ToString("x32"),kind=RoomObjectKind.Block}).ToArray());
            var journal=new RoomJournal(data);string target=data.objects[2].id;
            void Query(RoomRetentionGraph graph){graph.Retain(target,RoomRetentionReason.Audio);graph.Closure(new[]{target});}
            for(int i=0;i<10;i++){Query(journal.RetentionGraph());Query(new RoomRetentionGraph(data.objects,data.regions));}
            long rebuilt=Allocations(()=>Query(new RoomRetentionGraph(data.objects,data.regions))),reused=Allocations(()=>Query(journal.RetentionGraph()));
            TestContext.WriteLine($"Retention allocation events for 100 warm reads at {RoomDocument.MaximumObjects} creations: rebuilt={rebuilt}, reused={reused}. Desktop managed allocation events, not bytes, frame time or headset memory.");
            Assert.That(reused,Is.LessThan(3250),"At least fourfold fewer events than the measured pre-cache warm baseline");
            Assert.That(reused,Is.LessThan(rebuilt/4),"Warm journal queries should reuse authored topology while keeping fresh demand state");
            Assert.That(journal.RetentionGraph().Reasons(target),Is.EqualTo(RoomRetentionReason.None));
        }
        static void Apply(RoomJournal journal,params RoomObjectData[] values)=>Assert.That(journal.Apply(values,Array.Empty<string>(),out var error),Is.True,error);
        static void Regions(RoomJournal journal,params RoomRegion[] values)=>Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,regionEdits:new(){Replacements=values}),Is.True,error);
        static string[] Closure(RoomJournal journal,params char[] targets)=>journal.RetentionTopology.Closure(targets.Select(Id));
        [Test] public void CachedTopologyNeverSharesLiveReasonsOrExposesMutableMembership()
        {
            var data=Document(Item('1'),Item('2'),Item('3'));data.regions=new[]{Area('a','1','2')};var journal=new RoomJournal(data);
            var first=journal.RetentionGraph();first.Retain(Id('1'),RoomRetentionReason.Held);
            var second=journal.RetentionGraph();Assert.That(second.Reasons(Id('2')),Is.EqualTo(RoomRetentionReason.None));
            second.Retain(Id('2'),RoomRetentionReason.Audio);Assert.That(first.Reasons(Id('2')),Is.EqualTo(RoomRetentionReason.Held));
            var topology=journal.RetentionTopology;topology.Members(Id('a'))[0]="outside";topology.Closure(new[]{Id('1')})[0]="outside";topology.Groups().First()[0]="outside";
            var targets=(System.Collections.Generic.IList<string>)topology.Targets;Assert.Throws<NotSupportedException>(()=>targets[0]="outside");
            data.regions[0].members[0]=Id('3');data.objects[2].connections=new[]{new RoomConnection{connected=Id('3')}};
            Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));Assert.That(topology.Members(Id('a')),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Assert.That(journal.RetentionGraph().Reasons(Id('1')),Is.EqualTo(RoomRetentionReason.None));Assert.That(journal.CanUndo,Is.False);
        }
        [Test] public void AcceptedConnectionEditsAndHistoryReplaceTopologyWithoutChangingOlderObservations()
        {
            var journal=new RoomJournal(Document(Item('1',Id('2')),Item('2'),Item('3')));var old=journal.RetentionGraph();old.Retain(Id('1'),RoomRetentionReason.Audio);var original=journal.RetentionTopology;
            var value=journal.Read(Id('1'));value.connections[0].connected=Id('3');Apply(journal,value);
            Assert.That(journal.RetentionTopology,Is.Not.SameAs(original));Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('3')}));
            Assert.That(old.Closure(new[]{Id('1')}),Is.EqualTo(new[]{Id('1'),Id('2')}));Assert.That(old.Reasons(Id('2')),Is.EqualTo(RoomRetentionReason.Audio));
            value.connections[0].enabled=false;Apply(journal,value);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1')}));
            Assert.That(journal.Undo(),Is.True);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('3')}));
            Assert.That(journal.Undo(),Is.True);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Assert.That(journal.Redo(),Is.True);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('3')}));Assert.That(journal.RetentionGraph().Reasons(Id('1')),Is.EqualTo(RoomRetentionReason.None));
        }
        [Test] public void DeletedPeerAndExactRecreationRefreshMissingDependencies()
        {
            var journal=new RoomJournal(Document(Item('1',Id('2')),Item('2')));var old=journal.RetentionTopology;
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[]{Id('2')},out var error),Is.True,error);var missing=journal.RetentionTopology;
            Assert.That(missing.Contains(Id('2')),Is.False);Assert.That(missing.MissingDependency(Id('1')),Is.True);Assert.That(old.Contains(Id('2')),Is.True);Assert.That(old.MissingDependency(Id('1')),Is.False);
            Assert.That(journal.Undo(),Is.True);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));Assert.That(journal.RetentionTopology.MissingDependency(Id('1')),Is.False);
            Assert.That(journal.Redo(),Is.True);Apply(journal,Item('2'));Assert.That(journal.RetentionTopology.MissingDependency(Id('1')),Is.False);Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Assert.That(missing.Contains(Id('2')),Is.False,"A retained immutable observation cannot resurrect a deleted peer");
        }
        [Test] public void NamedMembershipAndEmptyRegionsRefreshClosureAndHomeMembership()
        {
            var journal=new RoomJournal(Document(Item('1'),Item('2'),Item('3')));Regions(journal,Area('a','1','2'));var old=journal.RetentionTopology;
            Regions(journal,Area('a','1'),Area('b'));var split=journal.RetentionTopology;
            Assert.That(split,Is.Not.SameAs(old));Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1')}));Assert.That(split.Members(""),Is.EqualTo(new[]{Id('2'),Id('3')}));Assert.That(split.Members(Id('b')),Is.Empty);
            Assert.That(old.Members(Id('b')),Is.Null);Assert.That(old.Closure(new[]{Id('1')}),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Regions(journal,Area('b','2','3'));Assert.That(Closure(journal,'2'),Is.EqualTo(new[]{Id('2'),Id('3')}));Assert.That(journal.RetentionTopology.Members(""),Is.Empty);
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,regionEdits:new(){Removals=new[]{Id('b')}}),Is.True,error);
            Assert.That(Closure(journal,'2'),Is.EqualTo(new[]{Id('2')}));Assert.That(journal.RetentionTopology.Members(Id('b')),Is.Null);
            Assert.That(journal.Undo(),Is.True);Assert.That(Closure(journal,'2'),Is.EqualTo(new[]{Id('2'),Id('3')}));
        }
        [Test] public void PlacementAppearanceJointSettingsAndRegionNamesKeepTheSameTopology()
        {
            var journal=new RoomJournal(Document(Item('1',Id('2')),Item('2'),Item('3'),Item('4')));Regions(journal,Area('a','3','4'));var topology=journal.RetentionTopology;
            var value=journal.Read(Id('1'));value.color=Color.blue;value.name="Painted";value.connections[0].drive.force=2;Apply(journal,value);
            Assert.That(journal.RetentionTopology,Is.SameAs(topology));Assert.That(journal.UpdatePlacement(Id('1'),Vector3.up,Quaternion.identity),Is.True);Assert.That(journal.RetentionTopology,Is.SameAs(topology));
            var area=journal.ReadRegion(Id('a'));area.name="Renamed";area.members=area.members.Reverse().ToArray();Regions(journal,area);Assert.That(journal.RetentionTopology,Is.SameAs(topology));
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,worldTime:new RoomWorldTime{settings=new(){running=true}}),Is.True,error);Assert.That(journal.RetentionTopology,Is.SameAs(topology));
            Assert.That(journal.AdvanceWorldTime(.2),Is.True);Assert.That(journal.RetentionTopology,Is.SameAs(topology));Assert.That(journal.Undo(),Is.True);Assert.That(journal.RetentionTopology,Is.SameAs(topology));
            value=journal.Read(Id('1'));value.connections[0].enabled=false;Apply(journal,value);var disabled=journal.RetentionTopology;
            value.connections[0].connected=Id('3');Apply(journal,value);Assert.That(journal.RetentionTopology,Is.SameAs(disabled));Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1')}));
        }
        [Test] public void PreparedRejectedAndDisposedChangesCannotReplaceAcceptedTopology()
        {
            var journal=new RoomJournal(Document(Item('1'),Item('2')));var topology=journal.RetentionTopology;
            var joined=journal.Read(Id('1'));joined.connections=new[]{new RoomConnection{connected=Id('2')}};
            Assert.That(journal.Prepare(new[]{joined},Array.Empty<string>(),out var cancelled,out var error),Is.True,error);
            Assert.That(journal.RetentionTopology,Is.SameAs(topology));cancelled.Dispose();Assert.That(cancelled.Accept(journal,out _),Is.False);Assert.That(journal.RetentionTopology,Is.SameAs(topology));
            Assert.That(journal.Prepare(new[]{joined},Array.Empty<string>(),out var stale,out error),Is.True,error);
            using(stale){Assert.That(journal.UpdatePlacement(Id('1'),Vector3.up,Quaternion.identity),Is.True);Assert.That(stale.Accept(journal,out _),Is.False);Assert.That(journal.RetentionTopology,Is.SameAs(topology));}
            Assert.That(journal.Prepare(new[]{joined},Array.Empty<string>(),out var accepted,out error),Is.True,error);
            using(accepted){var candidate=accepted.Snapshot();candidate.objects.Single(x=>x.id==Id('1')).connections[0].connected=Id('3');Assert.That(journal.RetentionTopology,Is.SameAs(topology));Assert.That(accepted.Accept(journal,out error),Is.True,error);}
            Assert.That(journal.RetentionTopology,Is.Not.SameAs(topology));Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));
        }
        [Test] public void ForksShareOnlyImmutableTopologyAndDivergeInBothDirections()
        {
            var source=new RoomJournal(Document(Item('1',Id('2')),Item('2'),Item('3')));var topology=source.RetentionTopology;var fork=source.Fork();Assert.That(fork.RetentionTopology,Is.SameAs(topology));
            var old=fork.RetentionGraph();old.Retain(Id('1'),RoomRetentionReason.Ownership);Assert.That(source.RetentionGraph().Reasons(Id('1')),Is.EqualTo(RoomRetentionReason.None));
            var value=fork.Read(Id('1'));value.connections[0].connected=Id('3');Apply(fork,value);
            Assert.That(source.RetentionTopology,Is.SameAs(topology));Assert.That(Closure(fork,'1'),Is.EqualTo(new[]{Id('1'),Id('3')}));Assert.That(Closure(source,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Apply(source,Item('4'));Assert.That(fork.RetentionTopology.Contains(Id('4')),Is.False);Assert.That(topology.Contains(Id('4')),Is.False);Assert.That(source.RetentionTopology.Contains(Id('4')),Is.True);
            Assert.That(old.Reasons(Id('2')),Is.EqualTo(RoomRetentionReason.Ownership));Assert.That(old.Reasons(Id('3')),Is.EqualTo(RoomRetentionReason.None));
        }
        [Test] public void SnapshotAcceptanceUndoAndReloadUseTheAcceptedDependencyIdentity()
        {
            var source=new RoomJournal(Document(Item('1'),Item('2'),Item('3')));Regions(source,Area('a','1','2'));var original=source.RetentionTopology;
            var fork=source.Fork();Regions(fork,Area('a','2','3'));Assert.That(source.ApplySnapshot(fork.Snapshot(),out var error),Is.True,error);
            Assert.That(Closure(source,'1'),Is.EqualTo(new[]{Id('1')}));Assert.That(Closure(source,'2'),Is.EqualTo(new[]{Id('2'),Id('3')}));
            var reloaded=new RoomJournal(source.Snapshot());Assert.That(Closure(reloaded,'2'),Is.EqualTo(Closure(source,'2')));Assert.That(reloaded.RetentionTopology,Is.Not.SameAs(source.RetentionTopology));
            Assert.That(source.Undo(),Is.True);Assert.That(Closure(source,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));Assert.That(original.Members(Id('a')),Is.EqualTo(new[]{Id('1'),Id('2')}));
            Assert.That(source.Redo(),Is.True);Assert.That(Closure(source,'2'),Is.EqualTo(new[]{Id('2'),Id('3')}));
        }
        [Test] public void InvalidAndNoopEditsKeepTopologyAndCannotMutateItThroughReadback()
        {
            var journal=new RoomJournal(Document(Item('1',Id('2')),Item('2')));var topology=journal.RetentionTopology;var read=journal.Read(Id('1'));read.connections[0].connected=Id('3');
            Assert.That(Closure(journal,'1'),Is.EqualTo(new[]{Id('1'),Id('2')}));read.position=new Vector3(float.NaN,0,0);Assert.That(journal.Apply(new[]{read},Array.Empty<string>(),out _),Is.False);Assert.That(journal.RetentionTopology,Is.SameAs(topology));
            Apply(journal,journal.Read(Id('1')));Assert.That(journal.RetentionTopology,Is.SameAs(topology));Assert.That(journal.CanUndo,Is.False);
        }
        [Test] public void AreaAndConnectionClosureRetainsBothEndsAcrossCyclesWithoutRetainingUnrelatedArea()
        {
            var graph=new RoomRetentionGraph(new[]{Item('1',Id('3')),Item('2'),Item('3'),Item('4',Id('2')),Item('5'),Item('6')},new[]{Area('a','1','2'),Area('b','3','4'),Area('c','5','6')});
            Assert.That(graph.Retain(Id('4'),RoomRetentionReason.Audio),Is.True);
            foreach(char c in "1234")Assert.That(graph.Reasons(Id(c)),Is.EqualTo(RoomRetentionReason.Audio));
            foreach(char c in "56")Assert.That(graph.Reasons(Id(c)),Is.EqualTo(RoomRetentionReason.None));
            graph.Retain(Id('1'),RoomRetentionReason.Ownership);
            foreach(char c in "1234")Assert.That(graph.Reasons(Id(c)),Is.EqualTo(RoomRetentionReason.Audio|RoomRetentionReason.Ownership));
        }
        [Test] public void WorldOwnedActorsDoNotPinAllHomeCreationsAndDisabledConnectionsDoNotConnectAreas()
        {
            var graph=new RoomRetentionGraph(new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},Item('1',Id('2'),false),Item('2'),Item('3')},new[]{Area('a','1')});
            graph.Retain("book",RoomRetentionReason.WorldOwned);graph.Retain(Id('1'),RoomRetentionReason.Held);
            Assert.That(graph.Members(""),Is.EqualTo(new[]{Id('2'),Id('3')}));
            Assert.That(graph.Reasons(Id('2')),Is.EqualTo(RoomRetentionReason.None));Assert.That(graph.Reasons("maestro"),Is.EqualTo(RoomRetentionReason.None));
            graph.Retain(Id('2'),RoomRetentionReason.Physics);Assert.That(graph.Reasons(Id('3')),Is.EqualTo(RoomRetentionReason.None));
        }
        [Test] public void DependencySnapshotDoesNotBorrowMutableJournalArraysOrRetainPriorDemands()
        {
            var first=Item('1',Id('2'));var area=Area('a','1');var values=new[]{first,Item('2'),Item('3')};
            var graph=new RoomRetentionGraph(values,new[]{area,Area('b','2'),Area('c','3')});first.connections[0].connected=Id('3');area.members[0]=Id('3');
            graph.Members(Id('a'))[0]="mutated";graph.Retain(Id('1'),RoomRetentionReason.Audio);
            Assert.That(graph.Members(Id('a')),Is.EqualTo(new[]{Id('1')}));Assert.That(graph.Reasons(Id('2')),Is.EqualTo(RoomRetentionReason.Audio));Assert.That(graph.Reasons(Id('3')),Is.EqualTo(RoomRetentionReason.None));
            var next=new RoomRetentionGraph(values,new[]{area});Assert.That(next.Reasons(Id('1')),Is.EqualTo(RoomRetentionReason.None));
        }
        [Test] public void MissingConnectionPeerIsAnUnavailableDependencyNotAnInventedEntity()
        {
            var graph=new RoomRetentionGraph(new[]{Item('1',Id('2')),Item('3',Id('4'),false)},new[]{Area('a','1'),Area('b','3')});
            Assert.That(graph.MissingDependency(Id('1')),Is.True);Assert.That(graph.MissingDependency(Id('3')),Is.False);
            Assert.That(graph.Retain(Id('2'),RoomRetentionReason.Ownership),Is.False);Assert.That(graph.Targets,Does.Not.Contain(Id('2')));
            Assert.That(graph.Members(Id('c')),Is.Null);
        }
        [Test] public void EmptyAreasAndGlobalHoldsRemainBoundedAndDeterministic()
        {
            var graph=new RoomRetentionGraph(new[]{Item('1'),Item('2')},new[]{Area('a'),Area('b','1'),Area('c','2')});
            graph.RetainAll(RoomRetentionReason.WorkspaceBusy);graph.RetainAll(RoomRetentionReason.CollisionEnvironment);
            Assert.That(graph.Members(Id('a')),Is.Empty);
            Assert.That(RoomRetentionGraph.Names(graph.Reasons(Id('1'))),Is.EqualTo(new[]{"collisionEnvironment","workspaceBusy"}));
            Assert.That(graph.Reasons(Id('2')),Is.EqualTo(graph.Reasons(Id('1'))));
        }
        [Test] public void NativeGroupsKeepNamedAreasAndConnectionsAtomicButSeparateUnassignedCreations(){
            var graph=new RoomRetentionGraph(new[]{Item('1'),Item('2'),Item('3',Id('4')),Item('4'),Item('5'),new RoomObjectData{id="book",kind=RoomObjectKind.Book}},new[]{Area('a','1','2')});
            var groups=graph.Groups().ToArray();Assert.That(groups.Length,Is.EqualTo(4));
            Assert.That(groups[0],Is.EqualTo(new[]{Id('1'),Id('2')}));Assert.That(groups[1],Is.EqualTo(new[]{Id('3'),Id('4')}));
            Assert.That(groups[2],Is.EqualTo(new[]{Id('5')}));Assert.That(groups[3],Is.EqualTo(new[]{"book"}));
            Assert.That(graph.Members(""),Is.EqualTo(new[]{Id('3'),Id('4'),Id('5')}));
            graph.Retain(Id('3'),RoomRetentionReason.Ownership);Assert.That(graph.Reasons(Id('4')),Is.EqualTo(RoomRetentionReason.Ownership));Assert.That(graph.Reasons(Id('5')),Is.EqualTo(RoomRetentionReason.None));
        }
    }
}

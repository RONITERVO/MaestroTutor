// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class RoomRetentionGraphTests
    {
        static string Id(char c)=>new(c,32);
        static RoomObjectData Item(char c,string peer=null,bool enabled=true)=>new(){id=Id(c),kind=RoomObjectKind.Block,connections=peer==null?Array.Empty<RoomConnection>():new[]{new RoomConnection{connected=peer,enabled=enabled}}};
        static RoomRegion Area(char c,params char[] members)=>new(){id=Id(c),name="Area",members=members.Select(Id).ToArray()};
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
            graph.Retain(Id('2'),RoomRetentionReason.Physics);Assert.That(graph.Reasons(Id('3')),Is.EqualTo(RoomRetentionReason.Physics));
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
    }
}

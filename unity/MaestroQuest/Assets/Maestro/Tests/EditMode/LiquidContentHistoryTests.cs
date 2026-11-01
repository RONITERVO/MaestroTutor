// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class LiquidContentHistoryTests
    {
        static readonly string A=new('a',32),B=new('b',32);
        static RoomJournal Room()=>new(new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{
            new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},
            new RoomObjectData{id=A,kind=RoomObjectKind.Block,containers=new[]{new RoomContainer{amountMl=200,liquid="Juice",color=Color.red}}},
            new RoomObjectData{id=B,kind=RoomObjectKind.Block,containers=new[]{new RoomContainer()}}}});
        static RoomJournal.PreparedEdit Transfer(RoomJournal room) {
            var source=room.Read(A);var destination=room.Read(B);
            Assert.IsTrue(RoomContainer.Transfer(source.containers[0],destination.containers[0],37.125,out _,out var error),error);
            Assert.IsTrue(room.Prepare(new[]{source,destination},Array.Empty<string>(),out var edit,out error,scope:RoomEditScope.LiquidContents),error);return edit;
        }
        [Test] public void LiquidContentAcceptanceMergesOnlyContentsWithNewerRuntimePlacement() {
            var room=Room();using var edit=Transfer(room);var rotation=Quaternion.Euler(4,20,8);
            room.UpdatePlacement(A,Vector3.right,rotation,runtime:true);room.UpdatePlacement(B,Vector3.left,rotation,runtime:true);
            Assert.IsFalse(edit.Current(room));Assert.IsTrue(edit.Accept(room,out var error,allowRuntimeProgress:true),error);
            Assert.AreEqual(Vector3.right,room.Read(A).position);Assert.AreEqual(Vector3.left,room.Read(B).position);Assert.AreEqual(rotation,room.Read(A).rotation);
            Assert.AreEqual(200,room.Read(A).containers[0].amountMl+room.Read(B).containers[0].amountMl);
            Assert.AreEqual(37.125,room.Read(B).containers[0].amountMl);Assert.AreEqual("Juice",room.Read(B).containers[0].liquid);Assert.AreEqual(Color.red,room.Read(B).containers[0].color);
        }
        [Test] public void LiquidContentUndoRedoKeepsBothCurrentPlacementsAndRestoresExactIdentities() {
            var room=Room();using(var edit=Transfer(room))Assert.IsTrue(edit.Accept(room,out var error),error);
            room.UpdatePlacement(A,Vector3.up,Quaternion.identity,runtime:true);room.UpdatePlacement(B,Vector3.down,Quaternion.identity,runtime:true);
            Assert.IsTrue(room.PrepareHistory(true,out var undo,out var issue),issue);
            using(undo){Assert.IsTrue(undo.LiquidContentsOnly);Assert.AreEqual(Vector3.up,undo.ReadObject(A).position);Assert.IsTrue(undo.Accept(room,out issue),issue);}
            Assert.AreEqual(200,room.Read(A).containers[0].amountMl);Assert.AreEqual(0,room.Read(B).containers[0].amountMl);Assert.AreEqual("Water",room.Read(B).containers[0].liquid);
            room.UpdatePlacement(A,Vector3.forward,Quaternion.identity,runtime:true);Assert.IsTrue(room.Redo());
            Assert.AreEqual(Vector3.forward,room.Read(A).position);Assert.AreEqual(Vector3.down,room.Read(B).position);Assert.AreEqual(37.125,room.Read(B).containers[0].amountMl);Assert.AreEqual("Juice",room.Read(B).containers[0].liquid);
            Assert.IsTrue(room.Snapshot().Validate(out issue),issue);
        }
        [TestCase(true)][TestCase(false)] public void LiquidContentAdmissionRejectsAuthoredConflicts(bool placement) {
            var room=Room();using var edit=Transfer(room);
            if(placement)room.UpdatePlacement(A,Vector3.up,Quaternion.identity);
            else {var value=room.Read(B);value.containers[0].amountMl=10;Assert.IsTrue(room.Apply(new[]{value},Array.Empty<string>(),out var issue),issue);}
            Assert.IsFalse(edit.Accept(room,out _,allowRuntimeProgress:true));Assert.AreEqual(200,room.Read(A).containers[0].amountMl);
        }
        [Test] public void LiquidContentScopeRejectsGeometryCapacityAndUnrelatedFields() {
            Action<RoomObjectData>[] changes={v=>v.containers[0].capacityMl=300,v=>v.containers[0].height=.2f,v=>v.containers[0].frame.position=Vector3.up,v=>v.containers=Array.Empty<RoomContainer>(),v=>v.color=Color.green,v=>v.mass=2};
            foreach(var change in changes){var room=Room();var value=room.Read(A);change(value);Assert.IsFalse(room.Prepare(new[]{value},Array.Empty<string>(),out _,out _,scope:RoomEditScope.LiquidContents));Assert.AreEqual(200,room.Read(A).containers[0].amountMl);}
        }
        [Test] public void LiquidContentScopeRejectsCreationRemovalAndWorldMetadata() {
            var room=Room();var value=room.Read(A);value.id=new string('c',32);
            Assert.IsFalse(room.Prepare(new[]{value},Array.Empty<string>(),out _,out _,scope:RoomEditScope.LiquidContents));
            Assert.IsFalse(room.Prepare(Array.Empty<RoomObjectData>(),new[]{A},out _,out _,scope:RoomEditScope.LiquidContents));
            Assert.IsFalse(room.Prepare(new[]{room.Read(A)},Array.Empty<string>(),out _,out _,scope:RoomEditScope.LiquidContents,lighting:new(){enabled=true}));
            Assert.IsFalse(room.Prepare(new[]{room.Read(A)},Array.Empty<string>(),out _,out _,scope:(RoomEditScope)999));
        }
        [Test] public void LiquidContentCandidateIgnoresObservedPoseAndOwnsDeepCopies() {
            var room=Room();var value=room.Read(A);value.position=Vector3.one;value.containers[0].amountMl=25;value.containers[0].fluid.densityKgM3=700;
            Assert.IsTrue(room.Prepare(new[]{value},Array.Empty<string>(),out var edit,out var error,scope:RoomEditScope.LiquidContents),error);
            using(edit){value.containers[0].amountMl=99;value.containers[0].fluid.densityKgM3=800;Assert.AreEqual(Vector3.zero,edit.ReadObject(A).position);Assert.IsTrue(edit.Accept(room,out error),error);}
            Assert.AreEqual(25,room.Read(A).containers[0].amountMl);Assert.AreEqual(700,room.Read(A).containers[0].fluid.densityKgM3);
            var copy=room.Snapshot();copy.objects.Single(v=>v.id==A).containers[0].fluid.densityKgM3=900;Assert.AreEqual(700,room.Read(A).containers[0].fluid.densityKgM3);
        }
        [Test] public void LiquidContentNoopDoesNotConsumeUndoHistory() {
            var room=Room();using(var edit=Transfer(room))Assert.IsTrue(edit.Accept(room,out var error),error);
            Assert.IsTrue(room.Prepare(new[]{room.Read(A),room.Read(B)},Array.Empty<string>(),out var noop,out var issue,scope:RoomEditScope.LiquidContents),issue);
            using(noop)Assert.IsTrue(noop.Accept(room,out issue),issue);Assert.IsTrue(room.Undo());Assert.AreEqual(200,room.Read(A).containers[0].amountMl);Assert.IsFalse(room.Undo());
        }
    }
}

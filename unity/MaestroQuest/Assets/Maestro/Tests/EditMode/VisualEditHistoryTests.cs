// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class VisualEditHistoryTests
    {
        static RoomJournal Room()=>new(new RoomDocument{version=2,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}});
        static string Layer(RoomJournal room) {
            var value=new RoomVisibilityLayer{id=Guid.NewGuid().ToString("N"),name="View",opacity=.3f};
            Assert.IsTrue(room.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,visibilityEdits:new(){Replacements=new[]{value}},scope:RoomEditScope.Visual),error);
            using(edit)Assert.IsTrue(edit.Accept(room,out error),error);return value.id;
        }
        [Test] public void VisualBindingKeepsRuntimePlacementDuringAcceptanceAndHistory() {
            var room=Room();string layer=Layer(room);var data=room.Read("maestro");data.visibilityLayer=layer;
            Assert.IsTrue(room.Prepare(new[]{data},Array.Empty<string>(),out var edit,out var error,scope:RoomEditScope.Visual),error);
            using(edit) {
                var rotation=Quaternion.Euler(0,36,0);room.UpdatePlacement("maestro",Vector3.right,rotation,runtime:true);
                Assert.IsFalse(edit.Current(room));Assert.IsTrue(edit.Current(room,allowRuntimeProgress:true));Assert.IsTrue(edit.Accept(room,out error,allowRuntimeProgress:true),error);
                Assert.AreEqual(Vector3.right,room.Read("maestro").position);Assert.AreEqual(rotation,room.Read("maestro").rotation);Assert.AreEqual(layer,room.Read("maestro").visibilityLayer);
                room.UpdatePlacement("maestro",Vector3.down,rotation,runtime:true);
                Assert.IsTrue(room.PrepareHistory(true,out var undo,out error),error);
                using(undo){Assert.IsTrue(undo.VisualOnly);Assert.AreEqual(Vector3.down,undo.ReadObject("maestro").position);Assert.IsTrue(undo.Accept(room,out error),error);}
                Assert.AreEqual("",room.Read("maestro").visibilityLayer);Assert.AreEqual(Vector3.down,room.Read("maestro").position);
                room.UpdatePlacement("maestro",Vector3.left,rotation,runtime:true);Assert.IsTrue(room.Redo());Assert.AreEqual(layer,room.Read("maestro").visibilityLayer);Assert.AreEqual(Vector3.left,room.Read("maestro").position);
            }
        }
        [Test] public void VisualAdmissionDoesNotMergeAuthoredMovementOrOtherEdits() {
            foreach(bool movement in new[]{true,false}) {
                var room=Room();string layer=Layer(room);var data=room.Read("maestro");data.visibilityLayer=layer;
                Assert.IsTrue(room.Prepare(new[]{data},Array.Empty<string>(),out var edit,out var error,scope:RoomEditScope.Visual),error);
                using(edit){if(movement)room.UpdatePlacement("maestro",Vector3.up,Quaternion.identity);else {var other=room.Read("book");other.color=Color.red;Assert.IsTrue(room.Apply(new[]{other},Array.Empty<string>(),out error),error);}
                    Assert.IsFalse(edit.Accept(room,out error,allowRuntimeProgress:true));Assert.AreEqual("",room.Read("maestro").visibilityLayer);}
            }
        }
        [Test] public void VisualEditsCannotSmugglePhysicalContentAndDoNotCaptureSuppliedPose() {
            var room=Room();string layer=Layer(room);var value=room.Read("maestro");value.visibilityLayer=layer;value.position=Vector3.one;
            Assert.IsTrue(room.Prepare(new[]{value},Array.Empty<string>(),out var edit,out var error,scope:RoomEditScope.Visual),error);
            using(edit){Assert.AreEqual(Vector3.zero,edit.ReadObject("maestro").position);Assert.AreEqual(Vector3.one,value.position);}
            value.color=Color.red;Assert.IsFalse(room.Prepare(new[]{value},Array.Empty<string>(),out _,out _,scope:RoomEditScope.Visual));
            Assert.IsFalse(room.Prepare(Array.Empty<RoomObjectData>(),new[]{"maestro"},out _,out _,scope:RoomEditScope.Visual));
            Assert.IsFalse(room.Prepare(new[]{new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block}},Array.Empty<string>(),out _,out _,scope:RoomEditScope.Visual));
        }
        [Test] public void NoopLightingAndLayerReceiptsKeepTheAcceptedRevision() {
            var room=Room();string layer=Layer(room);int lighting=room.LightingRevision,revision=room.VisibilityRevision(layer);
            Assert.IsTrue(room.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,lighting:room.Lighting,visibilityEdits:new(){Replacements=new[]{room.ReadVisibility(layer)}},scope:RoomEditScope.Visual),error);
            using(edit){Assert.IsTrue(edit.Accept(room,out error),error);Assert.IsTrue(room.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var next,out error,lighting:new(){enabled=true},scope:RoomEditScope.Visual),error);
                using(next)Assert.IsTrue(next.Accept(room,out error),error);Assert.AreEqual(lighting,edit.AcceptedLightingRevision);Assert.AreEqual(revision,edit.AcceptedVisibilityRevision(layer));Assert.AreNotEqual(lighting,room.LightingRevision);}
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class HistoryResidencyTests {
  static RoomJournal Room()=>new(new RoomDocument{version=2,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}});
  static RoomObjectData Block()=>new(){id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block};
  [Test]public void FailedTraversalCannotOverwriteColdHistoryAndLiveTraversalCan(){
   var room=Room();var item=Block();Assert.IsTrue(room.Prepare(new[]{item},Array.Empty<string>(),out var create,out var error),error);
   using(create){create.CaptureDormancy(Array.Empty<string>(),new[]{item.id});Assert.IsTrue(create.Accept(room,out error),error);}
   Assert.IsTrue(room.PrepareHistory(true,out var undo,out error),error);
   using(undo){undo.CaptureDormancy(new[]{item.id},Array.Empty<string>());Assert.IsTrue(undo.Accept(room,out error),error);}
   Assert.IsTrue(room.PrepareHistory(false,out var abandoned,out error),error);
   using(abandoned){Assert.IsTrue(abandoned.RestoresDormant(item.id));abandoned.CaptureDormancy(Array.Empty<string>(),Array.Empty<string>());}
   Assert.IsTrue(room.PrepareHistory(false,out var redo,out error),error);
   using(redo){Assert.IsTrue(redo.RestoresDormant(item.id));redo.CaptureDormancy(Array.Empty<string>(),new[]{item.id});Assert.IsTrue(redo.Accept(room,out error),error);}
   // A view is now live: traversing Undo captures that current presentation.
   Assert.IsTrue(room.PrepareHistory(true,out undo,out error),error);
   using(undo){undo.CaptureDormancy(Array.Empty<string>(),Array.Empty<string>());Assert.IsTrue(undo.Accept(room,out error),error);}
   Assert.IsTrue(room.PrepareHistory(false,out redo,out error),error);
   using(redo)Assert.IsFalse(redo.RestoresDormant(item.id));
  }
  [Test]public void SnapshotHistoryRestoresDeletedColdIdentityWithoutPersistingPresentation(){
   var room=Room();var item=Block();Assert.IsTrue(room.Apply(new[]{item},Array.Empty<string>(),out var error),error);
   var snapshot=room.Fork();Assert.IsTrue(snapshot.Apply(Array.Empty<RoomObjectData>(),new[]{item.id},out error),error);
   Assert.IsTrue(room.ApplySnapshot(snapshot.Snapshot(),out error,new[]{item.id},Array.Empty<string>()),error);
   Assert.IsTrue(room.PrepareHistory(true,out var undo,out error),error);
   using(undo){Assert.IsTrue(undo.RestoresDormant(item.id));Assert.IsTrue(undo.Accept(room,out error),error);}
   var reloaded=new RoomJournal(room.Snapshot());Assert.IsFalse(reloaded.CanUndo);
   Assert.IsNotNull(reloaded.Read(item.id));
  }
  [Test]public void HintCaptureDetachesInputsAndIgnoresUnrelatedOrAbsentIds(){
   var room=Room();var item=Block();Assert.IsTrue(room.Prepare(new[]{item},Array.Empty<string>(),out var create,out var error),error);
   var hints=new[]{item.id,"book","missing"};
   using(create){create.CaptureDormancy(Array.Empty<string>(),hints);hints[0]="changed";Assert.IsTrue(create.Accept(room,out error),error);}
   Assert.IsTrue(room.PrepareHistory(true,out var undo,out error),error);
   using(undo){undo.CaptureDormancy(new[]{item.id},Array.Empty<string>());Assert.IsTrue(undo.Accept(room,out error),error);}
   Assert.IsTrue(room.PrepareHistory(false,out var redo,out error),error);
   using(redo){Assert.IsTrue(redo.RestoresDormant(item.id));Assert.IsFalse(redo.RestoresDormant("book"));Assert.IsFalse(redo.RestoresDormant("missing"));}
  }
 }
}

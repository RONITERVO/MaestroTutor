// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  IEnumerator LiquidSaved() {
   float deadline=Time.realtimeSinceStartup+10;
   while(editor.PendingLiquidSave&&Time.realtimeSinceStartup<deadline){editor.PollLiquidCheckpoints();yield return null;}
   Assert.IsFalse(editor.PendingLiquidSave,"Liquid publication did not drain");
  }
  IEnumerator LiquidWorkerEntered(PublicationPause pause) {
   float deadline=Time.realtimeSinceStartup+5;
   while(!pause.Entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.IsTrue(pause.Entered.IsSet,"Liquid worker did not reach the barrier");
  }
  [UnityTest] public IEnumerator LiquidCheckpointBoundsQueueAndKeepsBodiesMovingUntilOrderedDurability() {
   var(from,to)=FixedFlowCups();physics.StartPhysics();
   using var pause=new PublicationPause();editor.LiquidPublicationCheckpoint=pause.Check;
   int caller=System.Threading.Thread.CurrentThread.ManagedThreadId;var events=new List<double>();
   editor.ContainerPoured+=(id,amount,_,_,_)=>{if(id==from)events.Add(amount);};
   double first=0,total=0,second=0;
   try {
    editor.Liquids.Tick(.05f);first=FlowAmount(to);Assert.Greater(first,0);
    Assert.IsTrue(editor.Liquids.Finish(out var error),error);yield return LiquidWorkerEntered(pause);
    Assert.AreNotEqual(caller,pause.Worker);Assert.AreEqual(0,events.Count);
    Assert.AreEqual(first,editor.Read(to).containers[0].amountMl);
    Assert.AreEqual(0,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==to).containers[0].amountMl);
    editor.Liquids.Tick(.05f);total=FlowAmount(to);second=FlowAmount(from,"transferredMl");Assert.Greater(total,first);
    Assert.IsTrue(editor.Liquids.Finish(out error),error);
    Assert.IsTrue(BehaviourCatalog.TryRead("world.liquidPersistence",1,new JObject(),new BehaviourCatalog.FactContext(editor:editor),out var fact));
    Assert.AreEqual(2,(int)((JObject)fact.Value)["pending"]);Assert.AreEqual("saving",(string)((JObject)fact.Value)["phase"]);
    for(int i=0;i<30;i++)editor.Liquids.Tick(.1f);
    Assert.AreEqual(total,FlowAmount(to));Assert.IsFalse(editor.Liquids.Active);Assert.AreEqual("waiting",(string)editor.Liquids.Observe(to)["phase"]);
    var item=editor.Find(from);var body=item.GetComponent<Rigidbody>();MovingLiquidBody(body);
    Vector3 before=item.transform.position;
    for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
    Assert.Greater(Vector3.Distance(before,item.transform.position),.001f,"Storage admission must not pin a vessel");
    Assert.IsFalse(editor.WriteGate.CanFreeze(out _));Assert.IsFalse(editor.BeginTemporaryRoom(out _));
    editor.transform.position+=new Vector3(2,0,1); // Locomotion changes the viewing frame, not conserved quantities.
   }finally{pause.Release();editor.LiquidPublicationCheckpoint=null;}
   yield return LiquidSaved();
   CollectionAssert.AreEqual(new[]{first,second},events);
   Assert.AreEqual(total,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==to).containers[0].amountMl);
   Assert.IsTrue(editor.WriteGate.CanFreeze(out _));physics.PausePhysics();
  }
  [UnityTest] public IEnumerator LiquidCheckpointUndoInvalidatesDelayedEventsAndSavesLatestAcceptedState() {
   var(from,to)=FixedFlowCups();physics.StartPhysics();
   using var pause=new PublicationPause();editor.LiquidPublicationCheckpoint=pause.Check;int events=0;
   editor.ContainerPoured+=(_,_,_,_,_)=>events++;
   try {
    editor.Liquids.Tick(.05f);Assert.IsTrue(editor.Liquids.Finish(out var error),error);yield return LiquidWorkerEntered(pause);
    editor.Undo();Assert.AreEqual(0,editor.Read(to).containers[0].amountMl);Assert.AreEqual(400,editor.Read(from).containers[0].amountMl);
   }finally{pause.Release();editor.LiquidPublicationCheckpoint=null;}
   yield return LiquidSaved();Assert.AreEqual(0,events);Assert.IsTrue(editor.TryFlush(out var issue),issue);
   Assert.AreEqual(0,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==to).containers[0].amountMl);
   physics.PausePhysics();
  }
  [UnityTest] public IEnumerator LiquidCheckpointDestroyedPouringStillOwnsAcceptedSaveAndReportsOnce() {
   var(from,to)=FixedFlowCups();physics.StartPhysics();
   using var pause=new PublicationPause();editor.LiquidPublicationCheckpoint=pause.Check;int events=0;
   editor.ContainerPoured+=(_,_,_,_,_)=>events++;
   try {
    editor.Liquids.Tick(.05f);Assert.IsTrue(editor.Liquids.Finish(out var error),error);yield return LiquidWorkerEntered(pause);
    UnityEngine.Object.Destroy(editor.Liquids);yield return null;Assert.AreEqual(0,events);Assert.IsFalse(editor.WriteGate.CanFreeze(out _));
   }finally{pause.Release();editor.LiquidPublicationCheckpoint=null;}
   yield return LiquidSaved();Assert.AreEqual(1,events);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
   editor.PollLiquidCheckpoints();Assert.AreEqual(1,events);physics.PausePhysics();
  }
  [UnityTest] public IEnumerator LiquidCheckpointQueuedFailurePreservesAcceptedAmountsAndNeverReplays() {
   var(from,to)=FixedFlowCups();physics.StartPhysics();
   using var pause=new PublicationPause();int workers=0;
   editor.LiquidPublicationCheckpoint=phase=>{pause.Check(phase);if(phase=="publishing"){System.Threading.Interlocked.Increment(ref workers);throw new IOException("Injected liquid publication failure");}};
   int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>events++;double total=0;
   try {
    editor.Liquids.Tick(.05f);Assert.IsTrue(editor.Liquids.Finish(out var error),error);yield return LiquidWorkerEntered(pause);
    editor.Liquids.Tick(.05f);total=FlowAmount(to);Assert.IsTrue(editor.Liquids.Finish(out error),error);
   }finally{pause.Release();editor.LiquidPublicationCheckpoint=null;}
   yield return LiquidSaved();
   Assert.AreEqual(total,FlowAmount(to));Assert.AreEqual(total,editor.Read(to).containers[0].amountMl);
   Assert.AreEqual(0,events);Assert.AreEqual(1,workers,"A failed ancestor must prevent the next publication");
   Assert.AreEqual("unconfirmed",(string)editor.ObserveLiquidPersistence()["phase"]);Assert.IsFalse(editor.CanSaveRoom);
   Assert.AreEqual(0,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==to).containers[0].amountMl);
   physics.PausePhysics();physics.StartPhysics();editor.Liquids.Tick(.1f);
   Assert.IsFalse(editor.Liquids.Active);Assert.AreEqual(total,FlowAmount(to));Assert.AreEqual(0,events);physics.PausePhysics();
  }
  [UnityTest] public IEnumerator LiquidCheckpointAndPreparedEditShareStorageAdmission() {
   var(from,to)=FixedFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);
   // Preparing an unrelated authored edit first accepts the existing flow.
   Assert.IsTrue(editor.BeginRotation(editor.Identity(block),Quaternion.Euler(0,45,0),out var edit,out var error),error);
   try {
    Assert.IsFalse(editor.Liquids.Active);Assert.Greater(editor.Read(to).containers[0].amountMl,0);
    editor.Liquids.Tick(.05f);Assert.IsFalse(editor.Liquids.Active);
    yield return Completed(edit);yield return LiquidSaved();Assert.IsTrue(editor.CanSaveRoom);
   }finally{edit.Dispose();}
   physics.PausePhysics();
  }
 }
}

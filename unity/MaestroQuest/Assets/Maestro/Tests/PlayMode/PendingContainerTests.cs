// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator PendingContainerCopiesDefinitionAndCancellationLeavesTheOriginal()
        {
            var(_,from,_)=Containers();int revision=editor.ObjectRevision(from);var original=editor.Read(from).containers[0];
            var definition=original.Copy();definition.amountMl=80;definition.radius=.08f;
            Assert.IsTrue(editor.BeginContainer(from,revision,definition,out var cancelled,out var error),error);
            definition.amountMl=5;yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(revision,editor.ObjectRevision(from));Assert.AreEqual(200,editor.Read(from).containers[0].amountMl);
            definition.amountMl=80;Assert.IsTrue(editor.BeginContainer(from,revision,definition,out var pending,out error),error);
            using(pending){definition.amountMl=5;yield return Completed(pending);}
            Assert.AreEqual(80,editor.Read(from).containers[0].amountMl);Assert.AreEqual(.08f,editor.Read(from).containers[0].radius);
            Assert.AreEqual(80,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==from).containers[0].amountMl,error);
            editor.Undo();Assert.AreEqual(original.amountMl,editor.Read(from).containers[0].amountMl);
        }
        [UnityTest] public IEnumerator PendingContainerTransferWaitsForDurabilityWithoutHoldingVesselMotion()
        {
            var(_,from,to)=Containers();editor.Liquids.enabled=false;var body=editor.Find(from).GetComponent<Rigidbody>();MovingLiquidBody(body);
            using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginContainerTransfer(from,editor.ObjectRevision(from),to,editor.ObjectRevision(to),150,out var moved,out var pending,out var error,pause.Check),error);
            try {
                Assert.AreEqual(100,moved);yield return Prepared(pending);Assert.AreEqual(200,editor.Read(from).containers[0].amountMl);
                Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                AssertLiquidMomentum(body);Assert.IsFalse(pending.Complete(out _));
                var position=body.position;for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
                Assert.IsTrue(pause.Entered.IsSet);Assert.Greater(Vector3.Distance(body.position,position),.001f);
                Assert.AreNotEqual(System.Threading.Thread.CurrentThread.ManagedThreadId,pause.Worker);
                Assert.AreEqual(100,editor.Read(from).containers[0].amountMl);Assert.AreEqual(100,editor.Read(to).containers[0].amountMl);
                pending.Dispose();StringAssert.Contains("Saving is still pending",pending.InterruptionStatus);
            } finally {pause.Release();pending.Dispose();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            var saved=new RoomStorage(directory).Load(out error);Assert.AreEqual(100,saved.objects.Single(x=>x.id==to).containers[0].amountMl,error);
            var now=body.position;editor.Undo();Assert.AreEqual(now,body.position);Assert.AreEqual(200,editor.Read(from).containers[0].amountMl);Assert.AreEqual(0,editor.Read(to).containers[0].amountMl);
        }
        [UnityTest] public IEnumerator PendingContainerTransferRejectsAChangedDestinationWhileQueued()
        {
            var(_,from,to)=Containers();var args=(JObject)TransferCall(from,to,50)["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation op=null;
            try {
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.container.transfer").Module;
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"transfer",args,out op,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,op.State(out error),error);Assert.IsEmpty(op.Result);
                Assert.IsTrue(editor.PaintObject(to,Color.red,out error),error);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,op.State(out error));Assert.IsEmpty(op.Result);
                Assert.AreEqual(200,editor.Read(from).containers[0].amountMl);Assert.AreEqual(0,editor.Read(to).containers[0].amountMl);
            } finally {op?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingContainerTransferCancellationAndAcceptedFailureHaveDifferentOutcomes()
        {
            var(_,from,to)=Containers();int sourceRevision=editor.ObjectRevision(from),destinationRevision=editor.ObjectRevision(to);
            Assert.IsTrue(editor.BeginContainerTransfer(from,sourceRevision,to,destinationRevision,50,out _,out var cancelled,out var error),error);
            yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(sourceRevision,editor.ObjectRevision(from));Assert.AreEqual(destinationRevision,editor.ObjectRevision(to));
            Assert.AreEqual(200,editor.Read(from).containers[0].amountMl);Assert.AreEqual(0,editor.Read(to).containers[0].amountMl);
            byte[] original=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginContainerTransfer(from,sourceRevision,to,destinationRevision,50,out _,out var failed,out error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected container publication failure");}}),error);
            try {
                yield return Prepared(failed);Assert.IsTrue(failed.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(failed.Accepted);
                pause.Release();float end=Time.realtimeSinceStartup+20;
                while(failed.Poll(out ready,out error)){Assert.IsFalse(ready);Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                Assert.AreEqual(150,editor.Read(from).containers[0].amountMl);Assert.AreEqual(50,editor.Read(to).containers[0].amountMl);
                Assert.IsFalse(failed.Complete(out _));Assert.IsFalse(failed.Published);
                Assert.AreEqual(original,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                Assert.IsFalse(editor.BeginContainerTransfer(from,editor.ObjectRevision(from),to,editor.ObjectRevision(to),10,out _,out _,out _));
            } finally {pause.Release();failed.Dispose();}
            yield return Drained(failed);
        }
        [UnityTest] public IEnumerator PendingContainerTransferCapturesRequestAndReturnsOneImmutableDurableResult()
        {
            var(ex,from,to)=Containers();var call=TransferCall(from,to,75);var requested=call["arguments"]["amountMl"].DeepClone();var request=ContainerRequest(call);
            Assert.IsTrue(ex.Execute(request,out var error,out _),error);Assert.AreEqual("preparing",(string)ex.Executions.Observe()["selected"]["phase"]);
            Assert.IsNull(ex.Executions.Observe()["selected"]["output"]);call["arguments"]["amountMl"]=1;
            Assert.IsFalse(ex.Execute(request,out error,out _),"A changed request cannot reuse its accepted action ID");StringAssert.Contains("different call",error);
            call["arguments"]["amountMl"]=requested;Assert.IsTrue(ex.Execute(request,out error,out _),error);yield return SpatialSettingsCompletion.Wait(ex.Executions);
            var result=ex.Executions.Observe()["selected"]["output"];Assert.AreEqual(75,(double)result["transferredMl"]);
            Assert.AreEqual(125,editor.Read(from).containers[0].amountMl);Assert.AreEqual(75,editor.Read(to).containers[0].amountMl);
            editor.Undo();Assert.IsTrue(ex.Execute(request,out error,out _),error);Assert.AreEqual(200,editor.Read(from).containers[0].amountMl);
            Assert.AreEqual(result,ex.Executions.Observe()["selected"]["output"]);
        }
    }
}

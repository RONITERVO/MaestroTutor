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
        [UnityTest] public IEnumerator PendingTerrainDefinitionIsCapturedAndCancelledPreparationKeepsCollision()
        {
            var ex=new RoomAgentExecutor(editor);string target=Snow(ex);var original=editor.Read(target).heightFields[0];
            var view=editor.Find(target).GetComponent<HeightFieldView>();var collision=view.Collision;
            var definition=original.Copy();definition.heights=Enumerable.Repeat(.12f,definition.heights.Length).ToArray();
            int revision=editor.ObjectRevision(target);
            Assert.IsTrue(editor.BeginHeightField(target,revision,definition,out var cancelled,out var error),error);
            yield return Prepared(cancelled);Assert.AreSame(collision,view.Collision);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(revision,editor.ObjectRevision(target));Assert.AreEqual(original.heights,view.Accepted.heights);
            Assert.IsTrue(editor.BeginHeightField(target,revision,definition,out var pending,out error),error);
            using(pending){definition.heights[144]=.2f;yield return Completed(pending);}
            Assert.AreEqual(.12f,editor.Read(target).heightFields[0].heights[144]);Assert.AreEqual(.12f,view.Accepted.heights[144]);
            Assert.AreEqual(.12f,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).heightFields[0].heights[144],error);
            editor.Undo();Assert.AreEqual(original.heights,editor.Read(target).heightFields[0].heights);
            var remove=new JObject{["id"]="object.field.edit",["version"]=1,["arguments"]=new JObject{["operation"]="remove",["target"]=target,["revision"]=editor.ObjectRevision(target)}};
            yield return ContainerAction(ex,remove);Assert.IsEmpty(editor.Read(target).heightFields);Assert.IsFalse(view.Collision&&view.Collision.enabled);
            editor.Undo();Assert.AreEqual(original.heights,editor.Read(target).heightFields[0].heights);
        }
        [UnityTest] public IEnumerator PendingTerrainSculptPublishesMatchingGeometryWhileUnrelatedMotionContinues()
        {
            string target=Snow(new RoomAgentExecutor(editor));var item=editor.Find(target);var view=item.GetComponent<HeightFieldView>();
            var path=new[]{Vector2.zero};var body=block.GetComponent<Rigidbody>();MovingLiquidBody(body);
            using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginSculptHeightField(target,editor.ObjectRevision(target),"lower",path,.14f,.06f,out var changed,out var pending,out var error,pause.Check),error);
            try {
                path[0]=new Vector2(99,99);yield return Prepared(pending);Assert.AreEqual(.08f,view.Accepted.heights[144]);
                Assert.Greater(changed,0);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                Assert.AreEqual(.02f,view.Accepted.heights[144],.000001f);Physics.SyncTransforms();
                Assert.IsTrue(view.Collision.Raycast(new Ray(item.transform.position+Vector3.up,Vector3.down),out var hit,2));
                Assert.AreEqual(.02f,item.transform.InverseTransformPoint(hit.point).y,.000001f);
                var position=body.position;for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
                Assert.Greater(Vector3.Distance(position,body.position),.001f);Assert.IsTrue(pause.Entered.IsSet);
                Assert.AreNotEqual(System.Threading.Thread.CurrentThread.ManagedThreadId,pause.Worker);
                Assert.IsFalse(pending.Complete(out _));pending.Dispose();StringAssert.Contains("Saving is still pending",pending.InterruptionStatus);
            } finally {pause.Release();pending.Dispose();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            Assert.AreEqual(.02f,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).heightFields[0].heights[144],.000001f,error);
            editor.Undo();Assert.AreEqual(.08f,editor.Read(target).heightFields[0].heights[144]);
        }
        [UnityTest] public IEnumerator PendingTerrainTransferCapturesQueuedInputsAndOnlyReturnsDurableMeasuredResult()
        {
            var(_,from,to)=TransferSurfaces();var args=(JObject)SurfaceTransfer(from,to)["arguments"];
            double total=editor.Read(from).heightFields[0].VolumeLitres+editor.Read(to).heightFields[0].VolumeLitres;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.field.transfer").Module;
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"transfer",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsEmpty(operation.Result);
                args["amountLitres"]=.1;args["source"]["centre"]["x"]=99;
                blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(operation);
                var result=operation.Result;Assert.AreEqual(1,(double)result["removedLitres"],.000001);
                double after=editor.Read(from).heightFields[0].VolumeLitres+editor.Read(to).heightFields[0].VolumeLitres;
                Assert.AreEqual((double)result["roundingLitres"],after-total,.00000001);
                result["removedLitres"]=99;Assert.AreEqual(1,(double)operation.Result["removedLitres"],.000001);
                var saved=new RoomStorage(directory).Load(out error);Assert.AreEqual(editor.Read(to).heightFields[0].heights,saved.objects.Single(x=>x.id==to).heightFields[0].heights,error);
            } finally {operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingTerrainTransferRefusesEitherChangedParticipantWhileQueued()
        {
            var(_,from,to)=TransferSurfaces();
            foreach(var target in new[]{from,to}){
                var args=(JObject)SurfaceTransfer(from,to)["arguments"];var beforeFrom=editor.Read(from).heightFields[0].heights;var beforeTo=editor.Read(to).heightFields[0].heights;
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
                try {
                    yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.field.transfer").Module.Start(new CapabilityContext(editor,animations),"waiting",args,out operation,out error),error);
                    Assert.IsTrue(editor.PaintObject(target,Color.red,out error),error);blocker.Dispose();yield return Drained(blocker);
                    Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsEmpty(operation.Result);
                    Assert.AreEqual(beforeFrom,editor.Read(from).heightFields[0].heights);Assert.AreEqual(beforeTo,editor.Read(to).heightFields[0].heights);
                } finally {operation?.Stop(false);blocker.Dispose();}
                editor.Undo();
            }
        }
        [UnityTest] public IEnumerator PendingTerrainTransferCancellationAndAcceptedFailurePreserveDifferentStates()
        {
            var(_,from,to)=TransferSurfaces();var args=(JObject)SurfaceTransfer(from,to)["arguments"];
            var beforeFrom=editor.Read(from).heightFields[0].heights;var beforeTo=editor.Read(to).heightFields[0].heights;
            Assert.IsTrue(editor.BeginFieldTransfer(args,out _,out var cancelled,out var error),error);
            yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(beforeFrom,editor.Read(from).heightFields[0].heights);Assert.AreEqual(beforeTo,editor.Read(to).heightFields[0].heights);
            var original=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginFieldTransfer(args,out _,out var failed,out error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected terrain publication failure");}}),error);
            try {
                yield return Prepared(failed);Assert.IsTrue(failed.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(failed.Accepted);
                pause.Release();yield return Rejected(failed);
                Assert.AreNotEqual(beforeFrom,editor.Read(from).heightFields[0].heights);Assert.AreNotEqual(beforeTo,editor.Read(to).heightFields[0].heights);
                foreach(var target in new[]{from,to})Assert.AreEqual(editor.Read(target).heightFields[0].heights,editor.Find(target).GetComponent<HeightFieldView>().Accepted.heights);
                Assert.AreEqual(original,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.IsFalse(failed.Published);
                Assert.IsFalse(editor.BeginSculptHeightField(from,editor.ObjectRevision(from),"lower",new[]{Vector2.zero},.1f,.01f,out _,out _,out _));
            } finally {pause.Release();failed.Dispose();}
            yield return Drained(failed);
        }
    }
}

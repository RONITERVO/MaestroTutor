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
        static readonly string[] PendingCreationKinds={"primitive","recipe","copy","drawing","template"};
        JObject PendingCreationCall(string kind)
        {
            var definition=BehaviourCatalog.Action("object.create");
            var variant=((JArray)definition.InputSchema["oneOf"]).OfType<JObject>().Single(x=>(string)x["properties"]["kind"]["enum"][0]==kind);
            var args=(JObject)variant["examples"][0].DeepClone();args["name"]="Pending "+kind;
            if(kind=="copy"){args["target"]=editor.Identity(block);args["revision"]=editor.ObjectRevision(editor.Identity(block));}
            return new JObject{["id"]=definition.Id,["version"]=1,["arguments"]=args};
        }
        bool BeginCreationTest(string kind,out string id,out RoomEditor.PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var position=new Vector3(.3f,1.3f,.65f);string source=editor.Identity(block);
            switch(kind){
                case "primitive":return editor.BeginCreatePrimitive(RoomObjectKind.Ball,"Pending ball",position,1,Color.blue,out id,out pending,out error,checkpoint);
                case "recipe":return editor.BeginCreateRecipe("Pending robot",position,.4f,RecipeTemplates.BoxRobot(false),null,null,out id,out pending,out error,checkpoint);
                case "copy":return editor.BeginCopyObject(source,editor.ObjectRevision(source),"Pending copy",position,out id,out pending,out error,checkpoint);
                case "drawing":return editor.BeginCreateDrawing("Pending stroke",position,1,Color.blue,.003f,new[]{Vector3.zero,Vector3.right*.1f,Vector3.up*.1f},out id,out pending,out error,checkpoint);
                default:var entry=CreationTemplates.All.First(x=>x.Id=="cup");return editor.BeginCreateRecipe("Pending cup",position,1,entry.Recipe,entry.Collision,entry.Physics,out id,out pending,out error,checkpoint,entry.Surfaces,entry.DrawingTips,entry.SnapPoints,entry.Containers,entry.HeightFields,entry.SculptTips,entry.MaterialStores);
            }
        }
        [UnityTest] public IEnumerator PendingCreationAllKindsSaveOneIdentityAndOneUndoWithoutEarlyNativeContent()
        {
            foreach(var kind in PendingCreationKinds){
                Assert.IsTrue(editor.TryFlush(out var error),error);int count=editor.Snapshot().objects.Length;
                Assert.IsTrue(BeginCreationTest(kind,out var id,out var pending,out error),error);
                using(pending){
                    yield return Prepared(pending);Assert.IsFalse(editor.HasSavedObject(id));Assert.IsNull(editor.Find(id));
                    Assert.AreEqual(count,new RoomStorage(directory).Load(out _).objects.Length);
                    yield return Completed(pending);Assert.IsTrue(pending.Published);Assert.IsTrue(editor.HasSavedObject(id));Assert.IsTrue(editor.Find(id));
                    var saved=new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id);
                    Assert.AreEqual(JsonUtility.ToJson(editor.Read(id)),JsonUtility.ToJson(saved));
                    editor.Undo();Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.IsFalse(editor.HasSavedObject(id));
                    Assert.IsTrue(pending.Complete(out error),error);Assert.IsFalse(editor.HasSavedObject(id));
                    editor.Redo();Assert.AreEqual(count+1,editor.Snapshot().objects.Length);Assert.IsTrue(editor.HasSavedObject(id));editor.Undo();
                }
            }
        }
        [UnityTest] public IEnumerator PendingCreationCancellationAndStalePreparationLeaveNoGhostOrHistory()
        {
            foreach(var kind in PendingCreationKinds){
                Assert.IsTrue(editor.TryFlush(out var error),error);int count=editor.Snapshot().objects.Length,revision=editor.Revision;
                Assert.IsTrue(BeginCreationTest(kind,out var id,out var cancelled,out error),error);
                yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
                Assert.AreEqual(revision,editor.Revision);Assert.IsFalse(editor.HasSavedObject(id));Assert.IsNull(editor.Find(id));
                Assert.IsTrue(BeginCreationTest(kind,out id,out var stale,out error),error);
                using(stale){yield return Prepared(stale);Assert.IsTrue(editor.PaintObject(editor.Identity(block),Color.red,out error),error);yield return Rejected(stale);}
                yield return Drained(stale);Assert.IsFalse(stale.Accepted);Assert.AreEqual(count,editor.Snapshot().objects.Length);
                Assert.IsFalse(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id));
                editor.Undo();Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
            }
        }
        [UnityTest] public IEnumerator PendingCreationFailedStorageAndInvalidFrameNeverPublishOrRetainCapacity()
        {
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");
            foreach(var kind in PendingCreationKinds){
                Assert.IsTrue(editor.TryFlush(out var error),error);int count=editor.Snapshot().objects.Length;
                Directory.CreateDirectory(obstacle);
                try {
                    Assert.IsTrue(BeginCreationTest(kind,out var id,out var failed,out error),error);
                    using(failed){yield return Rejected(failed);Assert.IsFalse(failed.Accepted);Assert.IsFalse(editor.HasSavedObject(id));}
                    yield return Drained(failed);
                }finally{Directory.Delete(obstacle);}
                Assert.IsTrue(BeginCreationTest(kind,out var invalidId,out var invalid,out error),error);
                using(invalid){
                    yield return Prepared(invalid);root.transform.localScale=new Vector3(1,2,1);
                    try{yield return Rejected(invalid);Assert.IsFalse(editor.HasSavedObject(invalidId));}finally{root.transform.localScale=Vector3.one;}
                }
                yield return Drained(invalid);Assert.AreEqual(count,editor.Snapshot().objects.Length);
                Assert.IsTrue(BeginCreationTest(kind,out var retried,out var retry,out error),error);
                using(retry){yield return Completed(retry);Assert.IsTrue(editor.HasSavedObject(retried));}editor.Undo();
            }
        }
        [UnityTest] public IEnumerator PendingCreationAllKindsQueueWithoutOutputsThenConfirmExactImmutableResults()
        {
            var operations=new System.Collections.Generic.List<CapabilityOperation>();int count=editor.Snapshot().objects.Length;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.Euler(0,40,0),out var blocker,out var error),error);
            try{
                yield return Prepared(blocker);
                foreach(var kind in PendingCreationKinds){
                    var call=PendingCreationCall(kind);Assert.IsTrue(BehaviourCatalog.Action("object.create").Module.Start(new CapabilityContext(editor,animations),Guid.NewGuid().ToString("N"),(JObject)call["arguments"],out var operation,out error),error);
                    operations.Add(operation);Assert.IsEmpty(operation.Result);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);
                }
                Assert.AreEqual(count,editor.Snapshot().objects.Length);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Preparing,operations.Last().State(out error),error);Assert.AreEqual(count,editor.Snapshot().objects.Length);
                for(int i=0;i<operations.Count;i++){
                    var operation=operations[i];yield return PlacementOperationDone(operation);string id=(string)operation.Result["objectId"];
                    Assert.IsTrue(editor.HasSavedObject(id));Assert.AreEqual(count+i+1,editor.Snapshot().objects.Length);
                    operation.Result["objectId"]="mutated";Assert.AreEqual(id,(string)operation.Result["objectId"]);
                }
            }finally{foreach(var operation in operations)operation.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingCreationReceiptsWaitForSavingAndReplayNeverRecreatesAfterUndo()
        {
            var executor=new RoomAgentExecutor(editor);
            foreach(var kind in PendingCreationKinds){
                var request=ObjectEditRequest(PendingCreationCall(kind));int count=editor.Snapshot().objects.Length;
                Assert.IsTrue(executor.Execute(request,out var error,out _),error);
                var first=executor.Executions.Observe()["selected"];Assert.AreEqual("preparing",(string)first["phase"]);Assert.IsNull(first["output"]);
                Assert.IsTrue(executor.Execute(request,out error,out _),error);yield return AgentPlacementCompleted(executor);
                var receipt=executor.Executions.Observe()["selected"];string id=(string)receipt["output"]["objectId"];
                Assert.IsTrue(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id));Assert.AreEqual(count+1,editor.Snapshot().objects.Length);
                editor.Undo();Assert.IsTrue(executor.Execute(request,out error,out _),error);Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.IsFalse(editor.HasSavedObject(id));
            }
        }
        [UnityTest] public IEnumerator PendingCreationStopAfterAcceptanceDrainsOneSavedObject()
        {
            foreach(var kind in PendingCreationKinds){
                Assert.IsTrue(editor.TryFlush(out var error),error);using var pause=new PublicationPause();
                Assert.IsTrue(BeginCreationTest(kind,out var id,out var pending,out error,pause.Check),error);
                try{
                    yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                    pending.Dispose();Assert.IsFalse(pending.Drained);Assert.IsTrue(editor.HasSavedObject(id));Assert.IsFalse(pending.Complete(out _));
                }finally{pause.Release();}
                yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.AreEqual(1,new RoomStorage(directory).Load(out _).objects.Count(x=>x.id==id));
                editor.Undo();Assert.IsFalse(editor.HasSavedObject(id));
            }
        }
        [UnityTest] public IEnumerator PendingCreationTemporaryObjectsOnlyBecomeDurableOnKeep()
        {
            Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);yield return TemporaryPlacementSaveDone();
            Assert.IsTrue(BeginCreationTest("template",out var id,out var pending,out error),error);
            using(pending){
                yield return Completed(pending);Assert.IsFalse(pending.Published);Assert.IsTrue(editor.HasSavedObject(id));
                Assert.IsFalse(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id));editor.Undo();Assert.IsFalse(editor.HasSavedObject(id));editor.Redo();
                Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);yield return TemporaryPlacementSaveDone();
                Assert.IsTrue(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id));Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);Assert.IsTrue(editor.HasSavedObject(id));
            }
        }
        [UnityTest] public IEnumerator PendingCreationCancelledWaiterAndChangedCopySourceCannotCreateLater()
        {
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation cancelled=null,copy=null;
            int count=editor.Snapshot().objects.Length;
            try{
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.create").Module;var context=new CapabilityContext(editor,animations);
                Assert.IsTrue(module.Start(context,"cancelled",(JObject)PendingCreationCall("primitive")["arguments"],out cancelled,out error),error);
                Assert.IsTrue(module.Start(context,"copy",(JObject)PendingCreationCall("copy")["arguments"],out copy,out error),error);
                cancelled.Stop(false);Assert.IsTrue(editor.PaintObject(editor.Identity(block),Color.red,out error),error);
                blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,cancelled.State(out error));Assert.AreEqual(RuleActionState.Failed,copy.State(out error));StringAssert.Contains("target changed",error);
                Assert.IsEmpty(cancelled.Result);Assert.IsEmpty(copy.Result);Assert.AreEqual(count,editor.Snapshot().objects.Length);
                Assert.IsTrue(module.Start(context,"fresh",(JObject)PendingCreationCall("copy")["arguments"],out var fresh,out error),error);
                try{yield return PlacementOperationDone(fresh);Assert.AreEqual(Color.red,editor.Read((string)fresh.Result["objectId"]).color);}finally{fresh.Stop(false);}
            }finally{cancelled?.Stop(false);copy?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingCreationRechecksCapacityAfterWaitingWithoutInventingAResult()
        {
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation waiting=null;
            try{
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.create").Module;
                Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"waiting",(JObject)PendingCreationCall("primitive")["arguments"],out waiting,out error),error);
                int remaining=RoomDocument.MaximumObjects+2-editor.Snapshot().objects.Length;
                while(remaining>0){
                    int take=Math.Min(16,remaining);
                    var batch=new CreationBatch{position=new Vector3(2,1,1),blueprint=new CreationBlueprint{pieces=Enumerable.Range(0,take).Select(i=>new CreationPiece{
                        slot="piece_"+i,name="Capacity "+i,source=new CreationSource{kind="prototype",prototype=new CreationPrototype{kind="block",physics=new ObjectPhysicsSettings{mode="fixed",shape="automatic",mass=.5f}}}
                    }).ToArray()}};
                    Assert.IsTrue(editor.CreateBatch(batch,out _,out error,deferCreations:true),error);remaining-=take;
                }
                blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,waiting.State(out error));StringAssert.Contains("creation limit",error);Assert.IsEmpty(waiting.Result);
                Assert.AreEqual(RoomDocument.MaximumObjects+2,editor.Snapshot().objects.Length);Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
            }finally{waiting?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingCreationInterruptedReceiptIdentifiesAcceptedObjectWithoutClaimingSavedOutput()
        {
            using var pause=new PublicationPause();RoomEditor.PendingPersistedEdit pending=null;string id=null;
            Assert.IsTrue(PersistedEditOperation.StartCreation(editor,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool ok=editor.BeginCreatePrimitive(RoomObjectKind.Ball,"Interrupted ball",Vector3.up,1,Color.blue,out id,out edit,out issue,pause.Check);pending=edit;return ok;},
                ()=>new JObject{["objectId"]=id},out var operation,out var error),error);
            try{
                yield return Prepared(pending);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(pending.Accepted);
                operation.Stop(false);Assert.IsEmpty(operation.Result);StringAssert.Contains(id,operation.InterruptionStatus);StringAssert.Contains("Saving is still pending",operation.InterruptionStatus);
            }finally{pause.Release();operation.Stop(false);}
            yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.AreEqual(1,new RoomStorage(directory).Load(out _).objects.Count(x=>x.id==id));Assert.IsEmpty(operation.Result);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        string UnfinishedStage(string text="unfinished private bytes"){
            string path=Path.Combine(directory,RoomStorage.FileName)+".prepare."+Guid.NewGuid().ToString("N");
            File.WriteAllText(path,text);return path;
        }
        RoomAgentRequest MaintenanceRequest(string action,JObject args=null)=>new RoomAgentRequest{
            version=2,conditions=Array.Empty<RoomObjectCondition>(),
            commands=new[]{new RoomAgentCommand{action="execution",execution=new JObject{
                ["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,
                ["call"]=new JObject{["id"]="room.storage.preparations."+action,["version"]=1,["arguments"]=args??new JObject()}}}}};
        IEnumerator MaintenanceFinished(RoomStagingMaintenance.Job job){
            float end=Time.realtimeSinceStartup+20;
            while(!job.Worker.IsCompleted){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
            editor.StagingMaintenance.Poll();Assert.IsTrue(job.Finished);
        }
        IEnumerator MaintenanceReceipt(string id){
            float end=Time.realtimeSinceStartup+20;
            while((string)runtime.Scheduler.Invocation(id)["phase"]!="completed"&&(string)runtime.Scheduler.Invocation(id)["phase"]!="failed"){
                Assert.Less(Time.realtimeSinceStartup,end);yield return null;
            }
            Assert.AreEqual("completed",(string)runtime.Scheduler.Invocation(id)["phase"],runtime.Scheduler.Invocation(id).ToString());
        }
        JObject MaintenanceFact(string id,JObject args=null){
            Assert.IsTrue(BehaviourCatalog.TryRead(id,1,args??new JObject(),new BehaviourCatalog.FactContext(editor:editor),out var value),id);
            return JObject.FromObject(value.Value);
        }
        JObject DiscardArguments(JObject output,JObject entry)=>new JObject{
            ["inspectionId"]=(string)output["inspectionId"],["entryId"]=(string)entry["entryId"],
            ["fingerprint"]=(string)entry["fingerprint"],["confirmation"]="discard "+(string)entry["entryId"]};
        [UnityTest] public IEnumerator StagingMaintenanceUsesSharedReceiptsFactsAndOneShotDisposal()
        {
            string path=UnfinishedStage();var executor=new RoomAgentExecutor(editor);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(RoomStagingCapability.Feature));
            var inspect=MaintenanceRequest("inspect");string inspectId=(string)inspect.commands[0].execution["runId"];
            Assert.IsTrue(executor.Execute(inspect,out var error,out _),error);yield return MaintenanceReceipt(inspectId);
            var inspected=executor.Executions.Observe();
            var output=(JObject)runtime.Scheduler.Invocation(inspectId)["output"];
            Assert.AreEqual(1,(int)output["count"]);
            var status=MaintenanceFact("room.storage.preparations");
            Assert.AreEqual(inspectId,(string)status["runId"]);
            var entry=MaintenanceFact("room.storage.preparations.entry",new JObject{["inspectionId"]=(string)output["inspectionId"],["index"]=0});
            Assert.IsTrue((bool)entry["eligible"]);Assert.IsNull(entry["path"]);StringAssert.DoesNotContain("private bytes",entry.ToString());
            var args=DiscardArguments(output,entry);var wrong=(JObject)args.DeepClone();wrong["confirmation"]="discard "+new string('0',32);
            Assert.IsFalse(editor.StagingMaintenance.CanStart("discard",wrong,out _));Assert.IsTrue(File.Exists(path));
            string primary=Path.Combine(directory,RoomStorage.FileName);var before=File.ReadAllBytes(primary);
            var remove=MaintenanceRequest("discard",args);string removeId=(string)remove.commands[0].execution["runId"];
            Assert.IsTrue(executor.Execute(remove,out error,out _),error);yield return MaintenanceReceipt(removeId);
            Assert.IsFalse(File.Exists(path));CollectionAssert.AreEqual(before,File.ReadAllBytes(primary));
            Assert.IsFalse(BehaviourCatalog.TryRead("room.storage.preparations.entry",1,new JObject{["inspectionId"]=(string)output["inspectionId"],["index"]=0},new BehaviourCatalog.FactContext(editor:editor),out _));
            var removed=executor.Executions.Observe();
            File.WriteAllText(path,"new private preparation");
            Assert.IsTrue(executor.Execute(remove,out error,out _),error);Assert.IsTrue(File.Exists(path),"Receipt replay must not repeat disposal");
            Assert.IsFalse(editor.StagingMaintenance.CanStart("discard",args,out _));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_STAGING_MAINTENANCE_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"shared-receipts.json"),new JObject{
                ["inspection"]=inspected,["status"]=status,["entry"]=entry,["discard"]=removed}.ToString());}
        }
        [UnityTest] public IEnumerator StagingMaintenanceRefusesActiveWritersAndPreservesPinnedPreparations()
        {
            var service=editor.StagingMaintenance;string primary=Path.Combine(directory,RoomStorage.FileName);
            using(var live=PreparedFileStage.Create(primary,new byte[]{1,2,3},default)){
                Assert.IsTrue(service.Start("inspect","live-inspection",new JObject(),out var inspect,out var error),error);
                yield return MaintenanceFinished(inspect);Assert.IsNull(inspect.Error);
                var entry=service.Entry((string)inspect.Result["inspectionId"],0);Assert.IsFalse((bool)entry["eligible"]);
            }
            string path=UnfinishedStage();
            Assert.IsTrue(service.Start("inspect","inactive-inspection",new JObject(),out var reviewed,out var problem),problem);
            yield return MaintenanceFinished(reviewed);Assert.IsNull(reviewed.Error);
            var args=DiscardArguments(reviewed.Result,service.Entry((string)reviewed.Result["inspectionId"],0));
            using(var writer=editor.WriteGate.Write()){
                Assert.IsFalse(service.CanStart("discard",args,out problem));StringAssert.Contains("current edit",problem);
            }
            Assert.IsTrue(service.CanStart("discard",args,out problem),problem);Assert.IsTrue(File.Exists(path));
        }
        [UnityTest] public IEnumerator StagingMaintenanceCleanupReopensBoundedRotationAdmission()
        {
            for(int i=0;i<PreparedStageInventory.MaximumStages;i++)UnfinishedStage();
            string id=editor.Identity(block);var original=editor.Read(id).rotation;
            Assert.IsTrue(editor.BeginRotation(id,Quaternion.Euler(0,70,0),out var blocked,out var error),error);
            using(blocked){
                float end=Time.realtimeSinceStartup+20;bool failed=false;
                while(Time.realtimeSinceStartup<end){
                    if(!blocked.Poll(out var ready,out error)){failed=true;break;}
                    Assert.IsFalse(ready,"Full staging storage must not admit another candidate");yield return null;
                }
                Assert.IsTrue(failed);Assert.IsFalse(blocked.Published);Assert.AreEqual(original,editor.Read(id).rotation);
            }
            yield return Drained(blocked);
            var service=editor.StagingMaintenance;
            Assert.IsTrue(service.Start("inspect","full-inspection",new JObject(),out var review,out error),error);yield return MaintenanceFinished(review);
            Assert.IsNull(review.Error);Assert.AreEqual(4,(int)review.Result["count"]);
            var args=DiscardArguments(review.Result,service.Entry((string)review.Result["inspectionId"],0));
            Assert.IsTrue(service.Start("discard","cleanup",args,out var remove,out error),error);yield return MaintenanceFinished(remove);Assert.IsNull(remove.Error);
            Assert.IsTrue(editor.BeginRotation(id,Quaternion.Euler(0,70,0),out var pending,out error),error);
            using(pending){yield return Completed(pending);}
            yield return Drained(pending);Assert.Less(Quaternion.Angle(Quaternion.Euler(0,70,0),editor.Read(id).rotation),.01);
            Assert.AreEqual(3,Directory.GetFiles(directory,RoomStorage.FileName+".prepare.*").Length);
        }
        [UnityTest] public IEnumerator StagingMaintenanceCancellationClearsInspectionAndDrainsRetirement()
        {
            string path=UnfinishedStage();var service=editor.StagingMaintenance;
            Assert.IsTrue(service.Start("inspect","cancel-inspection",new JObject(),out var job,out var error),error);
            service.Cancel(job);editor.enabled=false;var retired=editor.WriteGate.Retire();
            yield return MaintenanceFinished(job);
            float end=Time.realtimeSinceStartup+20;while(!retired.IsCompleted){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
            Assert.IsNotNull(job.Error);Assert.AreEqual("",(string)service.Status()["inspectionId"]);Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(service.CanStart("inspect",new JObject(),out _));
        }
        [UnityTest] public IEnumerator StagingMaintenanceChangedBytesFailWithoutUndoingTheAcceptedRoom()
        {
            string path=UnfinishedStage("version-one");var service=editor.StagingMaintenance;
            Assert.IsTrue(service.Start("inspect","change-inspection",new JObject(),out var review,out var error),error);yield return MaintenanceFinished(review);
            var args=DiscardArguments(review.Result,service.Entry((string)review.Result["inspectionId"],0));
            File.WriteAllText(path,"version-two");var before=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            Assert.IsTrue(service.Start("discard","changed-discard",args,out var job,out error),error);yield return MaintenanceFinished(job);
            Assert.IsNotNull(job.Error);Assert.AreEqual("version-two",File.ReadAllText(path));
            CollectionAssert.AreEqual(before,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            Assert.IsFalse(service.CanStart("discard",args,out _));Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
        }
    }
}

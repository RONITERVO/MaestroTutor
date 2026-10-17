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
    internal static class SpatialSettingsCompletion
    {
        internal static IEnumerator Wait(RoomExecutions executions,string expected="completed")
        {
            float end=Time.realtimeSinceStartup+20;
            while((string)executions.Observe()["selected"]["phase"] is "preparing" or "running"){
                Assert.Less(Time.realtimeSinceStartup,end,"Settings save did not finish");yield return null;
            }
            Assert.AreEqual(expected,(string)executions.Observe()["selected"]["phase"],executions.Observe().ToString());
        }
    }
    public sealed partial class RoomRulesTests
    {
        JObject PendingPhysicsArgs(string target)=>new(){["target"]=target,["revision"]=editor.ObjectRevision(target),["mode"]="bouncy",["shape"]="sphere",["mass"]=2};
        [UnityTest] public IEnumerator PendingSettingsCapturePreferencesAndPreserveNewerLivePlacement()
        {
            string id=editor.Identity(block);var value=new ObjectPhysicsSettings{mode="bouncy",shape="sphere",mass=2};var before=editor.Read(id);
            Assert.IsTrue(editor.BeginConfigurePhysics(id,editor.ObjectRevision(id),value,out var pending,out var error),error);
            using(pending){
                value.mass=9;yield return Prepared(pending);Assert.AreEqual(before.mass,editor.Read(id).mass);
                var live=block.transform.position+Vector3.right*.25f;block.transform.position=live;
                yield return Completed(pending);Assert.AreEqual(2,editor.Read(id).mass);Assert.AreEqual(2,block.GetComponent<Rigidbody>().mass);
                Assert.AreEqual(live,block.transform.position);Assert.AreEqual(2,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).mass,error);
                editor.Undo();Assert.AreEqual(before.mass,editor.Read(id).mass);editor.Redo();Assert.AreEqual(2,editor.Read(id).mass);
            }
        }
        [UnityTest] public IEnumerator PendingSettingsCancelledOrStalePreparationCannotChangePreferences()
        {
            string id=editor.Identity(block);int revision=editor.ObjectRevision(id);var before=editor.Read(id);var value=new RoomWaterTraversal{mode="wade",maxDepthMetres=.2f};
            Assert.IsTrue(editor.BeginConfigureWaterTraversal(id,revision,value,out var cancelled,out var error),error);
            value.mode="ignore";yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(revision,editor.ObjectRevision(id));Assert.AreEqual(before.waterTraversal.mode,editor.Read(id).waterTraversal.mode);
            Assert.IsTrue(editor.BeginConfigureWaterTraversal(id,revision,new(){mode="avoid"},out var stale,out error),error);
            using(stale){
                yield return Prepared(stale);Assert.IsTrue(editor.PaintObject(id,Color.red,out error),error);
                Assert.IsFalse(stale.Poll(out _,out error));StringAssert.Contains("changed",error);yield return Drained(stale);
                Assert.AreEqual(before.waterTraversal.mode,editor.Read(id).waterTraversal.mode);Assert.AreEqual(Color.red,editor.Read(id).color);
                editor.Undo();Assert.AreEqual(before.color,editor.Read(id).color);
            }
        }
        [UnityTest] public IEnumerator PendingSettingsShareCreationQueueAndReturnOnlyConfirmedImmutableRevisions()
        {
            string target=editor.Identity(block);var context=new CapabilityContext(editor,animations);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            CapabilityOperation create=null,settings=null;
            try{
                yield return Prepared(blocker);var createModule=BehaviourCatalog.Action("object.create").Module;
                Assert.IsTrue(createModule.Start(context,"create",(JObject)PendingCreationCall("primitive")["arguments"],out create,out error),error);
                var args=PendingPhysicsArgs(target);var module=BehaviourCatalog.Action("object.physics.configure").Module;
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(context,"settings",args,out settings,out error),error);
                args["mass"]=9;Assert.IsEmpty(settings.Result);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Preparing,settings.State(out error),error);Assert.AreEqual(.5f,editor.Read(target).mass);
                yield return PlacementOperationDone(create);yield return PlacementOperationDone(settings);
                Assert.AreEqual(2,editor.Read(target).mass);Assert.AreEqual(target,(string)settings.Result["target"]);
                Assert.AreEqual(editor.ObjectRevision(target),(int)settings.Result["revision"]);
                settings.Result["revision"]=-1;Assert.AreEqual(editor.ObjectRevision(target),(int)settings.Result["revision"]);
                Assert.IsFalse((bool)settings.Result["temporary"]);
            }finally{create?.Stop(false);settings?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingSettingsReceiptsDoNotClaimEarlySuccessOrReplayAfterUndo()
        {
            string id=editor.Identity(block);var executor=new RoomAgentExecutor(editor);
            var request=ObjectEditRequest(new JObject{["id"]="object.physics.configure",["version"]=1,["arguments"]=PendingPhysicsArgs(id)});
            Assert.IsTrue(executor.Execute(request,out var error,out _),error);
            Assert.AreEqual("preparing",(string)executor.Executions.Observe()["selected"]["phase"]);Assert.IsNull(executor.Executions.Observe()["selected"]["output"]);
            Assert.IsTrue(executor.Execute(request,out error,out _),error);yield return AgentPlacementCompleted(executor);
            var receipt=executor.Executions.Observe()["selected"];Assert.AreEqual(editor.ObjectRevision(id),(int)receipt["output"]["revision"]);
            editor.Undo();Assert.AreEqual(.5f,editor.Read(id).mass);Assert.IsTrue(executor.Execute(request,out error,out _),error);
            Assert.AreEqual(.5f,editor.Read(id).mass);Assert.AreEqual(receipt["output"],executor.Executions.Observe()["selected"]["output"]);
        }
        [UnityTest] public IEnumerator PendingSettingsResultKeepsItsAcceptedRevisionAcrossLaterHumanEdits()
        {
            string id=editor.Identity(block);var module=BehaviourCatalog.Action("object.physics.configure").Module;
            Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"settings",PendingPhysicsArgs(id),out var operation,out var error),error);
            try{
                float end=Time.realtimeSinceStartup+20;
                while(operation.State(out error)==RuleActionState.Preparing){Assert.Less(Time.realtimeSinceStartup,end,error);yield return null;}
                Assert.AreEqual(RuleActionState.Ready,operation.State(out error),error);int applied=editor.ObjectRevision(id);
                Assert.IsTrue(editor.PaintObject(id,Color.red,out error),error);Assert.AreNotEqual(applied,editor.ObjectRevision(id));
                Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(applied,(int)operation.Result["revision"]);Assert.AreEqual(Color.red,editor.Read(id).color);
                Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(applied,(int)operation.Result["revision"]);
            }finally{operation.Stop(false);}
        }
        [UnityTest] public IEnumerator PendingSettingsAcceptedStopDrainsSaveWithoutReturningSuccess()
        {
            string id=editor.Identity(block);using var pause=new PublicationPause();RoomEditor.PendingPersistedEdit pending=null;
            Assert.IsTrue(PersistedEditOperation.Start(editor,id,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool ok=editor.BeginConfigurePhysics(id,editor.ObjectRevision(id),new(){mode="bouncy",shape="sphere",mass=2},out edit,out issue,pause.Check);pending=edit;return ok;},
                out var operation,out var error,output:()=>new JObject{["target"]=id,["revision"]=editor.ObjectRevision(id)}),error);
            try{
                yield return Prepared(pending);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(pending.Accepted);
                operation.Stop(false);Assert.IsEmpty(operation.Result);StringAssert.Contains("Saving is still pending",operation.InterruptionStatus);
                Assert.IsFalse(operation.Complete(out _));Assert.AreEqual(2,editor.Read(id).mass);
            }finally{pause.Release();operation.Stop(false);}
            yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.AreEqual(2,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).mass,error);
            editor.Undo();Assert.AreEqual(.5f,editor.Read(id).mass);
        }
        [UnityTest] public IEnumerator PendingSettingsRefuseChangedWaitingTargetWithoutAResult()
        {
            string id=editor.Identity(block);Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation settings=null;
            try{
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.physics.configure").Module.Start(new CapabilityContext(editor,animations),"waiting",PendingPhysicsArgs(id),out settings,out error),error);
                Assert.IsTrue(editor.PaintObject(id,Color.red,out error),error);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,settings.State(out error));StringAssert.Contains("target changed",error);Assert.IsEmpty(settings.Result);Assert.AreEqual(.5f,editor.Read(id).mass);
            }finally{settings?.Stop(false);blocker.Dispose();}
        }
    }
}

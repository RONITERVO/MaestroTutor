// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
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
        JObject NativePhysicsCall()=>new(){["id"]="physics.simulation.set",["version"]=1,["arguments"]=new JObject{["operation"]="start",["stateId"]=physics.ObserveSimulation()["stateId"]}};
        string RetirePhysicsBlock()
        {
            physics.SetSurfaces(true,"Aligned fixture floor");string id=editor.Identity(block),area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);return id;
        }
        IEnumerator DrainPhysicsPreparation()
        {
            float until=Time.realtimeSinceStartup+12;while(editor.NativeActivationPending&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(editor.NativeActivationPending,Is.False);
        }
        [UnityTest] public IEnumerator NativePhysicsStartAcquiresAllAreasWithoutSavedEditsAndReceiptCannotRestart()
        {
            physics.SetSurfaces(true,"Aligned floor");var ids=editor.Snapshot().objects.Where(o=>!o.IsBuiltIn).Select(o=>o.id).ToArray();
            foreach(var id in ids){string area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var issue),Is.True,issue);}
            int revision=editor.Revision;var executor=new RoomAgentExecutor(editor);var call=NativePhysicsCall();var request=new RoomAgentRequest{version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand{action="execution",execution=new JObject{["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=call}}}};string run=(string)request.commands[0].execution["runId"];
            Assert.That(executor.Catalog.Execute(new JObject{["operation"]="check",["call"]=call},out var error),Is.True,error);
            Assert.That((bool)executor.Catalog.Observe()["available"],Is.True);Assert.That(editor.NativeActivationPending,Is.False,"Preflight is passive");
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(physics.Running,Is.False);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("preparing"));
            yield return NativeActionDone(run);Assert.That(physics.Running,Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(ids.All(id=>editor.Find(id)&&!editor.NativeEntityDormant(id)),Is.True);
            Assert.That((bool)runtime.Scheduler.Invocation(run)["output"]["running"],Is.True);
            physics.PausePhysics();Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator NativePhysicsStopCancelsPreparationAndFreshRequestCanRetry()
        {
            string id=RetirePhysicsBlock();Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.CancelInvocation(run,out error),Is.True,error);yield return DrainPhysicsPreparation();Assert.That(physics.Running,Is.False);Assert.That(editor.Find(id),Is.Null);
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var retry,out error),Is.True,error);yield return NativeActionDone(retry);Assert.That(physics.Running,Is.True);
        }
        [UnityTest] public IEnumerator NativePhysicsManualPauseAfterLoadingStillCancelsBeforeStart()
        {
            string id=RetirePhysicsBlock();var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                Assert.That(scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);yield return NativeActionLoadedBeforeTick(id);
                Assert.That(physics.Running,Is.False,"Loading is not the Start effect");physics.PausePhysics();scheduler.Tick(Time.unscaledTime);
                Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That(physics.Running,Is.False);
            }finally{scheduler.StopAll();}
        }
        [UnityTest] public IEnumerator NativePhysicsScanAndLifecycleRoundTripsInvalidatePendingIntent()
        {
            string id=RetirePhysicsBlock();
            Action[] changes={physics.PausePhysics,()=>{physics.SetSurfaces(false,"Changed");physics.SetSurfaces(true,"Restored");},()=>{physics.SendMessage("OnApplicationFocus",false);physics.SendMessage("OnApplicationFocus",true);},()=>{physics.SendMessage("OnApplicationPause",true);physics.SendMessage("OnApplicationPause",false);},()=>{physics.enabled=false;physics.enabled=true;},()=>{Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out _),Is.True);Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],true,out _,out _),Is.True);}};
            int caseIndex=0;
            foreach(var change in changes){
                string area=editor.RegionFor(id);var activate=editor.ActivateNativeArea(area);yield return new WaitUntil(()=>activate.IsCompleted);Assert.That(activate.Result,Is.Null);
                Assert.That(editor.RetireNativeArea(area,out var retirement),Is.True,retirement);
                Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("preparing"));change();
                // Unity SendMessage dispatches focus/pause to every component on
                // the room root; the shared scheduler cancels those runs itself.
                yield return NativeActionDone(run,caseIndex is 2 or 3?"cancelled":"failed");caseIndex++;
                yield return DrainPhysicsPreparation();Assert.That(physics.Running,Is.False);Assert.That(editor.Find(id),Is.Null);
            }
        }
        [UnityTest] public IEnumerator NativePhysicsNewerStartSupersedesOldIntentButSharesTheNativePreparation()
        {
            string id=RetirePhysicsBlock();Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var first,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var second,out error),Is.True,error);
            yield return NativeActionDone(first,"failed");yield return NativeActionDone(second);Assert.That(physics.Running,Is.True);Assert.That(editor.Find(id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator NativePhysicsFailedImportNeverStartsAndFreshRetryUsesItsSameAsset()
        {
            var asset=ModelLibrary.Inspect("physics.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var create=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>create.IsCompleted);Assert.That(create.Exception,Is.Null);string id=create.Result;
            physics.SetSurfaces(true,"Aligned floor");Assert.That(editor.RetireNativeArea(NativeAreaFor(id),out var error),Is.True,error);yield return null;
            string path=Path.Combine(directory,"models",asset.Hash+".glb"),parked=path+".pending";File.Move(path,parked);
            try{Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var failed,out error),Is.True,error);yield return NativeActionDone(failed,"failed");yield return DrainPhysicsPreparation();Assert.That(physics.Running,Is.False);Assert.That(editor.Find(id),Is.Null);}finally{File.Move(parked,path);}
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var retry,out error),Is.True,error);yield return NativeActionDone(retry);Assert.That(physics.Running,Is.True);Assert.That(editor.Read(id).modelHash,Is.EqualTo(asset.Hash));
        }
        [UnityTest] public IEnumerator NativePhysicsMissingGroundFailsAfterPreparationAndLaterReadinessDoesNotResume()
        {
            string id=RetirePhysicsBlock();physics.SetSurfaces(false,"No scan");Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);
            yield return NativeActionDone(run,"failed");Assert.That(editor.Find(id),Is.Not.Null);Assert.That(physics.Running,Is.False);physics.SetSurfaces(true,"Later alignment");yield return null;Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator NativePhysicsPostLoadAuthoredEditInvalidatesTheCapturedWorld()
        {
            string id=RetirePhysicsBlock();var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                Assert.That(scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);yield return NativeActionLoadedBeforeTick(id);
                var changed=editor.Read(id);changed.position+=Vector3.up*.1f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
                scheduler.Tick(Time.unscaledTime);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That(physics.Running,Is.False);
            }finally{scheduler.StopAll();}
        }
        [UnityTest] public IEnumerator NativePhysicsNewSavedParticipantInvalidatesWholeWorldAdmission()
        {
            string id=RetirePhysicsBlock();var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                Assert.That(scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);yield return NativeActionLoadedBeforeTick(id);
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Later ball",new Vector3(1,1,1),1,Color.red,out _,out error),Is.True,error);
                scheduler.Tick(Time.unscaledTime);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That(physics.Running,Is.False);
            }finally{scheduler.StopAll();}
        }
        [UnityTest] public IEnumerator NativePhysicsPhysicalToolUsesTheSamePreparingReceiptAndPausePriority()
        {
            string id=RetirePhysicsBlock();var scan=root.AddComponent<ScannedRoom>();scan.Initialize(physics);physics.SetSurfaces(true,"Aligned floor");
            var tray=new GameObject("Physics tool fixture");tray.transform.SetParent(root.transform,false);var tools=tray.AddComponent<PhysicsTools>();tools.Build(editor,physics,scan,root.GetComponent<RoomInteraction>());
            string run=runtime.Scheduler.Receipts.NextId;tray.GetComponentsInChildren<RuleToolAction>().Single(x=>x.AccessibleName=="Start physics").Command();Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("preparing"));
            tray.GetComponentsInChildren<RuleToolAction>().Single(x=>x.AccessibleName=="Pause").Command();yield return NativeActionDone(run,"failed");yield return DrainPhysicsPreparation();Assert.That(physics.Running,Is.False);Assert.That(editor.Find(id),Is.Null);
            Assert.That(tools.RequestPhysicsStart(out var retry,out var error),Is.True,error);yield return NativeActionDone(retry);Assert.That(physics.Running,Is.True);
        }
        [UnityTest] public IEnumerator NativePhysicsRuntimeHoldCancelsWithoutLateStart()
        {
            string id=RetirePhysicsBlock();Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);
            using(editor.RuntimeGate.Hold("Review current room")){}yield return NativeActionDone(run,"cancelled");yield return DrainPhysicsPreparation();Assert.That(physics.Running,Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator NativePhysicsStalePreflightDoesNotStartLoading()
        {
            string id=RetirePhysicsBlock();var stale=NativePhysicsCall();physics.PausePhysics();var before=physics.ObserveSimulation();
            Assert.That(runtime.Scheduler.Invoke(stale,Time.unscaledTime,out _,out var error),Is.False);Assert.That(error,Does.Contain("changed"));
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(JToken.DeepEquals(before,physics.ObserveSimulation()),Is.True);Assert.That(editor.Find(id),Is.Null);yield return null;
        }
        [UnityTest] public IEnumerator NativePhysicsLostPreparedInstanceCannotBeSkippedAtCompletion()
        {
            string id=RetirePhysicsBlock();var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                Assert.That(scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);yield return NativeActionLoadedBeforeTick(id);
                UnityEngine.Object.Destroy(editor.Find(id).gameObject);yield return null;scheduler.Tick(Time.unscaledTime);
                Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That(physics.Running,Is.False);
            }finally{scheduler.StopAll();}
        }
        [UnityTest] public IEnumerator NativePhysicsDeadlineCancelsWithoutStartingAfterLatePreparation()
        {
            string id=RetirePhysicsBlock();var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                float now=Time.unscaledTime;Assert.That(scheduler.Invoke(NativePhysicsCall(),now,out var run,out var error),Is.True,error);
                scheduler.Tick(now+121);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));yield return DrainPhysicsPreparation();Assert.That(physics.Running,Is.False);Assert.That(editor.Find(id),Is.Null);
            }finally{scheduler.StopAll();}
        }
    }
}

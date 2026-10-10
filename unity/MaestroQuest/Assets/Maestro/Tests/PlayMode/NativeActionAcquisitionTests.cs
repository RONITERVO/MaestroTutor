// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
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
        IEnumerator NativeActionDone(string run,string phase="completed")
        {
            float deadline=Time.realtimeSinceStartup+12;
            while((string)runtime.Scheduler.Invocation(run)?["phase"] is "running" or "preparing"&&Time.realtimeSinceStartup<deadline)yield return null;
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt?["phase"],Is.EqualTo(phase),receipt?.ToString());
        }
        JObject NativeMove(string target)=>ObjectEditCall("object.position.set",target,("x",.6f),("y",1.2f),("z",.8f));
        IEnumerator NativeActionLoadedBeforeTick(string id)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(id).isActiveAndEnabled,Is.True);
        }
        [UnityTest] public IEnumerator NativeActionPostLoadEditCannotBeOverwrittenOnTheNextSchedulerFrame()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                Assert.That(scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.True,error);
                yield return NativeActionLoadedBeforeTick(id);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("preparing"));
                var changed=editor.Read(id);changed.position+=Vector3.left*.2f;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);int revision=editor.Revision;
                scheduler.Tick(Time.unscaledTime);
                Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That((string)scheduler.Invocation(run)["status"],Does.Contain("changed before"));
                Assert.That(editor.Read(id).position,Is.EqualTo(changed.position));Assert.That(editor.Revision,Is.EqualTo(revision));
            }finally{scheduler.StopAll();}
        }
        [UnityTest] public IEnumerator NativeActionPostLoadTemporaryRoomBoundaryCannotReceiveTheOldAction()
        {
            yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                Assert.That(scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.True,error);yield return NativeActionLoadedBeforeTick(id);
                int revision=editor.Revision;var position=editor.Read(id).position;
                Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);Assert.That(editor.Revision,Is.GreaterThan(revision));
                scheduler.Tick(Time.unscaledTime);
                Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That((string)scheduler.Invocation(run)["status"],Does.Contain("changed before"));Assert.That(editor.Read(id).position,Is.EqualTo(position));
                while(editor.TemporarySavePending)yield return null;
            }finally{scheduler.StopAll();}
        }
        [UnityTest] public IEnumerator NativeActionPostLoadEditorRestartIsStaleAtTheSameRevision()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var scheduler=new RuleScheduler(new RoomRuleActions(editor,animations));
            try{
                Assert.That(scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.True,error);yield return NativeActionLoadedBeforeTick(id);
                int revision=editor.Revision;var position=editor.Read(id).position;
                editor.enabled=false;editor.enabled=true;Assert.That(editor.Revision,Is.EqualTo(revision));
                scheduler.Tick(Time.unscaledTime);
                Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That((string)scheduler.Invocation(run)["status"],Does.Contain("changed before"));Assert.That(editor.Read(id).position,Is.EqualTo(position));
            }finally{scheduler.StopAll();}
        }
        [UnityTest] public IEnumerator NativeActionAgentInvocationLoadsBeforeItsInstantEffectAndKeepsOneUndo()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);var saved=editor.Read(id);int revision=editor.ObjectRevision(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var request=ObjectEditRequest(NativeMove(id));string run=(string)request.commands[0].execution["runId"];var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Catalog.Execute(new JObject{["operation"]="check",["call"]=NativeMove(id)},out error),Is.True,error);
            var check=executor.Catalog.Observe();Assert.That((bool)check["available"],Is.True);Assert.That((string)check["status"],Does.Contain("loading required objects"));Assert.That(editor.Find(id),Is.Null);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("preparing"));
            Assert.That((string)runtime.Scheduler.Invocation(run)["status"],Is.EqualTo("Loading required objects"));
            Assert.That(editor.Read(id).position,Is.EqualTo(saved.position));Assert.That(editor.Find(id),Is.Null);
            yield return NativeActionDone(run);
            Assert.That(editor.Read(id).position,Is.EqualTo(new Vector3(.6f,1.2f,.8f)));Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision+1));
            Assert.That(editor.Find(id).isActiveAndEnabled,Is.True);Assert.That(editor.NativeEntityDormant(id),Is.False);
            editor.Undo();Assert.That(editor.Read(id).position,Is.EqualTo(saved.position));
        }
        [UnityTest] public IEnumerator NativeActionCancelNeverAppliesEffectAndCanRetryAfterCleanup()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);var saved=editor.Read(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.True,error);
            Assert.That(runtime.Scheduler.CancelInvocation(run,out error),Is.True,error);yield return NativeActionDone(run,"cancelled");
            float deadline=Time.realtimeSinceStartup+5;while(editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Read(id).position,Is.EqualTo(saved.position));Assert.That(editor.Find(id),Is.Null);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var retry,out error),Is.True,error);yield return NativeActionDone(retry);
            Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));
        }
        [UnityTest] public IEnumerator NativeActionRuntimeHoldCancelsLoadingWithoutLatePublication()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.True,error);
            using(var hold=editor.RuntimeGate.Hold("Pause pending acquisition")){}yield return NativeActionDone(run,"cancelled");
            for(int i=0;i<4;i++)yield return null;Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator NativeActionSavedFactDoesNotLoadAndMissingObjectCannotBeResurrected()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            Assert.That((bool)PresenceFact(id)["saved"],Is.True);Assert.That(editor.Read(id),Is.Not.Null);Assert.That(editor.Find(id),Is.Null);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.False);Assert.That(error,Does.Contain("missing"));
            Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That(editor.HasSavedObject(id),Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator NativeActionLoadsConnectedAreasAsOneDependencySet()
        {
            var (id,peer)=FixedPieces();Assert.That(Connect(AttachCall(id,peer,0),out var error),Is.True,error);string area=NativeAreaFor(id);NativeAreaFor(peer);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(ObjectEditCall("object.color.set",id,("red",1),("green",0),("blue",0)),Time.unscaledTime,out var run,out error),Is.True,error);
            Assert.That(editor.Find(peer),Is.Null);yield return NativeActionDone(run);
            Assert.That(editor.Find(peer).isActiveAndEnabled,Is.True);Assert.That(editor.Find(id).isActiveAndEnabled,Is.True);
        }
        [UnityTest] public IEnumerator NativeActionLoadingRequiresExclusiveAdmissionButSavedReferencesDoNot()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var wait=new JObject{["id"]="time.wait",["version"]=1,["arguments"]=new JObject{["seconds"]=10}};
            Assert.That(runtime.Scheduler.Invoke(wait,Time.unscaledTime,out var waiting,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.False);Assert.That(error,Does.Contain("current interaction or action"));Assert.That(editor.Find(id),Is.Null);
            Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));runtime.StopAll();
        }
        [UnityTest] public IEnumerator NativeActionImportFailureIsVisibleAndRetryUsesTheSameModel()
        {
            var asset=ModelLibrary.Inspect("action.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var placement=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>placement.IsCompleted);string id=placement.Result,area=NativeAreaFor(id);var saved=editor.Read(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string path=Path.Combine(directory,"models",asset.Hash+".glb"),parked=path+".pending";File.Move(path,parked);
            try{
                runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var failed,out error);
                yield return NativeActionDone(failed,"failed");Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Read(id).position,Is.EqualTo(saved.position));
            }finally{File.Move(parked,path);}
            Assert.That(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var run,out error),Is.True,error);yield return NativeActionDone(run);
            Assert.That(editor.Find(id).GetComponent<CreatedRoomObject>().ModelGeometryReady,Is.True);Assert.That(editor.Read(id).modelHash,Is.EqualTo(asset.Hash));
        }
    }
}

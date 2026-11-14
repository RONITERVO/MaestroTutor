// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    internal static class ControllerSaveTestWait
    {
        internal static IEnumerator Until(Func<bool> ready,string label)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while(!ready()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(ready(),Is.True,label);
        }
        internal static IEnumerator Manual(MovementControls controls)=>Until(()=>!controls.ConfigurationPending,"Controller settings did not drain");
        internal static IEnumerator Receipt(RoomExecutions actions)=>Until(()=>{
            string phase=(string)actions.Observe()["selected"]?["phase"];
            return phase=="completed"||phase=="failed"||phase=="cancelled";
        },"Controller receipt did not finish");
        internal static IEnumerator Operation(CapabilityOperation operation)
        {
            yield return Until(()=>operation.State(out _)!=RuleActionState.Preparing,"Controller operation did not finish");
            Assert.That(operation.State(out var error),Is.EqualTo(RuleActionState.Ready),error);
            Assert.That(operation.Complete(out error),Is.True,error);
        }
    }
    public sealed partial class RoomRulesTests
    {
        sealed class HeldControllerSave:IDisposable
        {
            readonly ManualResetEventSlim release=new(false);
            readonly TaskCompletionSource<bool> entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal bool Entered=>entered.Task.IsCompleted;
            internal int WorkerThread {get;private set;}
            internal void Checkpoint(string stage)
            {
                if(stage!="saving")return;
                WorkerThread=Thread.CurrentThread.ManagedThreadId;entered.TrySetResult(true);
                if(!release.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException("Test did not release controller save");
            }
            internal void Release()=>release.Set();
            public void Dispose()=>Release();
        }
        [UnityTest] public IEnumerator ControllerSavingKeepsOldBindingsAndFramesAliveUntilDetachedCopyConfirms()
        {
            var controls=BindingControls();var before=ControlFact();var next=controls.Preferences;
            next.deadZone=.3f;next.buttons[1]=new ControllerBinding {command=ControllerCommand.Sequence,sequenceId=sequenceId};
            bindingFrame.a=true;int ownerThread=Thread.CurrentThread.ManagedThreadId,changedThread=0;
            Action changed=()=>changedThread=Thread.CurrentThread.ManagedThreadId;controls.Changed+=changed;
            using var hold=new HeldControllerSave();CapabilityOperation operation=null;
            try{
                Assert.That(controls.StartConfiguration((string)before["configurationId"],next,out operation,out var error,hold.Checkpoint),Is.True,error);
                next.deadZone=.4f;next.buttons[1].sequenceId=new string('f',32);
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                yield return ControllerSaveTestWait.Until(()=>hold.Entered,"Save did not reach worker");
                Assert.That(hold.WorkerThread,Is.Not.EqualTo(ownerThread));
                Assert.That(editor.WriteGate.TryFreeze(out error),Is.Null,"Archive cannot capture older live values alongside a pending file");
                for(int i=0;i<10;i++){yield return null;Assert.That(JToken.DeepEquals(before,ControlFact()),Is.True);}
                Assert.That(runtime.Scheduler.RunningCount,Is.Zero);hold.Release();
                yield return ControllerSaveTestWait.Operation(operation);
                Assert.That(changedThread,Is.EqualTo(ownerThread));
                var after=ControlFact();Assert.That((double)after["deadZone"],Is.EqualTo(.3).Within(.00001));
                Assert.That((string)after["buttons"][1]["programId"],Is.EqualTo(sequenceId));
                Assert.That((string)operation.Result["configurationId"],Is.EqualTo((string)after["configurationId"]));
                Assert.That(controls.UserEnabled||controls.AvatarEnabled,Is.False);
                yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Held input must be neutral after confirmed rebinding");
                using var freeze=editor.WriteGate.TryFreeze(out error);Assert.That(freeze,Is.Not.Null,error);
                var saved=new ControllerPreferenceStorage(directory).Load(out error);Assert.That(error,Is.Null);Assert.That(saved.deadZone,Is.EqualTo(.3f));Assert.That(saved.buttons[1].sequenceId,Is.EqualTo(sequenceId));
            }finally{hold.Release();operation?.Stop(false);controls.Changed-=changed;}
            yield return ControllerSaveTestWait.Manual(controls);
        }
        [UnityTest] public IEnumerator ControllerCancellationBeforeDispatchLeavesNoSaveAndReleasesAdmission()
        {
            var controls=BindingControls();var before=ControlFact();var next=controls.Preferences;next.deadZone=.3f;
            Assert.That(controls.StartConfiguration((string)before["configurationId"],next,out var operation,out var error),Is.True,error);
            operation.Stop(false);yield return null;
            Assert.That(operation.State(out _),Is.EqualTo(RuleActionState.Failed));
            Assert.That(File.Exists(Path.Combine(directory,"controls.v2.json")),Is.False);
            Assert.That(JToken.DeepEquals(before,ControlFact()),Is.True);Assert.That(editor.PendingEditSave,Is.False);
            Assert.That(controls.Apply(next),Is.True);yield return ControllerSaveTestWait.Manual(controls);Assert.That(controls.Preferences.deadZone,Is.EqualTo(.3f));
        }
        [UnityTest] public IEnumerator StoppedAcceptedControllerSaveFinishesAndStaleQueuedChangesCannotOverwriteIt()
        {
            var controls=BindingControls();var before=ControlFact();var next=controls.Preferences;next.deadZone=.3f;
            using var hold=new HeldControllerSave();CapabilityOperation first=null,stale=null,cancelled=null;
            try{
                Assert.That(controls.StartConfiguration((string)before["configurationId"],next,out first,out var error,hold.Checkpoint),Is.True,error);
                Assert.That(first.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                yield return ControllerSaveTestWait.Until(()=>hold.Entered,"Save did not reach worker");
                var later=controls.Preferences;later.deadZone=.35f;
                Assert.That(controls.StartConfiguration((string)before["configurationId"],later,out stale,out error),Is.True,error);
                Assert.That(stale.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                Assert.That(controls.StartConfiguration((string)before["configurationId"],later,out cancelled,out error),Is.True,error);cancelled.Stop(false);
                first.Stop(false);StringAssert.Contains("continues",first.InterruptionStatus);
                Assert.That(JToken.DeepEquals(before,ControlFact()),Is.True);hold.Release();
                yield return ControllerSaveTestWait.Manual(controls);
                Assert.That(controls.Preferences.deadZone,Is.EqualTo(.3f));Assert.That(stale.State(out error),Is.EqualTo(RuleActionState.Failed));StringAssert.Contains("changed",error);
                Assert.That(cancelled.State(out _),Is.EqualTo(RuleActionState.Failed));Assert.That(editor.PendingEditSave,Is.False);
                using var freeze=editor.WriteGate.TryFreeze(out error);Assert.That(freeze,Is.Not.Null,error);
            }finally{hold.Release();first?.Stop(false);stale?.Stop(false);cancelled?.Stop(false);}
        }
        [UnityTest] public IEnumerator CompletedControllerOutputKeepsExactIdentityAfterAnotherManualSave()
        {
            var controls=BindingControls();var next=controls.Preferences;next.deadZone=.3f;
            Assert.That(controls.StartConfiguration((string)ControlFact()["configurationId"],next,out var operation,out var error),Is.True,error);
            yield return ControllerSaveTestWait.Operation(operation);var result=operation.Result;
            next.deadZone=.35f;Assert.That(controls.Apply(next),Is.True);yield return ControllerSaveTestWait.Manual(controls);
            Assert.That(JToken.DeepEquals(operation.Result,result),Is.True);
            Assert.That((string)ControlFact()["configurationId"],Is.Not.EqualTo((string)result["configurationId"]));
            result["configurationId"]="caller mutation";Assert.That((string)operation.Result["configurationId"],Is.Not.EqualTo("caller mutation"));operation.Stop(false);
        }
        [UnityTest] public IEnumerator ControllerSaveRefusesIndependentReplacementWithoutBlessingItsBytes()
        {
            var controls=BindingControls();Assert.That(controls.Apply(controls.Preferences),Is.True);yield return ControllerSaveTestWait.Manual(controls);
            var before=ControlFact();var next=controls.Preferences;next.deadZone=.3f;
            using var hold=new HeldControllerSave();CapabilityOperation operation=null;
            try{
                Assert.That(controls.StartConfiguration((string)before["configurationId"],next,out operation,out var error,hold.Checkpoint),Is.True,error);
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);yield return ControllerSaveTestWait.Until(()=>hold.Entered,"Save did not reach worker");
                string path=Path.Combine(directory,"controls.v2.json");var external=controls.Preferences;external.userSpeed=1;string bytes=JsonUtility.ToJson(external);File.WriteAllText(path,bytes);
                hold.Release();yield return ControllerSaveTestWait.Until(()=>operation.State(out _)!=RuleActionState.Preparing,"Conflict did not finish");
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Failed),error);Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));
                var after=ControlFact();Assert.That((bool)after["storageReady"],Is.False);after["storageReady"]=before["storageReady"].DeepClone();
                Assert.That(JToken.DeepEquals(after,before),Is.True);Assert.That(controls.Apply(next),Is.False);Assert.That(editor.PendingEditSave,Is.False);
            }finally{hold.Release();operation?.Stop(false);}
        }
        [UnityTest] public IEnumerator ControllerRetirementDrainsAcceptedSaveEvenAfterUnityOwnerIsDestroyed()
        {
            var controls=BindingControls();var next=controls.Preferences;next.deadZone=.3f;
            using var hold=new HeldControllerSave();CapabilityOperation operation=null;
            try{
                Assert.That(controls.StartConfiguration((string)ControlFact()["configurationId"],next,out operation,out var error,hold.Checkpoint),Is.True,error);
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);yield return ControllerSaveTestWait.Until(()=>hold.Entered,"Save did not reach worker");
                UnityEngine.Object.Destroy(controls);yield return null;var retirement=editor.WriteGate.Retire();
                for(int i=0;i<3;i++)yield return null;Assert.That(retirement.IsCompleted,Is.False,"Old workspace must retain its writer");
                hold.Release();yield return ControllerSaveTestWait.Until(()=>retirement.IsCompleted,"Accepted writer did not drain after destruction");
                Assert.That(retirement.IsFaulted,Is.False);var saved=new ControllerPreferenceStorage(directory).Load(out error);
                Assert.That(error,Is.Null);Assert.That(saved.deadZone,Is.EqualTo(.3f));
                Assert.That(operation.State(out _),Is.EqualTo(RuleActionState.Failed),"A destroyed owner cannot report live application");
            }finally{hold.Release();operation?.Stop(false);}
        }
        [UnityTest] public IEnumerator UnconfirmedControllerPublicationPreservesFileAndOldLiveSettingsUntilReopen()
        {
            var controls=BindingControls();var before=ControlFact();var next=controls.Preferences;next.deadZone=.3f;
            Assert.That(controls.StartConfiguration((string)before["configurationId"],next,out var operation,out var error,
                stage=>{if(stage=="saved")throw new IOException("Simulated lost publication acknowledgement");}),Is.True,error);
            yield return ControllerSaveTestWait.Until(()=>operation.State(out _)!=RuleActionState.Preparing,"Unconfirmed save did not finish");
            Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Failed));StringAssert.Contains("not be confirmed",error);
            var after=ControlFact();Assert.That((bool)after["storageReady"],Is.False);after["storageReady"]=before["storageReady"].DeepClone();
            Assert.That(JToken.DeepEquals(after,before),Is.True,"Unconfirmed storage cannot activate new bindings");
            Assert.That(controls.Apply(next),Is.False);Assert.That(editor.PendingEditSave,Is.False);
            var reopened=new ControllerPreferenceStorage(directory);Assert.That(reopened.Load(out error).deadZone,Is.EqualTo(.3f));Assert.That(error,Is.Null);
            operation.Stop(false);
        }
        [UnityTest] public IEnumerator FailingControllerStatusListenerCannotStrandConfirmedSaveOrWorkspace()
        {
            var controls=BindingControls();var before=ControlFact();var next=controls.Preferences;next.deadZone=.3f;
            Action broken=()=>throw new InvalidOperationException("Listener fixture");
            Assert.That(controls.StartConfiguration((string)before["configurationId"],next,out var operation,out var error),Is.True,error);
            controls.Changed+=broken;
            try{
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                yield return ControllerSaveTestWait.Until(()=>operation.State(out _)!=RuleActionState.Preparing,"Listener failure stranded save");
                Assert.That(operation.State(out _),Is.EqualTo(RuleActionState.Failed));
                Assert.That(editor.PendingEditSave,Is.False);
                using var freeze=editor.WriteGate.TryFreeze(out error);Assert.That(freeze,Is.Not.Null,error);
                Assert.That(new ControllerPreferenceStorage(directory).Load(out error).deadZone,Is.EqualTo(.3f));
                Assert.That((string)ControlFact()["configurationId"],Is.Not.EqualTo((string)before["configurationId"]),"Inspect live settings before retrying unconfirmed completion");
            }finally{controls.Changed-=broken;operation.Stop(false);}
        }
    }
}

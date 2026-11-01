// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
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
    public sealed partial class RoomEditorTests
    {
        PhysicsTools PhysicalPhysicsTray()
        {
            var scan=root.AddComponent<ScannedRoom>();scan.Initialize(physics);
            var tray=new GameObject("Physical physics test tray");tray.transform.SetParent(root.transform,false);
            var tools=tray.AddComponent<PhysicsTools>();tools.Build(editor,physics,scan,room);return tools;
        }
        [UnityTest] public IEnumerator PhysicalPhysicsWaitsForSavingAndKeepsTheClickedTarget()
        {
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);editor.Select(editor.Find(target.id));
            Assert.That(editor.TryFlush(out var error),Is.True,error);int thread=Thread.CurrentThread.ManagedThreadId;
            var path=Path.Combine(directory,RoomStorage.FileName);var saved=File.ReadAllBytes(path);var tray=PhysicalPhysicsTray();
            using var hold=new HeldManualPublication();editor.ManualCreationCheckpoint=hold.Check;
            try{
                editor.CycleMass();Assert.That(editor.ManualCreationPending,Is.True,"The physical setting must expose pending completion");
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Physics setting did not reach publication");
                Assert.That(hold.ThreadId,Is.Not.EqualTo(thread));
                physics.SetSurfaces(false,"Unrelated room status");
                Assert.That(tray.GetComponentsInChildren<TextMesh>().Any(x=>x.text.Contains("Preparing and saving object edit")),Is.True,"The tray hid the pending edit behind scan/physics status");
                float accepted=editor.Read(target.id).mass;Assert.That(accepted,Is.GreaterThan(target.mass));
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved),"Pending publication must leave the accepted primary intact");
                Assert.That(editor.Find(target.id).GetComponent<Rigidbody>().mass,Is.EqualTo(accepted));
                Assert.That(editor.CanUndo,Is.False);
                Assert.That(editor.Ownership.CanAcquire("competing",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim(target.id,"wholeTarget")},out _),Is.False);
                editor.CycleMass();Assert.That(editor.Read(target.id).mass,Is.EqualTo(accepted),"Repeated pending taps must not advance twice");
                editor.Select(editor.Find("book"));hold.Release();yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(editor.SelectedId,Is.EqualTo("book"));
                Assert.That(tray.GetComponentsInChildren<TextMesh>().Any(x=>x.text.Contains("Physics settings saved")),Is.True,editor.Status);
                Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==target.id).mass,Is.EqualTo(accepted));
                editor.Undo();Assert.That(editor.Read(target.id).mass,Is.EqualTo(target.mass));
                editor.Redo();Assert.That(editor.Read(target.id).mass,Is.EqualTo(accepted));
            }finally{hold.Release();editor.ManualCreationCheckpoint=null;}
        }
        [UnityTest] public IEnumerator PhysicalPhysicsQueuedSettingsCopyIntentAndCancelBeforeAdmission()
        {
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);editor.Select(editor.Find(target.id));
            Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Ball,"blocker",Vector3.up,1,Color.white,out _,out var blocker,out var error),Is.True,error);
            using(blocker){
                editor.CycleMass();Assert.That(editor.ManualCreationPending,Is.True);
                editor.CancelManualCreation(true);Assert.That(editor.ManualCreationPending,Is.False);
                Assert.That(editor.Read(target.id).mass,Is.EqualTo(target.mass));
                var intent=new ObjectPhysicsSettings {mode="solid",shape="box",mass=3.25f};
                editor.ConfigurePhysicsManually(target.id,intent);Assert.That(editor.ManualCreationPending,Is.True);
                intent.mass=20;intent.shape="sphere";editor.Select(editor.Find("book"));
                for(int i=0;i<3;i++)yield return null;
                Assert.That(editor.Read(target.id).mass,Is.EqualTo(target.mass));
            }
            yield return PhysicalCreationTestWait.Finish(editor);
            var result=editor.Read(target.id);Assert.That(result.mass,Is.EqualTo(3.25f),editor.Status);
            Assert.That(result.collisionShape,Is.EqualTo(ItemCollider.Box));Assert.That(result.physics,Is.EqualTo(ItemPhysics.Solid));
            Assert.That(editor.SelectedId,Is.EqualTo("book"));Assert.That(physics.Running,Is.False);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==target.id).mass,Is.EqualTo(3.25f));
        }
        [UnityTest] public IEnumerator PhysicalPhysicsQueuedSettingsRefuseAChangedTarget()
        {
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);editor.Select(editor.Find(target.id));
            Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Ball,"blocker",Vector3.up,1,Color.white,out _,out var blocker,out var error),Is.True,error);
            using(blocker){
                editor.CycleMass();Assert.That(editor.ManualCreationPending,Is.True);
                Assert.That(editor.PaintObject(target.id,Color.blue,out error),Is.True,error);
            }
            yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Read(target.id).mass,Is.EqualTo(target.mass));Assert.That(editor.Read(target.id).color,Is.EqualTo(Color.blue));
            Assert.That(editor.Status,Does.Contain("changed"));
        }
        [UnityTest] public IEnumerator PhysicalPhysicsAcceptedFailureKeepsLiveSettingsUnconfirmedWithoutReplay()
        {
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Heavy lesson block",new Vector3(2,1,2),1,Color.white,out var id,out var error),Is.True,error);
            var target=editor.Read(id);editor.Select(editor.Find(id));
            Assert.That(editor.TryFlush(out error),Is.True,error);
            var path=Path.Combine(directory,RoomStorage.FileName);var saved=File.ReadAllBytes(path);
            var tray=PhysicalPhysicsTray();
            editor.ManualCreationCheckpoint=phase=>{if(phase=="publishing")throw new IOException("Injected physics settings publication failure");};
            try{
                editor.CycleMass();yield return PhysicalCreationTestWait.Finish(editor);
                yield return PhysicalCreationTestWait.Until(()=>!editor.PendingEditSave,"Failed physics save did not drain");
                float accepted=editor.Read(target.id).mass;Assert.That(accepted,Is.GreaterThan(target.mass));
                Assert.That(editor.Status,Does.Contain("accepted"));Assert.That(editor.Status,Does.Contain("Physics tools: Heavy lesson block"));
                Assert.That(tray.GetComponentsInChildren<TextMesh>().Any(x=>x.text.Contains("accepted")),Is.True,"The physical tray must expose unconfirmed acceptance");
                Assert.That(editor.CanSaveRoom,Is.False);Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved));
                Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Physics tools"),Is.False);
                for(int i=0;i<8;i++)yield return null;
                Assert.That(editor.Read(target.id).mass,Is.EqualTo(accepted));Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved));
            }finally{editor.ManualCreationCheckpoint=null;}
        }
        [UnityTest] public IEnumerator PhysicalPhysicsTrayEditsNativeBodiesInsideTemporaryRoom()
        {
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Ball);editor.Select(editor.Find(target.id));
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);
            yield return PhysicalCreationTestWait.Until(()=>!editor.TemporarySavePending,"Temporary baseline did not save");
            var path=Path.Combine(directory,RoomStorage.FileName);var before=File.ReadAllBytes(path);
            var tray=PhysicalPhysicsTray();
            RuleToolAction Button(string name)=>tray.GetComponentsInChildren<RuleToolAction>().Single(x=>x.AccessibleName==name);
            Button("Object mode").Activate();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Read(target.id).physics,Is.EqualTo(ItemPhysics.Solid),editor.Status);
            Button("Collision shape").Activate();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Find(target.id).Grab.colliders.Single(),Is.TypeOf<BoxCollider>());
            Button("Mass").Activate();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Find(target.id).GetComponent<Rigidbody>().mass,Is.GreaterThan(target.mass));
            Assert.That(physics.Running,Is.False,"Settings must not start room physics");
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));
            editor.Undo();Assert.That(editor.Read(target.id).mass,Is.EqualTo(target.mass));
            editor.Undo();Assert.That(editor.Read(target.id).collisionShape,Is.EqualTo(target.collisionShape));
            editor.Undo();Assert.That(editor.Read(target.id).physics,Is.EqualTo(target.physics));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
        }
    }
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator PhysicalPhysicsTakesOnlyTheTargetProgram()
        {
            string id=editor.Identity(block);float mass=editor.Read(id).mass;
            Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(runtime.Scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=30}},Time.unscaledTime,out var waiting,out var error),Is.True,error);
            yield return new WaitForSeconds(.15f);var position=block.transform.localPosition;
            editor.CycleMass();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Read(id).mass,Is.GreaterThan(mass),editor.Status);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
            Assert.That(Vector3.Distance(position,block.transform.localPosition),Is.LessThan(.025f));
        }
        [UnityTest] public IEnumerator PhysicalPhysicsCannotDisplaceAHigherPriorityGrip()
        {
            string id=PhysicalEditingOther();editor.Select(editor.Find(id));float mass=editor.Read(id).mass;
            Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(editor.Ownership.TryAcquire("held","Your grip",RoomActorRole.Grab,new[]{new BehaviourCatalog.Claim(id,"wholeTarget")},_=>Assert.Fail("Physics controls displaced a grip"),out var held,out var error),Is.True,error);
            using(held){
                editor.CycleMass();yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(held.Held,Is.True);Assert.That(editor.Read(id).mass,Is.EqualTo(mass));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator PhysicalPhysicsKeepsTheTargetRecordingDraftRecoverable()
        {
            string id=editor.Identity(block);float mass=editor.Read(id).mass;
            Assert.That(animations.StartRecording(animations.RecordingSessionId,id,editor.ObjectRevision(id),out _,out var error,manual:true),Is.True,error);
            yield return new WaitForSeconds(.15f);block.transform.localPosition+=Vector3.up*.1f;
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{
                editor.CycleMass();yield return PhysicalCreationTestWait.Finish(editor);
                yield return PhysicalCreationTestWait.Until(()=>!animations.IsSavingRecording,"Blocked recording did not finish");
                Assert.That(animations.HasUnsavedRecording,Is.True);int frames=(int)animations.ObserveRecording()["frames"];Assert.That(frames,Is.GreaterThan(1));
                Assert.That(editor.Read(id).mass,Is.EqualTo(mass));
                editor.CycleMass();yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That((int)animations.ObserveRecording()["frames"],Is.EqualTo(frames));Assert.That(editor.Read(id).mass,Is.EqualTo(mass));
                Assert.That(editor.Status,Does.Contain("unfinished animation"));
            }finally{Directory.Delete(obstacle);}
            Assert.That(animations.DiscardRecording(animations.RecordingSessionId,out _,out error),Is.True,error);
            editor.CycleMass();yield return PhysicalCreationTestWait.Finish(editor);Assert.That(editor.Read(id).mass,Is.GreaterThan(mass),editor.Status);
        }
        [UnityTest] public IEnumerator PhysicalPhysicsMassPreservesUnrelatedMotion()
        {
            string id=PhysicalEditingOther();editor.Select(editor.Find(id));workshop.ToggleRepeat();
            Assert.That(runtime.Trigger(sequenceId),Is.True);yield return new WaitForSeconds(.15f);
            var position=block.transform.localPosition;float mass=editor.Read(id).mass;
            editor.CycleMass();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"A physics preference must not stop another object's program");
            Assert.That(editor.Read(id).mass,Is.GreaterThan(mass));
            yield return new WaitForSeconds(.15f);
            Assert.That(Vector3.Distance(position,block.transform.localPosition),Is.GreaterThan(.005f));
            editor.Undo();Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));Assert.That(editor.Read(id).mass,Is.EqualTo(mass));
            editor.Redo();Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
        }
    }
}

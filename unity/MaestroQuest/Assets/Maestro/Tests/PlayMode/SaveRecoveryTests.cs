// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest.Tests
{
    public sealed class SaveRecoveryTests
    {
        GameObject root;
        RoomEditor editor;
        RuleWorkshop rules;
        string directory,id;
        string RoomPath=>Path.Combine(directory,"room.v11.json");
        string RulesPath=>Path.Combine(directory,"behaviours.v2.json");
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static Task<string> Pending(MonoBehaviour owner)=>(Task<string>)owner.GetType().GetField("saveTask",Private).GetValue(owner);
        static void Pending(MonoBehaviour owner,Task<string> task)=>owner.GetType().GetField("saveTask",Private).SetValue(owner,task);
        static void Due(MonoBehaviour owner)=>owner.GetType().GetField("saveAt",Private).SetValue(owner,0f);
        static void Tick(MonoBehaviour owner)=>owner.SendMessage("Update");
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroSaveRecovery-"+Guid.NewGuid().ToString("N"));
            root=new GameObject("Save recovery tests");root.AddComponent<XRInteractionManager>();
            var room=root.AddComponent<RoomInteraction>();
            RoomItem Item(string name) {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root.transform,false);
                var item=obj.AddComponent<RoomItem>();item.Configure(new[]{obj.GetComponent<Collider>()});room.Register(item);return item;
            }
            var book=Item("book");var maestro=Item("maestro");
            editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,maestro,directory);editor.enabled=false;
            rules=root.AddComponent<RuleWorkshop>();rules.Initialize(editor,directory);rules.enabled=false;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Saved baseline",Vector3.up,1,Color.white,out id,out var error),Is.True,error);
            rules.NewSequence();Assert.That(rules.TryFlush(out error),Is.True,error);
            float memoryDeadline=Time.realtimeSinceStartup+5;while(!rules.Memory.Ready&&Time.realtimeSinceStartup<memoryDeadline)yield return null;Assert.That(rules.Memory.Ready,Is.True);Assert.That(rules.Memory.Error,Is.Null);
            yield return null;
        }
        void Rename(string name)
        {
            var data=editor.Read(id);data.name=name;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
        }
        void Block() {Directory.CreateDirectory(RoomPath+".pending");Directory.CreateDirectory(RulesPath+".pending");}
        void Unblock() {foreach(string path in new[]{RoomPath,RulesPath})if(Directory.Exists(path+".pending"))Directory.Delete(path+".pending");}
        IEnumerator FinishWorkers()
        {
            float deadline=Time.realtimeSinceStartup+10;
            while((Pending(editor)?.IsCompleted==false||Pending(rules)?.IsCompleted==false)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(Pending(editor)?.IsCompleted,Is.Not.False);Assert.That(Pending(rules)?.IsCompleted,Is.Not.False);
        }
        void AssertSaved(string name,int sequences)
        {
            var room=new RoomStorage(directory).Load(out var error);Assert.That(room,Is.Not.Null,error);
            Assert.That(room.objects.Single(x=>x.id==id).name,Is.EqualTo(name));
            var saved=new RuleStorage(directory).Load(out error);Assert.That(saved.sequences.Length,Is.EqualTo(sequences),error);
        }
        [Test] public void FailedPauseSavesRetainAcceptedEditsUntilAnotherLifecycleFlush()
        {
            var roomBytes=File.ReadAllBytes(RoomPath);var ruleBytes=File.ReadAllBytes(RulesPath);
            Rename("Accepted before pause");rules.NewSequence();Block();
            editor.SendMessage("OnApplicationPause",true);rules.SendMessage("OnApplicationPause",true);
            Assert.That(editor.HasUnsavedChanges,Is.True);Assert.That(rules.HasUnsavedChanges,Is.True);
            Assert.That(editor.TryFlush(out var roomError),Is.False);Assert.That(roomError,Is.Not.Empty);
            Assert.That(rules.TryFlush(out var rulesError),Is.False);Assert.That(rulesError,Is.Not.Empty);
            Assert.That(File.ReadAllBytes(RoomPath),Is.EqualTo(roomBytes));Assert.That(File.ReadAllBytes(RulesPath),Is.EqualTo(ruleBytes));
            Unblock(); // No new edit or SaveNow: the failed flush itself must preserve the need to save.
            editor.SendMessage("OnApplicationFocus",false);rules.SendMessage("OnApplicationFocus",false);
            Assert.That(editor.HasUnsavedChanges,Is.False);Assert.That(rules.HasUnsavedChanges,Is.False);
            AssertSaved("Accepted before pause",2);
            Assert.That(editor.Status,Is.EqualTo("Room saved"));Assert.That(rules.Status,Is.EqualTo("Behaviours saved"));
        }
        [UnityTest] public IEnumerator FailedAutosavesRetryLatestAcceptedStateWithoutAnotherEdit()
        {
            Rename("Earlier snapshot");rules.NewSequence();Block();Due(editor);Due(rules);Tick(editor);Tick(rules);
            Assert.That(Pending(editor),Is.Not.Null);Assert.That(Pending(rules),Is.Not.Null);
            // The owner keeps accepting edits while its detached snapshot writes.
            Rename("Latest accepted snapshot");rules.NewSequence();yield return FinishWorkers();Tick(editor);Tick(rules);
            Assert.That(editor.HasUnsavedChanges,Is.True);Assert.That(rules.HasUnsavedChanges,Is.True);
            Assert.That(Pending(editor),Is.Null);Assert.That(Pending(rules),Is.Null,"Failure must delay retries rather than start one each frame");
            Unblock();Tick(editor);Tick(rules);Assert.That(Pending(editor),Is.Null);Assert.That(Pending(rules),Is.Null);
            // Advance only the retry deadline; do not create a new edit or dirty flag.
            Due(editor);Due(rules);Tick(editor);Tick(rules);
            editor.ReportStatus("Selected another block");rules.Say("Selected another behaviour");
            yield return FinishWorkers();Tick(editor);Tick(rules);
            Assert.That(editor.HasUnsavedChanges,Is.False);Assert.That(rules.HasUnsavedChanges,Is.False);
            AssertSaved("Latest accepted snapshot",3);
            Assert.That(editor.Status,Is.EqualTo("Selected another block"));Assert.That(rules.Status,Is.EqualTo("Selected another behaviour"));
        }
        [UnityTest] public IEnumerator SuccessfulOlderSnapshotCannotClearEditsAcceptedDuringItsWrite()
        {
            Rename("Older snapshot");rules.NewSequence();Due(editor);Due(rules);Tick(editor);Tick(rules);
            Rename("Newer than dispatched snapshot");rules.NewSequence();yield return FinishWorkers();
            Tick(editor);Tick(rules);Assert.That(editor.HasUnsavedChanges,Is.True);Assert.That(rules.HasUnsavedChanges,Is.True);
            Assert.That(editor.TryFlush(out var error),Is.True,error);Assert.That(rules.TryFlush(out error),Is.True,error);
            AssertSaved("Newer than dispatched snapshot",3);
        }
        [Test] public void FaultedWritersBecomeRetryableFailuresInsteadOfEscapingUpdate()
        {
            Rename("Recover faulted write");rules.NewSequence();
            // Emulate a failed dispatch after it consumed the dirty flag.
            typeof(RoomEditor).GetField("dirty",Private).SetValue(editor,false);
            typeof(RuleWorkshop).GetField("dirty",Private).SetValue(rules,false);
            Pending(editor,Task.FromException<string>(new IOException("Injected writer failure")));
            Pending(rules,Task.FromException<string>(new IOException("Injected writer failure")));
            Assert.DoesNotThrow(()=>Tick(editor));Assert.DoesNotThrow(()=>Tick(rules));
            Assert.That(editor.HasUnsavedChanges,Is.True);Assert.That(rules.HasUnsavedChanges,Is.True);
            editor.ReportStatus("Selected block");rules.Say("Selected behaviour");
            Assert.That(editor.TryFlush(out var error),Is.True,error);Assert.That(rules.TryFlush(out error),Is.True,error);
            AssertSaved("Recover faulted write",2);
            Assert.That(editor.Status,Is.EqualTo("Selected block"));Assert.That(rules.Status,Is.EqualTo("Selected behaviour"));
        }
        [Test] public void FailedSynchronousEditDoesNotForgetAnEarlierUnconfirmedSnapshot()
        {
            Rename("Accepted before rejected edit");
            typeof(RoomEditor).GetField("dirty",Private).SetValue(editor,false);
            Pending(editor,Task.FromResult("Earlier save failed"));Block();
            Assert.That(editor.MoveObject(id,Vector3.up*3,out _),Is.False);
            Assert.That(editor.Read(id).position,Is.EqualTo(Vector3.up));Assert.That(editor.HasUnsavedChanges,Is.True);
            Unblock();Assert.That(editor.TryFlush(out var error),Is.True,error);
            AssertSaved("Accepted before rejected edit",1);
        }
        [UnityTest] public IEnumerator CheckedFlushNeverKeepsARejectedTemporaryBaselineOrItsLaterFork()
        {
            Rename("Starting accepted edit");Block();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            Rename("Temporary edit");editor.SendMessage("OnApplicationPause",true);
            Assert.That(editor.TemporarySaveError,Is.Not.Null);Unblock();
            Assert.That(editor.TryFlush(out error),Is.False);Assert.That(error,Does.Contain("temporary"));
            editor.SendMessage("OnApplicationFocus",false);AssertSaved("Saved baseline",1);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.TryFlush(out error),Is.True,error);AssertSaved("Starting accepted edit",1);yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Unblock();if(root)UnityEngine.Object.Destroy(root);yield return null;
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        IEnumerator ReadyForDamagedPreservation(bool damaged=false)
        {
            var current=Prepare(Archive("Saved original robot"));var previous=Prepare(Archive("Retained robot"));var selected=store.Activate(current.Id,current.Receipt.ManifestHash,"initial",previous.Id,previous.Receipt.ManifestHash);
            if(damaged)File.WriteAllText(Path.Combine(store.DataDirectory(selected.Active),"behaviours.v2.json"),"{unreadable original");
            Open();yield return ReadyReviewOwners();Assert.That(host.Current.Rules.ReadOnly,Is.EqualTo(damaged));
        }
        static string RecoveryText(string path,string entry){using var zip=ZipFile.OpenRead(path);using var reader=new StreamReader(zip.GetEntry(entry).Open(),Encoding.UTF8);return reader.ReadToEnd();}
        static void SetSave(object owner,Task<string> task)=>owner.GetType().GetField("saveTask",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,task);
        static void CloseRecovery(WorkspaceRecoveryHold hold){try{hold.Completion.GetAwaiter().GetResult();}catch(Exception){}hold.Dispose();}
        [UnityTest] public IEnumerator DamagedBehaviourStoreAndUnsavedRoomEditsArePreservedWithoutLifecycleOverwritingOriginals()
        {
            yield return ReadyForDamagedPreservation(true);var editor=host.Current.Editor;var rules=host.Current.Rules;string data=editor.SaveDirectory;byte[] original=File.ReadAllBytes(Path.Combine(data,"room.v14.json"));AcceptedEdit("Accepted but not yet saved");
            Assert.That(WorkspaceArchiveCapture.CanStart(editor,rules,host.Current.Controls,out _),Is.False);
            Assert.That(WorkspaceRecoveryHold.TryAcquire(editor,rules,host.Current.Controls,out var hold,out var error),Is.True,error);
            try {
                Assert.That(editor.TryFlush(out _),Is.False);Assert.That(rules.TryFlush(out _),Is.False);editor.SendMessage("OnApplicationPause",true);rules.SendMessage("OnApplicationPause",true);
                var capture=hold.Capture(Path.Combine(directory,"recovery-evidence"));while(!capture.IsCompleted)yield return null;var result=capture.GetAwaiter().GetResult();yield return new WaitForSeconds(.7f);
                Assert.That(File.ReadAllBytes(Path.Combine(data,"room.v14.json")),Is.EqualTo(original));Assert.That(RecoveryText(result.Path,"raw/behaviours.v2.json"),Is.EqualTo("{unreadable original"));
                var accepted=JObject.Parse(RecoveryText(result.Path,"accepted.json"));Assert.That((bool)accepted["available"]["behaviours"],Is.False);Assert.That((bool)accepted["available"]["room"],Is.True);Assert.That(accepted["room"]["objects"].Any(x=>(string)x["name"]=="Accepted but not yet saved"),Is.True);
                Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(editor.CanUndo,Is.False);Assert.That(physics.Running,Is.False);
                string evidence=Environment.GetEnvironmentVariable("MAESTRO_RECOVERY_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.Copy(result.Path,Path.Combine(evidence,"damaged-workspace-evidence.zip"),true);File.WriteAllText(Path.Combine(evidence,"accepted.json"),accepted.ToString());}
                UnityEngine.Object.Destroy(root);yield return null;Assert.That(File.ReadAllBytes(Path.Combine(data,"room.v14.json")),Is.EqualTo(original),"Teardown cannot replace originals with fallback or accepted snapshots");
            }finally{CloseRecovery(hold);}
        }
        [UnityTest] public IEnumerator CancellationCannotReleaseOwnershipUntilBothPreviouslyDispatchedSavesFinish()
        {
            yield return ReadyForDamagedPreservation();var editor=host.Current.Editor;var roomSave=new TaskCompletionSource<string>();var ruleSave=new TaskCompletionSource<string>();SetSave(editor,roomSave.Task);SetSave(host.Current.Rules,ruleSave.Task);
            Assert.That(WorkspaceRecoveryHold.TryAcquire(editor,host.Current.Rules,host.Current.Controls,out var hold,out var error),Is.True,error);using var cancellation=new CancellationTokenSource();
            try {
                var capture=hold.Capture(Path.Combine(directory,"recovery-evidence"),cancellation.Token);cancellation.Cancel();yield return null;
                Assert.That(capture.IsCompleted,Is.False);Assert.Throws<InvalidOperationException>(()=>hold.Dispose());roomSave.SetResult(null);yield return null;Assert.That(capture.IsCompleted,Is.False);Assert.That(editor.WriteGate.Frozen,Is.True);
                ruleSave.SetResult(null);while(!capture.IsCompleted)yield return null;Assert.That(capture.IsCanceled,Is.True);Assert.That(Directory.Exists(Path.Combine(directory,"recovery-evidence")),Is.False);
            }finally{roomSave.TrySetResult(null);ruleSave.TrySetResult(null);CloseRecovery(hold);}
            Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(editor.RuntimeGate.Held,Is.True,"An existing review hold remains owned");
        }
        [UnityTest] public IEnumerator CaptureReadsOriginalFilesOnlyAfterAnOlderSaveSettlesAndKeepsTheNewerAcceptedSnapshot()
        {
            yield return ReadyForDamagedPreservation();var editor=host.Current.Editor;string data=editor.SaveDirectory;var older=editor.Snapshot();older.objects.Single(x=>!x.IsBuiltIn).name="Older save finished";AcceptedEdit("Newer accepted edit");var save=new TaskCompletionSource<string>();SetSave(editor,save.Task);
            Assert.That(WorkspaceRecoveryHold.TryAcquire(editor,host.Current.Rules,host.Current.Controls,out var hold,out var error),Is.True,error);
            try {
                var capture=hold.Capture(Path.Combine(directory,"recovery-evidence"));yield return null;Assert.That(capture.IsCompleted,Is.False);
                Assert.That(new RoomStorage(data).Save(older,out error),Is.True,error);save.SetResult(null);while(!capture.IsCompleted)yield return null;var result=capture.GetAwaiter().GetResult();
                Assert.That(RecoveryText(result.Path,"raw/room.v14.json"),Does.Contain("Older save finished"));Assert.That(RecoveryText(result.Path,"accepted.json"),Does.Contain("Newer accepted edit"));
            }finally{save.TrySetResult(null);CloseRecovery(hold);}
            Assert.That(editor.CanUndo,Is.True);Assert.That(editor.TryFlush(out error),Is.True,error);Assert.That(File.ReadAllText(Path.Combine(data,"room.v14.json")),Does.Contain("Newer accepted edit"));
        }
        [UnityTest] public IEnumerator TemporaryForkAndSavedBaseRemainSeparateInRecoveryEvidence()
        {
            yield return ReadyForDamagedPreservation();var editor=host.Current.Editor;Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;AcceptedEdit("Temporary live robot");
            var memory=host.Current.Rules.Memory;string program=new string('a',32),cell=new string('b',32);
            var memoryWrite=memory.Write(memory.Snapshot().Revision,program,new System.Collections.Generic.Dictionary<string,ProgramMemoryDocument.Cell>{{cell,new("count",new ProgramValue(7))}});while(!memoryWrite.IsCompleted)yield return null;Assert.That(memoryWrite.Result.Error,Is.Null);
            Assert.That(WorkspaceRecoveryHold.TryAcquire(editor,host.Current.Rules,host.Current.Controls,out var hold,out error),Is.True,error);
            try {
                var capture=hold.Capture(Path.Combine(directory,"recovery-evidence"));while(!capture.IsCompleted)yield return null;var result=capture.GetAwaiter().GetResult();var accepted=JObject.Parse(RecoveryText(result.Path,"accepted.json"));
                Assert.That((int)accepted["version"],Is.EqualTo(2));Assert.That((bool)accepted["available"]["programMemory"],Is.True);
                Assert.That(ProgramMemoryDocument.Decode(Encoding.UTF8.GetBytes(accepted["programMemory"].ToString())).Programs,Is.Empty);
                Assert.That(ProgramMemoryDocument.Decode(Encoding.UTF8.GetBytes(accepted["temporaryMemory"].ToString())).Programs[program][cell].Value.Number,Is.EqualTo(7));
                Assert.That(accepted["room"]["objects"].Any(x=>(string)x["name"]=="Saved original robot"),Is.True);Assert.That(accepted["temporaryRoom"]["objects"].Any(x=>(string)x["name"]=="Temporary live robot"),Is.True);Assert.That(RecoveryText(result.Path,"raw/room.v14.json"),Does.Not.Contain("Temporary live robot"));
            }finally{CloseRecovery(hold);}
            Assert.That(editor.TemporaryRoom,Is.True);Assert.That(editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Temporary live robot"));
        }
        [UnityTest] public IEnumerator FailedEvidenceWriteLeavesCurrentEditsAndOriginalFilesAvailable()
        {
            yield return ReadyForDamagedPreservation();var editor=host.Current.Editor;AcceptedEdit("Keep this edit after failure");Assert.That(WorkspaceRecoveryHold.TryAcquire(editor,host.Current.Rules,host.Current.Controls,out var hold,out var error),Is.True,error);
            try {var capture=hold.Capture(Path.Combine(editor.SaveDirectory,"overlapping-output"));while(!capture.IsCompleted)yield return null;Assert.That(capture.IsFaulted,Is.True);Assert.That(Directory.Exists(Path.Combine(editor.SaveDirectory,"overlapping-output")),Is.False);}
            finally{CloseRecovery(hold);}
            Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(editor.CanUndo,Is.True);Assert.That(editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Keep this edit after failure"));Assert.That(editor.TryFlush(out error),Is.True,error);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
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
        [UnityTest]public IEnumerator TemporaryMemoryUsesSharedActionsAndKeepDiscardWithoutReplayingPrograms()
        {
            while(!workshop.Memory.Ready)yield return null;
            var program=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-memory.json"));var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",reference="temporary",sequence=new RuleSequence{id="",name="Temporary count",program=program}}}}}}},out var error,out var ids),Is.True,error);string id=ids.Single(),cell=new string('c',32);
            JObject Args(double value)=>new(){["sessionId"]=editor.TemporarySessionId,["kind"]="set",["programId"]=id,["variableId"]=cell,["revision"]=workshop.Memory.Snapshot().Revision,["rulesRevision"]=workshop.Revision,["valueJson"]=value.ToString(System.Globalization.CultureInfo.InvariantCulture)};
            bool Edit(JObject args,out string run,out string issue)=>runtime.Scheduler.Invoke(new JObject{["id"]="program.memory.edit",["version"]=1,["arguments"]=args},Time.unscaledTime,out run,out issue);
            IEnumerator Complete(string run){for(int i=0;i<300&&(string)runtime.Scheduler.Invocation(run)["phase"]=="preparing";i++)yield return null;Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("completed"),runtime.Scheduler.Invocation(run).ToString());}
            IEnumerator Session(string kind){Assert.That(runtime.Scheduler.Invoke(new JObject{["id"]="room.session",["version"]=1,["arguments"]=new JObject{["operation"]=kind,["sessionId"]=editor.TemporarySessionId}},Time.unscaledTime,out var run,out var issue),Is.True,issue);yield return Complete(run);}
            Assert.That(Edit(Args(10),out var run,out error),Is.True,error);yield return Complete(run);var stale=Args(99);var disk=File.ReadAllBytes(Path.Combine(directory,ProgramMemoryStore.FileName));
            yield return Session("begin");Assert.That(workshop.Memory.Temporary,Is.True);Assert.That(Edit(stale,out _,out error),Is.False);Assert.That(error,Does.Contain("session changed"));
            var view=workshop.Observe(true).memory;Assert.That(view.temporary,Is.True);Assert.That(view.sessionId,Is.EqualTo(editor.TemporarySessionId));
            Assert.That(Edit(Args(20),out run,out error),Is.True,error);yield return Complete(run);Assert.That(File.ReadAllBytes(Path.Combine(directory,ProgramMemoryStore.FileName)),Is.EqualTo(disk));
            Assert.That(runtime.Trigger(id),Is.True);for(int i=0;i<250&&workshop.Memory.Snapshot().Programs[id][cell].Value.Number!=21;i++)yield return new WaitForSeconds(.02f);Assert.That(workshop.Memory.Snapshot().Programs[id][cell].Value.Number,Is.EqualTo(21));runtime.Scheduler.StopAll();while(workshop.Memory.Pending)yield return null;Assert.That(File.ReadAllBytes(Path.Combine(directory,ProgramMemoryStore.FileName)),Is.EqualTo(disk));
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Kept",new Vector3(.3f,1,.7f),1,Color.white,out var kept,out error),Is.True,error);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);Assert.That(Edit(Args(30),out run,out error),Is.True,error);yield return Complete(run);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.TemporarySaveError,Is.Null);Assert.That(workshop.Memory.Snapshot().Programs[id][cell].Value.Number,Is.EqualTo(30));Assert.That(ProgramMemoryStore.ReadSaved(directory).Programs[id][cell].Value.Number,Is.EqualTo(21));
            yield return Session("discard");Assert.That(workshop.Memory.Temporary,Is.False);Assert.That(workshop.Memory.Snapshot().Programs[id][cell].Value.Number,Is.EqualTo(21));Assert.That(editor.Find(kept),Is.Not.Null);editor.Undo();Assert.That(editor.Find(kept),Is.Null);Assert.That(workshop.Memory.Snapshot().Programs[id][cell].Value.Number,Is.EqualTo(21));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
        }
        [UnityTest]public IEnumerator TemporaryMemoryLifecycleFlushDoesNotSaveTheUnkeptFork()
        {
            while(!workshop.Memory.Ready)yield return null;Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;Assert.That(editor.TemporarySaveError,Is.Null);
            string program=new string('a',32),cell=new string('b',32);var write=workshop.Memory.Write(workshop.Memory.Snapshot().Revision,program,new System.Collections.Generic.Dictionary<string,ProgramMemoryDocument.Cell>{{cell,new("count",new ProgramValue(7))}});while(!write.IsCompleted)yield return null;Assert.That(write.Result.Error,Is.Null);
            editor.SendMessage("OnApplicationPause",true);workshop.SendMessage("OnApplicationPause",true);Assert.That(ProgramMemoryStore.ReadSaved(directory).Programs,Is.Empty);Assert.That(workshop.Memory.Snapshot().Programs,Is.Not.Empty);
            using var restarted=new MemoryOwner(directory);while(!restarted.Store.Ready)yield return null;Assert.That(restarted.Store.Snapshot().Programs,Is.Empty);
        }
        sealed class MemoryOwner:IDisposable {internal readonly ProgramMemoryStore Store;internal MemoryOwner(string directory){Store=new(directory,new WorkspaceWriteGate());}public void Dispose(){Store.Drain().GetAwaiter().GetResult();}}
    }
}

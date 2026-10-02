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
        [UnityTest]public IEnumerator RememberedValuesUseSharedCommandsReceiptsStopRestartAndCompleteArchive()
        {
            while(!workshop.Memory.Ready)yield return null;
            var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-memory.json")));
            var sequence=new RuleSequence{id="",name="Remember my count",program=program.ToString(Newtonsoft.Json.Formatting.None)};var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",reference="remember",sequence=sequence}}}}}},out var error,out var ids),Is.True,error);string id=ids.Single();
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var evidence=new JObject();
            void Capture(string phase){var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);evidence[phase]=JObject.Parse(RoomAgentWire.Serialize(state));}
            JObject Args(string kind,string revision,string value="41")=>new(){["sessionId"]=editor.TemporarySessionId,["kind"]=kind,["programId"]=id,["variableId"]=new string('c',32),["revision"]=revision,["rulesRevision"]=workshop.Revision,["valueJson"]=value};
            bool Edit(JObject args,out string run,out string issue)=>runtime.Scheduler.Invoke(new JObject{["id"]="program.memory.edit",["version"]=1,["arguments"]=args},Time.unscaledTime,out run,out issue);
            Capture("initial");var initialRevision=workshop.Memory.Snapshot().Revision;
            Assert.That(Edit(Args("set",initialRevision),out var run,out error),Is.True,error);
            for(int i=0;i<300&&(string)runtime.Scheduler.Invocation(run)["phase"]=="preparing";i++)yield return null;
            Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("completed"),runtime.Scheduler.Invocation(run).ToString());Capture("edited");
            Assert.That(Edit(Args("set",initialRevision),out _,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            Assert.That(runtime.Trigger(id),Is.True,workshop.Status);
            for(int i=0;i<200&&workshop.Memory.Snapshot().Programs[id][new string('c',32)].Value.Number!=42;i++)yield return new WaitForSeconds(.02f);
            Assert.That(workshop.Memory.Snapshot().Programs[id][new string('c',32)].Value.Number,Is.EqualTo(42));
            while(runtime.Scheduler.ObserveRuns().Single().status=="Saving remembered values")yield return null;Capture("running");
            Assert.That(Edit(Args("set",workshop.Memory.Snapshot().Revision),out _,out error),Is.False);Assert.That(error,Does.Contain("Stop"));runtime.Scheduler.StopAll();Capture("stopped");
            var controls=root.AddComponent<Maestro.Quest.Interaction.MovementControls>();controls.Initialize(root.GetComponent<Maestro.Quest.Interaction.RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,directory:directory);workshop.Modules.Flush();
            var export=WorkspaceArchiveCapture.Start(editor,workshop,controls,Path.Combine(directory,"memory-exports"));while(!export.IsCompleted)yield return null;var archive=export.GetAwaiter().GetResult();
            using(var input=File.OpenRead(archive.Path))using(var staged=WorkspaceArchive.Stage(input,directory))Assert.That(ProgramMemoryDocument.Decode(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,ProgramMemoryStore.FileName))).Identity,Is.EqualTo(workshop.Memory.Snapshot().Identity));
            Assert.That(runtime.Trigger(id),Is.True);
            for(int i=0;i<200&&workshop.Memory.Snapshot().Programs[id][new string('c',32)].Value.Number!=43;i++)yield return new WaitForSeconds(.02f);
            Assert.That(workshop.Memory.Snapshot().Programs[id][new string('c',32)].Value.Number,Is.EqualTo(43));runtime.Scheduler.StopAll();while(workshop.Memory.Pending)yield return null;
            var reset=Args("reset",workshop.Memory.Snapshot().Revision);reset.Remove("valueJson");reset["variableId"]="";Assert.That(Edit(reset,out run,out error),Is.True,error);
            for(int i=0;i<300&&(string)runtime.Scheduler.Invocation(run)["phase"]=="preparing";i++)yield return null;Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("completed"));Assert.That(workshop.Memory.Snapshot().Programs,Is.Empty);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Capture("reset");
            string output=Environment.GetEnvironmentVariable("MAESTRO_PROGRAM_MEMORY_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"remembered-program.json"),evidence.ToString());}
        }
    }
}

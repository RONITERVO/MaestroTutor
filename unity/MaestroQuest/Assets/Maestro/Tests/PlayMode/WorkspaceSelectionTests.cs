// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceSelectionTests
    {
        sealed class Picker:IWorkspaceArchivePicker
        {
            public string CacheRoot {get;set;}
            public bool ReadyToStart {get;set;}=true;
            public int Starts,Releases;
            public JObject Result;
            public string Current;
            public void Start(string id){Current=id;Starts++;Result=new JObject {["id"]=id,["phase"]="selecting",["path"]="",["name"]="",["error"]=""};}
            public JObject Read(string id)=>Current==id?(JObject)Result?.DeepClone():null;
            public void Release(string id){if(Current==id)Interlocked.Increment(ref Releases);}
            public string Choose(string archive)
            {
                string folder=Path.Combine(CacheRoot,Guid.NewGuid().ToString("D"));Directory.CreateDirectory(folder);
                string target=Path.Combine(folder,Guid.NewGuid().ToString("D"));File.Copy(archive,target);
                Result["phase"]="selected";Result["path"]=target;Result["name"]="My workspace.zip";return target;
            }
        }
        string appRoot,directory,archive;
        GameObject root;
        RoomEditor editor;RuleWorkshop workshop;RoomRules runtime;WorkspaceImport imports;Picker picker;RoomExecutions executions;
        [UnitySetUp] public IEnumerator Setup()
        {
            appRoot=Path.Combine(Path.GetTempPath(),"MaestroWorkspaceSelection-"+Guid.NewGuid().ToString("N"));directory=Path.Combine(appRoot,"room");
            root=new GameObject("Workspace selection tests");root.AddComponent<XRInteractionManager>();var room=root.AddComponent<RoomInteraction>();
            RoomItem Item(string name){var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root.transform,false);var value=obj.AddComponent<RoomItem>();value.Configure(new[]{obj.GetComponent<Collider>()});room.Register(value);return value;}
            var book=Item("book");var maestro=Item("maestro");editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,maestro,directory);
            var animations=root.AddComponent<AnimationWorkshop>();animations.Initialize(editor);
            workshop=root.AddComponent<RuleWorkshop>();workshop.Initialize(editor,directory);
            runtime=root.AddComponent<RoomRules>();runtime.Initialize(workshop,editor,animations,null,room,null);
            var controls=root.AddComponent<MovementControls>();controls.Initialize(room,editor,animations,null,runtime,workshop,null,null,()=>true,directory:directory);
            workshop.NewSequence();workshop.Modules.Flush();
            var capture=WorkspaceArchiveCapture.Start(editor,workshop,controls,Path.Combine(appRoot,"exports"));
            float deadline=Time.realtimeSinceStartup+10;while(!capture.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(capture.IsCompleted,Is.True);archive=capture.GetAwaiter().GetResult().Path;
            picker=new Picker {CacheRoot=Path.Combine(appRoot,"cache","maestro-selected")};imports=root.AddComponent<WorkspaceImport>();imports.InitializeForTests(appRoot,picker);imports.enabled=false;
            executions=new RoomExecutions(editor);
        }
        JObject Request(string capability,JObject args)=>new() {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=new JObject {["id"]=capability,["version"]=1,["arguments"]=args}};
        string Select(out JObject request)
        {
            request=Request("workspace.archive.select",new JObject());Assert.That(executions.Execute(request,out var error),Is.True,error);
            var receipt=runtime.Scheduler.Invocation((string)request["runId"]);Assert.That((string)receipt["phase"],Is.EqualTo("completed"));
            return (string)receipt["output"]["requestId"];
        }
        JObject View(string id)=>imports.ReadSelection(id);
        IEnumerator Finish(string id,string phase)
        {
            float deadline=Time.realtimeSinceStartup+10;
            while((string)View(id)["phase"]!=phase&&Time.realtimeSinceStartup<deadline){imports.Poll();yield return null;}
            Assert.That((string)View(id)["phase"],Is.EqualTo(phase),View(id).ToString());
        }
        [UnityTest] public IEnumerator SelectionReceiptSurvivesPickerPauseAndVerifiedPreviewNeverActivates()
        {
            string room=JsonUtility.ToJson(editor.Snapshot()),rules=JsonUtility.ToJson(workshop.Snapshot());var id=Select(out var request);
            Assert.That(executions.Execute(request,out var error),Is.True,error);Assert.That(picker.Starts,Is.EqualTo(1));
            runtime.SendMessage("OnApplicationPause",true);imports.SendMessage("OnApplicationPause",true);
            Assert.That((string)runtime.Scheduler.Invocation((string)request["runId"])["phase"],Is.EqualTo("completed"));
            picker.Choose(archive);runtime.SendMessage("OnApplicationPause",false);imports.SendMessage("OnApplicationPause",false);
            yield return Finish(id,"prepared");var view=View(id);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(room));Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(rules));
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(new WorkspaceGenerationStore(appRoot).Load().Active.Generation,Is.EqualTo("original"));
            Assert.That((string)view["generationId"],Has.Length.EqualTo(32));Assert.That((string)view["manifestHash"],Has.Length.EqualTo(64));
            Assert.That((int)view["summary"]["files"],Is.GreaterThanOrEqualTo(5));Assert.That(picker.Releases,Is.GreaterThan(0));
            Assert.That(runtime.TryReadFact("workspace.archive.selection",1,new JObject {["requestId"]=id},out var fact),Is.True);
            Assert.That(JToken.DeepEquals(JToken.FromObject(fact.Value),view),Is.True);StringAssert.DoesNotContain(picker.CacheRoot,view.ToString());StringAssert.DoesNotContain("path",view.ToString());
            var catalog=new RoomCapabilityCatalog(editor);Assert.That(catalog.Execute(new JObject {["operation"]="inspect",["category"]="facts",["capability"]="workspace.archive.selection",["version"]=1,["arguments"]=new JObject {["requestId"]=id}},out error),Is.True,error);
            Assert.That(JToken.DeepEquals(catalog.Observe()["value"],view),Is.True);
            var evidence=Environment.GetEnvironmentVariable("MAESTRO_WORKSPACE_SELECTION_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)){
                Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"workspaceSelection.json"),new JObject {
                    ["receipt"]=runtime.Scheduler.Invocation((string)request["runId"]),["prepared"]=catalog.Observe()
                }.ToString());
            }
            Assert.That(imports.CanSelect(out _),Is.False,"Keep one explicit preview until discarded or activated");
            var cancel=Request("workspace.archive.cancel",new JObject {["requestId"]=id});Assert.That(executions.Execute(cancel,out error),Is.True,error);yield return Finish(id,"cancelled");
            Assert.That(Directory.GetDirectories(Path.Combine(appRoot,"workspace-generations.v1","generations")),Is.Empty);
            Assert.That(File.Exists(archive),Is.True,"Cancel never deletes the exported source file");Assert.That(imports.CanSelect(out _),Is.True);
            picker.ReadyToStart=false;Assert.That(imports.CanSelect(out _),Is.False,"Wait for released provider work before allocating another copy worker");
        }
        [Test] public void StaleRequestCannotCancelNewChoiceAndReadsNeverOpenChooser()
        {
            var old=Select(out _);Assert.That(runtime.TryReadFact("workspace.archive.selection",1,new JObject {["requestId"]=old},out _),Is.True);Assert.That(picker.Starts,Is.EqualTo(1));
            imports.Cancel(old);var current=Select(out _);Assert.That(current,Is.Not.EqualTo(old));
            Assert.That(imports.CanCancel(old,out _),Is.False);Assert.Throws<InvalidOperationException>(()=>imports.Cancel(old));Assert.That(View(old),Is.Null);
            Assert.That(runtime.TryReadFact("workspace.archive.selection",1,new JObject {["requestId"]=old},out _),Is.False);Assert.That((string)View(current)["phase"],Is.EqualTo("selecting"));
        }
        [UnityTest] public IEnumerator InvalidArchiveAndForeignPathsCannotChangeCurrentRoom()
        {
            string before=JsonUtility.ToJson(editor.Snapshot());
            foreach(bool foreign in new[]{false,true}){
                var id=Select(out _);string path=picker.Choose(archive);
                if(foreign)picker.Result["path"]=archive;else File.WriteAllText(path,"This is not a workspace ZIP.");
                yield return Finish(id,"failed");Assert.That((string)View(id)["generationId"],Is.Empty);Assert.That((string)View(id)["error"],Is.Not.Empty);
                Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(new WorkspaceGenerationStore(appRoot).Load().Active.Generation,Is.EqualTo("original"));
                Assert.That(imports.CanSelect(out _),Is.True);
            }
        }
        [UnityTest] public IEnumerator CancellationDuringPreparationDiscardsEvenAnAlreadyCompletedWorkerResult()
        {
            var id=Select(out _);picker.Choose(archive);imports.Poll();Assert.That((string)View(id)["phase"],Is.EqualTo("preparing"));
            imports.Cancel(id);yield return Finish(id,"cancelled");Assert.That((string)View(id)["generationId"],Is.Empty);
            string generations=Path.Combine(appRoot,"workspace-generations.v1","generations");if(Directory.Exists(generations))Assert.That(Directory.GetDirectories(generations),Is.Empty);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(imports.CanSelect(out _),Is.True);
        }
        [Test] public void MalformedNativeResultFailsClosedAndBoundedErrorsRemainReadableAsFacts()
        {
            var id=Select(out _);picker.Result["name"]=new JObject();Assert.DoesNotThrow(()=>imports.Poll());Assert.That((string)View(id)["phase"],Is.EqualTo("failed"));
            Assert.That(runtime.TryReadFact("workspace.archive.selection",1,new JObject {["requestId"]=id},out _),Is.True);
            id=Select(out _);picker.Result["phase"]="failed";picker.Result["error"]=new string('"',2000);imports.Poll();
            Assert.That(runtime.TryReadFact("workspace.archive.selection",1,new JObject {["requestId"]=id},out var value),Is.True);
            Assert.That((string)JToken.FromObject(value.Value)["error"],Has.Length.EqualTo(64));
            id=Select(out _);picker.Result["phase"]="failed";picker.Result["error"]=new string('\u2028',100)+new string('\ud800',100);imports.Poll();
            Assert.That(runtime.TryReadFact("workspace.archive.selection",1,new JObject {["requestId"]=id},out value),Is.True,"Untrusted provider text must fit the same program value budget");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(imports&&picker.Current!=null){imports.Cancel(picker.Current);yield return Finish(picker.Current,"cancelled");}
            if(root)UnityEngine.Object.Destroy(root);yield return null;
            if(Directory.Exists(appRoot))Directory.Delete(appRoot,true);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        GameObject root;string directory;RoomInteraction room;RoomItem book;NativeBookBrowser browser;BookPointerRouter router;BookControllerInput input;
        RoomPhysicsWorld physics;RoomNavigation navigation;ScannedRoom scan;VirtualRoomView view;RoomAgent agent;WorkspaceHost host;WorkspaceGenerationStore store;int builds;
        [SetUp] public void Setup()=>BuildShell(Path.Combine(Path.GetTempPath(),"mqh-"+Guid.NewGuid().ToString("N")));
        void BuildShell(string path)
        {
            builds=0;afterContentBuild=null;directory=path;store=new WorkspaceGenerationStore(directory);
            root=new GameObject("Persistent shell test");root.AddComponent<XRInteractionManager>();
            var cameraObject=new GameObject("Viewer",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);var camera=cameraObject.GetComponent<Camera>();
            var content=new GameObject("Persistent room origin");content.transform.SetParent(root.transform,false);room=content.AddComponent<RoomInteraction>();room.Viewer=camera.transform;
            var bookObject=new GameObject("Persistent book");bookObject.transform.SetParent(content.transform,false);bookObject.transform.localPosition=new Vector3(0,1,1);
            var handle=bookObject.AddComponent<BoxCollider>();book=bookObject.AddComponent<RoomItem>();book.Configure(new[]{handle});room.Register(book);browser=bookObject.AddComponent<NativeBookBrowser>();
            router=root.AddComponent<BookPointerRouter>();router.Browser=browser;input=root.AddComponent<BookControllerInput>();input.enabled=false;input.Router=router;input.Room=room;
            physics=root.AddComponent<RoomPhysicsWorld>();navigation=root.AddComponent<RoomNavigation>();navigation.Initialize(physics);scan=root.AddComponent<ScannedRoom>();scan.Initialize(physics);
            agent=root.AddComponent<RoomAgent>();agent.Initialize(null,browser);
            view=root.AddComponent<VirtualRoomView>();view.Initialize(cameraObject.transform,camera,scan,physics);host=root.AddComponent<WorkspaceHost>();
        }
        void Build(WorkspaceContent content,string data,string receipts,RoomRuntimeGate gate)
        {
            builds++;content.Build(room,book,browser,router,input,physics,navigation,scan,view,()=>false,directory,data,receipts,gate);afterContentBuild?.Invoke();
        }
        void Open()=>host.Initialize(directory,room.transform,Build,agent);
        IEnumerator ReadyHost(bool content=true)
        {
            float deadline=Time.realtimeSinceStartup+15;
            bool Waiting()=>!host.Ready||host.Retiring||host.Switching||content&&(!host.Current||host.Activation?.Busy==true||host.Review?.Busy==true||host.Recovery?.Busy==true);
            while(Waiting()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(Waiting(),Is.False,host.Status);
        }
        byte[] Archive(string label)
        {
            var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=true;
            var doc=new RoomDocument {version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book,position=new Vector3(0,1,1)},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro,position=new Vector3(-1,0,1)},new RoomObjectData {id=Guid.NewGuid().ToString("N"),name=label,kind=RoomObjectKind.Assembly,recipe=recipe,position=new Vector3(1,1,1)}}};
            byte[] Json(object v)=>Encoding.UTF8.GetBytes(JsonUtility.ToJson(v));
            var docs=new Dictionary<string,byte[]> {["room.v7.json"]=Json(doc),["behaviours.v2.json"]=Json(new RuleDocument()),["controls.v2.json"]=Json(new ControllerPreferences {deadZone=.3f}),["avatar-activities.v2.json"]=Json(new AvatarActivityDocument()),["motions/motions.v2.json"]=Encoding.UTF8.GetBytes("{\"version\":2,\"entries\":[],\"sources\":[]}")};
            using var output=new MemoryStream();WorkspaceArchive.Write(output,new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>()));return output.ToArray();
        }
        PreparedWorkspaceGeneration Prepare(byte[] bytes)=>store.Prepare(new MemoryStream(bytes,false));
        [UnityTest] public IEnumerator StartupUsesSelectedRootsAndAppliesReviewBeforeFirstContentFrame()
        {
            var incoming=Prepare(Archive("Imported robot"));var retained=Prepare(Archive("Previous robot"));var selected=store.Activate(incoming.Id,incoming.Receipt.ManifestHash,"initial",retained.Id,retained.Receipt.ManifestHash);
            int browserId=browser.GetInstanceID();Open();Assert.That(host.Current,Is.Not.Null,host.Status);
            var editor=host.Current.Editor;Assert.That(editor.SaveDirectory,Is.EqualTo(store.DataDirectory(selected.Active)));Assert.That(editor.ReceiptDirectory,Is.EqualTo(store.ReceiptDirectory(selected.Active)));
            Assert.That(editor.RuntimeGate.Held,Is.True);Assert.That(host.ReviewRequired,Is.True);Assert.That(host.Current.Controls.Preferences.deadZone,Is.EqualTo(.3f));
            var created=editor.Snapshot().objects.Single(x=>!x.IsBuiltIn);Assert.That(created.name,Is.EqualTo("Imported robot"));var recipe=editor.Find(created.id).GetComponent<RecipeObject>();
            for(int i=0;i<4;i++){recipe.Restart();yield return null;Assert.That(recipe.IsPlaying,Is.False);}
            Assert.That(physics.SetRunning(true,out _),Is.False);Assert.That(browser.GetInstanceID(),Is.EqualTo(browserId));Assert.That(root.GetComponentsInChildren<NativeBookBrowser>().Length,Is.EqualTo(1));
            Assert.That(router.Editor,Is.SameAs(editor));Assert.That(input.Editor,Is.SameAs(editor));Assert.That(editor.transform.localPosition,Is.EqualTo(Vector3.zero));
        }
        [UnityTest] public IEnumerator CommittedReplacementKeepsBrowserAndBookAndReplacesEveryContentOwner()=>Replace(false);
        [UnityTest] public IEnumerator DisabledHostReopensTheCommittedSelectionWithoutOldOwners()=>Replace(true);
        IEnumerator Replace(bool interrupt)
        {
            Open();Assert.That(host.Current,Is.Not.Null,host.Status);var previous=host.Current;var editor=previous.Editor;previous.Rules.Modules.Flush();
            while(editor.Find("maestro").GetComponent<MaestroAvatar>().ModelBusy)yield return null;
            var objectData=editor.Snapshot().objects.First(x=>!x.IsBuiltIn);objectData.name="Accepted just before replacement";
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{objectData},Array.Empty<string>(),out var error),Is.True,error);
            var oldLibrary=previous.GetComponent<LibraryBookController>();oldLibrary.SetVisible(true);Assert.That(oldLibrary.State.visible,Is.True);
            var picker=new MaintenancePicker();host.Import.InitializeForTests(directory,picker);var execution=new RoomExecutions(editor,host);
            var select=MaintenanceStart(execution,"workspace.archive.select",new JObject());Assert.That(execution.Execute(select,out var selectError),Is.True,selectError);
            var selectReceipt=(JObject)execution.Observe()["workspace"]["selected"];string requestId=(string)selectReceipt["output"]["requestId"];int importOwner=host.Import.GetInstanceID(),runtimeOwner=host.Runtime.GetInstanceID();
            var beforeSession=agent.Observe().session;int bookId=book.GetInstanceID(),browserId=browser.GetInstanceID();
            Assert.That(WorkspaceEditHold.TryAcquire(editor,previous.Rules,previous.Controls,out var held,out error),Is.True,error);
            try {
                var capture=WorkspaceArchiveCapture.Start(editor,previous.Rules,previous.Controls,Path.Combine(directory,"exports"));while(!capture.IsCompleted)yield return null;var archive=capture.GetAwaiter().GetResult();
                PreparedWorkspaceGeneration retained;
                try {var task=Task.Run(()=>{using var source=File.OpenRead(archive.Path);return store.Prepare(source);});while(!task.IsCompleted)yield return null;retained=task.GetAwaiter().GetResult();}finally{File.Delete(archive.Path);}
                var incoming=Prepare(Archive("Replacement robot"));var selected=store.Activate(incoming.Id,incoming.Receipt.ManifestHash,host.Selection.Revision,retained.Id,retained.Receipt.ManifestHash);
                Assert.That(host.ReplaceCommitted(selected,held,out error),Is.True,error);Assert.That(oldLibrary.State.visible,Is.False);
                if(interrupt){root.SetActive(false);Assert.That(host.Switching,Is.False);root.SetActive(true);yield return ReadyHost();}
                for(int i=0;i<30&&host.Switching;i++)yield return null;
                Assert.That(host.Switching,Is.False);Assert.That(host.Current,Is.Not.Null,host.Status);Assert.That(!previous&&!editor,Is.True);
                Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Replacement robot"));Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);
                Assert.That(agent.Observe().session,Is.Not.EqualTo(beforeSession));Assert.That(book.GetInstanceID(),Is.EqualTo(bookId));Assert.That(browser.GetInstanceID(),Is.EqualTo(browserId));
                Assert.That(root.GetComponentsInChildren<RoomEditor>().Length,Is.EqualTo(1));Assert.That(root.GetComponentsInChildren<MovementControls>().Length,Is.EqualTo(1));Assert.That(root.GetComponentsInChildren<SpatialDrawing>().Length,Is.EqualTo(1));
                Assert.That(room.RegisteredCount,Is.EqualTo(root.GetComponentsInChildren<RoomItem>().Length));Assert.That(router.Editor,Is.SameAs(host.Current.Editor));Assert.That(input.Drawing,Is.SameAs(host.Current.GetComponent<SpatialDrawing>()));
                Assert.That(new RoomStorage(store.DataDirectory(selected.Previous)).Load(out error).objects.Single(x=>x.id==objectData.id).name,Is.EqualTo(objectData.name));Assert.That(error,Is.Null);
                Assert.That(host.TryOpenSelected(out _),Is.False,"A retry must not replace a live editor without retention");
                Assert.That(host.Import.GetInstanceID(),Is.EqualTo(importOwner));Assert.That(host.Runtime.GetInstanceID(),Is.EqualTo(runtimeOwner));
                var reopened=new RoomExecutions(host.Current.Editor,host);Assert.That(reopened.Execute(select,out error),Is.True,error);Assert.That(picker.Starts,Is.EqualTo(1));Assert.That(host.Import.ReadSelection(requestId),Is.Not.Null);
            }finally{held.Dispose();}
        }
        [UnityTest] public IEnumerator DamagedSelectionNeverCreatesFallbackOwnersAndExplicitRetryUsesTheSameShell()
        {
            Prepare(Archive("Unused preview"));string path=Path.Combine(directory,"workspace-generations.v1","current.v1.json");var saved=File.ReadAllText(path);File.WriteAllText(path,"{broken");
            Open();yield return null;Assert.That(host.Current,Is.Null);Assert.That(builds,Is.Zero);Assert.That(root.GetComponentsInChildren<RoomEditor>().Length,Is.Zero);StringAssert.Contains("recovery",host.Status);
            Assert.That(browser&&book,Is.True);var unavailable=agent.Observe();StringAssert.Contains("recovery",unavailable.status);Assert.That(unavailable.objects,Is.Empty);Assert.That(unavailable.capabilities,Does.Contain("workspaceMaintenance.v1"));Assert.DoesNotThrow(()=>RoomAgentWire.Serialize(unavailable));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_HOST_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"unavailable.json"),RoomAgentWire.Serialize(unavailable));}Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));
            File.WriteAllText(path,saved);Assert.That(host.TryOpenSelected(out var error),Is.True,error);Assert.That(host.Current,Is.Not.Null);Assert.That(builds,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator DamagedPreferencesDoNotHideHealthyContentOrOverwriteTheirFile()
        {
            string data=Path.Combine(directory,"room");Directory.CreateDirectory(data);string file=Path.Combine(data,"controls.v2.json");File.WriteAllText(file,"{broken");
            Open();yield return null;Assert.That(host.Current,Is.Not.Null,host.Status);Assert.That(host.Current.Editor.CanSaveRoom,Is.True);Assert.That(host.Current.Controls.ArchiveReady,Is.False);
            Assert.That(root.GetComponentsInChildren<RoomEditor>().Length,Is.EqualTo(1));Assert.That(router.Editor,Is.SameAs(host.Current.Editor));Assert.That(input.Drawing,Is.Not.Null);
            Assert.That(host.Current.Controls.Apply(new ControllerPreferences()),Is.False);Assert.That(book.Process(null,null),Is.True);Assert.That(browser&&book,Is.True);Assert.That(File.ReadAllText(file),Is.EqualTo("{broken"));
        }
        [UnityTest] public IEnumerator PersistentAgentInvalidatesOldRequestsAndAllowsReadOnlyDiscoveryWithoutContent()
        {
            Open();var editor=host.Current.Editor;string client=Guid.NewGuid().ToString("N");
            agent.Receive(new Newtonsoft.Json.Linq.JObject {["clientId"]=client}.ToString());string previous=agent.Observe().session;
            string Wire(string session,int sequence,Newtonsoft.Json.Linq.JObject command)=>new Newtonsoft.Json.Linq.JObject {
                ["clientId"]=client,["session"]=session,["request"]=new Newtonsoft.Json.Linq.JObject {["version"]=2,["session"]=session,["sequence"]=sequence,["sceneRevision"]=editor.Revision,["conditions"]=new Newtonsoft.Json.Linq.JArray(),["commands"]=new Newtonsoft.Json.Linq.JArray(command)}}.ToString();
            var inspect=new Newtonsoft.Json.Linq.JObject {["action"]="catalog",["catalog"]=new Newtonsoft.Json.Linq.JObject {["operation"]="search",["query"]="workspace",["offset"]=0}};
            agent.Bind(null,"Selected workspace unavailable");string absent=agent.Observe().session;
            agent.Receive(Wire(previous,1,inspect));Assert.That(agent.Observe().ack,Is.Zero);
            agent.Receive(Wire(absent,1,inspect));var read=agent.Observe();Assert.That(read.ack,Is.EqualTo(1));Assert.That(read.ok,Is.True);Assert.That(read.catalog,Is.Not.Null);
            agent.Receive(Wire(absent,2,new Newtonsoft.Json.Linq.JObject {["action"]="undo"}));var refused=agent.Observe();Assert.That(refused.ack,Is.EqualTo(2));Assert.That(refused.ok,Is.False);StringAssert.Contains("unavailable",refused.status);
            agent.Bind(editor,"Workspace ready");Assert.That(agent.Observe().session,Is.Not.EqualTo(absent));agent.Receive(Wire(absent,3,inspect));Assert.That(agent.Observe().ack,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator InterruptedInitializationRemovesPartialOwnersButKeepsTheShell()
        {
            host.Initialize(directory,room.transform,(content,data,receipts,gate)=>{Build(content,data,receipts,gate);throw new IOException("Test initialization failure");},agent);
            yield return null;Assert.That(host.Current,Is.Null);Assert.That(root.GetComponentsInChildren<RoomEditor>().Length,Is.Zero);Assert.That(router.Editor,Is.Null);Assert.That(input.Drawing,Is.Null);
            Assert.That(room.RegisteredCount,Is.EqualTo(1));Assert.That(book.Process(null,null),Is.True);Assert.That(browser&&book,Is.True);StringAssert.Contains("recovery",agent.Observe().status);
        }
        [UnityTest] public IEnumerator StatusObserversCannotInterruptOwnerCreation()
        {
            int delivered=0;host.Changed+=()=>throw new InvalidOperationException("Test observer failure");host.Changed+=()=>delivered++;
            LogAssert.Expect(LogType.Warning,"A workspace status observer failed.");Open();Assert.That(host.Current,Is.Not.Null,host.Status);Assert.That(delivered,Is.EqualTo(1));yield return null;
        }
        sealed class MaintenancePicker:IWorkspaceArchivePicker
        {
            public string CacheRoot=>null;public bool ReadyToStart=>true;public int Starts,Releases;
            public void Start(string id){Starts++;}
            public JObject Read(string id)=>new JObject {["id"]=id,["phase"]="selecting",["name"]="",["path"]="",["error"]=""};
            public void Release(string id){Releases++;}
        }
        static JObject MaintenanceStart(RoomExecutions executions,string id,JObject args)=>new JObject {["operation"]="start",["runId"]=executions.Observe()["workspace"]["nextRunId"].DeepClone(),["call"]=new JObject {["id"]=id,["version"]=1,["arguments"]=args}};
        void MaintenanceEvidence(string name,JObject value)
        {string evidence=Environment.GetEnvironmentVariable("MAESTRO_MAINTENANCE_EVIDENCE");if(string.IsNullOrEmpty(evidence))return;Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,name+".json"),value.ToString());}
        [UnityTest] public IEnumerator MaintenanceWorksWithoutRoomContentAndReconcilesAfterRestart()
        {
            Prepare(Archive("Preview"));File.WriteAllText(Path.Combine(directory,"workspace-generations.v1","current.v1.json"),"{broken");Open();Assert.That(host.Current,Is.Null);
            var picker=new MaintenancePicker();host.Import.InitializeForTests(directory,picker);var actions=new RoomAgentExecutor(null,host);var execution=actions.Executions;
            var request=MaintenanceStart(execution,"workspace.archive.select",new JObject());
            Assert.That(actions.Execute(new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=request}}},out var error,out _),Is.True,error);
            Assert.That(picker.Starts,Is.EqualTo(1));var state=execution.Observe();Assert.That((string)state["storageError"],Is.Not.Empty);Assert.That((string)state["workspace"]["selected"]["phase"],Is.EqualTo("completed"));
            string id=(string)state["workspace"]["selected"]["output"]["requestId"];var args=new JObject {["requestId"]=id};
            Assert.That(host.Runtime.TryRead("workspace.archive.selection",1,args,out var fact),Is.True);Assert.That((string)JObject.FromObject(fact.Value)["phase"],Is.EqualTo("selecting"));MaintenanceEvidence("no-room-selected",state);
            host.Runtime.SendMessage("OnApplicationPause",true);Assert.That(host.Runtime.TryRead("workspace.archive.selection",1,args,out _),Is.False);host.Runtime.SendMessage("OnApplicationPause",false);
            Assert.That(execution.Execute(request,out error),Is.True,error);Assert.That(picker.Starts,Is.EqualTo(1));
            var cancel=MaintenanceStart(execution,"workspace.archive.cancel",args);Assert.That(execution.Execute(cancel,out error),Is.True,error);Assert.That((string)host.Import.ReadSelection(id)["phase"],Is.EqualTo("cancelled"));
            string saved=directory;UnityEngine.Object.Destroy(root);yield return null;BuildShell(saved);Open();yield return ReadyHost(false);Assert.That(host.Current,Is.Null);var after=new MaintenancePicker();host.Import.InitializeForTests(directory,after);
            execution=new RoomExecutions(null,host);Assert.That(execution.Execute(request,out error),Is.True,error);Assert.That(after.Starts,Is.Zero,"A retained opening receipt is not permission to reopen the picker");Assert.That(host.Import.ReadSelection(id),Is.Null);
            MaintenanceEvidence("restart-reconciled",execution.Observe());
        }
        [UnityTest] public IEnumerator ReviewHoldAllowsExportWithoutBorrowingRoomReceiptIds()
        {
            var incoming=Prepare(Archive("Reviewed robot"));var retained=Prepare(Archive("Old robot"));store.Activate(incoming.Id,incoming.Receipt.ManifestHash,"initial",retained.Id,retained.Receipt.ManifestHash);Open();Assert.That(host.Current,Is.Not.Null);
            var current=host.Current;current.Rules.Modules.Flush();while(current.Editor.Find("maestro").GetComponent<MaestroAvatar>().ModelBusy)yield return null;
            int published=0;host.Export.InitializeForTests(current.Editor,current.Rules,current.Controls,Path.Combine(directory,"out"),path=>{System.Threading.Interlocked.Increment(ref published);return "Downloads/Maestro/test.zip";});
            var execution=new RoomExecutions(current.Editor,host);string roomId=(string)execution.Observe()["nextRunId"];
            var wrong=MaintenanceStart(execution,"workspace.archive.export",new JObject());wrong["runId"]=roomId;Assert.That(execution.Execute(wrong,out _),Is.False);Assert.That(published,Is.Zero);
            var request=MaintenanceStart(execution,"workspace.archive.export",new JObject());Assert.That(execution.Execute(request,out var error),Is.True,error);
            for(int i=0;i<900&&(string)execution.Observe()["workspace"]["selected"]["phase"]=="preparing";i++)yield return null;
            var state=execution.Observe();Assert.That((string)state["workspace"]["selected"]["phase"],Is.EqualTo("completed"),state.ToString());Assert.That(published,Is.EqualTo(1));Assert.That((string)state["nextRunId"],Is.EqualTo(roomId));Assert.That(current.Editor.RuntimeGate.Held,Is.True);Assert.That(physics.Running,Is.False);
            Assert.That(execution.Execute(request,out error),Is.True,error);Assert.That(published,Is.EqualTo(1));MaintenanceEvidence("held-export",state);
            var roomCall=new JObject {["operation"]="start",["runId"]=roomId,["call"]=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=1}}};Assert.That(execution.Execute(roomCall,out _),Is.False);
        }
        [UnityTearDown] public IEnumerator Cleanup(){var closing=host;if(root)UnityEngine.Object.Destroy(root);yield return null;if(!ReferenceEquals(closing,null))while(!closing.Retirement.IsCompleted)yield return null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}

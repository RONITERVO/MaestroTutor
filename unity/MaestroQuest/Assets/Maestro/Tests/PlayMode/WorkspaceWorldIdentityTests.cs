// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        JObject WorldIdentityFact()
        {
            Assert.That(BehaviourCatalog.TryRead("world.identity",1,null,new BehaviourCatalog.FactContext(editor:host.Current.Editor),out var value),Is.True);
            return JObject.FromObject(value.Value);
        }
        [UnityTest]public IEnumerator WorldIdentityIsSavedBeforeObservationAndSurvivesPhysicalRestart()
        {
            PhysicalFloor();Open();yield return ReadyHost();var editor=host.Current.Editor;var expected=WorldIdentityFact();string path=Path.Combine(editor.SaveDirectory,RoomStorage.FileName);
            Assert.That(File.Exists(path),Is.True);var saved=new RoomStorage(editor.SaveDirectory).Load(out var error);Assert.That(saved,Is.Not.Null,error);
            Assert.That((string)expected["worldId"],Is.EqualTo(saved.world.worldId));Assert.That((string)expected["regionId"],Is.EqualTo(saved.world.regionId));
            string bytes=File.ReadAllText(path);for(int i=0;i<5;i++)Assert.That(JToken.DeepEquals(WorldIdentityFact(),expected),Is.True);
            Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));Assert.That(editor.CanUndo,Is.False);
            string previousSession=agent.Observe().session,location=directory;var closing=host;UnityEngine.Object.Destroy(root);yield return null;while(!closing.Retirement.IsCompleted)yield return null;
            BuildShell(location);PhysicalFloor();room.Viewer.SetPositionAndRotation(new Vector3(3,1.7f,-4),Quaternion.Euler(0,75,0));recoveryHeadTracked=true;Open();yield return ReadyHost();
            Assert.That(JToken.DeepEquals(WorldIdentityFact(),expected),Is.True);Assert.That(agent.Observe().session,Is.Not.EqualTo(previousSession));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_WORLD_IDENTITY");
            if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"identity.json"),new JObject{["identity"]=expected,["state"]=JObject.Parse(RoomAgentWire.Serialize(agent.Observe()))}.ToString());}
        }
        [UnityTest]public IEnumerator LegacyWorldIsPinnedOnceBeforeTheAgentCanReferenceIt()
        {
            string data=Path.Combine(directory,"room");Directory.CreateDirectory(data);
            var doc=new RoomDocument{version=22,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(0,1,1)},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
            var wire=JObject.Parse(JsonUtility.ToJson(doc));wire.Remove("world");string path=Path.Combine(data,"room.v22.json"),text=wire.ToString();File.WriteAllText(path,text);
            Open();yield return ReadyHost();var expected=WorldIdentityFact();var loaded=new RoomStorage(data).Load(out var error);
            Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.WorldNeedsSave,Is.False);Assert.That((string)expected["worldId"],Is.EqualTo(loaded.world.worldId));Assert.That(File.ReadAllText(path),Is.EqualTo(text));
        }
        [UnityTest]public IEnumerator FailedWorldIdentityCheckpointKeepsStorageAndObservationUnavailable()
        {
            string data=Path.Combine(directory,"room"),blocked=Path.Combine(data,RoomStorage.FileName);Directory.CreateDirectory(blocked);
            string sentinel=Path.Combine(blocked,"preserve.txt");File.WriteAllText(sentinel,"Preserve failed-save evidence");
            Open();yield return ReadyHost();var editor=host.Current.Editor;Assert.That(editor.WorldIdentityReady,Is.False);Assert.That(editor.CanSaveRoom,Is.False);
            Assert.That(BehaviourCatalog.TryRead("world.identity",1,null,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            Assert.That(File.ReadAllText(sentinel),Is.EqualTo("Preserve failed-save evidence"));Assert.That(editor.Status,Does.Contain("identity"));
        }
        [UnityTest]public IEnumerator TemporaryWorldEditsAndViewpointKeepOneAuthoredIdentity()
        {
            PhysicalFloor();Open();yield return ReadyHost();recoveryHeadTracked=true;var editor=host.Current.Editor;var expected=WorldIdentityFact();
            while(editor.Find("maestro").GetComponent<Maestro.Quest.Avatar.MaestroAvatar>().ModelBusy)yield return null;
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            while(editor.TemporarySavePending){editor.PollTemporarySave();yield return null;}Assert.That(editor.TemporarySaveError,Is.Null);
            Assert.That(view.Enter(),Is.True);Assert.That(view.Turn(30),Is.True);editor.GetComponent<WorkspaceViewpoint>().Capture();
            Assert.That(editor.MoveObject("book",editor.Read("book").position+Vector3.right,out error),Is.True,error);editor.Undo();
            Assert.That(JToken.DeepEquals(WorldIdentityFact(),expected),Is.True);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(JToken.DeepEquals(WorldIdentityFact(),expected),Is.True);Assert.That(editor.TryFlush(out error),Is.True,error);
            Assert.That(new RoomStorage(editor.SaveDirectory).Load(out error).world.worldId,Is.EqualTo((string)expected["worldId"]));
        }
        [UnityTest]public IEnumerator WorkspaceActivationUsesArchiveIdentityWithoutReusingSessionGuards()
        {
            Open();yield return ReadyHost();var previous=host.Current;previous.Rules.Modules.Flush();string oldWorld=(string)WorldIdentityFact()["worldId"],oldSession=agent.Observe().session;
            while(previous.Editor.Find("maestro").GetComponent<Maestro.Quest.Avatar.MaestroAvatar>().ModelBusy)yield return null;
            var incoming=Prepare(Archive("Identity-preserving import"));
            var retained=Prepare(Archive("Retained preview"));
            Assert.That(WorkspaceEditHold.TryAcquire(previous.Editor,previous.Rules,previous.Controls,out var held,out var error),Is.True,error);
            try{
                var selected=store.Activate(incoming.Id,incoming.Receipt.ManifestHash,host.Selection.Revision,retained.Id,retained.Receipt.ManifestHash);
                var staged=new RoomStorage(store.DataDirectory(selected.Active)).Load(out error);Assert.That(staged,Is.Not.Null,error);
                Assert.That(host.ReplaceCommitted(selected,held,out error),Is.True,error);yield return ReadyHost();
                var actual=WorldIdentityFact();Assert.That((string)actual["worldId"],Is.EqualTo(staged.world.worldId));Assert.That((string)actual["regionId"],Is.EqualTo(staged.world.regionId));
                Assert.That((string)actual["worldId"],Is.Not.EqualTo(oldWorld));Assert.That(agent.Observe().session,Is.Not.EqualTo(oldSession));Assert.That(host.ReviewRequired,Is.True);
            }finally{held.Dispose();}
        }
    }
}

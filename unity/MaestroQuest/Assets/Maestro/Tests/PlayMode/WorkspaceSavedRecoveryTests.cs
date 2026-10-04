// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [UnityTest] public IEnumerator SharedRecoveryOpensLaterSavedRoomAndRememberedValuesAfterPreviewRestart()
        {
            var source=Prepare(Archive("Original imported object"));string data=Path.Combine(directory,"workspace-generations.v1","generations",source.Id,"data"),roomPath=Path.Combine(data,"room.v14.json");
            var document=JObject.Parse(File.ReadAllText(roomPath));document["objects"][2]["name"]="Saved after import";File.WriteAllText(roomPath,document.ToString());
            string program=new string('a',32),cell=new string('b',32);var memory=ProgramMemoryDocument.Empty().WithValues(program,new Dictionary<string,ProgramMemoryDocument.Cell>{[cell]=new("count",new ProgramValue(23d))});File.WriteAllBytes(Path.Combine(data,ProgramMemoryStore.FileName),memory.Encode());
            using(RoomSnapshotTransaction.Enter(data)){}File.WriteAllText(Path.Combine(data,"private-history.json"),"Retained private evidence");
            File.WriteAllText(Path.Combine(directory,"workspace-generations.v1","current.v1.json"),"{damaged selected room");Open();Assert.That(host.Current,Is.Null);
            yield return InspectAndSelectRecovery(source.Id);Assert.That((string)recoveryPreview["manifestHash"],Is.Not.EqualTo(source.Receipt.ManifestHash));var preview=recoveryPreview.DeepClone();
            document["objects"][2]["name"]="Edited after preview";File.WriteAllText(roomPath,document.ToString());string path=directory;
            UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost(false);Assert.That(JToken.DeepEquals(preview,host.Recovery.Preview(recoveryId)),Is.True);
            RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery("review");
            float deadline=Time.realtimeSinceStartup+10;while(!host.Current.Rules.Memory.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Current.Rules.Memory.Ready,Is.True);Assert.That(host.Current.Rules.Memory.Snapshot().TryRead(program,cell,ProgramType.Number,out var count),Is.True);Assert.That(count.Number,Is.EqualTo(23));
            Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Saved after import"));Assert.That(host.ReviewRequired,Is.True);Assert.That(physics.Running,Is.False);
            Assert.That(File.ReadAllText(roomPath),Does.Contain("Edited after preview"));Assert.That(File.ReadAllText(Path.Combine(data,"private-history.json")),Is.EqualTo("Retained private evidence"));Assert.That(File.Exists(Path.Combine(host.Current.Editor.SaveDirectory,"private-history.json")),Is.False);
            yield return ReadyReviewOwners();string review=BeginReview();yield return FinishReview(review);ApproveReview(review);yield return FinishReview(review);Assert.That(host.ReviewRequired,Is.False);
            string output=Environment.GetEnvironmentVariable("MAESTRO_SAVED_RECOVERY_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"saved-recovery.json"),new JObject {["candidate"]=recoveryCandidate,["preview"]=preview,["completed"]=host.Recovery.Status(),["opening"]=recoveryOpening,["rememberedCount"]=count.Number,["reviewRequired"]=host.ReviewRequired}.ToString());}
        }
    }
}

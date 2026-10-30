// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        [UnityTest] public IEnumerator FullLibraryRecoversThroughSharedActionsThenReviewsAndExplicitlyDiscardsEligibleRooms()
        {
            var saved=new List<PreparedWorkspaceGeneration>();var bytes=Archive("Recovered full library");
            for(int i=0;i<64;i++){saved.Add(Prepare(bytes));if(i%8==7)yield return null;}
            string pointer=Path.Combine(directory,"workspace-generations.v1","current.v1.json");File.WriteAllText(pointer,"{unreadable full-library selection");Open();Assert.That(host.Current,Is.Null);
            yield return InspectAndSelectRecovery(saved[0].Id);Assert.That((int)recoveryInspection["candidateCount"],Is.EqualTo(64));
            RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery("review");Assert.That(host.ReviewRequired,Is.True);Assert.That(physics.Running,Is.False);
            yield return ReadyReviewOwners();string review=BeginReview();yield return FinishReview(review);ApproveReview(review);yield return FinishReview(review);Assert.That(host.ReviewRequired,Is.False);
            var observations=new JArray();
            foreach(var item in saved.Skip(1).Take(2)){
                yield return RetentionInspect(item.Id);observations.Add(retentionOutput.DeepClone());
                yield return RetentionCall("reviewRemoval",RemovalReviewArgs());Assert.That((bool)retentionOutput["eligible"],Is.True,retentionOutput.ToString());
                yield return RetentionCall("remove",RemovalArgs(retentionOutput));Assert.That(Directory.Exists(Path.Combine(directory,"workspace-generations.v1","generations",item.Id)),Is.False);
            }
            Assert.That((int)observations[0]["count"],Is.EqualTo(65));Assert.That((int)observations[1]["count"],Is.EqualTo(64));Assert.That(Prepare(bytes),Is.Not.Null);
            string recovered=Path.Combine(directory,"workspace-generations.v1","generations",host.Selection.Active.Generation);
            Assert.That(File.ReadAllText(Path.Combine(recovered,"origin-current.bin")),Is.EqualTo("{unreadable full-library selection"));Assert.That(Directory.Exists(Path.Combine(directory,"workspace-generations.v1","generations",saved[0].Id)),Is.True);
            Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Recovered full library"));
            string output=Environment.GetEnvironmentVariable("MAESTRO_RECOVERY_CAPACITY_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"capacity.json"),new JObject {["inventories"]=observations,["completed"]=host.Recovery.Status(),["reviewRequired"]=host.ReviewRequired}.ToString());}
        }
        [UnityTest] public IEnumerator FullReserveInventorySurvivesRestartAndReturnsAnActionableCapacityFailure()
        {
            var bytes=Archive("Retained");for(int i=0;i<64;i++){Prepare(bytes);if(i%8==7)yield return null;}
            string pointer=Path.Combine(directory,"workspace-generations.v1","current.v1.json");File.WriteAllText(pointer,"{unreadable");
            var origin=(string)store.InspectRecovery()["originHash"];store.PrepareFreshRecovery(origin);Open();
            RecoveryCall("workspace.recovery.inspect",new JObject());yield return FinishRecovery("inspected");string id=(string)host.Recovery.Status()["requestId"];Assert.That((int)host.Recovery.Status()["candidateCount"],Is.EqualTo(65));
            string path=directory;UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost(false);
            Assert.That((string)host.Recovery.Status()["requestId"],Is.EqualTo(id));Assert.That((int)host.Recovery.Status()["candidateCount"],Is.EqualTo(65));
            Assert.That(host.Runtime.TryRead("workspace.recovery.candidate",1,new JObject {["requestId"]=id,["index"]=64},out _),Is.True);
            RecoveryCall("workspace.recovery.select",new JObject {["requestId"]=id,["originHash"]=origin,["source"]=new JObject {["kind"]="fresh"}});yield return FinishRecovery("failed");
            Assert.That((string)host.Recovery.Status()["status"],Is.EqualTo(WorkspaceGenerationStore.RecoveryCapacityError));Assert.That(File.ReadAllText(pointer),Is.EqualTo("{unreadable"));Assert.That(host.Current,Is.Null);
        }
    }
}

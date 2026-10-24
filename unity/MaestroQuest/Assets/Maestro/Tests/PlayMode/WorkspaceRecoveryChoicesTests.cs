// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        void MissingSelection()
        {
            string generations=Path.Combine(directory,"workspace-generations.v1");Directory.CreateDirectory(generations);File.WriteAllText(Path.Combine(generations,"current.v1.json.previous"),"Unavailable original selection");Open();Assert.That(host.Current,Is.Null);Assert.That(builds,Is.Zero);
        }
        IEnumerator ChooseFreshRecovery()
        {
            RecoveryCall("workspace.recovery.inspect",new JObject());yield return FinishRecovery("inspected");var inspected=host.Recovery.Status();recoveryId=(string)inspected["requestId"];
            RecoveryCall("workspace.recovery.select",new JObject {["requestId"]=recoveryId,["originHash"]=inspected["originHash"].DeepClone(),["source"]=new JObject {["kind"]="fresh"}});yield return FinishRecovery("prepared");
            recoveryPreview=host.Recovery.Preview(recoveryId);Assert.That((string)recoveryPreview["source"]["kind"],Is.EqualTo("fresh"));Assert.That((string)recoveryPreview["source"]["generationId"],Is.Empty);
            recoveryArgs=new JObject {["requestId"]=recoveryId,["originHash"]=inspected["originHash"].DeepClone(),["generationId"]=recoveryPreview["generationId"].DeepClone(),["manifestHash"]=recoveryPreview["manifestHash"].DeepClone()};
        }
        [UnityTest] public IEnumerator FreshRecoveryWithNoCandidatesSurvivesRestartAndRequiresSeparateCommitAndReview()
        {
            MissingSelection();yield return ChooseFreshRecovery();Assert.That((int)host.Recovery.Status()["candidateCount"],Is.Zero);Assert.That(host.Current,Is.Null);var preview=(JObject)recoveryPreview.DeepClone();string path=directory;
            UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost(false);Assert.That(host.Current,Is.Null);Assert.That((string)host.Recovery.Status()["phase"],Is.EqualTo("prepared"));Assert.That(JToken.DeepEquals(preview,host.Recovery.Preview(recoveryId)),Is.True);
            RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery("review");Assert.That(host.Current.Editor.Snapshot().objects.Length,Is.EqualTo(2));Assert.That(host.Current.Rules.Snapshot().sequences,Is.Empty);Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);Assert.That(physics.Running,Is.False);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_RECOVERY_FLOW_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"fresh.json"),new JObject {["preview"]=preview,["completed"]=host.Recovery.Status(),["opening"]=recoveryOpening}.ToString());}
        }
        [UnityTest] public IEnumerator FreshRecoveryPreservesDamagedCurrentDataAndNewerEditsBeforeOpeningOnlyBuiltIns()
        {
            yield return ReadyForDamagedPreservation(true);var old=host.Current;string original=old.Editor.SaveDirectory;AcceptedEdit("Kept before explicit fresh workspace");yield return ChooseFreshRecovery();Assert.That(host.Current,Is.SameAs(old));
            RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery("review");Assert.That(host.Current.Editor.Snapshot().objects.Length,Is.EqualTo(2));Assert.That(File.ReadAllText(Path.Combine(original,"behaviours.v2.json")),Is.EqualTo("{unreadable original"));
            string generation=Path.Combine(directory,"workspace-generations.v1","generations",host.Selection.Active.Generation);string preserved=Directory.GetFiles(Path.Combine(generation,"preservation"),"*.zip").Single();Assert.That(RecoveryText(preserved,"accepted.json"),Does.Contain("Kept before explicit fresh workspace"));
        }
        IEnumerator ChooseExternalRecoveryArchive()
        {
            host.Import.InitializeForTests(directory,new ActivationPicker(directory,Archive("External recovery robot")));var actions=new RoomExecutions(host.Current?host.Current.Editor:null,host);
            Assert.That(actions.Execute(MaintenanceStart(actions,"workspace.archive.select",new JObject()),out var issue),Is.True,issue);string id=(string)actions.Observe()["workspace"]["selected"]["output"]["requestId"];
            float deadline=Time.realtimeSinceStartup+20;while((string)host.Import.ReadSelection(id)["phase"]!="prepared"&&Time.realtimeSinceStartup<deadline){host.Import.Poll();yield return null;}
            Assert.That((string)host.Import.ReadSelection(id)["phase"],Is.EqualTo("prepared"));externalSelection=host.Import.ReadSelection(id);
        }
        JObject externalSelection;
        [UnityTest] public IEnumerator ExternalArchiveRecoversMissingSelectionThroughTheExistingChooserAndSharedCandidateFlow()
        {
            MissingSelection();yield return ChooseExternalRecoveryArchive();Assert.That(File.Exists(Path.Combine(directory,"workspace-generations.v1","current.v1.json")),Is.False);Assert.That(host.Current,Is.Null);
            yield return InspectAndSelectRecovery((string)externalSelection["generationId"]);Assert.That((string)host.Import.ReadSelection((string)externalSelection["requestId"])["phase"],Is.EqualTo("retained"));Assert.That(host.Import.Occupied,Is.False);
            Assert.That((string)recoveryPreview["source"]["generationId"],Is.EqualTo((string)externalSelection["generationId"]));RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery("review");
            Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("External recovery robot"));Assert.That(host.ReviewRequired,Is.True);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator ExternalPreviewCannotBeDiscardedWhileRecoveryCopiesItAndCancellationRetainsItsSource()
        {
            MissingSelection();yield return ChooseExternalRecoveryArchive();RecoveryCall("workspace.recovery.inspect",new JObject());yield return FinishRecovery("inspected");var status=host.Recovery.Status();string id=(string)status["requestId"];
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Recovery.Fault=phase=>{if(phase=="damage.copied"){entered.Set();release.Wait(TimeSpan.FromSeconds(20));}};
            try{
                RecoveryCall("workspace.recovery.select",new JObject {["requestId"]=id,["originHash"]=status["originHash"].DeepClone(),["source"]=new JObject {["kind"]="retained",["generationId"]=externalSelection["generationId"].DeepClone(),["manifestHash"]=externalSelection["manifestHash"].DeepClone()}});
                float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);Assert.That(host.Import.CanCancel((string)externalSelection["requestId"],out _),Is.False);RecoveryCall("workspace.recovery.cancel",new JObject {["requestId"]=id});
            }finally{release.Set();}
            yield return FinishRecovery("cancelled");Assert.That(host.Import.Occupied,Is.False);Assert.That(host.Current,Is.Null);Assert.That(Directory.Exists(Path.Combine(directory,"workspace-generations.v1","generations",(string)externalSelection["generationId"])),Is.True);
        }
    }
}

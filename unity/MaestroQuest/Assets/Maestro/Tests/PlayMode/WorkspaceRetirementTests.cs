// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        [UnityTest] public IEnumerator NewHostCannotOpenTheSameDataWhileRetiringAcceptedWritersArePending()
        {
            Open();var oldHost=host;var oldRoot=root;var oldEditor=host.Current.Editor;var writer=oldEditor.WriteGate.Write();string path=directory;
            try {
                BuildShell(path);Open();Assert.That(host.Ready,Is.False);Assert.That(host.Current,Is.Null);Assert.That(builds,Is.Zero);
                UnityEngine.Object.Destroy(oldRoot);yield return null;yield return null;
                Assert.That(host.Current,Is.Null);Assert.That(oldHost.Retirement.IsCompleted,Is.False);Assert.That(oldEditor.WriteGate.TryWrite(out _),Is.Null);
                writer.Dispose();for(int i=0;i<120&&!host.Current;i++)yield return null;
                Assert.That(oldHost.Retirement.IsCompleted,Is.True);Assert.That(host.Current,Is.Not.Null,host.Status);Assert.That(builds,Is.EqualTo(1));
            }finally{writer.Dispose();if(oldRoot)UnityEngine.Object.Destroy(oldRoot);}
        }
        [UnityTest] public IEnumerator DestroyedImportComponentStillOwnsItsWorkerUntilHostRetirementFinishes()
        {
            Open();var oldHost=host;var oldRoot=root;string path=directory;var importer=host.Import;
            importer.InitializeForTests(directory,new ActivationPicker(directory,Archive("Pending import")));
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            importer.Fault=phase=>{if(phase=="prepare.verified"){entered.Set();release.Wait(TimeSpan.FromSeconds(20));}};
            try {
                var actions=new RoomExecutions(host.Current.Editor,host);Assert.That(actions.Execute(MaintenanceStart(actions,"workspace.archive.select",new JObject()),out var issue),Is.True,issue);
                importer.Poll();float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                UnityEngine.Object.Destroy(importer);yield return null;UnityEngine.Object.Destroy(oldRoot);yield return null;
                BuildShell(path);Open();yield return null;Assert.That(host.Ready,Is.False);Assert.That(oldHost.Retirement.IsCompleted,Is.False);Assert.That(host.Current,Is.Null);
            }finally{release.Set();if(oldRoot)UnityEngine.Object.Destroy(oldRoot);}
            yield return ReadyHost();Assert.That(oldHost.Retirement.IsCompleted,Is.True);Assert.That(builds,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator FailedPartialInitializationMustDrainBeforeTheSameHostCanOpenNewOwners()
        {
            IDisposable writer=null;afterContentBuild=()=>{writer=host.Current.Editor.WriteGate.Write();throw new IOException("Injected initialization failure");};Open();Assert.That(host.Current,Is.Null);Assert.That(host.Retiring,Is.True);afterContentBuild=null;
            try {
                yield return null;Assert.That(host.TryOpenSelected(out _),Is.False);Assert.That(builds,Is.EqualTo(1));writer.Dispose();
                for(int i=0;i<120&&host.Retiring;i++)yield return null;
                Assert.That(host.TryOpenSelected(out var error),Is.True,error);Assert.That(host.Current,Is.Not.Null);Assert.That(builds,Is.EqualTo(2));
            }finally{writer?.Dispose();}
        }
        [UnityTest] public IEnumerator RecoveredReplacementKeepsDamagedOriginalsWhileOpeningFreshReviewedOwners()=>RecoverOwners(false);
        [UnityTest] public IEnumerator DisabledRecoveredReplacementKeepsSaveDispatchPausedThroughDestruction()=>RecoverOwners(true);
        IEnumerator RecoverOwners(bool disable)
        {
            yield return ReadyForDamagedPreservation(true);var old=host.Current;var editor=old.Editor;var candidate=store.Previous();string originalDirectory=editor.SaveDirectory;byte[] originalRoom=File.ReadAllBytes(Path.Combine(originalDirectory,"room.v12.json"));AcceptedEdit("Live edit kept in recovery evidence");
            string origin=(string)store.InspectRecovery()["originHash"];var preview=store.PrepareDamagedRecovery(origin,candidate.Generation,candidate.ManifestHash);Assert.That(WorkspaceRecoveryHold.TryAcquire(editor,old.Rules,old.Controls,out var hold,out var error),Is.True,error);
            try {
                var capture=hold.Capture(store.RecoveryEvidenceDirectory(preview.Id,preview.Receipt.ManifestHash,origin));while(!capture.IsCompleted)yield return null;var evidence=capture.GetAwaiter().GetResult();
                var selected=store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence);Assert.That(host.ReplaceRecovered(selected,hold,out error),Is.True,error);hold=null;
                if(disable){root.SetActive(false);root.SetActive(true);}
                for(int i=0;i<120&&(host.Switching||!host.Current);i++)yield return null;
                Assert.That(host.Current,Is.Not.Null,host.Status);Assert.That(host.Current,Is.Not.SameAs(old));Assert.That(host.Selection.Revision,Is.EqualTo(selected.Revision));Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);Assert.That(host.Current.Rules.ReadOnly,Is.False);
                Assert.That(File.ReadAllBytes(Path.Combine(originalDirectory,"room.v12.json")),Is.EqualTo(originalRoom));Assert.That(File.ReadAllText(Path.Combine(originalDirectory,"behaviours.v2.json")),Is.EqualTo("{unreadable original"));Assert.That(RecoveryText(evidence.Path,"accepted.json"),Does.Contain("Live edit kept in recovery evidence"));Assert.That(physics.Running,Is.False);Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Retained robot"));
            }finally{if(hold!=null)CloseRecovery(hold);}
        }
    }
}

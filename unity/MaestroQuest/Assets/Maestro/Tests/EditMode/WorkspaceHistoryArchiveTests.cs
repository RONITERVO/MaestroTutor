// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceHistoryArchiveTests
    {
        string root,parent,path;WorkspaceHistoryArchive archive;byte[] accepted;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"mwh-"+Guid.NewGuid().ToString("N"));parent=Path.Combine(root,"workspace-recovery.v1");path=Path.Combine(parent,"latest.json");Directory.CreateDirectory(parent);archive=new WorkspaceHistoryArchive(root);accepted=WorkspaceHistoryArchive.Accepted(new JObject {["phase"]="accepted but unsaved"});}
        [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
        WorkspaceHistoryArchive.Snapshot Inspect()=>archive.Inspect("recovery",accepted,CancellationToken.None);
        string Reset(WorkspaceHistoryArchive.Snapshot value)=>Path.Combine(root,"workspace-history.v1","evidence",archive.Reset(value,accepted,CancellationToken.None));
        [Test] public void PreservesExactRawAndAcceptedBytesWithoutMovingCapturesOrSelection()
        {
            byte[] raw={0,255,1,32,2};File.WriteAllBytes(path,raw);Directory.CreateDirectory(Path.Combine(parent,"captures"));File.WriteAllText(Path.Combine(parent,"captures","saved.zip"),"untouched archive");File.WriteAllText(Path.Combine(root,"selection"),"untouched selection");File.WriteAllText(Path.Combine(parent,"leftover.pending"),"pending bytes");
            var value=Inspect();Assert.That(Directory.Exists(Path.Combine(root,"workspace-history.v1")),Is.False);string evidence=Reset(value);
            Assert.That(File.ReadAllBytes(Path.Combine(evidence,"original")),Is.EqualTo(raw));Assert.That(File.ReadAllBytes(Path.Combine(evidence,"accepted.json")),Is.EqualTo(accepted));Assert.That(JToken.DeepEquals(JObject.Parse(File.ReadAllText(Path.Combine(evidence,"manifest.json"))),value.Manifest()),Is.True);
            Assert.That(File.Exists(path),Is.False);Assert.That(File.ReadAllText(Path.Combine(parent,"captures","saved.zip")),Is.EqualTo("untouched archive"));Assert.That(File.ReadAllText(Path.Combine(parent,"leftover.pending")),Is.EqualTo("pending bytes"));Assert.That(File.ReadAllText(Path.Combine(root,"selection")),Is.EqualTo("untouched selection"));
        }
        [Test] public void ChangedBytesOrAcceptedRecordRequireNewInspection()
        {
            File.WriteAllText(path,"old");var value=Inspect();File.WriteAllText(path,"new");Assert.Throws<InvalidOperationException>(()=>Reset(value));Assert.That(File.ReadAllText(path),Is.EqualTo("new"));
            File.WriteAllText(path,"old");accepted=Encoding.UTF8.GetBytes("null");Assert.Throws<InvalidOperationException>(()=>Reset(value));Assert.That(File.ReadAllText(path),Is.EqualTo("old"));Assert.That(Directory.Exists(Path.Combine(root,"workspace-history.v1")),Is.False);
        }
        [Test] public void RechecksAfterEvidenceIsWrittenAndNeverOverwritesChangedSource()
        {
            File.WriteAllText(path,"old");var value=Inspect();archive.Fault=point=>{if(point=="history.beforeMove")File.WriteAllText(path,"changed during preservation");};Assert.Throws<InvalidOperationException>(()=>Reset(value));Assert.That(File.ReadAllText(path),Is.EqualTo("changed during preservation"));Assert.That(Directory.GetFiles(Path.Combine(root,"workspace-history.v1"),"accepted.json",SearchOption.AllDirectories),Has.Length.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void CancellationBeforeRenamePreservesSourceButAfterRenameReportsCommitted(bool after)
        {
            File.WriteAllText(path,"original");var value=Inspect();using var cancel=new CancellationTokenSource();archive.Fault=point=>{if(point==(after?"history.afterMove":"history.beforeMove"))cancel.Cancel();};
            if(after){string id=archive.Reset(value,accepted,cancel.Token);Assert.That(File.ReadAllText(Path.Combine(root,"workspace-history.v1","evidence",id,"original")),Is.EqualTo("original"));Assert.That(File.Exists(path),Is.False);}
            else{Assert.Throws<OperationCanceledException>(()=>archive.Reset(value,accepted,cancel.Token));Assert.That(File.ReadAllText(path),Is.EqualTo("original"));}
        }
        [TestCase(false)] [TestCase(true)] public void AcknowledgementFaultCannotConvertCommittedRenameIntoFailure(bool after)
        {
            File.WriteAllText(path,"original");var value=Inspect();archive.Fault=point=>{if(point==(after?"history.afterMove":"history.beforeMove"))throw new IOException();};
            if(after){Assert.That(File.ReadAllText(Path.Combine(Reset(value),"original")),Is.EqualTo("original"));Assert.That(File.Exists(path),Is.False);}
            else{Assert.Throws<IOException>(()=>Reset(value));Assert.That(File.ReadAllText(path),Is.EqualTo("original"));}
        }
        [TestCase("parent")] [TestCase("directory")] [TestCase("absent")] public void PreservesObstructionsAndEmptyDirectories(string kind)
        {
            if(kind=="parent"){Directory.Delete(parent);File.WriteAllText(parent,"not a directory");}
            else if(kind=="directory"){Directory.CreateDirectory(Path.Combine(path,"empty"));File.WriteAllText(Path.Combine(path,"raw"),"not a status");}
            var value=Inspect();string evidence=Reset(value);
            if(kind=="parent")Assert.That(File.ReadAllText(Path.Combine(evidence,"original")),Is.EqualTo("not a directory"));
            if(kind=="directory"){Assert.That(Directory.Exists(Path.Combine(evidence,"original","empty")),Is.True);Assert.That(File.ReadAllText(Path.Combine(evidence,"original","raw")),Is.EqualTo("not a status"));}
            Assert.That(File.Exists(path)||Directory.Exists(path),Is.False);Assert.That(File.Exists(Path.Combine(evidence,"accepted.json")),Is.True);
        }
        [Test] public void BoundedInventoryAndByteCapacityRefuseWithoutChangingSource()
        {
            using(var file=File.Create(path))file.SetLength(WorkspaceHistoryArchive.MaxSourceBytes+1);Assert.Throws<InvalidDataException>(()=>Inspect());Assert.That(new FileInfo(path).Length,Is.EqualTo(WorkspaceHistoryArchive.MaxSourceBytes+1));File.Delete(path);
            Directory.CreateDirectory(path);for(int i=0;i<256;i++)File.WriteAllText(Path.Combine(path,i.ToString()),"x");Assert.Throws<InvalidDataException>(()=>Inspect());Assert.That(Directory.GetFiles(path),Has.Length.EqualTo(256));Assert.That(Directory.Exists(Path.Combine(root,"workspace-history.v1")),Is.False);
        }
        [Test] public void FullEvidenceKeepsOriginalAndNoNewEvidenceIsCreated()
        {
            File.WriteAllText(path,"original");var value=Inspect();string evidence=Path.Combine(root,"workspace-history.v1","evidence");Directory.CreateDirectory(evidence);using(var file=File.Create(Path.Combine(evidence,"full")))file.SetLength(WorkspaceHistoryArchive.MaxEvidenceBytes);
            Assert.Throws<IOException>(()=>Reset(value));Assert.That(File.ReadAllText(path),Is.EqualTo("original"));Assert.That(Directory.GetFileSystemEntries(evidence),Has.Length.EqualTo(1));
        }
        [Test] public void PreservedDeepSourceDoesNotPreventAnotherRepair()
        {
            Directory.CreateDirectory(Path.Combine(path,"a","b","c","d","e"));File.WriteAllText(Path.Combine(path,"a","b","c","d","e","raw"),"deep original");Reset(Inspect());File.WriteAllText(path,"another original");Assert.That(File.ReadAllText(Path.Combine(Reset(Inspect()),"original")),Is.EqualTo("another original"));
        }
        [Test] public void UnknownTargetCannotSelectCallerPaths(){Assert.Throws<ArgumentException>(()=>archive.Inspect("../selection",accepted,CancellationToken.None));Assert.That(Directory.GetFileSystemEntries(parent),Is.Empty);}
    }
}

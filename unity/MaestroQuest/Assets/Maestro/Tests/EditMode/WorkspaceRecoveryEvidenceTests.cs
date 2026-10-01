// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceRecoveryEvidenceTests
    {
        string root,source,output;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"mq-recovery-"+Guid.NewGuid().ToString("N"));source=Path.Combine(root,"source");output=Path.Combine(root,"evidence");Directory.CreateDirectory(source);}
        [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
        static JObject Accepted()=>new JObject {["version"]=1,["available"]=new JObject {["room"]=true,["behaviours"]=false,["controls"]=true,["activities"]=true},["room"]=new JObject {["version"]=2},["behaviours"]=new JObject(),["controls"]=new JObject(),["activities"]=new JObject(),["temporaryRoom"]=JValue.CreateNull()};
        static byte[] Bytes(ZipArchiveEntry entry){using var input=entry.Open();using var bytes=new MemoryStream();input.CopyTo(bytes);return bytes.ToArray();}
        [Test] public void PreservesMalformedOriginalBytesAndLabelledAcceptedDocumentsWithIndependentDigests()
        {
            var malformed=new byte[]{0xff,0xfe,0,123,42};File.WriteAllBytes(Path.Combine(source,"behaviours.v2.json"),malformed);Directory.CreateDirectory(Path.Combine(source,"models"));File.WriteAllBytes(Path.Combine(source,"models","broken.glb"),new byte[]{1,2,3});File.WriteAllText(Path.Combine(source,"notes.txt"),"Hyvää päivää 世界");
            var accepted=WorkspaceRecoveryEvidence.EncodeAccepted(Accepted());var result=WorkspaceRecoveryEvidence.Write(source,output,accepted);Assert.That(result.RawFiles,Is.EqualTo(3));Assert.That(result.Sha256,Is.EqualTo(ModelLibrary.Hash(File.ReadAllBytes(result.Path))));
            using(var zip=ZipFile.OpenRead(result.Path)){
                Assert.That(Bytes(zip.GetEntry("raw/behaviours.v2.json")),Is.EqualTo(malformed));Assert.That(Bytes(zip.GetEntry("accepted.json")),Is.EqualTo(accepted));var manifest=JObject.Parse(Encoding.UTF8.GetString(Bytes(zip.GetEntry("manifest.json"))));
                Assert.That((string)manifest["format"],Is.EqualTo("maestro-workspace-recovery-evidence"));Assert.That((string)manifest["accepted"]["sha256"],Is.EqualTo(result.AcceptedHash));
                foreach(var file in (JArray)manifest["files"]){var bytes=Bytes(zip.GetEntry((string)file["path"]));Assert.That(bytes.LongLength,Is.EqualTo((long)file["bytes"]));Assert.That(ModelLibrary.Hash(bytes),Is.EqualTo((string)file["sha256"]));}
            }
            Assert.That(File.ReadAllBytes(Path.Combine(source,"behaviours.v2.json")),Is.EqualTo(malformed));Assert.That(Directory.GetFiles(output,"*.partial"),Is.Empty);
            string staging=Path.Combine(root,"stage");Directory.CreateDirectory(staging);using var input=File.OpenRead(result.Path);Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(input,staging),"Forensic evidence must never become an executable workspace import");
        }
        [Test] public void CancellationAndOutputOverlapCannotAlterOrConsumeTheSource()
        {
            File.WriteAllText(Path.Combine(source,"original.txt"),"Keep");var accepted=WorkspaceRecoveryEvidence.EncodeAccepted(Accepted());using var cancellation=new CancellationTokenSource();cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(()=>WorkspaceRecoveryEvidence.Write(source,output,accepted,cancellation.Token));Assert.That(Directory.Exists(output),Is.False);
            Assert.Throws<IOException>(()=>WorkspaceRecoveryEvidence.Write(source,Path.Combine(source,"nested"),accepted));Assert.That(Directory.Exists(Path.Combine(source,"nested")),Is.False);Assert.That(File.ReadAllText(Path.Combine(source,"original.txt")),Is.EqualTo("Keep"));
        }
        [Test] public void RejectsUnlabelledFallbacksAndOversizedAcceptedDataBeforeWritingAnArtifact()
        {
            var value=Accepted();value["available"]=new JObject();Assert.Throws<InvalidDataException>(()=>WorkspaceRecoveryEvidence.EncodeAccepted(value));
            value=Accepted();value["room"]=new JArray();Assert.Throws<InvalidDataException>(()=>WorkspaceRecoveryEvidence.EncodeAccepted(value));
            Assert.Throws<InvalidDataException>(()=>WorkspaceRecoveryEvidence.Write(source,output,new byte[WorkspaceRecoveryEvidence.MaximumAcceptedBytes+1]));Assert.That(Directory.Exists(output),Is.False);
        }
        [Test] public void ExcessiveDirectoryDepthStopsWithoutDeletingOriginalFiles()
        {
            string nested=source;for(int i=0;i<10;i++)nested=Path.Combine(nested,"nested");Directory.CreateDirectory(nested);string original=Path.Combine(nested,"old.txt");File.WriteAllText(original,"Keep");
            Assert.Throws<InvalidDataException>(()=>WorkspaceRecoveryEvidence.Write(source,output,WorkspaceRecoveryEvidence.EncodeAccepted(Accepted())));Assert.That(File.ReadAllText(original),Is.EqualTo("Keep"));Assert.That(Directory.GetFiles(output),Is.Empty);
        }
        [Test] public void ExcessiveFileInventoryIsRejectedBeforeOpeningTheEvidenceFile()
        {
            for(int i=0;i<=WorkspaceRecoveryEvidence.MaximumFiles;i++)File.WriteAllBytes(Path.Combine(source,i+".txt"),Array.Empty<byte>());
            Assert.Throws<InvalidDataException>(()=>WorkspaceRecoveryEvidence.Write(source,output,WorkspaceRecoveryEvidence.EncodeAccepted(Accepted())));Assert.That(Directory.GetFiles(source).Length,Is.EqualTo(WorkspaceRecoveryEvidence.MaximumFiles+1));Assert.That(Directory.GetFiles(output),Is.Empty);
        }
    }
}

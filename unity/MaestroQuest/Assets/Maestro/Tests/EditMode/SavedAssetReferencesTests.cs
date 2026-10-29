// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class SavedAssetReferencesTests
    {
        const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string Id="cccccccccccccccccccccccccccccccc",Cell="dddddddddddddddddddddddddddddddd";
        string directory;
        string PathFor(string name)=>Path.Combine(directory,name);
        [SetUp]public void Before(){directory=Path.Combine(Path.GetTempPath(),"MaestroSavedReferences-"+Guid.NewGuid().ToString("N"));_=BehaviourCatalog.Actions.Count;}
        [TearDown]public void After(){RoomSnapshotTransaction.InspectionWaiting=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static RoomDocument Room(string hash=null)=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}},
            appearances=hash==null?Array.Empty<RoomAppearance>():new[]{new RoomAppearance{id=Id,name="Saved appearance",style=new(){patternMode="image",imageHash=hash}}}};
        static byte[] RoomBytes(string hash=null,int version=RoomDocument.CurrentVersion){var room=Room(hash);room.version=version;Assert.That(room.Validate(out var error),Is.True,error);return Encoding.UTF8.GetBytes(RoomLiquidCodec.Encode(room));}
        static string Source(string identity)=>new JObject{["version"]=3,["entry"]="main",["resources"]=new JArray(),["state"]=new JArray(new JObject{["name"]="image",["initial"]=identity}),["events"]=new JArray(),
            ["functions"]=new JArray(new JObject{["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),["body"]=new JArray()})}.ToString(Formatting.None);
        static byte[] Memory(string identity)=>ProgramMemoryDocument.Empty().WithValues(Id,new Dictionary<string,ProgramMemoryDocument.Cell>{{Cell,new("image",new ProgramValue(identity))}}).Encode();
        void Write(string name,byte[] bytes){Directory.CreateDirectory(Path.GetDirectoryName(PathFor(name)));File.WriteAllBytes(PathFor(name),bytes);}
        void Write(string name,string source)=>Write(name,Encoding.UTF8.GetBytes(source));
        Dictionary<string,string> Evidence()=>Directory.Exists(directory)?Directory.GetFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(p=>p.Substring(directory.Length),p=>Convert.ToBase64String(File.ReadAllBytes(p))):new();
        SavedAssetReferenceProof Proof()=>SavedAssetReferences.ReadImages(directory,Hash);
        static void Clear(SavedAssetReferenceProof proof){Assert.That(proof.Ready,Is.True);Assert.That(proof.Uncertain,Is.False);Assert.That(proof.Retained,Is.False);Assert.That(proof.Reasons,Is.Empty);}
        void Retained(){var before=Evidence();var proof=Proof();Assert.That(proof.Ready,Is.True);Assert.That(proof.Retained,Is.True);Assert.That(proof.Uncertain,Is.False,string.Join("; ",proof.Reasons));Assert.That(Evidence(),Is.EquivalentTo(before));}
        void Uncertain(){var before=Evidence();var proof=Proof();Assert.That(proof.Uncertain,Is.True);Assert.That(proof.Reasons,Is.Not.Empty);Assert.That(Evidence(),Is.EquivalentTo(before));}
        [Test]public void AbsentAndEmptyWorkspaceHaveNoReferencesAndAreNeverInitialized(){Clear(Proof());Assert.That(Directory.Exists(directory),Is.False);Directory.CreateDirectory(directory);Clear(Proof());Assert.That(Directory.GetFileSystemEntries(directory),Is.Empty);}
        [Test]public void SavedRoomWithoutImageAndItsNormalWriterLockAreClear(){var storage=new RoomStorage(directory);Assert.That(storage.Save(Room(),out var error),Is.True,error);Assert.That(File.Exists(PathFor("room-snapshot.writer.lock")),Is.True);var before=Evidence();Clear(Proof());Assert.That(Evidence(),Is.EquivalentTo(before));}
        [TestCase("")][TestCase(".backup")][TestCase(".pending")][TestCase(".unreadable")][TestCase(".prepare.cccccccccccccccccccccccccccccccc")]
        public void EveryRetainedRoomDocumentProtectsAnUnboundAppearance(string suffix){Write(RoomStorage.FileName+suffix,RoomBytes(Hash));Retained();}
        [Test]public void SupportedOlderRoomStillProtectsItsImage(){Write("room.v25.json",RoomBytes(Hash,25));Retained();}
        [Test]public void FreshProofDoesNotTrustSameLengthAndTimestampFromAnEarlierInspection(){Write(RoomStorage.FileName,RoomBytes(Other));Clear(Proof());var stamp=File.GetLastWriteTimeUtc(PathFor(RoomStorage.FileName));Write(RoomStorage.FileName,RoomBytes(Hash));File.SetLastWriteTimeUtc(PathFor(RoomStorage.FileName),stamp);Retained();}
        [TestCase("")][TestCase(".backup")][TestCase(".pending")][TestCase(".unreadable")][TestCase(".prepare.cccccccccccccccccccccccccccccccc")]
        public void SavedBehaviourAndRetainedSourcesProtectExactAssetStrings(string suffix){var rules=new RuleDocument{sequences=new[]{new RuleSequence{id=Id,name="Remember image",program=Source(Hash)}}};Assert.That(rules.Validate(out var error),Is.True,error);Write("behaviours.v2.json"+suffix,JsonUtility.ToJson(rules));Retained();}
        [Test]public void AnUnavailableSavedProgramCannotProveTheImageUnreferenced(){var rules=new RuleDocument{sequences=new[]{new RuleSequence{id=Id,name="Unavailable",program="{\"version\":999}"}}};Assert.That(rules.Validate(out _,true),Is.True);Write("behaviours.v2.json",JsonUtility.ToJson(rules));Uncertain();}
        [TestCase("")][TestCase(".backup")][TestCase(".pending.cccccccccccccccccccccccccccccccc")]
        public void RememberedValuesIncludePrimaryBackupAndPendingWrites(string suffix){Write(ProgramMemoryStore.FileName+suffix,Memory(Hash));Retained();}
        [TestCase(false)][TestCase(true)]public void SavedAndStagedModuleDefinitionsProtectExactAssetStrings(bool pending){var definition=ProgramModuleLibrary.Definition(Source(Hash),"Saved image",new[]{"main"});Write("program-modules.v1/"+(pending?Id+".tmp":ProgramModules.Hash(definition)+".json"),definition.ToString(Formatting.None));Retained();}
        [Test]public void ModuleIdentityMismatchAndCompilationFailureAreUncertain(){var definition=ProgramModuleLibrary.Definition(Source(Hash),"Saved image",new[]{"main"});Write("program-modules.v1/"+Other+".json",definition.ToString(Formatting.None));Uncertain();File.Delete(PathFor("program-modules.v1/"+Other+".json"));definition["exports"]=new JArray("missing");Write("program-modules.v1/"+ProgramModules.Hash(definition)+".json",definition.ToString(Formatting.None));Uncertain();}
        [Test]public void NonmatchingProgramModuleAndMemoryValuesDoNotBecomeReferences(){var value=Hash+"-different";var rules=new RuleDocument{sequences=new[]{new RuleSequence{id=Id,name="Other text",program=Source(value)}}};Write("behaviours.v2.json",JsonUtility.ToJson(rules));Write(ProgramMemoryStore.FileName,Memory(value));var module=ProgramModuleLibrary.Definition(Source(value),"Other text",new[]{"main"});Write("program-modules.v1/"+ProgramModules.Hash(module)+".json",module.ToString(Formatting.None));Clear(Proof());}
        [TestCase("room.v999.json")][TestCase("room.v46.json.prepare.unknown")][TestCase("behaviours.v3.json")][TestCase("rules.v1.json")]
        [TestCase("program-memory.v2.json")][TestCase("program-memory.v1.json.unreadable")][TestCase("program-modules.v2")][TestCase("Room.v46.json")]
        public void UnsupportedRetainedFormatsAndAliasesArePreservedAndUncertain(string name){Write(name,"retained evidence");Uncertain();}
        [TestCase("room-snapshot.v33.json")][TestCase("room-snapshot.v17.json")][TestCase("room.v46.json.snapshot.cccccccccccccccccccccccccccccccc")][TestCase("program-memory.v1.json.snapshot.cccccccccccccccccccccccccccccccc")]
        public void InterruptedSnapshotEvidenceIsNeverRecoveredByProof(string name){Write(RoomStorage.FileName,RoomBytes());Write(name,"interrupted evidence");Uncertain();}
        [TestCase(false)][TestCase(true)]public void MalformedAndOversizedRoomEvidenceIsUncertain(bool oversized){Write(RoomStorage.FileName,oversized?new byte[4*1024*1024+1]:new byte[]{0xff});Uncertain();}
        [Test]public void DirectoryAtDocumentPathAndFileAtModuleRootCannotBecomeEmptyLibraries(){Directory.CreateDirectory(PathFor(RoomStorage.FileName));Assert.That(Proof().Uncertain,Is.True);Directory.Delete(PathFor(RoomStorage.FileName));Write("program-modules.v1","preserve");Uncertain();}
        [Test]public void ASharingDenialDoesNotBecomeAbsence(){var bytes=RoomBytes();Write(RoomStorage.FileName,bytes);using(var held=new FileStream(PathFor(RoomStorage.FileName),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){var proof=Proof();Assert.That(proof.Uncertain,Is.True);}Assert.That(File.ReadAllBytes(PathFor(RoomStorage.FileName)),Is.EqualTo(bytes));}
        [Test]public void ALinkedRetainedDocumentIsUncertainAndDoesNotReadOrChangeItsTarget(){var target=PathFor("original.bin");Write("original.bin",RoomBytes());AssetSidecarTestLinks.Create(PathFor(RoomStorage.FileName),target);var before=File.ReadAllBytes(target);try{Assert.That(Proof().Uncertain,Is.True);Assert.That(File.ReadAllBytes(target),Is.EqualTo(before));}finally{File.Delete(PathFor(RoomStorage.FileName));}}
        [Test]public void EnumerationCountsUnrelatedEntriesAndDoesNotTraversePastItsLimit(){Directory.CreateDirectory(directory);for(int i=0;i<=SavedAssetReferences.MaximumVisitedEntries;i++)Write("unrelated-"+i,"");var proof=Proof();Assert.That(proof.Ready,Is.False);Assert.That(proof.Uncertain,Is.True);Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(SavedAssetReferences.MaximumVisitedEntries+1));}
        [Test]public void CumulativeByteBudgetCannotBeBypassedByManyIndividuallyValidRoomStages(){var body=RoomBytes();var padded=new byte[4*1024*1024];Array.Copy(body,padded,body.Length);for(int i=body.Length;i<padded.Length;i++)padded[i]=(byte)' ';int count=(int)(SavedAssetReferences.MaximumReadBytes/padded.Length)+1;for(int i=0;i<count;i++)Write(RoomStorage.FileName+".prepare."+Guid.NewGuid().ToString("N"),padded);var proof=Proof();Assert.That(proof.Uncertain,Is.True);Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(count));}
        [Test]public void CancellationStopsProofWhileAnotherSnapshotReaderStillOwnsTheWorkspace(){
            Write(RoomStorage.FileName,RoomBytes());var before=Evidence();using var owner=RoomSnapshotTransaction.Inspect(directory);using var entered=new ManualResetEventSlim();using var stop=new CancellationTokenSource();
            RoomSnapshotTransaction.InspectionWaiting=path=>{if(path==directory)entered.Set();};var reader=Task.Run(()=>SavedAssetReferences.ReadImages(directory,Hash,stop.Token));
            try{Assert.That(entered.Wait(TimeSpan.FromSeconds(2)),Is.True,"Proof must reach snapshot admission");Assert.That(reader.IsCompleted,Is.False);stop.Cancel();Assert.That(SpinWait.SpinUntil(()=>reader.IsCompleted,2000),Is.True,"Stop must finish before the existing reader releases");var error=Assert.Catch<OperationCanceledException>(()=>reader.GetAwaiter().GetResult());Assert.That(error.CancellationToken,Is.EqualTo(stop.Token));Assert.That(Evidence(),Is.EquivalentTo(before));}
            finally{stop.Cancel();RoomSnapshotTransaction.InspectionWaiting=null;owner.Dispose();try{reader.GetAwaiter().GetResult();}catch(OperationCanceledException){}}
        }
    }
}

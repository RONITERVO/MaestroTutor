// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class ProgramMemoryTests
    {
        const string Program="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",Count="cccccccccccccccccccccccccccccccc",Items="dddddddddddddddddddddddddddddddd";
        string directory;readonly List<ProgramMemoryStore> stores=new();
        static string Id(int number)=>number.ToString("x32");
        static Dictionary<string,ProgramMemoryDocument.Cell> Values(double number=1,string name="count")=>new() {[Count]=new(name,new ProgramValue(number))};
        static byte[] Bytes(string source)=>Encoding.UTF8.GetBytes(source);
        string Primary=>Path.Combine(directory,ProgramMemoryStore.FileName);
        ProgramMemoryStore Open(WorkspaceWriteGate gate=null,Action<string> fault=null)
        {
            var store=new ProgramMemoryStore(directory,gate??new(),fault);stores.Add(store);store.Initialization.GetAwaiter().GetResult();return store;
        }
        static ProgramMemoryStore.Result Write(ProgramMemoryStore store,double number=1)=>store.Write(store.Snapshot().Revision,Program,Values(number)).GetAwaiter().GetResult();
        static double Read(ProgramMemoryDocument doc,string program=Program,string cell=Count)
        {Assert.That(doc.TryRead(program,cell,ProgramType.Number,out var value),Is.True);return value.Number;}
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"maestro-memory-"+Guid.NewGuid().ToString("N"));}
        [TearDown] public void Cleanup(){foreach(var store in stores)store.Drain().GetAwaiter().GetResult();stores.Clear();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test] public void TypedRoundTripIsDetachedAndPreservesNamesWithoutChangingIdentity()
        {
            var value=JObject.Parse("{\"positions\":[],\"done\":false}");var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"positions\":{\"list\":\"number\"},\"done\":\"boolean\"}}"));
            var writes=Values();writes[Items]=new("progress",ProgramValue.Literal(value,type));
            var original=ProgramMemoryDocument.Empty().WithValues(Program,writes);value["done"]=true;writes.Clear();
            var decoded=ProgramMemoryDocument.Decode(original.Encode());Assert.That(decoded.Identity,Is.EqualTo(original.Identity));Assert.That(Read(decoded),Is.EqualTo(1));
            Assert.That(decoded.TryRead(Program,Items,type,out var saved),Is.True);var observed=(JObject)saved.Value;Assert.That((bool)observed["done"],Is.False);observed["done"]=true;
            Assert.That(decoded.TryRead(Program,Items,type,out saved),Is.True);Assert.That((bool)((JObject)saved.Value)["done"],Is.False);
            var renamed=decoded.WithValues(Program,Values(1,"score"));Assert.That(renamed.Programs[Program][Count].Name,Is.EqualTo("score"));Assert.That(renamed.Programs[Program].Count,Is.EqualTo(2));
            Assert.That(renamed.TryRead(Other,Count,ProgramType.Number,out _),Is.False,"Copied behaviour identities do not inherit memory.");
            Assert.Throws<InvalidDataException>(()=>renamed.TryRead(Program,Count,ProgramType.Text,out _));
            Assert.Throws<InvalidDataException>(()=>renamed.WithValues(Program,new Dictionary<string,ProgramMemoryDocument.Cell>{[Count]=new("score",new ProgramValue("one"))}));
        }
        [Test] public void ExistingBehaviourIdentityCaseIsPreservedWithoutFoldingDistinctKeys()
        {
            string upper=Program.ToUpperInvariant();Assert.That(Maestro.Quest.Rules.RuleDocument.IsId(upper),Is.True);
            var doc=ProgramMemoryDocument.Empty().WithValues(upper,Values(8)).WithValues(Program,Values(2));
            doc=ProgramMemoryDocument.Decode(doc.Encode());Assert.That(Read(doc,upper),Is.EqualTo(8));Assert.That(Read(doc),Is.EqualTo(2));
            var store=Open();Assert.That(store.Write(store.Snapshot().Revision,upper,Values(8)).GetAwaiter().GetResult().Error,Is.Null);Assert.That(Read(Open().Snapshot(),upper),Is.EqualTo(8));
        }
        [Test] public void CanonicalIdentityIgnoresFormattingAndObjectOrderButChangesWithContent()
        {
            var doc=ProgramMemoryDocument.Empty().WithValues(Program,Values());var json=JObject.Parse(Encoding.UTF8.GetString(doc.Encode()));
            var reordered=new JObject(json.Properties().Reverse().Select(p=>new JProperty(p.Name,p.Value.DeepClone())));
            Assert.That(ProgramMemoryDocument.Decode(Bytes(reordered.ToString(Formatting.Indented))).Identity,Is.EqualTo(doc.Identity));
            json["programs"][0]["cells"][0]["value"]=2;
            Assert.That(ProgramMemoryDocument.Decode(Bytes(json.ToString())).Revision,Is.EqualTo(doc.Revision));
            Assert.That(ProgramMemoryDocument.Decode(Bytes(json.ToString())).Identity,Is.Not.EqualTo(doc.Identity));
        }
        [Test] public void ExplicitResetOnlyRemovesSelectedValuesAndCannotReuseAnOldRevision()
        {
            var first=ProgramMemoryDocument.Empty().WithValues(Program,Values()).WithValues(Other,Values(9));
            var reset=first.Forget(Program,Count);Assert.That(reset.TryRead(Program,Count,ProgramType.Number,out _),Is.False);Assert.That(Read(reset,Other),Is.EqualTo(9));
            var again=reset.WithValues(Program,Values());Assert.That(Read(again),Is.EqualTo(1));Assert.That(again.Revision,Is.Not.EqualTo(first.Revision));
            Assert.That(again.Forget(Id(1)),Is.SameAs(again));Assert.That(again.WithValues(Program,Values()),Is.SameAs(again));
            var empty=again.Forget(Other).Forget(Program);Assert.That(empty.Revision,Is.Not.EqualTo(ProgramMemoryDocument.InitialRevision));
        }
        [Test] public void NestedSavedReferencesAreVisibleButAreOnlyPassiveValues()
        {
            string motion=new string('a',64);var data=ProgramValue.Literal(JObject.Parse("{\"motion\":\""+motion+"\",\"objects\":[\""+Other+"\"]}"));
            var doc=ProgramMemoryDocument.Empty().WithValues(Program,new Dictionary<string,ProgramMemoryDocument.Cell>{[Count]=new("assets",data)});
            Assert.That(doc.Retains(motion),Is.True);Assert.That(doc.Retains(Other),Is.True);Assert.That(doc.Retains(Program),Is.False);
        }
        [TestCase("version", "2")]
        [TestCase("version", "1.5")]
        [TestCase("revision", "\"../outside\"")]
        [TestCase("programs", "null")]
        [TestCase("unknown", "true")]
        public void UnknownAndMalformedDocumentFieldsAreRejected(string key,string raw)
        {
            var json=JObject.Parse(Encoding.UTF8.GetString(ProgramMemoryDocument.Empty().Encode()));json[key]=JToken.Parse(raw);
            Assert.Throws<InvalidDataException>(()=>ProgramMemoryDocument.Decode(Bytes(json.ToString())));
        }
        [Test] public void DuplicateFieldsIdentitiesAndInvalidValuesAreRejected()
        {
            Assert.Throws<JsonReaderException>(()=>ProgramMemoryDocument.Decode(Bytes("{\"version\":1,\"version\":1,\"revision\":\"initial\",\"programs\":[]}")));
            var doc=ProgramMemoryDocument.Empty().WithValues(Program,Values());var json=JObject.Parse(Encoding.UTF8.GetString(doc.Encode()));
            ((JArray)json["programs"]).Add(json["programs"][0].DeepClone());Assert.Throws<InvalidDataException>(()=>ProgramMemoryDocument.Decode(Bytes(json.ToString())));
            json=JObject.Parse(Encoding.UTF8.GetString(doc.Encode()));((JArray)json["programs"][0]["cells"]).Add(json["programs"][0]["cells"][0].DeepClone());Assert.Throws<InvalidDataException>(()=>ProgramMemoryDocument.Decode(Bytes(json.ToString())));
            json=JObject.Parse(Encoding.UTF8.GetString(doc.Encode()));json["programs"][0]["cells"][0]["value"]="wrong type";Assert.Throws<ProgramFault>(()=>ProgramMemoryDocument.Decode(Bytes(json.ToString())));
            Assert.Throws<ProgramFault>(()=>new ProgramMemoryDocument.Cell("score",new ProgramValue(double.NaN)));
            Assert.Throws<JsonReaderException>(()=>ProgramMemoryDocument.Decode(doc.Encode().Concat(Bytes(" {} ")).ToArray()));
            Assert.Throws<DecoderFallbackException>(()=>ProgramMemoryDocument.Decode(new byte[]{0xc0,0xaf}));
        }
        [Test] public void BoundsCoverProgramsCellsWritesAndRetainedCells()
        {
            var doc=ProgramMemoryDocument.Empty();for(int i=0;i<64;i++)doc=doc.WithValues(Id(i),Values());
            var full=doc;Assert.Throws<InvalidDataException>(()=>full.WithValues(Id(64),Values()));
            var many=Enumerable.Range(0,16).ToDictionary(Id,i=>new ProgramMemoryDocument.Cell("v"+i,new ProgramValue(i)));
            doc=ProgramMemoryDocument.Empty();for(int i=0;i<32;i++)doc=doc.WithValues(Id(i),many);
            full=doc;Assert.Throws<InvalidDataException>(()=>full.WithValues(Id(32),Values()));
            many[Id(16)]=new("tooMany",new ProgramValue(1));Assert.Throws<InvalidDataException>(()=>ProgramMemoryDocument.Empty().WithValues(Program,many));
            doc=ProgramMemoryDocument.Empty();for(int i=0;i<32;i++)doc=doc.WithValues(Program,new Dictionary<string,ProgramMemoryDocument.Cell>{[Id(i)]=new("v"+i,new ProgramValue(i))});
            full=doc;Assert.Throws<InvalidDataException>(()=>full.WithValues(Program,Values()));
        }
        [Test] public void OpeningDoesNotWriteAndAnAcceptedCheckpointSurvivesRestart()
        {
            var store=Open();Assert.That(store.Error,Is.Null);Assert.That(Directory.Exists(directory),Is.False);var old=store.Snapshot();
            var result=Write(store,12);Assert.That(result.Error,Is.Null);Assert.That(result.Changed,Is.True);Assert.That(Read(store.Snapshot()),Is.EqualTo(12));
            Assert.That(old.Programs,Is.Empty);Assert.That(Read(Open().Snapshot()),Is.EqualTo(12));
            var duplicate=Write(store,12);Assert.That(duplicate.Changed,Is.False);Assert.That(duplicate.Revision,Is.EqualTo(result.Revision));
            var reset=store.Reset(result.Revision,Program).GetAwaiter().GetResult();Assert.That(reset.Error,Is.Null);Assert.That(Open().Snapshot().Programs,Is.Empty);
            Assert.Throws<InvalidOperationException>(()=>store.Write(result.Revision,Program,Values()));
        }
        [Test] public void AcceptedInputIsDetachedAndIncompatibleTypesFailBeforeFilesystemChanges()
        {
            var store=Open();var values=Values(6);var task=store.Write(store.Snapshot().Revision,Program,values);values[Count]=new("mutated",new ProgramValue(99));
            Assert.That(task.GetAwaiter().GetResult().Error,Is.Null);Assert.That(Read(store.Snapshot()),Is.EqualTo(6));var bytes=File.ReadAllBytes(Primary);
            var bad=new Dictionary<string,ProgramMemoryDocument.Cell>{[Count]=new("differentType",new ProgramValue("text"))};
            var result=store.Write(store.Snapshot().Revision,Program,bad).GetAwaiter().GetResult();Assert.That(result.Error,Is.Not.Null);Assert.That(result.Changed,Is.False);
            Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(bytes));Assert.That(Read(store.Snapshot()),Is.EqualTo(6));
        }
        [Test] public void WriteFailurePreservesCurrentAndBackupBytesAndAllowsExplicitRetry()
        {
            var store=Open();Assert.That(Write(store).Error,Is.Null);Assert.That(Write(store,2).Error,Is.Null);
            var primary=File.ReadAllBytes(Primary);var backup=File.ReadAllBytes(Primary+".backup");bool fail=true;
            store=Open(fault:stage=>{if(fail&&stage=="before-publish")throw new IOException("Injected storage failure");});
            var failed=Write(store,3);Assert.That(failed.Error,Does.Contain("Injected"));Assert.That(failed.Changed,Is.False);Assert.That(Read(store.Snapshot()),Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(Primary+".backup"),Is.EqualTo(backup));Assert.That(Directory.GetFiles(directory,"*.pending.*"),Is.Empty);
            fail=false;Assert.That(Write(store,3).Error,Is.Null);Assert.That(Read(Open().Snapshot()),Is.EqualTo(3));
        }
        [Test] public void CorruptPrimaryNeverSilentlyRollsBackToAnOlderGoodCheckpoint()
        {
            var store=Open();Write(store,1);Write(store,2);File.WriteAllText(Primary,"broken");
            var backup=File.ReadAllBytes(Primary+".backup");store=Open();Assert.That(store.Error,Is.Not.Null);Assert.Throws<InvalidOperationException>(()=>store.Snapshot());
            Assert.Throws<InvalidOperationException>(()=>store.Write("initial",Program,Values(3)));Assert.That(File.ReadAllText(Primary),Is.EqualTo("broken"));Assert.That(File.ReadAllBytes(Primary+".backup"),Is.EqualTo(backup));
        }
        [TestCase(".backup")]
        [TestCase(".pending.11111111111111111111111111111111")]
        public void MissingPrimaryWithRetainedEvidenceDoesNotInitializeEmpty(string suffix)
        {
            Directory.CreateDirectory(directory);var bytes=ProgramMemoryDocument.Empty().WithValues(Program,Values()).Encode();File.WriteAllBytes(Primary+suffix,bytes);
            var store=Open();Assert.That(store.Error,Is.Not.Null);Assert.That(File.Exists(Primary),Is.False);Assert.That(File.ReadAllBytes(Primary+suffix),Is.EqualTo(bytes));
        }
        [TestCase("program-memory.v2.json")]
        [TestCase("program-memory.v2.json.backup")]
        [TestCase("program-memory.v99999999999999999.json.pending")]
        public void NewerFormatsArePreservedAndPreventOlderWrites(string name)
        {
            var store=Open();Write(store);string newer=Path.Combine(directory,name);File.WriteAllText(newer,"future data");
            Assert.That(Write(store,2).Error,Is.Not.Null);Assert.That(store.Error,Is.Not.Null);Assert.Throws<InvalidOperationException>(()=>store.Snapshot());
            store=Open();Assert.That(store.Error,Is.Not.Null);Assert.That(File.ReadAllText(newer),Is.EqualTo("future data"));Assert.That(Read(ProgramMemoryDocument.Decode(File.ReadAllBytes(Primary))),Is.EqualTo(1));
        }
        [Test] public void AChangedDiskIdentityRejectsEvenAnOtherwiseIdenticalWrite()
        {
            var first=Open();Write(first);var second=Open();Write(second,5);
            var stale=Write(first,1);Assert.That(stale.Changed,Is.False);Assert.That(stale.Error,Is.Not.Null);Assert.That(first.Error,Is.Not.Null);Assert.That(Read(Open().Snapshot()),Is.EqualTo(5));
        }
        [Test] public void EditingValuesWithoutChangingTheRevisionCannotBypassDiskComparison()
        {
            var store=Open();Write(store);var json=JObject.Parse(File.ReadAllText(Primary));json["programs"][0]["cells"][0]["value"]=42;File.WriteAllText(Primary,json.ToString());
            var result=Write(store,2);Assert.That(result.Error,Is.Not.Null);Assert.That(Read(Open().Snapshot()),Is.EqualTo(42));
        }
        [Test] public void ACorruptBackupIsNotOverwrittenByTheNextCheckpoint()
        {
            var store=Open();Write(store);Write(store,2);File.WriteAllText(Primary+".backup","retain evidence");var bytes=File.ReadAllBytes(Primary);
            var result=Write(store,3);Assert.That(result.Error,Is.Not.Null);Assert.That(result.Changed,Is.False);Assert.That(File.ReadAllText(Primary+".backup"),Is.EqualTo("retain evidence"));Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(bytes));
        }
        [Test] public async Task AcceptedWriteDrainsThroughRetirementAndReleasesItsLeaseWithoutPolling()
        {
            var gate=new WorkspaceWriteGate();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            var store=Open(gate,stage=>{if(stage=="written"){entered.Set();if(!release.Wait(5000))throw new IOException("Test barrier expired");}});
            var task=store.Write(store.Snapshot().Revision,Program,Values(8));
            try {
                Assert.That(entered.Wait(5000),Is.True);Assert.That(store.Pending,Is.True);Assert.That(gate.CanFreeze(out _),Is.False);
                Assert.Throws<InvalidOperationException>(()=>store.Write(store.Snapshot().Revision,Program,Values(9)));
                var retired=gate.Retire();Assert.That(retired.IsCompleted,Is.False);release.Set();var result=await task;await retired;
                Assert.That(result.Error,Is.Null);Assert.That(store.Pending,Is.False);Assert.That(Read(store.Snapshot()),Is.EqualTo(8));Assert.That(Read(Open().Snapshot()),Is.EqualTo(8));
                Assert.Throws<InvalidOperationException>(()=>store.Write(store.Snapshot().Revision,Program,Values(9)));
            }finally{release.Set();await task;}
        }
        [Test] public void FreezeRejectsMutationAndNoOpWithoutChangingFiles()
        {
            var gate=new WorkspaceWriteGate();var store=Open(gate);Write(store);var bytes=File.ReadAllBytes(Primary);
            using var held=gate.TryFreeze(out _);Assert.That(held,Is.Not.Null);
            Assert.Throws<InvalidOperationException>(()=>store.Write(store.Snapshot().Revision,Program,Values()));Assert.Throws<InvalidOperationException>(()=>store.Reset(store.Snapshot().Revision,Other));Assert.That(File.ReadAllBytes(Primary),Is.EqualTo(bytes));
        }
        [TestCase(false)][TestCase(true)] public async Task TwoOwnersCannotOverwriteTheCheckpointBeingPublished(bool trailingSeparator)
        {
            var first=Open();Write(first);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            first=Open(fault:stage=>{if(stage=="written"){entered.Set();if(!release.Wait(5000))throw new IOException("Test barrier expired");}});var second=new ProgramMemoryStore(directory+(trailingSeparator?Path.DirectorySeparatorChar.ToString():""),new());stores.Add(second);await second.Initialization;
            var task=first.Write(first.Snapshot().Revision,Program,Values(2));
            try{Assert.That(entered.Wait(5000),Is.True);var competing=await second.Write(second.Snapshot().Revision,Program,Values(3));Assert.That(competing.Error,Is.Not.Null);release.Set();Assert.That((await task).Error,Is.Null);Assert.That(Read(Open().Snapshot()),Is.EqualTo(2));}
            finally{release.Set();await task;}
        }
        [Test] public void InvalidWritesAndOccupiedPathsNeverCreateOrReplaceStorage()
        {
            Assert.Throws<ArgumentException>(()=>new ProgramMemoryStore(Path.GetPathRoot(Path.GetFullPath(directory)),new WorkspaceWriteGate()));
            var store=Open();Assert.Throws<InvalidDataException>(()=>store.Write(store.Snapshot().Revision,"../outside",Values()));Assert.That(Directory.Exists(directory),Is.False);
            Directory.CreateDirectory(Primary);store=Open();Assert.That(store.Error,Is.Not.Null);Assert.That(Directory.Exists(Primary),Is.True);
        }
    }
}

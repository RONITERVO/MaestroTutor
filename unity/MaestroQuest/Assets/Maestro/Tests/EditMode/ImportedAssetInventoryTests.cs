// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Imports;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ImportedAssetInventoryTests
    {
        string directory;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroInventory-"+Guid.NewGuid().ToString("N"));}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);}
        ImportedAssetInventory<string> Scan(Func<IEnumerable<string>> paths=null,Func<string,ImportedAssetReadBudget,CancellationToken,string> inspect=null,CancellationToken cancel=default,ImportedAssetReadBudget budget=null)=>
            ImportedAssetInventory.Scan(directory,".asset",budget??new(1024,1024),inspect??((path,_,__)=>Path.GetFileName(path)),cancel,paths);
        [Test]public void MissingDirectoryIsCompleteEmptyAndDoesNotCreateAnything()
        {
            var result=Scan();Assert.That(result.Ready&&result.Complete&&result.EnumerationComplete,Is.True);Assert.That(result.Entries,Is.Empty);Assert.That(result.Error,Is.Empty);Assert.That(result.IssueCount,Is.Zero);Assert.That(Directory.Exists(directory),Is.False);
        }
        [Test]public void WrongRootKindIsUnavailableRatherThanAnEmptyLibrary()
        {
            File.WriteAllText(directory,"Preserve me");var result=Scan();Assert.That(result.Ready||result.Complete||result.EnumerationComplete,Is.False);Assert.That(result.Error,Is.Not.Empty);Assert.That(result.IssueCount,Is.EqualTo(1));Assert.Throws<IOException>(()=>result.RequireComplete());Assert.That(File.ReadAllText(directory),Is.EqualTo("Preserve me"));
        }
        [Test]public void DirectoryLimitCountsUnrelatedEntriesAndStopsAfterOneEndProbe()
        {
            Directory.CreateDirectory(directory);int visited=0,inspected=0;
            IEnumerable<string> Paths(){for(int i=0;i<1000000;i++){visited++;if(visited>ImportedAssetInventory.MaximumVisitedEntries+1)Assert.Fail("Unbounded directory traversal");yield return Path.Combine(directory,i+".unrelated");}}
            var result=Scan(Paths,(path,_,__)=>{inspected++;return path;});Assert.That(visited,Is.EqualTo(ImportedAssetInventory.MaximumVisitedEntries+1));Assert.That(inspected,Is.Zero);Assert.That(result.EnumerationComplete||result.Complete,Is.False);Assert.That(result.Error,Is.Not.Empty);
        }
        [Test]public void IssueCountIncludesEnumerationLimitDisposalAndEveryObservedEntryFailure()
        {
            Directory.CreateDirectory(directory);
            IEnumerable<string> Paths(){try{for(int i=0;i<=ImportedAssetInventory.MaximumVisitedEntries;i++)yield return i+".asset";}finally{throw new IOException("Enumeration disposal failed");}}
            var result=Scan(Paths,(_,__,___)=>throw new InvalidDataException("Invalid entry"));Assert.That(result.IssueCount,Is.EqualTo(ImportedAssetInventory.MaximumVisitedEntries+2));Assert.That(result.Ready,Is.True);Assert.That(result.Complete||result.EnumerationComplete,Is.False);Assert.That(result.Entries,Is.Empty);
        }
        [Test]public void ExpectedEntryFailureKeepsSortedDenseResultsAndOriginalStrictFailure()
        {
            Directory.CreateDirectory(directory);var failure=new IOException("Controlled sharing failure");
            var result=Scan(()=>new[]{"z.asset","bad.asset","a.asset"},(path,_,__)=>path=="bad.asset"?throw failure:path);
            Assert.That(result.Ready&&result.EnumerationComplete,Is.True);Assert.That(result.Complete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(1));Assert.That(result.Entries,Is.EqualTo(new[]{"a.asset","z.asset"}));Assert.That(Assert.Throws<IOException>(()=>result.RequireComplete()),Is.SameAs(failure));Assert.That(result.Error.Length,Is.LessThanOrEqualTo(128));
        }
        [TestCase(".image")][TestCase(".wav")][TestCase(".glb")]public void ExtensionAliasesCannotBeInspectedAsDuplicateCanonicalIdentities(string extension)
        {
            Directory.CreateDirectory(directory);string hash=new string('a',64),canonical=Path.Combine(directory,hash+extension),alias=Path.Combine(directory,hash+extension.ToUpperInvariant());var inspected=new List<string>();
            // The enumerator models case-sensitive storage even on Windows.
            var result=ImportedAssetInventory.Scan(directory,extension,new ImportedAssetReadBudget(1,1),(path,_,__)=>{inspected.Add(path);return Path.GetFileNameWithoutExtension(path);},CancellationToken.None,()=>new[]{canonical,alias});
            Assert.That(inspected,Is.EqualTo(new[]{canonical}));Assert.That(result.Entries,Is.EqualTo(new[]{hash}));Assert.That(result.Ready&&result.EnumerationComplete,Is.True);Assert.That(result.Complete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(1));Assert.That(result.Error,Is.Not.Empty);
        }
        [TestCase(false)][TestCase(true)]public void EnumerationFailureKeepsItsUsablePrefixButDoesNotClaimCompleteness(bool prefix)
        {
            Directory.CreateDirectory(directory);
            IEnumerable<string> Paths(){if(prefix)yield return "one.asset";throw new IOException("Directory changed");}
            var result=Scan(Paths);Assert.That(result.Ready,Is.EqualTo(prefix));Assert.That(result.Entries.Length,Is.EqualTo(prefix?1:0));Assert.That(result.Complete||result.EnumerationComplete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(1));
        }
        [Test]public void RetainedResultLimitDoesNotReturnAnUnmarkedTruncatedLibrary()
        {
            Directory.CreateDirectory(directory);int inspected=0;var paths=Enumerable.Range(0,ImportedAssetInventory.MaximumRetainedEntries+1).Select(i=>i.ToString("D3")+".asset");
            var result=Scan(()=>paths,(path,_,__)=>{inspected++;return path;});Assert.That(inspected,Is.EqualTo(ImportedAssetInventory.MaximumRetainedEntries));Assert.That(result.Entries,Has.Length.EqualTo(ImportedAssetInventory.MaximumRetainedEntries));Assert.That(result.Ready&&result.EnumerationComplete,Is.True);Assert.That(result.Complete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(1));
        }
        [TestCase(false)][TestCase(true)]public void ReadReservationsBoundCumulativePayloadAndMetadataWork(bool metadata)
        {
            Directory.CreateDirectory(directory);string first=Path.Combine(directory,"a.asset"),second=Path.Combine(directory,"b.asset");File.WriteAllBytes(first,new byte[4]);File.WriteAllBytes(second,new byte[4]);int reads=0;
            var result=Scan(inspect:(path,budget,token)=>{
                if(metadata){var bytes=budget.ReadMetadata(path,4,token);reads+=bytes.Length;}
                else{budget.Payload(new FileInfo(path).Length+1);reads+=File.ReadAllBytes(path).Length;}
                return Path.GetFileName(path);
            },budget:new ImportedAssetReadBudget(5,5));
            Assert.That(reads,Is.EqualTo(4));Assert.That(result.Entries,Is.EqualTo(new[]{"a.asset"}));Assert.That(result.Ready&&result.EnumerationComplete,Is.True);Assert.That(result.Complete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(1));
        }
        [Test]public void CancellationDuringEnumerationDoesNotPublishAPartialSuccess()
        {
            Directory.CreateDirectory(directory);using var stop=new CancellationTokenSource();
            IEnumerable<string> Paths(){yield return "one.asset";stop.Cancel();yield return "two.asset";}
            var failure=Assert.Catch<OperationCanceledException>(()=>Scan(Paths,cancel:stop.Token));Assert.That(failure.CancellationToken,Is.EqualTo(stop.Token));Assert.That(Directory.GetFiles(directory),Is.Empty);
        }
        [TestCase(false)][TestCase(true)]public void UnexpectedEnumeratorOrInspectorFaultIsNotHiddenAsDamagedData(bool enumerate)
        {
            Directory.CreateDirectory(directory);var failure=new InvalidOperationException("Programmer fault");
            IEnumerable<string> Paths(){if(enumerate)throw failure;yield return "one.asset";}
            Assert.That(Assert.Throws<InvalidOperationException>(()=>Scan(Paths,(_,__,___)=>throw failure)),Is.SameAs(failure));
        }
    }
}

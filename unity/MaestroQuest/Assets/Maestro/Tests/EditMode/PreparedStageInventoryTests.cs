// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class PreparedStageInventoryTests
    {
        string directory,primary;
        string Stage(string value="unfinished"){
            string path=primary+".prepare."+Guid.NewGuid().ToString("N");
            File.WriteAllText(path,value);return path;
        }
        string[] Stages()=>Directory.GetFileSystemEntries(directory,Path.GetFileName(primary)+".prepare.*");
        [SetUp] public void Before(){
            directory=Path.Combine(Path.GetTempPath(),"MaestroStageInventory-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);primary=Path.Combine(directory,RoomStorage.FileName);
            File.WriteAllText(primary,"accepted");File.WriteAllText(primary+".backup","backup");File.WriteAllText(primary+".unreadable","evidence");
        }
        [TearDown] public void After(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        void Originals(){Assert.That(File.ReadAllText(primary),Is.EqualTo("accepted"));Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo("backup"));Assert.That(File.ReadAllText(primary+".unreadable"),Is.EqualTo("evidence"));}
        [Test] public void AbandonedPreparationsCountAgainstAdmissionWithoutDeletion(){
            for(int i=0;i<PreparedStageInventory.MaximumStages;i++)Stage("orphan "+i);
            var before=Stages().ToDictionary(x=>x,File.ReadAllText);
            var failure=Assert.Throws<IOException>(()=>PreparedFileStage.Create(primary,Encoding.UTF8.GetBytes("next"),default));
            Assert.That(failure.Message,Is.EqualTo(PreparedStageInventory.CapacityError));Assert.That(Stages().Length,Is.EqualTo(before.Count));
            foreach(var pair in before)Assert.That(File.ReadAllText(pair.Key),Is.EqualTo(pair.Value));Originals();
        }
        [Test] public async Task ConcurrentStoresCannotOverAdmitStages(){
            var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(()=>{
                try{return PreparedFileStage.Create(primary,new byte[32768],default);}catch(IOException){return null;}
            })));
            try{Assert.That(results.Count(x=>x!=null),Is.EqualTo(PreparedStageInventory.MaximumStages));Assert.That(Stages().Length,Is.EqualTo(PreparedStageInventory.MaximumStages));Originals();}
            finally{foreach(var result in results)result?.Dispose();}
            Assert.That(Stages(),Is.Empty);
            using var next=PreparedFileStage.Create(primary,new byte[32],default);
        }
        [Test] public void OversizedOrUnsafeStagesBlockNewWritesAndStayIneligible(){
            string path=Stage();using(var output=new FileStream(path,FileMode.Open,FileAccess.Write))output.SetLength(PreparedStageInventory.MaximumStageBytes+1);
            Assert.Throws<IOException>(()=>PreparedFileStage.Create(primary,new byte[16],default));
            var review=PreparedStageInventory.Inspect(primary);Assert.That(review.Entries.Single().Eligible,Is.False);
            File.Delete(path);Directory.CreateDirectory(path);
            Assert.Throws<IOException>(()=>PreparedFileStage.Create(primary,new byte[16],default));
            Assert.That(PreparedStageInventory.Inspect(primary).Entries.Single().Eligible,Is.False);Assert.That(Directory.Exists(path),Is.True);Originals();
        }
        [Test] public void CandidateBytesAreBoundedBeforeCreatingAnything(){
            Assert.Throws<ArgumentException>(()=>PreparedFileStage.Create(primary,new byte[PreparedStageInventory.MaximumStageBytes+1],default));
            Assert.Throws<ArgumentException>(()=>PreparedFileStage.Create(primary,Array.Empty<byte>(),default));
            Assert.That(Stages(),Is.Empty);Originals();
        }
        [Test] public void LivePinnedStagesAreVisibleButCannotBeDiscarded(){
            using var active=PreparedFileStage.Create(primary,Encoding.UTF8.GetBytes("active"),default);
            var review=PreparedStageInventory.Inspect(primary);var entry=review.Entries.Single();
            Assert.That(entry.Eligible,Is.False);Assert.That(entry.Hash,Is.Null);
            Assert.Throws<InvalidOperationException>(()=>review.Discard(review.Id,entry.Id,"","discard "+entry.Id));
            Assert.That(Stages().Length,Is.EqualTo(1));Originals();
        }
        [Test] public void ExplicitDisposalOnlyRemovesTheExactReviewedPreparationOnce(){
            string first=Stage("first"),second=Stage("second");
            var review=PreparedStageInventory.Inspect(primary);var entry=review.Entries.Single(x=>x.Name==Path.GetFileName(first));
            Assert.Throws<InvalidOperationException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"yes"));
            Assert.Throws<InvalidOperationException>(()=>review.Discard(Guid.NewGuid().ToString("N"),entry.Id,entry.Hash,"discard "+entry.Id));
            Assert.Throws<InvalidOperationException>(()=>review.Discard(review.Id,entry.Id,new string('0',64),"discard "+entry.Id));
            review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id);
            Assert.That(File.Exists(first),Is.False);Assert.That(File.ReadAllText(second),Is.EqualTo("second"));Originals();
            File.WriteAllText(first,"new owner");
            Assert.Throws<InvalidOperationException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id));
            Assert.That(File.ReadAllText(first),Is.EqualTo("new owner"));
        }
        [Test] public void SameMetadataSubstitutionIsPreservedAndConsumesTheReview(){
            string path=Stage("original");var review=PreparedStageInventory.Inspect(primary);var entry=review.Entries.Single();
            var stamp=File.GetLastWriteTimeUtc(path);File.WriteAllText(path,"replaced");File.SetLastWriteTimeUtc(path,stamp);
            Assert.Throws<IOException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id));
            Assert.That(File.ReadAllText(path),Is.EqualTo("replaced"));Originals();
            Assert.Throws<InvalidOperationException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id));
        }
        [Test] public void ANewOwnerAfterInspectionPreventsDisposal(){
            string path=Stage();var review=PreparedStageInventory.Inspect(primary);var entry=review.Entries.Single();
            using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.Throws<IOException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id));
            Assert.That(File.ReadAllText(path),Is.EqualTo("unfinished"));Originals();
        }
        [Test] public void CancelledDisposalDoesNotDeleteAndCannotReplay(){
            string path=Stage();var review=PreparedStageInventory.Inspect(primary);var entry=review.Entries.Single();
            using var cancellation=new CancellationTokenSource();cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id,cancellation.Token));
            Assert.That(File.Exists(path),Is.True);Assert.Throws<InvalidOperationException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id));Originals();
        }
        [Test] public void InspectionIsBoundedAndReportsThatMoreEntriesRemain(){
            for(int i=0;i<PreparedStageInventory.MaximumEntries+2;i++)Stage();
            var review=PreparedStageInventory.Inspect(primary);Assert.That(review.Entries.Length,Is.EqualTo(PreparedStageInventory.MaximumEntries));Assert.That(review.More,Is.True);
            Assert.That(Stages().Length,Is.EqualTo(PreparedStageInventory.MaximumEntries+2));Originals();
        }
        [Test] public async Task HashingAnInactivePreparationDoesNotHoldTheRoomCoordinator(){
            Stage();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            var work=Task.Run(()=>PreparedStageInventory.Inspect(primary,default,phase=>{entered.Set();if(!release.Wait(5000))throw new TimeoutException();}));
            try {
                Assert.That(entered.Wait(5000),Is.True);
                using var writer=RoomSnapshotTransaction.Enter(directory,wait:false);
                Assert.That(work.IsCompleted,Is.False);
            } finally {release.Set();}
            Assert.That((await work).Entries.Single().Eligible,Is.True);Originals();
        }
        [TestCase("verified")] [TestCase("claimed")]
        public void ASubstitutionDuringDisposalIsRetainedAsEvidence(string phase){
            string path=Stage("original");var review=PreparedStageInventory.Inspect(primary);var entry=review.Entries.Single();
            Assert.Throws<IOException>(()=>review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id,default,point=>{
                if(point==phase){string target=phase=="verified"?path:Stages().Single();File.WriteAllText(target,"replaced");}
            }));
            Assert.That(Stages().Length,Is.EqualTo(1));Assert.That(File.ReadAllText(Stages().Single()),Is.EqualTo("replaced"));Originals();
        }
        [Test] public void ExplicitDisposalFreesCapacityWithoutChangingTheAcceptedRoom(){
            for(int i=0;i<PreparedStageInventory.MaximumStages;i++)Stage();
            var review=PreparedStageInventory.Inspect(primary);var entry=review.Entries.First();
            review.Discard(review.Id,entry.Id,entry.Hash,"discard "+entry.Id);
            using var next=PreparedFileStage.Create(primary,new byte[64],default);
            Assert.That(Stages().Length,Is.EqualTo(PreparedStageInventory.MaximumStages));Originals();
        }
    }
}

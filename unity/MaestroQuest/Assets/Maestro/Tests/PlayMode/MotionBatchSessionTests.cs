// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        sealed class SelectedMotionSource:IMotionBatchSource
        {
            public System.Exception ReadError;public string DisplayName;public byte[][] Bytes;public int[] Reads;public bool Disposed,Reading;public int HoldIndex=-1;
            public TaskCompletionSource<bool> Gate;
            public int Count=>Bytes.Length;
            public SelectedMotionSource(params byte[][] bytes){Bytes=bytes;Reads=new int[bytes.Length];}
            public string Name(int index)=>DisplayName??("Motion "+index+".glb");
            public async Task<MotionBatchInput> ReadAsync(int index,CancellationToken token){Assert.That(Disposed,Is.False);Reads[index]++;if(ReadError!=null)throw ReadError;if(index==HoldIndex){Reading=true;await Gate.Task;}token.ThrowIfCancellationRequested();return new MotionBatchInput(Name(index),Bytes[index]);}
            public void Dispose()=>Disposed=true;
        }
        sealed class BatchChoice:IMotionBatchPicker
        {
            public string Id;public int Starts,Releases;public AndroidMotionBatchSource.Selection Result;public SelectedMotionSource Files;
            public bool ReadyToStart=>Id==null;
            public void Start(string id){Assert.That(Id,Is.Null);Id=id;Starts++;}
            public AndroidMotionBatchSource.Selection Read(string id)=>Result;
            public IMotionBatchSource Source(int count,string id){Assert.That(id,Is.EqualTo(Id));Assert.That(count,Is.EqualTo(Files.Count));return Files;}
            public void Choose(SelectedMotionSource files){Files=files;Result=new AndroidMotionBatchSource.Selection {session=Id,kind="ready",count=files.Count};}
            public void Release(string id){if(id!=Id)return;Id=null;Result=null;Releases++;}
        }
        BatchChoice batchChoice;ImportBatchWorkshop batches;
        void BatchRuntime(){ImportRuntime();batches=imports.Batches;batchChoice=new BatchChoice();batches.SetPickerForTests(batchChoice);}
        JObject BatchFact()=>Fact("motion.import.batch.status",null);
        JObject BatchFile(int index)=>Fact("motion.import.batch.file",new JObject {["requestId"]=batches.SessionId,["index"]=index,["motionOffset"]=0});
        JObject BatchArgs(string operation){var args=new JObject {["operation"]=operation};if(operation!="select")args["requestId"]=batches.SessionId;if(operation is "start" or "retry" or "category")args["version"]=BatchFact()["version"].DeepClone();return args;}
        JObject BatchRequest(JObject args)=>new() {["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="motion.import.batch",["version"]=1,["arguments"]=args}};
        IEnumerator BatchAction(JObject args){Assert.That(authorActions.Execute(BatchRequest(args),out var error),Is.True,error);yield return ModelFinished();}
        IEnumerator BatchPhase(string phase){float deadline=Time.realtimeSinceStartup+20;while((string)BatchFact()["phase"]!=phase&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That((string)BatchFact()["phase"],Is.EqualTo(phase),BatchFact().ToString());}
        [UnityTest] public IEnumerator SharedBatchReceiptsImportRealFilesKeepExactIdsAndRetryOnlyFailures()
        {
            BatchRuntime();var before=BatchFact();var request=BatchRequest(BatchArgs("select"));Assert.That(authorActions.Execute(request,out var error),Is.True,error);yield return ModelFinished();var select=authorActions.Observe().DeepClone();var choosing=BatchFact();
            Assert.That(authorActions.Execute(request,out error),Is.True,error);Assert.That(batchChoice.Starts,Is.EqualTo(1));Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);
            var source=new SelectedMotionSource(ModelFixture.Mixamo(),new byte[8],ModelFixture.Mixamo());batchChoice.Choose(source);yield return BatchPhase("ready");var ready=BatchFact();Assert.That(source.Reads.Sum(),Is.Zero);
            var tag=BatchArgs("category");tag["category"]="gesture";yield return BatchAction(tag);var categoryReceipt=authorActions.Observe().DeepClone();var tagged=BatchFact();
            yield return BatchAction(BatchArgs("start"));var started=authorActions.Observe().DeepClone();yield return BatchPhase("partial");var after=BatchFact();var file=BatchFile(0);var failed=BatchFile(1);
            Assert.That((int)after["counts"]["saved"],Is.EqualTo(2));Assert.That((int)after["counts"]["failed"],Is.EqualTo(1));Assert.That(source.Reads,Is.EqualTo(new[]{1,1,1}));
            Assert.That(JToken.DeepEquals(file["motionIds"],BatchFile(2)["motionIds"]),Is.True);string id=(string)file["motionIds"][0];Assert.That(editor.Motions.Inspect(id).tags,Contains.Item("gesture"));
            Assert.That(editor.Motions.ResidentClipCount,Is.Zero);Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);
            Assert.That(authorActions.Execute(BatchRequest(tag),out _),Is.False);Assert.That(authorActions.Execute(ImportRequest(new JObject {["operation"]="select"}),out _),Is.False);
            string capture=Environment.GetEnvironmentVariable("MAESTRO_BATCH_IMPORT");if(!string.IsNullOrEmpty(capture)){Directory.CreateDirectory(capture);File.WriteAllText(Path.Combine(capture,"batch.json"),new JObject {["before"]=before,["select"]=select,["choosing"]=choosing,["ready"]=ready,["category"]=categoryReceipt,["tagged"]=tagged,["start"]=started,["after"]=after,["file"]=file,["failedFile"]=failed}.ToString());}
            source.Bytes[1]=ModelFixture.TranslationMotion("STEP");yield return BatchAction(BatchArgs("retry"));yield return BatchPhase("completed");Assert.That(source.Reads,Is.EqualTo(new[]{1,2,1}));Assert.That((string)BatchFile(0)["motionIds"][0],Is.EqualTo(id));
            yield return BatchAction(BatchArgs("clear"));Assert.That(source.Disposed,Is.True);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);Assert.That(editor.Motions.List().Length,Is.EqualTo(2));Assert.That(batchChoice.Releases,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator BatchChooserSurvivesPauseAndOldRequestsCannotAffectReplacement()
        {
            BatchRuntime();yield return BatchAction(BatchArgs("select"));string old=batches.SessionId;batches.SendMessage("OnApplicationPause",true);batches.SendMessage("OnApplicationFocus",false);
            batchChoice.Choose(new SelectedMotionSource(ModelFixture.Create()));yield return new WaitForSecondsRealtime(.15f);Assert.That((string)BatchFact()["phase"],Is.EqualTo("selecting"));
            batches.SendMessage("OnApplicationPause",false);batches.SendMessage("OnApplicationFocus",true);yield return BatchPhase("ready");Assert.That(batchChoice.Files.Reads[0],Is.Zero);
            yield return BatchAction(BatchArgs("clear"));yield return BatchAction(BatchArgs("select"));Assert.That(batches.SessionId,Is.Not.EqualTo(old));
            Assert.That(authorActions.Execute(BatchRequest(new JObject {["operation"]="stop",["requestId"]=old}),out _),Is.False);yield return BatchAction(BatchArgs("stop"));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
        }
        [UnityTest] public IEnumerator BatchStopDrainsCurrentReadAndExplicitResumePreservesCompletedFiles()
        {
            BatchRuntime();var source=new SelectedMotionSource(ModelFixture.Mixamo(),ModelFixture.TranslationMotion("STEP")){HoldIndex=1,Gate=new TaskCompletionSource<bool>()};
            Assert.That(batches.Prepare(source),Is.True);yield return BatchAction(BatchArgs("start"));yield return new WaitUntil(()=>source.Reading);
            string id=(string)BatchFile(0)["motionIds"][0];yield return BatchAction(BatchArgs("stop"));Assert.That((string)BatchFact()["phase"],Is.EqualTo("stopping"));Assert.That(source.Disposed,Is.False);
            Assert.That(authorActions.Execute(BatchRequest(BatchArgs("clear")),out _),Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);
            source.Gate.SetResult(true);yield return BatchPhase("partial");Assert.That((int)BatchFact()["counts"]["waiting"],Is.EqualTo(1));yield return null;Assert.That(source.Reads,Is.EqualTo(new[]{1,1}));
            yield return BatchAction(BatchArgs("start"));yield return BatchPhase("completed");Assert.That(source.Reads,Is.EqualTo(new[]{1,2}));Assert.That((string)BatchFile(0)["motionIds"][0],Is.EqualTo(id));batches.Clear();
        }
        [UnityTest] public IEnumerator DisabledBatchRetainsWriteAndSourceUntilUncooperativeReadDrains()
        {
            BatchRuntime();var source=new SelectedMotionSource(ModelFixture.Mixamo()){HoldIndex=0,Gate=new TaskCompletionSource<bool>()};Assert.That(batches.Prepare(source),Is.True);
            var run=batches.SaveAsync();yield return new WaitUntil(()=>source.Reading);batches.enabled=false;Assert.That(source.Disposed,Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);
            source.Gate.SetResult(true);yield return new WaitUntil(()=>run.IsCompleted);Assert.That(run.Exception,Is.Null);Assert.That(source.Disposed,Is.True);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);Assert.That(editor.Motions.List().Length,Is.Zero);
        }
        [UnityTest] public IEnumerator BatchManualControlsShareVersionsAndDoNotInterruptOtherActors()
        {
            BatchRuntime();workshop.TogglePose();Assert.That(authorActions.Execute(BatchRequest(BatchArgs("select")),out _),Is.False);Assert.That(workshop.IsPosing,Is.True);workshop.Stop();
            Assert.That(batches.Prepare(new SelectedMotionSource(ModelFixture.Mixamo())),Is.True);var stale=BatchArgs("start");batches.NextCategory();Assert.That(authorActions.Execute(BatchRequest(stale),out _),Is.False);
            Assert.That(editor.Ownership.TryAcquire("other","Other actor",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},_=>{},out var lease,out var error),Is.True,error);
            var run=batches.SaveAsync();yield return new WaitUntil(()=>run.IsCompleted);Assert.That(run.Exception,Is.Null);Assert.That(lease.Held,Is.True);lease.Dispose();Assert.That((string)BatchFact()["phase"],Is.EqualTo("completed"));
            batches.NextCategory();Assert.That(batches.Batch.Category,Is.EqualTo("idle"));batches.Clear();
        }
        static byte[] DistinctMotionExport()
        {
            byte[] original=ModelFixture.Create();int jsonLength=BitConverter.ToInt32(original,12);var data=JObject.Parse(System.Text.Encoding.UTF8.GetString(original,20,jsonLength));
            using var binary=new MemoryStream();using var writer=new BinaryWriter(binary);writer.Write(original,28+jsonLength,(int)data["buffers"][0]["byteLength"]);
            var template=data["animations"][0];var animations=new JArray();var views=(JArray)data["bufferViews"];var accessors=(JArray)data["accessors"];
            for(int i=0;i<32;i++){int offset=(int)binary.Position;var rotation=Quaternion.AngleAxis(i+1,Vector3.forward);foreach(float v in new[]{0f,0f,0f,1f,rotation.x,rotation.y,rotation.z,rotation.w})writer.Write(v);
                views.Add(new JObject {["buffer"]=0,["byteOffset"]=offset,["byteLength"]=32});accessors.Add(new JObject {["bufferView"]=views.Count-1,["componentType"]=5126,["count"]=2,["type"]="VEC4"});
                var animation=(JObject)template.DeepClone();animation["name"]="Motion "+i;animation["samplers"][0]["output"]=accessors.Count-1;animations.Add(animation);}
            data["animations"]=animations;data["buffers"][0]["byteLength"]=binary.Length;return ModelFixture.Pack(data,binary.ToArray());
        }
        [UnityTest] public IEnumerator FullClipExportReturnsEveryExactIdThroughBoundedFactPages()
        {
            BatchRuntime();var bytes=DistinctMotionExport();
            Assert.That(batches.Prepare(new SelectedMotionSource(bytes)),Is.True);yield return BatchAction(BatchArgs("start"));yield return BatchPhase("completed");var ids=new System.Collections.Generic.List<string>();
            for(int offset=0;offset<32;offset+=8){var page=Fact("motion.import.batch.file",new JObject {["requestId"]=batches.SessionId,["index"]=0,["motionOffset"]=offset});Assert.That((int)page["motionCount"],Is.EqualTo(32));Assert.That((int)page["motionOffset"],Is.EqualTo(offset));Assert.That(((JArray)page["motionIds"]).Count,Is.EqualTo(8));ids.AddRange(page["motionIds"].Values<string>());}
            Assert.That(ids.Distinct().Count(),Is.EqualTo(32));Assert.That(ids.All(id=>editor.Motions.Inspect(id)!=null),Is.True);batches.Clear();
        }
        [UnityTest] public IEnumerator ArchivePreparationSurvivesPauseAndAllMemberIndicesAreInspectable()
        {
            BatchRuntime();yield return BatchAction(BatchArgs("select"));batchChoice.Result=new AndroidMotionBatchSource.Selection {session=batches.SessionId,kind="preparing"};
            yield return new WaitForSecondsRealtime(.15f);Assert.That((string)BatchFact()["phase"],Is.EqualTo("selecting"));Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);
            batches.SendMessage("OnApplicationPause",true);var source=new SelectedMotionSource(new byte[MotionBatch.MaximumFiles][]);batchChoice.Choose(source);
            yield return new WaitForSecondsRealtime(.15f);Assert.That((string)BatchFact()["phase"],Is.EqualTo("selecting"));batches.SendMessage("OnApplicationPause",false);yield return BatchPhase("ready");
            Assert.That((int)BatchFact()["counts"]["files"],Is.EqualTo(1024));Assert.That((string)BatchFile(1023)["name"],Is.EqualTo("Motion 1023.glb"));Assert.That((string)BatchFile(1023)["state"],Is.EqualTo("pending"));Assert.That(source.Reads.Sum(),Is.Zero);
            yield return BatchAction(BatchArgs("clear"));Assert.That(source.Disposed,Is.True);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
        }
        [UnityTest] public IEnumerator ForgedBatchChoiceFailsWithoutOpeningSourcesAndFreesRoomBoundaries()
        {
            BatchRuntime();yield return BatchAction(BatchArgs("select"));batchChoice.Result=new AndroidMotionBatchSource.Selection {session=new string('f',32),kind="ready",count=1};yield return BatchPhase("failed");
            Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);Assert.That(batchChoice.Releases,Is.EqualTo(1));yield return BatchAction(BatchArgs("select"));batchChoice.Result=new AndroidMotionBatchSource.Selection {session=batches.SessionId,kind="ready",count=MotionBatch.MaximumFiles+1};yield return BatchPhase("failed");Assert.That(batchChoice.Releases,Is.EqualTo(2));
        }
    }
}

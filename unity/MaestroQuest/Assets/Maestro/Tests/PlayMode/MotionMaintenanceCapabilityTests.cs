// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed partial class LibraryRuleAndWalkTests
    {
        JObject MotionArgs(string id,string operation)=>new(){["operation"]=operation,["motionId"]=id,["entryRevision"]=editor.Motions.EntryRevision(id)};
        JObject MotionCall(JObject arguments,string action="motion.library.edit")=>new(){["operation"]="start",["runId"]=new RoomExecutions(editor).Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]=action,["version"]=1,["arguments"]=arguments}};
        IEnumerator MotionFinished(string id){yield return Until(()=>{var phase=(string)runtime.Scheduler.Invocation(id)?["phase"];return phase!=null&&phase is not ("preparing" or "running");});}
        JObject MotionEntryFact(string id){Assert.That(runtime.TryReadFact("motion.library.entry",1,new JObject {["motionId"]=id},out var result),Is.True);return (JObject)result.Value;}
        static IEnumerator MaintenanceUntil(string phase,Func<bool> complete,Func<string> details)
        {
            TestContext.WriteLine("Motion maintenance wait: "+phase);
            float deadline=Time.realtimeSinceStartup+10;
            while(!complete()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(complete(),Is.True,phase+" did not finish: "+details());
        }
        [UnityTest]public IEnumerator BookAndAgentMetadataShareGuardsImmutableReceiptsAndPreservedAssignments()
        {
            var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,authoring);var book=root.AddComponent<LibraryBookController>();book.Initialize(editor,imports,rules);book.SetVisible(true);
            var selected=book.HandleAsync(new LibraryBookRequest {version=1,session=book.State.session,sequence=1,action="select",motionId=greeting.id});yield return Until(()=>selected.IsCompleted);
            var reviewed=book.State.selected;var args=MotionArgs(greeting.id,"save");args["name"]="Agent renamed";args["tags"]=new JArray("calm","shared");args["favourite"]=true;
            var executions=new RoomExecutions(editor);var call=MotionCall(args);Assert.That(executions.Execute(call,out var error),Is.True,error);string run=(string)call["runId"];yield return MotionFinished(run);
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((string)receipt["call"]["arguments"]["name"],Is.EqualTo("Agent renamed"));Assert.That(editor.Motions.Inspect(greeting.id).hash,Is.EqualTo(greeting.hash));Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(greeting.id));Assert.That(avatar.IsImportedClipPlaying,Is.False);
            var stale=book.HandleAsync(new LibraryBookRequest {version=1,session=book.State.session,sequence=2,action="save",motionId=greeting.id,entryRevision=reviewed.entryRevision,name="Stale user draft",tags=Array.Empty<string>()});yield return Until(()=>stale.IsCompleted);Assert.That(book.State.status,Does.Contain("changed"));Assert.That(editor.Motions.Inspect(greeting.id).name,Is.EqualTo("Agent renamed"));
            var fresh=MotionEntryFact(greeting.id);Assert.That((string)fresh["entryRevision"],Is.EqualTo(book.State.selected.entryRevision));
            var archive=book.HandleAsync(new LibraryBookRequest {version=1,session=book.State.session,sequence=3,action="archive",motionId=greeting.id,entryRevision=(string)fresh["entryRevision"]});yield return Until(()=>archive.IsCompleted);Assert.That(editor.Motions.Inspect(greeting.id).archived,Is.True,book.State.status);
            var manualReceipt=runtime.Scheduler.Invocation(book.State.maintenanceRunId);Assert.That((string)manualReceipt["call"]["id"],Is.EqualTo("motion.library.edit"));Assert.That((string)manualReceipt["phase"],Is.EqualTo("completed"));Assert.That((string)manualReceipt["output"]["entry"]["motionId"],Is.EqualTo(greeting.id));
            Assert.That(executions.Execute(call,out error),Is.True,error);Assert.That(editor.Motions.Inspect(greeting.id).archived,Is.True,"Replaying a completed save cannot overwrite a newer archive");Assert.That((bool)runtime.Scheduler.Invocation(run)["output"]["entry"]["archived"],Is.False);
            Assert.That(runtime.TryReadFact("motion.library.search",1,new JObject {["query"]="Agent renamed",["archived"]=true,["offset"]=0},out var search),Is.True);Assert.That((string)((JObject)search.Value)["entries"][0]["motionId"],Is.EqualTo(greeting.id));
        }
        [UnityTest]public IEnumerator SharedInspectionProtectsAssignedMotionAndUnusedRemovalForgetsOnlyAfterSave()
        {
            var actions=new RoomExecutions(editor);var inspectArgs=MotionArgs(greeting.id,"archive");inspectArgs.Remove("operation");var inspect=MotionCall(inspectArgs,"motion.library.inspect");Assert.That(actions.Execute(inspect,out var error),Is.True,error);yield return MotionFinished((string)inspect["runId"]);
            var view=runtime.Scheduler.Invocation((string)inspect["runId"]);Assert.That((string)view["phase"],Is.EqualTo("completed"),view.ToString());Assert.That((int)view["output"]["usage"]["total"],Is.GreaterThan(0));Assert.That((string)view["output"]["usage"]["protection"],Is.Not.Empty);
            var archive=MotionCall(MotionArgs(greeting.id,"archive"));Assert.That(actions.Execute(archive,out error),Is.True,error);yield return MotionFinished((string)archive["runId"]);
            var protect=MotionCall(MotionArgs(greeting.id,"removeDownload"));Assert.That(actions.Execute(protect,out error),Is.True,error);yield return MotionFinished((string)protect["runId"]);Assert.That((string)runtime.Scheduler.Invocation((string)protect["runId"])["phase"],Is.EqualTo("failed"));Assert.That(editor.Motions.Downloaded(greeting.id),Is.True);
            var add=editor.Motions.ImportAsync("unused.glb",Clip(8,"Unused for maintenance"));yield return Until(()=>add.IsCompleted);Assert.That(add.Exception,Is.Null);string id=add.Result.Single().id;
            foreach(string operation in new[]{"archive","removeDownload","forgetMotion"}){
                var call=MotionCall(MotionArgs(id,operation));Assert.That(actions.Execute(call,out error),Is.True,error);yield return MotionFinished((string)call["runId"]);var result=runtime.Scheduler.Invocation((string)call["runId"]);Assert.That((string)result["phase"],Is.EqualTo("completed"),result.ToString());
                if(operation=="removeDownload"){Assert.That(editor.Motions.PayloadPresent(id),Is.False);Assert.That((bool)result["output"]["entry"]["removed"],Is.True);}
                if(operation=="forgetMotion"){Assert.That(editor.Motions.Inspect(id),Is.Null);Assert.That((bool)result["output"]["entry"]["exists"],Is.False);Assert.That((string)result["output"]["entry"]["entryRevision"],Is.Empty);}
            }
            Assert.That(editor.Motions.Downloaded(greeting.id),Is.True);Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(greeting.id));
        }
        [UnityTest]public IEnumerator DestructivePublicationBlocksNewReferencesAndBookStopReportsItsDrainingWrite()
        {
            var add=editor.Motions.ImportAsync("unused.glb",Clip(8,"Unused guarded removal"));yield return MaintenanceUntil("fixture import",()=>add.IsCompleted,()=>add.Status+" "+add.Exception);Assert.That(add.Exception,Is.Null);string id=add.Result.Single().id;
            var archive=editor.Motions.ArchiveAsync(id,true);yield return MaintenanceUntil("fixture archive",()=>archive.IsCompleted,()=>archive.Status+" "+archive.Exception);Assert.That(archive.Exception,Is.Null);
            yield return MaintenanceUntil("reference stores ready",()=>rules.Memory.Ready&&!rules.Memory.Pending&&rules.Modules.Ready&&!rules.Modules.Pending,
                ()=>"memory ready="+rules.Memory.Ready+", pending="+rules.Memory.Pending+", error="+rules.Memory.Error+"; modules ready="+rules.Modules.Ready+", pending="+rules.Modules.Pending+", error="+rules.Modules.Error);
            var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,authoring);var book=root.AddComponent<LibraryBookController>();book.Initialize(editor,imports,rules);book.SetVisible(true);
            string Details()=>"book status="+book.State.status+", busy="+book.State.busy+", usagePending="+book.UsagePending+", frozen="+editor.WriteGate.Frozen+"; receipt="+(book.State.maintenanceRunId==null?"not accepted":runtime.Scheduler.Invocation(book.State.maintenanceRunId)?.ToString(Newtonsoft.Json.Formatting.None));
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();editor.Motions.MaintenanceCheckpoint=phase=>{if(phase=="publishing"){entered.Set();if(!release.Wait(10000))throw new TimeoutException("Test did not release the publication checkpoint");}};
            using var auditEntered=new ManualResetEventSlim();using var auditRelease=new ManualResetEventSlim();using var waiting=new ManualResetEventSlim();
            var audit=Task.Run(()=>{using var owner=RoomSnapshotTransaction.Inspect(directory);auditEntered.Set();if(!auditRelease.Wait(10000))throw new TimeoutException("Test did not release the retained-file reader");});
            RoomSnapshotTransaction.InspectionWaiting=path=>{if(path==directory)waiting.Set();};Task remove=null;
            try {
                yield return MaintenanceUntil("existing snapshot reader entered",()=>auditEntered.IsSet,()=>audit.Status+" "+audit.Exception);
                remove=book.HandleAsync(new LibraryBookRequest {version=1,session=book.State.session,sequence=1,action="removeDownload",motionId=id,entryRevision=editor.Motions.EntryRevision(id)});
                yield return MaintenanceUntil("authoritative read waits for existing inspection",()=>waiting.IsSet||remove.IsCompleted,Details);
                Assert.That(waiting.IsSet,Is.True,Details());Assert.That(remove.IsCompleted,Is.False,Details());Assert.That(entered.IsSet,Is.False,"Publication must follow retained-file inspection");
                auditRelease.Set();yield return MaintenanceUntil("existing inspection drains",()=>audit.IsCompleted,()=>audit.Status+" "+audit.Exception);Assert.That(audit.Exception,Is.Null);
                yield return MaintenanceUntil("book removal reaches publication",()=>entered.IsSet||remove.IsCompleted,Details);
                Assert.That(entered.IsSet,Is.True,"Book removal ended before publication: "+Details());Assert.That(editor.WriteGate.Frozen,Is.True);
                int revision=rules.Revision;rules.NewSequence();Assert.That(rules.Revision,Is.EqualTo(revision),"New program source must not bypass deletion's reference hold");
                Assert.That(rules.Modules.CanWrite(out _),Is.False,"A module containing a new motion ID must wait");
                var cells=new Dictionary<string,ProgramMemoryDocument.Cell>{{Guid.NewGuid().ToString("N"),new ProgramMemoryDocument.Cell("lateReference",new ProgramValue(id))}};
                Assert.Throws<InvalidOperationException>(()=>rules.Memory.Write(rules.Memory.Snapshot().Revision,Guid.NewGuid().ToString("N"),cells));
                var stop=book.HandleAsync(new LibraryBookRequest {version=1,session=book.State.session,sequence=2,action="stop"});yield return MaintenanceUntil("book stop acknowledges",()=>stop.IsCompleted,Details);Assert.That(book.State.status,Does.Contain("may still finish"));Assert.That(editor.WriteGate.Frozen,Is.True,"Stop must not release storage ownership while deletion drains");
                Assert.That((string)runtime.Scheduler.Invocation(book.State.maintenanceRunId)["phase"],Is.EqualTo("cancelled"));
            }finally{auditRelease.Set();release.Set();RoomSnapshotTransaction.InspectionWaiting=null;audit.GetAwaiter().GetResult();}
            yield return MaintenanceUntil("dispatched deletion drains workspace hold",()=>!editor.WriteGate.Frozen,Details);yield return MaintenanceUntil("original book request retires",()=>remove.IsCompleted,Details);editor.Motions.MaintenanceCheckpoint=null;
            Assert.That(editor.Motions.Inspect(id).removed,Is.True);Assert.That(editor.Motions.PayloadPresent(id),Is.False);Assert.That(book.State.status,Does.Contain("may still finish"));
            Assert.That(rules.Modules.CanWrite(out _),Is.True);int old=rules.Revision;rules.NewSequence();Assert.That(rules.Revision,Is.GreaterThan(old));
        }
        [UnityTest]public IEnumerator AgentStopWhileRetainedInspectionWaitsPreventsDeletionAndUnreadableEvidenceRemainsProtected()
        {
            var add=editor.Motions.ImportAsync("unused.glb",Clip(8,"Unused waiting removal"));yield return Until(()=>add.IsCompleted);Assert.That(add.Exception,Is.Null);string id=add.Result.Single().id;
            var archive=editor.Motions.ArchiveAsync(id,true);yield return Until(()=>archive.IsCompleted);Assert.That(archive.Exception,Is.Null);
            yield return Until(()=>rules.Memory.Ready&&!rules.Memory.Pending&&rules.Modules.Ready&&!rules.Modules.Pending);
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var waiting=new ManualResetEventSlim();
            var audit=Task.Run(()=>{using var owner=RoomSnapshotTransaction.Inspect(directory);entered.Set();if(!release.Wait(10000))throw new TimeoutException("Test did not release the retained-file reader");});
            RoomSnapshotTransaction.InspectionWaiting=path=>{if(path==directory)waiting.Set();};var actions=new RoomExecutions(editor);var call=MotionCall(MotionArgs(id,"removeDownload"));string run=(string)call["runId"];
            string Details()=>runtime.Scheduler.Invocation(run)?.ToString(Newtonsoft.Json.Formatting.None)??"No maintenance receipt";
            try {
                yield return MaintenanceUntil("existing inspection entered before agent action",()=>entered.IsSet,()=>audit.Status+" "+audit.Exception);
                Assert.That(actions.Execute(call,out var error),Is.True,error);
                yield return MaintenanceUntil("agent inspection waits for owner",()=>waiting.IsSet||(string)runtime.Scheduler.Invocation(run)?["phase"]=="failed",Details);Assert.That(waiting.IsSet,Is.True,Details());
                Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(runtime.Scheduler.CancelInvocation(run,out error),Is.True,error);
                yield return MaintenanceUntil("cancelled inspection releases its workspace hold",()=>!editor.WriteGate.Frozen,Details);
                Assert.That(audit.IsCompleted,Is.False,"Stop must cancel admission before the unrelated reader is released");
                Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));Assert.That(editor.Motions.Inspect(id).removed,Is.False);Assert.That(editor.Motions.PayloadPresent(id),Is.True);
            }finally{release.Set();RoomSnapshotTransaction.InspectionWaiting=null;audit.GetAwaiter().GetResult();}
            string evidence=Path.Combine(directory,RoomSnapshotTransaction.FileName);File.WriteAllText(evidence,"unreadable retained snapshot");
            try {
                var scan=Task.Run(()=>MotionUsage.ReadSaved(editor,rules,id,true));yield return MaintenanceUntil("unreadable retention inspection returns uncertainty",()=>scan.IsCompleted,()=>scan.Status+" "+scan.Exception);Assert.That(scan.Exception,Is.Null);Assert.That(scan.Result.Uncertain,Is.True);
                var refused=MotionCall(MotionArgs(id,"removeDownload"));if(actions.Execute(refused,out var error)){yield return MotionFinished((string)refused["runId"]);Assert.That((string)runtime.Scheduler.Invocation((string)refused["runId"])["phase"],Is.EqualTo("failed"));}else Assert.That(error,Is.Not.Empty);
                Assert.That(File.ReadAllText(evidence),Is.EqualTo("unreadable retained snapshot"));Assert.That(editor.Motions.Inspect(id).removed,Is.False);Assert.That(editor.Motions.PayloadPresent(id),Is.True);
            }finally{File.Delete(evidence);}
        }
        [UnityTest]public IEnumerator SchedulerMotionPagesKeepTheirDeclaredTypesForEmptyAndNonemptyResults()
        {
            const string name="Unique motion page fixture";
            JObject Read(string fact,JObject arguments){
                Assert.That(runtime.Scheduler.TryRead(fact,1,arguments,out var value),Is.True,fact);
                Assert.That(value.Type,Is.SameAs(BehaviourCatalog.Fact(fact).Type),"Empty and nonempty pages must use the fact's declared type: "+fact);
                return (JObject)value.Value;
            }
            JObject Tags(int offset)=>Read("motion.library.tags",new JObject {["motionId"]=greeting.id,["entryRevision"]=editor.Motions.EntryRevision(greeting.id),["offset"]=offset});
            JObject Search(string query,int offset=0,bool archived=false)=>Read("motion.library.search",new JObject {["query"]=query,["archived"]=archived,["offset"]=offset});
            void Empty(JObject page,string field,int total){Assert.That((JArray)page[field],Is.Empty);Assert.That((int)page["total"],Is.EqualTo(total));Assert.That((int)page["next"],Is.EqualTo(-1));}
            var save=editor.Motions.UpdateAsync(greeting.id,name,Array.Empty<string>(),false);yield return Until(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Empty(Tags(0),"tags",0);Empty(Tags(16),"tags",0);
            save=editor.Motions.UpdateAsync(greeting.id,name,new[]{"first","second","third"},false);yield return Until(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);string revision=editor.Motions.EntryRevision(greeting.id);
            var first=Tags(0);Assert.That(((JArray)first["tags"]).Values<string>(),Is.EqualTo(new[]{"first","second"}));Assert.That((int)first["total"],Is.EqualTo(3));Assert.That((int)first["next"],Is.EqualTo(2));Assert.That((string)first["entryRevision"],Is.EqualTo(revision));
            var last=Tags(2);Assert.That(((JArray)last["tags"]).Values<string>(),Is.EqualTo(new[]{"third"}));Assert.That((int)last["next"],Is.EqualTo(-1));Empty(Tags(3),"tags",3);Empty(Tags(16),"tags",3);
            var match=Search(name);Assert.That((int)match["total"],Is.EqualTo(1));Assert.That((int)match["next"],Is.EqualTo(-1));Assert.That((string)match["entries"][0]["motionId"],Is.EqualTo(greeting.id));Assert.That((string)match["entries"][0]["entryRevision"],Is.EqualTo(revision));
            Empty(Search("No matching motion "+Guid.NewGuid().ToString("N")),"entries",0);Empty(Search(name,1),"entries",1);Empty(Search(name,1024),"entries",1);
            var actions=new RoomExecutions(editor);var archive=MotionCall(MotionArgs(greeting.id,"archive"));Assert.That(actions.Execute(archive,out var error),Is.True,error);yield return MotionFinished((string)archive["runId"]);Assert.That((string)runtime.Scheduler.Invocation((string)archive["runId"])["phase"],Is.EqualTo("completed"));
            Empty(Search(name),"entries",0);var archived=Search(name,archived:true);Assert.That((int)archived["total"],Is.EqualTo(1));Assert.That((string)archived["entries"][0]["motionId"],Is.EqualTo(greeting.id));Assert.That((string)archived["entries"][0]["entryRevision"],Is.EqualTo(editor.Motions.EntryRevision(greeting.id)));
            Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(greeting.id));Assert.That(editor.Motions.PayloadPresent(greeting.id),Is.True);
        }
        [UnityTest]public IEnumerator MaximumEscapedMetadataRemainsExactThroughBoundedFactsAndProgramResults()
        {
            string name="A"+new string('\u2028',98)+"Z";var tags=Enumerable.Range(0,16).Select(i=>i.ToString("00")+new string('\u2028',29)+"z").ToArray();
            var save=editor.Motions.UpdateAsync(gait.id,name,tags,true);yield return Until(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var entry=MotionEntryFact(gait.id);Assert.That((int)entry["tagCount"],Is.EqualTo(16));Assert.That(ProgramValue.MeasureCharacters(entry),Is.LessThanOrEqualTo(ProgramValue.MaximumCharacters));
            Assert.That(runtime.TryReadFact("motion.library.name",1,new JObject {["motionId"]=gait.id},out var named),Is.True);Assert.That((string)((JObject)named.Value)["name"],Is.EqualTo(name));
            var observed=new List<string>();for(int offset=0;offset<16;offset+=2){Assert.That(runtime.TryReadFact("motion.library.tags",1,new JObject {["motionId"]=gait.id,["entryRevision"]=entry["entryRevision"].DeepClone(),["offset"]=offset},out var page),Is.True);observed.AddRange(((JArray)((JObject)page.Value)["tags"]).Values<string>());}
            Assert.That(observed,Is.EqualTo(tags));
            Assert.That(runtime.TryReadFact("motion.library.search",1,new JObject {["query"]="",["archived"]=false,["offset"]=0},out var search),Is.True);Assert.That(((JArray)((JObject)search.Value)["entries"]).Count,Is.EqualTo(1));
            var actions=new RoomExecutions(editor);var call=MotionCall(MotionArgs(gait.id,"archive"));Assert.That(actions.Execute(call,out var error),Is.True,error);yield return MotionFinished((string)call["runId"]);var receipt=runtime.Scheduler.Invocation((string)call["runId"]);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.DoesNotThrow(()=>ProgramValue.Literal(receipt["output"]["entry"]));
            Assert.That(runtime.TryReadFact("motion.library.tags",1,new JObject {["motionId"]=gait.id,["entryRevision"]=entry["entryRevision"].DeepClone(),["offset"]=0},out _),Is.False,"Metadata pages must not mix revisions");
        }
    }
}

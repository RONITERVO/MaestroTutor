// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject LeverSource()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-spring-lever.json")));
        JObject LeverCall()=>new(){["id"]="object.batch.create",["version"]=1,["arguments"]=LeverSource()["imports"][0]["module"]["program"]["functions"][1]["body"][0]["arguments"].DeepClone()};
        [UnityTest] public IEnumerator ConnectedBatchIsOneSavedUndoAndRetriesNeverCreateAnotherInstance()
        {
            var ex=new RoomAgentExecutor(editor);var request=TemplateRequest(LeverCall());int before=editor.Snapshot().objects.Length;
            Assert.That(ex.Execute(request,out var error,out _),Is.True,error);var ids=((JArray)ex.Executions.Observe()["selected"]["output"]["objectIds"]).Values<string>().ToArray();
            Assert.That(editor.Read(ids[1]).hinges.Single().connected,Is.EqualTo(ids[0]));Assert.That(physics.Running,Is.False);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==ids[1]).hinges[0].connected,Is.EqualTo(ids[0]));
            editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before));Assert.That(ex.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before));
            editor.Redo();Assert.That(editor.Read(ids[1]).hinges[0].connected,Is.EqualTo(ids[0]));Assert.That(editor.Find(ids[1]).GetComponent<RoomHingeView>(),Is.Not.Null);yield return null;
        }
        [UnityTest] public IEnumerator ConnectedCandidateFailureLeavesNoPartialBodiesOrReferences()
        {
            var ex=new RoomAgentExecutor(editor);string before=JsonUtility.ToJson(editor.Snapshot());var bad=LeverCall();bad["arguments"]["blueprint"]["pieces"][1]["position"]["z"]=.2;
            Assert.That(ex.Execute(TemplateRequest(bad),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(ex.Execute(TemplateRequest(LeverCall()),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));}finally{Directory.Delete(obstacle);}yield return null;
        }
        [UnityTest] public IEnumerator IncludedLeverProgramCreatesRealGrabbableBodiesAndSpringRespondsToAPhysicalPush()
        {
            var before=editor.Snapshot().objects.Select(x=>x.id).ToHashSet();var source=LeverSource();
            Assert.That(workshop.Execute(new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",reference="lever",sequence=new RuleSequence{id="",name="Build spring lever",program=source.ToString()}}}},out var error,out var sequenceIds),Is.True,error);
            Assert.That(runtime.Trigger(sequenceIds.Single()),Is.True,runtime.Scheduler.LastError);for(int i=0;i<30&&runtime.Scheduler.RunningCount>0;i++){runtime.Scheduler.Tick(Time.unscaledTime);yield return null;}
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);var created=editor.Snapshot().objects.Where(x=>!before.Contains(x.id)).ToArray();Assert.That(created.Length,Is.EqualTo(2));
            var data=created.Single(x=>x.hinges.Length==1);var item=editor.Find(data.id);var view=item.GetComponent<RoomHingeView>();var body=item.GetComponent<Rigidbody>();Assert.That(item.Grab,Is.Not.Null);
            physics.SetSurfaces(true,"Test surfaces aligned");physics.StartPhysics();view.Refresh();yield return new WaitForFixedUpdate();Assert.That(view.Active,Is.True,view.Error);
            float max=0;for(int i=0;i<30;i++){body.AddTorque(Vector3.right,ForceMode.Force);yield return new WaitForFixedUpdate();max=Mathf.Max(max,Mathf.Abs(view.Angle));}
            for(int i=0;i<80;i++)yield return new WaitForFixedUpdate();
            Assert.That(max,Is.GreaterThan(5).And.LessThan(54));Assert.That(Mathf.Abs(view.Angle),Is.LessThan(3));Assert.That(Vector3.Distance(item.transform.position,editor.Find(data.hinges[0].connected).transform.position),Is.LessThan(.015f));
        }
        [UnityTest] public IEnumerator ConnectedCreationInATemporaryRoomDiscardsEveryMember()
        {
            int count=editor.Snapshot().objects.Length;Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var ex=new RoomAgentExecutor(editor);Assert.That(ex.Execute(TemplateRequest(LeverCall()),out error,out _),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+2));Assert.That(new RoomStorage(directory).Load(out _).objects.Length,Is.EqualTo(count));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));yield return null;
        }
        [UnityTest] public IEnumerator ConnectedBatchesReserveTheWholeRoomHingeBudgetBeforeSaving()
        {
            CreationBatch Chain(int count){var args=(JObject)LeverCall()["arguments"];var p=args["blueprint"]["pieces"][1];var h=args["blueprint"]["hinges"][0];
                args["blueprint"]["pieces"]=new JArray(Enumerable.Range(0,count).Select(i=>{var c=p.DeepClone();c["slot"]="piece_"+i;return c;}));
                args["blueprint"]["hinges"]=new JArray(Enumerable.Range(0,count-1).Select(i=>{var c=h.DeepClone();c["owner"]="piece_"+i;c["connected"]="piece_"+(i+1);return c;}));return JsonUtility.FromJson<CreationBatch>(args.ToString());}
            Assert.That(editor.CreateBatch(Chain(16),out _,out var error),Is.True,error);Assert.That(editor.CreateBatch(Chain(2),out _,out error),Is.True,error);int before=editor.Snapshot().objects.Length;
            Assert.That(editor.CreateBatch(Chain(2),out _,out error),Is.False);Assert.That(error,Does.Contain("sixteen hinges"));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before));Assert.That(editor.Snapshot().objects.Sum(x=>x.hinges?.Length??0),Is.EqualTo(16));yield return null;
        }
    }
}

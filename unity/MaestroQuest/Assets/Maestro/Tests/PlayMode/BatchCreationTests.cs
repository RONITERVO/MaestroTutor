// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
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
        JObject BatchSource()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-batch-create.json")));
        JObject BatchCall()=>new() { ["id"]="object.batch.create",["version"]=1,["arguments"]=BatchSource()["functions"][0]["body"][0]["arguments"].DeepClone()};
        [UnityTest] public IEnumerator StructureCreationSavesIndependentEditableBodiesWithOneUndoAndNoReceiptReplay() {
            var call=BatchCall();var args=(JObject)call["arguments"];args["rotation"]=JObject.Parse(JsonUtility.ToJson(Quaternion.Euler(0,90,0)));args["scale"]=1.5f;
            int count=editor.Snapshot().objects.Length;var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(call);
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);var result=executor.Executions.Observe()["selected"]["output"];var ids=((JArray)result["objectIds"]).Values<string>().ToArray();Assert.That(ids.Length,Is.EqualTo(6));Assert.That(ids.Distinct().Count(),Is.EqualTo(6));
            var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.objects.Length,Is.EqualTo(count+6));
            for(int i=0;i<6;i++){var item=editor.Find(ids[i]);Assert.That(item.Grab,Is.Not.Null);Assert.That(item.GetComponent<Rigidbody>(),Is.Not.Null);Assert.That(editor.Read(ids[i]).recipe.parts.Length,Is.EqualTo(5));Assert.That((string)result["slots"][i],Is.EqualTo("brick_"+(i+1)));}
            var expected=new Vector3(.3f,1,.7f)+Quaternion.Euler(0,90,0)*new Vector3(.25f*1.5f,0,0);Assert.That(Vector3.Distance(editor.Find(ids[1]).transform.localPosition,expected),Is.LessThan(.0001f));
            editor.Undo();Assert.That(ids.All(id=>!editor.Find(id)),Is.True);Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            editor.Redo();Assert.That(ids.All(id=>editor.Find(id)),Is.True);Assert.That(editor.PaintObject(ids[0],Color.green,out error),Is.True,error);Assert.That(editor.Read(ids[1]).color,Is.EqualTo(Color.white));yield return null;
        }
        [UnityTest] public IEnumerator WholeCandidateCapacityAndFailedSaveNeverCreatePartialStructures() {
            var executor=new RoomAgentExecutor(editor);var call=BatchCall();var before=JsonUtility.ToJson(editor.Snapshot());
            var robot=CreationTemplates.All.First(e=>e.Id=="robot");var piece=call["arguments"]["blueprint"]["pieces"][0];call["arguments"]["blueprint"]["pieces"]=new JArray(Enumerable.Range(0,16).Select(i=>{var p=piece.DeepClone();p["slot"]="robot_"+i;p["source"]["templateHash"]=robot.Hash;return p;}));
            Assert.That(executor.Execute(TemplateRequest(call),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before),"The combined 304-part blueprint exceeds the room budget even though each robot fits");
            string obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(executor.Execute(TemplateRequest(BatchCall()),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));}finally{Directory.Delete(obstacle);}yield return null;
        }
        [UnityTest] public IEnumerator RealNativeProgramPaintsEveryReturnedPieceAndTemporaryDiscardRemovesWholeBatch() {
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);float end=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end)yield return null;Assert.That(editor.TemporarySavePending,Is.False);
            var before=editor.Snapshot().objects.Select(o=>o.id).ToHashSet();var source=BatchSource();
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="structure",sequence=new RuleSequence {id="",name="Build and paint castle",program=source.ToString()}}}},out error,out var sequenceIds),Is.True,error);
            Assert.That(runtime.Trigger(sequenceIds.Single()),Is.True,runtime.Scheduler.LastError);for(int i=0;i<30&&runtime.Scheduler.RunningCount>0;i++){runtime.Scheduler.Tick(Time.unscaledTime);yield return null;}
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);var pieces=editor.Snapshot().objects.Where(o=>!before.Contains(o.id)).ToArray();Assert.That(pieces.Length,Is.EqualTo(6));Assert.That(pieces.All(p=>p.color==new Color(.2f,.6f,.9f,1)),Is.True);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Length,Is.EqualTo(before.Count));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before.Count));
        }
    }
}

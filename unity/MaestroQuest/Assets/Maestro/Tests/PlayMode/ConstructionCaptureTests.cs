// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator CapturePublishesOnceAndItsConstructorSurvivesDeletingOriginals() {
            for(int i=0;i<120&&!workshop.Modules.Ready;i++)yield return null;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"My ball",new Vector3(.3f,1,.7f),1,Color.red,out var original,out var error),Is.True,error);
            int count=editor.Snapshot().objects.Length;
            var call=new JObject {["id"]="program.module.captureConstruction",["version"]=1,["arguments"]=new JObject {["name"]="My reusable ball",["members"]=new JArray(new JObject {["target"]=original,["slot"]="ball",["revision"]=editor.ObjectRevision(original)})}};
            var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(call);request.conditions=new[]{new RoomObjectCondition {id=original,revision=editor.ObjectRevision(original)}};Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            for(int i=0;i<180&&(string)executor.Executions.Observe()["selected"]?["phase"]!="completed";i++)yield return null;
            var receipt=executor.Executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
            string hash=(string)receipt["output"]["hash"];var module=workshop.Modules.Inspect(hash).ReadDefinition();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            int libraryRevision=workshop.Modules.Revision;Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(workshop.Modules.Revision,Is.EqualTo(libraryRevision));
            Assert.That(module.ToString(),Does.Not.Contain(original));Assert.That(editor.DeleteObject(original,out error),Is.True,error);
            var args=(JObject)module["program"]["functions"][1]["body"][0]["arguments"].DeepClone();args["position"]=JObject.Parse("{\"x\":-.3,\"y\":1,\"z\":.7}");
            Assert.That(executor.Execute(TemplateRequest(new JObject {["id"]="object.batch.create",["version"]=1,["arguments"]=args}),out error,out _),Is.True,error);
            string fresh=(string)executor.Executions.Observe()["selected"]["output"]["objectIds"][0];Assert.That(fresh,Is.Not.EqualTo(original));Assert.That(editor.Read(fresh).color,Is.EqualTo(Color.red));
            editor.Undo();Assert.That(editor.Find(fresh),Is.Null);
        }
        [UnityTest] public IEnumerator CaptureUsesLivePoseAndRejectsStaleRevisionsWithoutChangingOriginal() {
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Block",Vector3.one,1,Color.blue,out var id,out var error),Is.True,error);
            int revision=editor.ObjectRevision(id);editor.Find(id).transform.localPosition=new Vector3(2,1,1);
            var members=new[]{new ConstructionMember {target=id,slot="block",revision=revision}};
            Assert.That(editor.CaptureConstruction(members,out var batch,out error),Is.True,error);Assert.That(batch.position,Is.EqualTo(new Vector3(2,1,1)));
            Assert.That(editor.Read(id).position,Is.EqualTo(Vector3.one),"Read-only capture must not secretly save the live pose");
            Assert.That(editor.PaintObject(id,Color.red,out error),Is.True,error);Assert.That(editor.CaptureConstruction(members,out _,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            yield return null;
        }
        [UnityTest] public IEnumerator CapturedHingesBindFreshMembersAndRefuseExternalConnections() {
            var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-spring-lever.json")));
            var args=source["imports"][0]["module"]["program"]["functions"][1]["body"][0]["arguments"];
            Assert.That(editor.CreateBatch(JsonUtility.FromJson<CreationBatch>(args.ToString()),out var ids,out var error),Is.True,error);
            var members=ids.Select((id,i)=>new ConstructionMember {target=id,slot="piece_"+i,revision=editor.ObjectRevision(id)}).ToArray();
            Assert.That(editor.CaptureConstruction(members.Take(1).ToArray(),out _,out error),Is.False);Assert.That(error,Does.Contain("both ends"));
            Assert.That(editor.CaptureConstruction(members,out var captured,out error),Is.True,error);
            var module=ConstructionModule.Definition(captured,"My lever");ProgramModuleLibrary.Validate(module);Assert.That(module.ToString(),Does.Not.Contain(ids[0]));
            Assert.That(editor.CreateBatch(captured,out var fresh,out error),Is.True,error);Assert.That(editor.Read(fresh[1]).connections.Single().connected,Is.EqualTo(fresh[0]));
            editor.Undo();Assert.That(fresh.All(id=>!editor.Find(id)),Is.True);Assert.That(ids.All(id=>editor.Find(id)),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator CapturedInkTipAndMotionRemainEditableIndependentComponents() {
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/creation-prototypes-contract.json")));
            var batch=CreationBatch.Read((JObject)cases.Single(c=>(string)c["name"]=="saved flat ink")["arguments"]);
            var prototype=batch.blueprint.pieces[0].source.prototype;
            prototype.drawingTips=new[]{new DrawingTip {color=Color.yellow,position=new Vector3(0,0,.065f)}};
            prototype.motion=new PrototypeMotion {loop=true,frames=new[]{new PrototypeFrame(),new PrototypeFrame {time=1,position=new Vector3(.2f,0,0)}}};
            Assert.That(editor.CreateBatch(batch,out var ids,out var error),Is.True,error);
            Assert.That(editor.CaptureConstruction(new[]{new ConstructionMember {target=ids[0],revision=editor.ObjectRevision(ids[0]),slot="tool"}},out var captured,out error),Is.True,error);
            var definition=ConstructionModule.Definition(captured,"Marked moving tool");var arguments=(JObject)definition["program"]["functions"][1]["body"][0]["arguments"];
            Assert.That(editor.CreateBatch(CreationBatch.Read(arguments),out var copies,out error),Is.True,error);
            var copy=editor.Read(copies[0]);Assert.That(copy.surfaces.Single().strokes.Single().points.Length,Is.EqualTo(2));Assert.That(copy.drawingTips.Single().color,Is.EqualTo(Color.yellow));Assert.That(copy.motion.loop,Is.True);
            Assert.That(Vector3.Distance(copy.motion.frames[1].position,batch.position+new Vector3(.2f,0,0)),Is.LessThan(.0001));
            Assert.That(editor.Find(copies[0]).GetComponent<DrawingSurfaceView>(),Is.Not.Null);Assert.That(editor.Find(copies[0]).GetComponent<DrawingTipView>(),Is.Not.Null);
            copy.surfaces[0].strokes[0].points[0]=Vector3.one;Assert.That(editor.Read(ids[0]).surfaces[0].strokes[0].points[0],Is.Not.EqualTo(Vector3.one));yield return null;
        }
        [UnityTest] public IEnumerator MissingModelDependencyCreatesNothingAndCancelledCheckCannotCommitLater() {
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/creation-prototypes-contract.json")));
            var args=(JObject)cases.Single(c=>(string)c["name"]=="model exact hash")["arguments"];
            var module=new BatchCreationCapability();var context=new CapabilityContext(editor,null);int count=editor.Snapshot().objects.Length;
            Assert.That(module.Start(context,"test",args,out var operation,out var error),Is.True,error);
            Maestro.Quest.Rules.RuleActionState state=Maestro.Quest.Rules.RuleActionState.Preparing;
            for(int i=0;i<180&&(state=operation.State(out error))==Maestro.Quest.Rules.RuleActionState.Preparing;i++)yield return null;
            Assert.That(state,Is.EqualTo(Maestro.Quest.Rules.RuleActionState.Failed));Assert.That(error,Does.Contain("missing or damaged"));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            Assert.That(module.Start(context,"test2",args,out operation,out error),Is.True,error);operation.Stop(false);yield return null;
            Assert.That(operation.State(out error),Is.EqualTo(Maestro.Quest.Rules.RuleActionState.Failed));Assert.That(error,Does.Contain("cancelled"));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
        }
    }
}

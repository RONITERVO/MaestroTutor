// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator SupportTemplateCreationPersistsOneUndoAndReplayRetainsHistoricalReceipt()
        {
            var executor=new RoomAgentExecutor(editor);
            foreach(string template in new[]{"platform","stairs"}){
                var request=TemplateRequest(TemplateCall(template,new Vector3(2,1,0)));int count=editor.Snapshot().objects.Length;
                Assert.That(executor.Execute(request,out var error,out _),Is.True,error);yield return AgentPlacementCompleted(executor);var receipt=executor.Executions.Observe()["selected"].DeepClone();string id=(string)receipt["output"]["objectId"];Assert.That(id,Is.Not.Null,receipt.ToString());
                var data=editor.Read(id);Assert.That(data.staticSupport.enabled,Is.True);Assert.That(data.physics,Is.EqualTo(ItemPhysics.Fixed));Assert.That(data.recipe.playing,Is.False);Assert.That(data.collision.shapes.Length,Is.EqualTo(template=="stairs"?4:1));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==id).staticSupport.enabled,Is.True);
                editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.HasSavedObject(id),Is.False);Assert.That(executor.Executions.Observe()["selected"]["output"],Is.EqualTo(receipt["output"]));editor.Redo();Assert.That(editor.Read(id).staticSupport.enabled,Is.True);editor.Undo();yield return null;
            }
        }
        [UnityTest] public IEnumerator InlineSupportAndTemplateBatchUseOrdinarySavedCreationTransactions()
        {
            var entry=CreationTemplates.All.Single(t=>t.Id=="platform");var call=PendingCreationCall("recipe");var args=(JObject)call["arguments"];args["recipe"]=entry.Source["definition"]["recipe"];args["collision"]=entry.Source["definition"]["collision"];args["physics"]=entry.Source["definition"]["physics"];args["staticSupport"]=new JObject{["version"]=1,["enabled"]=true};
            var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(TemplateRequest(call),out var error,out _),Is.True,error);yield return AgentPlacementCompleted(executor);string id=(string)executor.Executions.Observe()["selected"]["output"]["objectId"];Assert.That(editor.Read(id).staticSupport.enabled,Is.True);editor.Undo();
            var batch=new CreationBatch{position=new Vector3(2,1,0),blueprint=new(){pieces=new[]{new CreationPiece{slot="First",name="",source=new(){kind="template",templateHash=entry.Hash}},new CreationPiece{slot="Second",name="",position=Vector3.right,source=new(){kind="template",templateHash=entry.Hash}}}}};int count=editor.Snapshot().objects.Length;
            Assert.That(editor.CreateBatch(batch,out var ids,out error),Is.True,error);Assert.That(ids.Length,Is.EqualTo(2));Assert.That(ids.All(target=>editor.Read(target).staticSupport.enabled),Is.True);Assert.That(new RoomStorage(directory).Load(out _).objects.Count(o=>ids.Contains(o.id)&&o.staticSupport.enabled),Is.EqualTo(2));editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(ids.All(target=>!editor.HasSavedObject(target)),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator SupportedPendingCreationCancellationAndFailedSaveNeverPublishIntent()
        {
            var entry=CreationTemplates.All.Single(t=>t.Id=="platform");int count=editor.Snapshot().objects.Length;
            Assert.That(editor.BeginCreateRecipe(entry.Name,Vector3.zero,1,entry.Recipe,entry.Collision,entry.Physics,out var id,out var pending,out var error,staticSupport:entry.StaticSupport),Is.True,error);yield return Prepared(pending);pending.Dispose();yield return Drained(pending);Assert.That(editor.HasSavedObject(id),Is.False);Assert.That(editor.Find(id),Is.Null);
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(editor.BeginCreateRecipe(entry.Name,Vector3.zero,1,entry.Recipe,entry.Collision,entry.Physics,out id,out pending,out error,staticSupport:entry.StaticSupport),Is.True,error);using(pending){yield return Rejected(pending);Assert.That(pending.Accepted,Is.False);Assert.That(editor.HasSavedObject(id),Is.False);}yield return Drained(pending);}
            finally{Directory.Delete(obstacle);}
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(new RoomStorage(directory).Load(out _).objects.Any(o=>o.id==id),Is.False);
        }
    }
}

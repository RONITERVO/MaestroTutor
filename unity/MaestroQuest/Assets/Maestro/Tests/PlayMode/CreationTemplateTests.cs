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
        RoomAgentRequest TemplateRequest(JObject call){var request=ObjectEditRequest(call);request.conditions=Array.Empty<RoomObjectCondition>();return request;}
        JObject TemplateRun(RoomAgentExecutor executor,JObject call){Assert.That(executor.Execute(TemplateRequest(call),out var error,out _),Is.True,error);return (JObject)executor.Executions.Observe().DeepClone();}
        JObject TemplateCall(string id,Vector3 position)=>new() {["id"]="object.create",["version"]=1,["arguments"]=new JObject {["kind"]="template",["templateHash"]=CreationTemplates.All.First(e=>e.Id==id).Hash,["name"]="",["x"]=position.x,["y"]=position.y,["z"]=position.z,["scale"]=1}};
        [UnityTest] public IEnumerator TemplateCreationSavesCompleteEditableObjectInOneUndoAndReceiptReplayDoesNotDuplicate() {
            var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(TemplateCall("cup",new Vector3(2,1,0)));int count=editor.Snapshot().objects.Length;
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);var receipt=(JObject)executor.Executions.Observe().DeepClone();string target=(string)receipt["selected"]["output"]["objectId"];Assert.That(target,Is.Not.Null,receipt.ToString());yield return null;
            var data=editor.Read(target);Assert.That(data.name,Is.EqualTo("Cup"));Assert.That(data.recipe.parts.Length,Is.EqualTo(2));Assert.That(data.collision.Pieces,Is.EqualTo(21));Assert.That(data.physics,Is.EqualTo(ItemPhysics.Solid));Assert.That(data.mass,Is.EqualTo(.3f));Assert.That(editor.Find(target).Grab.colliders.Count,Is.EqualTo(21));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==target).collision.Pieces,Is.EqualTo(21));
            editor.Undo();yield return null;Assert.That(editor.Read(target),Is.Null);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            editor.Redo();yield return null;Assert.That(editor.Read(target).recipe.parts.Length,Is.EqualTo(2));Assert.That(editor.Find(target).Grab.colliders.Count,Is.EqualTo(21));
            Assert.That(editor.PaintObject(target,Color.magenta,out error),Is.True,error);Assert.That(editor.Read(target).color,Is.EqualTo(Color.magenta));Assert.That(CreationTemplates.All.First(e=>e.Id=="cup").Recipe.parts[0].color,Is.Not.EqualTo(Color.magenta));
        }
        [UnityTest] public IEnumerator ConfiguredCreationValidatesWholeObjectAndFailedSaveLeavesNoPartialCreation() {
            var entry=CreationTemplates.All.First(e=>e.Id=="cup");int count=editor.Snapshot().objects.Length;var bad=entry.Collision;bad.shapes[1].innerRadius=.499f;
            Assert.That(editor.CreateRecipe("Bad",Vector3.zero,1,entry.Recipe,bad,entry.Physics,out var id,out _),Is.False);Assert.That(id,Is.Null);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            string obstacle=Path.Combine(directory,"room.v4.json.pending");Directory.CreateDirectory(obstacle);
            try {Assert.That(editor.CreateRecipe("Cup",Vector3.zero,1,entry.Recipe,entry.Collision,entry.Physics,out id,out _),Is.False);Assert.That(id,Is.Null);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));}finally{Directory.Delete(obstacle);}
            yield return null;
        }
        [UnityTest] public IEnumerator StarterDominoesToppleAndStackingBricksRestOnSyntheticFloor() {
            RoomPhysicsLayers.Configure();var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(2,-.1f,0);floor.transform.localScale=new Vector3(3,.2f,3);floor.layer=RoomPhysicsLayers.Scanned;
            var executor=new RoomAgentExecutor(editor);string Create(string id,Vector3 position)=>(string)TemplateRun(executor,TemplateCall(id,position))["selected"]["output"]["objectId"];
            var first=editor.Find(Create("domino",new Vector3(2,.083f,0)));var second=editor.Find(Create("domino",new Vector3(2,.083f,.08f)));
            var bottom=editor.Find(Create("brick",new Vector3(2.4f,.031f,0)));var top=editor.Find(Create("brick",new Vector3(2.4f,.105f,0)));
            Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic starter-kit floor");physics.StartPhysics();
            for(int i=0;i<80;i++)yield return new WaitForFixedUpdate();
            Assert.That(bottom.transform.position.y,Is.InRange(.02f,.045f));Assert.That(top.transform.position.y,Is.InRange(.08f,.13f));
            var rb=first.GetComponent<Rigidbody>();rb.AddForceAtPosition(Vector3.forward*.04f,rb.position+Vector3.up*.065f,ForceMode.Impulse);
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Dot(first.transform.up,Vector3.up),Is.LessThan(.6f));Assert.That(Vector3.Dot(second.transform.up,Vector3.up),Is.LessThan(.6f),"The second domino must respond to the first object's contact");
            Assert.That(second.transform.position.y,Is.GreaterThan(0));
        }
    }
}

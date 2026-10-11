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
        RoomRecipe LatheCup()=>JsonUtility.FromJson<RoomRecipe>(JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/lathe-contract.json")))[0]["recipe"].ToString());
        [UnityTest] public IEnumerator LatheCreationEditingReadbackUndoAndDisposalUseTheRealRoom()
        {
            var recipe=LatheCup();Assert.That(editor.CreateRecipe("Editable cup",new Vector3(.2f,1,.4f),1,recipe,out string target,out string error),Is.True,error);
            var item=editor.Find(target);var geometry=item.GetComponent<RecipeObject>();var original=geometry.Part("Body").GetComponentInChildren<MeshFilter>().sharedMesh;
            var call=RecipePatch(target);recipe.parts[0].profile[1].x=.48f;recipe.parts[0].segments=32;call["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(recipe.parts[0])));
            var receipt=RecipeRun(new RoomAgentExecutor(editor),call);yield return null;
            Assert.That(original==null,Is.True,"Replaced procedural meshes must be released");Assert.That(editor.Find(target),Is.SameAs(item));
            var replacement=geometry.Part("Body").GetComponentInChildren<MeshFilter>().sharedMesh;Assert.That(replacement.vertexCount,Is.GreaterThan(0));
            var args=new JObject {["target"]=target,["revision"]=editor.ObjectRevision(target),["part"]="Body",["offset"]=0};
            Assert.That(BehaviourCatalog.TryRead("object.recipe.profile",1,args,new BehaviourCatalog.FactContext(editor:editor),out var profile),Is.True);
            var observed=(JObject)profile.Value;Assert.That(profile.Characters,Is.LessThanOrEqualTo(1024));Assert.That((float)observed["points"][1]["x"],Is.EqualTo(.48f));Assert.That((int)observed["segments"],Is.EqualTo(32));
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==target).recipe.parts[0].segments,Is.EqualTo(32));
            editor.Undo();yield return null;
            Assert.That(replacement==null,Is.True);Assert.That(editor.Read(target).recipe.parts[0].segments,Is.EqualTo(24));Assert.That(BehaviourCatalog.TryRead("object.recipe.profile",1,args,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            editor.Undo();yield return null;Assert.That(editor.Find(target),Is.Null);
        }
        [UnityTest] public IEnumerator GeneratedRoomBudgetRefusesExcessGeometryBeforeSaving()
        {
            var template=LatheCup();var part=template.parts[0];part.segments=48;
            // Six points cost 588 vertices. 32 parts per assembly, bounded by the existing 256-part room cap.
            template.parts=Enumerable.Range(0,32).Select(i=>{var p=JsonUtility.FromJson<RecipePart>(JsonUtility.ToJson(part));p.id="Part"+i;return p;}).ToArray();
            Assert.That(template.Validate(out _),Is.True);
            // Use 16 valid points so the dedicated generated-vertex limit is reached before the part limit.
            var profile=Enumerable.Range(0,16).Select(i=>{float angle=2*Mathf.PI*i/16;return new Vector2(.3f+.15f*Mathf.Cos(angle),.4f*Mathf.Sin(angle));}).ToArray();
            foreach(var p in template.parts)p.profile=profile;
            string last=null;for(int i=0;i<5;i++)Assert.That(editor.CreateRecipe("Bounded lathe",new Vector3(.1f,1,.1f),1,template,out last,out var error),Is.True,error);
            int count=editor.Snapshot().objects.Length;Assert.That(editor.CreateRecipe("Too many vertices",new Vector3(.1f,1,.1f),1,template,out _,out _),Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            Assert.That(new RoomStorage(directory).Load(out _).objects.Length,Is.EqualTo(count));yield return null;
        }
    }
}

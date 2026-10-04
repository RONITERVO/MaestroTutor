// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class CreationTemplateTests
    {
        [Test] public void EveryBundledTemplatePinsItsBytesAndExpandsToValidDetachedComponents() {
            Assert.That(CreationTemplates.All.Count,Is.EqualTo(25));
            foreach(var entry in CreationTemplates.All) {
                var bytes=File.ReadAllBytes(Path.Combine(Application.dataPath,"Maestro/Resources/Creation/Templates",entry.Id+".json"));Assert.That(entry.Hash,Is.EqualTo(ModelLibrary.Hash(bytes)));
                var recipe=entry.Recipe;Assert.That(recipe.Validate(out var error),Is.True,error);Assert.That(entry.Collision.Validate(out error),Is.True,error);Assert.That(RoomControls.ValidPhysics(entry.Physics),Is.True);Assert.That(recipe.playing,Is.False);
                Assert.That(DrawingTip.ValidateCollection(new RoomObjectData {kind=RoomObjectKind.Assembly,recipe=entry.Recipe,drawingTips=entry.DrawingTips},out error),Is.True,error);
                var surfaces=entry.Surfaces;Assert.That(DrawingSurface.ValidateCollection(new RoomObjectData {kind=RoomObjectKind.Assembly,recipe=entry.Recipe,surfaces=surfaces},out error),Is.True,error);
                if(entry.Id=="chalkboard"){Assert.That(surfaces.Single().part,Is.EqualTo("Board"));surfaces[0].width=.02f;Assert.That(entry.Surfaces[0].width,Is.GreaterThan(1));}
                recipe.parts[0].id="changed";entry.Source["name"]="changed";entry.Collision.shapes[0].id="changed";entry.Physics.mass=99;
                Assert.That(entry.Recipe.parts[0].id,Is.Not.EqualTo("changed"));Assert.That(entry.Name,Is.Not.EqualTo("changed"));Assert.That(entry.Collision.shapes[0].id,Is.Not.EqualTo("changed"));Assert.That(entry.Physics.mass,Is.LessThan(20));
                int index=CreationTemplates.All.ToList().IndexOf(entry);Assert.That(BehaviourCatalog.TryRead("creation.template",1,new JObject {["index"]=index},default,out var fact),Is.True,entry.Id);Assert.That(fact.Characters,Is.LessThanOrEqualTo(1024));Assert.That((string)((JObject)fact.Value)["hash"],Is.EqualTo(entry.Hash));
            }
            Assert.That(BehaviourCatalog.TryRead("creation.template",1,new JObject {["index"]=CreationTemplates.All.Count},default,out _),Is.False);
        }
        [Test] public void ConfiguredRecipeCallsKeepAllComponentsAndCannotRoundTripThroughLossyNumericControls() {
            var entry=CreationTemplates.All.First(e=>e.Id=="cup");var args=(JObject)BehaviourCatalog.Action("object.create").InputSchema["oneOf"].Single(v=>(string)v["examples"][0]["kind"]=="recipe")["examples"][0];
            args["recipe"]=entry.Source["definition"]["recipe"];args["collision"]=entry.Source["definition"]["collision"];args["physics"]=entry.Source["definition"]["physics"];
            Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out var call,out var error),Is.True,error);Assert.That(call.TryStep(out _,out _),Is.False);
            var source=BehaviourProgram.FromInvocation(new JObject {["id"]="object.create",["version"]=1,["arguments"]=args});Assert.That(BehaviourProgram.TryParse(source,out var program,out error),Is.True,error);
            var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out var executed),Is.EqualTo(ProgramYield.Action));Assert.That(JToken.DeepEquals(executed.Arguments,JObject.Parse(source)["functions"][0]["body"][0]["arguments"]),Is.True);
            args["physics"]["mass"]=0;Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out _,out _),Is.False);
        }
        [Test] public void TemplateChoicesUseExactHashesNotNamesAndRequireNoNewNumericKinds() {
            var variant=(JObject)BehaviourCatalog.Action("object.create").InputSchema["oneOf"].Single(v=>(string)v["examples"][0]["kind"]=="template");
            foreach(var entry in CreationTemplates.All) {
                var args=(JObject)variant["examples"][0].DeepClone();args["templateHash"]=entry.Hash;
                Assert.That((string)variant["properties"]["templateHash"]["x-enum-labels"][entry.Hash],Is.EqualTo(entry.Name));
                Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out var call,out var error),Is.True,error);Assert.That(call.TryStep(out _,out _),Is.False);Assert.That(call.Resources,Is.Empty);
                args["templateHash"]=entry.Id;Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out _,out _),Is.False);
            }
        }
    }
}

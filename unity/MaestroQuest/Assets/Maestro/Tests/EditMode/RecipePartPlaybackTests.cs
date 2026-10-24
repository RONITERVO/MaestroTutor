// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class RecipePartPlaybackTests
 {
  static JObject Args(string part="RightUpperArm")=>new(){["target"]=new string('a',32),["source"]=new JObject{["kind"]="recipe",["part"]=part},["channel"]="recipePart",["seconds"]=1,["loop"]=false};
  [Test] public void PartClaimsComposeOnlyAcrossDistinctLocalJointsAndNeverEscapeWholeObjectOwnership(){
   Assert.That(BehaviourCatalog.TryCall("animation.play",1,Args(),out var first,out var error),Is.True,error);Assert.That(BehaviourCatalog.TryCall("animation.play",1,Args("RightLowerArm"),out var second,out error),Is.True,error);
   Assert.That(first.Claims.Single().Channel,Is.EqualTo("recipePart:RightUpperArm"));Assert.That(first.Claims.Single().Conflicts(second.Claims.Single()),Is.False);Assert.That(first.Resources,Is.EqualTo(new[]{new string('a',32)}));
   var ownership=new RoomOwnership();Assert.That(ownership.TryAcquire("first","Arm",RoomActorRole.Program,first.Claims,null,out var a,out _),Is.True);Assert.That(ownership.TryAcquire("second","Forearm",RoomActorRole.Program,second.Claims,null,out var b,out _),Is.True);
   Assert.That(ownership.CanAcquire("same",RoomActorRole.Program,first.Claims,out _),Is.False);var whole=new[]{new BehaviourCatalog.Claim(new string('a',32),"wholeTarget")};Assert.That(ownership.CanAcquire("whole",RoomActorRole.Program,whole,out _),Is.False);
   Assert.That(ownership.TryAcquire("hand","Your grip",RoomActorRole.Grab,whole,null,out var hand,out _),Is.True);Assert.That(a.Held||b.Held,Is.False);hand.Dispose();
  }
  [Test] public void PartContractsRejectMalformedIdsWrongSourcesAndUnsupportedArguments(){
   foreach(var part in new[]{"","arm/child","arm:child",new string('a',33)})Assert.That(BehaviourCatalog.TryCall("animation.play",1,Args(part),out _,out _),Is.False,part);
   foreach(var field in new[]{"prop","priority","resume"}){var args=Args();args[field]=true;Assert.That(BehaviourCatalog.TryCall("animation.play",1,args,out _,out _),Is.False,field);}
   var missing=Args();((JObject)missing["source"]).Remove("part");Assert.That(BehaviourCatalog.TryCall("animation.play",1,missing,out _,out _),Is.False);
   var wrong=Args();wrong["source"]["kind"]="recording";Assert.That(BehaviourCatalog.TryCall("animation.play",1,wrong,out _,out _),Is.False);
   Assert.That(BehaviourCatalog.Action("animation.recipe.part"),Is.Null,"Named parts extend the existing public animation verb");
  }
  [Test] public void SharedParallelProgramKeepsExactPartIdentityAndDeclaredResourceAuthority(){
   var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-recipe-parts.json"));Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);
   var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Parallel),machine.Error);
   Assert.That(machine.Branches.Length,Is.EqualTo(2));
   var schema=BehaviourCatalog.Action("animation.play").InputSchema;Assert.That(schema["oneOf"].Any(s=>(string)s["properties"]?["channel"]?["enum"]?[0]=="recipePart"),Is.True);
  }
 }
}

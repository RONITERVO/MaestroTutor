// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class ObjectAttachmentTests
 {
  static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-object-hold.json")));
  static JObject Args()=> (JObject)Source()["functions"][1]["body"][0]["arguments"];
  [Test] public void HoldClaimsOnlyThePropButRequiresBothResourceIdentities(){
   Assert.That(BehaviourCatalog.TryCall("object.hold",1,Args(),out var call,out var error),Is.True,error);Assert.That(call.Claims.Length,Is.EqualTo(1));Assert.That(call.Claims[0].Target,Is.EqualTo(new string('0',32)));Assert.That(call.Resources,Is.EquivalentTo(new[]{new string('0',32),new string('1',32)}));
   var source=Source();Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out error),Is.True,error);var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Parallel));Assert.That(machine.Branches[0].Advance(out call),Is.EqualTo(ProgramYield.Action),machine.Branches[0].Error);
   source["resources"]=new JArray(new string('0',32));Assert.That(BehaviourProgram.TryParse(source.ToString(),out program,out error),Is.True,error);machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Parallel));Assert.That(machine.Branches[0].Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Branches[0].Error,Does.Contain("resource"));
  }
  [Test] public void NestedAnchorArgumentsRemainTypedAndCannotChangeSelectorsOrInventFields(){
   var source=Source();var node=source["functions"][1]["body"][0];node["bindings"]["holder.part"]=new JObject{["value"]="Head"};Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out var error),Is.True,error);
   node["bindings"]["holder.kind"]=new JObject{["value"]="object"};Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out error),Is.False);Assert.That(error,Does.Contain("literal"));
   foreach(var mutation in new[]{"unknown","rotation","offset","source"}){var args=Args();if(mutation=="unknown")args["priority"]=10;else if(mutation=="rotation")args["rotation"]["w"]=0;else if(mutation=="offset")args["offset"]["x"]=1;else args["holder"]["kind"]="code";Assert.That(BehaviourCatalog.TryCall("object.hold",1,args,out _,out _),Is.False,mutation);}
   var hand=Args();hand["holder"]=new JObject{["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=""};Assert.That(BehaviourCatalog.TryCall("object.hold",1,hand,out _,out error),Is.True,error);
   hand["holder"]["revision"]=1;Assert.That(BehaviourCatalog.TryCall("object.hold",1,hand,out _,out _),Is.False);
  }
  [Test] public void AnchorFactBindingsResolveOnlyInsideTheSelectedVariant(){
   var definition=BehaviourCatalog.Fact("object.anchor");var args=new JObject{["holder"]=new JObject{["kind"]="object",["objectId"]="book",["revision"]=1}};
   Assert.That(definition.ArgumentType("holder.objectId",args),Is.EqualTo(ProgramType.Text));Assert.That(definition.ArgumentType("holder.revision",args),Is.EqualTo(ProgramType.Number));Assert.That(definition.ArgumentType("holder.kind",args),Is.EqualTo(ProgramType.Void));Assert.That(definition.ArgumentType("holder.part",args),Is.EqualTo(ProgramType.Void));
  }
 }
}

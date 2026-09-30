// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Maestro.Quest.Programs;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class BehaviourCatalogTests
    {
        [Test] public void ScopedCatalogQueriesAreStrictPagedDetachedAndReadOnlyWithoutARoom()
        {
            var catalog=new RoomCapabilityCatalog(null);
            JObject Query(string json) {Assert.That(catalog.Execute(JObject.Parse(json),out var error),Is.True,error);return catalog.Observe();}
            var first=Query("{\"operation\":\"search\",\"category\":\"events\",\"query\":\"\",\"offset\":0}");
            Assert.That((string)first["category"],Is.EqualTo("events"));Assert.That(first["entries"].Count(),Is.EqualTo(6));
            Assert.That(first["entries"].All(x=>((JObject)x).Count==3),Is.True,"Search pages must not expand schemas");
            var second=Query("{\"operation\":\"search\",\"category\":\"events\",\"query\":\"\",\"offset\":6}");
            var ids=first["entries"].Concat(second["entries"]).Select(x=>(string)x["id"]).ToArray();
            Assert.That(ids,Is.EqualTo(BehaviourCatalog.Events.Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal)));
            second["entries"][0]["label"]="Changed by caller";
            Assert.That((string)catalog.Observe()["entries"][0]["label"],Is.Not.EqualTo("Changed by caller"));
            var inspected=Query("{\"operation\":\"inspect\",\"category\":\"events\",\"capability\":\"object.collided\",\"version\":1}");
            Assert.That(JToken.DeepEquals(inspected["definition"],BehaviourCatalog.Events.Single(x=>x.Id=="object.collided").ToJson()),Is.True);
            inspected["definition"]["fields"]["properties"]["speed"]["type"]="string";
            Assert.That((string)catalog.Observe()["definition"]["fields"]["properties"]["speed"]["type"],Is.EqualTo("number"));
            var unavailable=Query("{\"operation\":\"inspect\",\"category\":\"facts\",\"capability\":\"physics.ready\",\"version\":1}");
            Assert.That((bool)unavailable["available"],Is.False);Assert.That(unavailable["value"].Type,Is.EqualTo(JTokenType.Null));
            Assert.That(JToken.DeepEquals(unavailable["definition"],BehaviourCatalog.Fact("physics.ready").ToJson()),Is.True);
            foreach(var category in new[]{"actions","events","facts"}) {
                var unknown=Query("{\"operation\":\"inspect\",\"category\":\""+category+"\",\"capability\":\"object.collided\",\"version\":2}");
                Assert.That(unknown["definition"].Type,Is.EqualTo(JTokenType.Null));
            }
            foreach(var invalid in new[]{
                "{\"operation\":\"search\",\"category\":\"unknown\",\"query\":\"\",\"offset\":0}",
                "{\"operation\":\"search\",\"category\":null,\"query\":\"\",\"offset\":0}",
                "{\"operation\":\"inspect\",\"category\":\"events\",\"capability\":\"object.collided\",\"version\":1,\"run\":true}",
                "{\"operation\":\"check\",\"category\":\"facts\",\"call\":{\"id\":\"time.wait\",\"version\":1,\"arguments\":{\"seconds\":1}}}"})
                Assert.That(RoomCapabilityCatalog.ValidRequest(JObject.Parse(invalid)),Is.False,invalid);
            var factSearch=Query("{\"operation\":\"search\",\"category\":\"facts\",\"query\":\"suspension\",\"offset\":0}");
            Assert.That(factSearch["entries"].Select(x=>(string)x["id"]),Does.Contain("maestro.state"));
        }
        [Test] public void NativeDefinitionsEqualTheCommittedWebManifest()
        {
            string path=Environment.GetEnvironmentVariable("MAESTRO_BEHAVIOUR_CATALOG");
            if(string.IsNullOrEmpty(path))path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../shared/generated/behaviourCatalog.json"));
            Assert.That(File.Exists(path),Is.True,"Run Verify-Quest with the shared catalog path, or open the source project.");
            var expected=JObject.Parse(File.ReadAllText(path));expected.Remove("sources");
            // Compare the serialized contract: a nullable C# string becomes JSON null.
            var actual=JObject.Parse(BehaviourCatalog.Manifest().ToString());
            Assert.That(JToken.DeepEquals(actual,expected),Is.True,"Regenerate the reviewed manifest from native registrations.");
            Assert.That(BehaviourCatalog.Actions.Where(x=>LegacyCapabilityAdapters.Kind(x.Id).HasValue).All(x=>x.Duration==(RuleDocument.IsInstant(LegacyCapabilityAdapters.Kind(x.Id).Value)?"instant":"timed")),Is.True);
            Assert.That(BehaviourCatalog.Action(RuleActionKind.UpperBodyGesture).Channels,Is.EqualTo(new[] {"upperBody"}));
            Assert.That(BehaviourCatalog.Action("avatar.follow.user").Channels,Is.EqualTo(new[] {"locomotion","gaze"}));
            Assert.That(BehaviourCatalog.Action(RuleActionKind.LibraryMotion).Channels,Is.EqualTo(new[] {"wholeTarget"}));
        }
        [Test] public void EveryExistingAdapterHasExactlyOneStableRegistration()
        {
            Assert.That(LegacyCapabilityAdapters.ActionIds.Select(id=>LegacyCapabilityAdapters.Kind(id).Value),Is.EquivalentTo(Enum.GetValues(typeof(RuleActionKind))));
            Assert.That(BehaviourCatalog.Events.Where(x=>x.Kind.HasValue).Select(x=>x.Kind.Value),Is.EquivalentTo(Enum.GetValues(typeof(RuleEventKind))));
            var ids=BehaviourCatalog.Actions.Select(x=>x.Id).Concat(BehaviourCatalog.Events.Select(x=>x.Id)).Concat(BehaviourCatalog.Facts.Select(x=>x.Id)).ToArray();
            Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length));
            Assert.That(ids.All(id=>System.Text.RegularExpressions.Regex.IsMatch(id,@"^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$")),Is.True);
            Assert.That(BehaviourCatalog.HasAction((RuleActionKind)999),Is.False);
            Assert.That(BehaviourCatalog.Event((RuleEventKind)999),Is.Null);
        }
        [Test] public void NamedOnlyCapabilityCompilesWithoutLegacyStepAndKeepsValidatedArgumentsDetached()
        {
            var arguments=new JObject {["target"]="book",["pitch"]=0,["yaw"]=90,["roll"]=0};
            Assert.That(BehaviourCatalog.TryCall("object.rotation.set",1,arguments,out var call,out var error),Is.True,error);
            Assert.That(LegacyCapabilityAdapters.Kind(call.Definition.Id),Is.Null);
            Assert.That(call.TryStep(out _,out _),Is.False);
            arguments["yaw"]=999;var exposed=call.Arguments;exposed["target"]="maestro";
            Assert.That((int)call.Arguments["yaw"],Is.EqualTo(90));Assert.That(call.Resources,Is.EqualTo(new[]{"book"}));
            Assert.That(call.Claims.Single().Target,Is.EqualTo("book"));Assert.That(call.Claims.Single().Channel,Is.EqualTo("wholeTarget"));
            var json=new JObject {["id"]=call.Definition.Id,["version"]=1,["arguments"]=call.Arguments};
            Assert.That(BehaviourProgram.TryParse(BehaviourProgram.FromInvocation(json),out var program,out error),Is.True,error);
            Assert.That(program.SimpleSteps(),Is.Null,"Old tray adapters must not invent or discard unknown fields");
            var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out var yielded),Is.EqualTo(ProgramYield.Action));
            Assert.That(yielded.Definition.Id,Is.EqualTo("object.rotation.set"));Assert.That(yielded.Instant,Is.True);
            Assert.That(JToken.DeepEquals(yielded.Arguments,call.Arguments),Is.True);
            Assert.That(BehaviourCatalog.TryCall("object.rotation.set",1,arguments,out _,out _),Is.False);
            arguments["yaw"]=90;arguments["seconds"]=1;Assert.That(BehaviourCatalog.TryCall("object.rotation.set",1,arguments,out _,out _),Is.False);
        }
        [Test] public void MissingFactsStayUnavailableAndPresentFalseIsNotMistakenForMissing()
        {
            Assert.That(BehaviourCatalog.TryRead("physics.ready",default,out _),Is.False);
            Assert.That(BehaviourCatalog.TryRead("maestro.state",default,out _),Is.False);
            Assert.That(BehaviourCatalog.TryRead("unknown.fact",default,out _),Is.False);
            Assert.That(BehaviourCatalog.TryRead("physics.ready",new BehaviourCatalog.FactContext(physicsReady:false),out var ready),Is.True);
            Assert.That(ready.Type,Is.EqualTo(ProgramType.Boolean));Assert.That(ready.Boolean,Is.False);
            Assert.That(BehaviourCatalog.TryRead("maestro.state",new BehaviourCatalog.FactContext("speaking"),out var state),Is.True);
            Assert.That(state.Text,Is.EqualTo("speaking"));
        }
        [Test] public void ExportAndNativeValidationShareTheRegisteredTypesAndEventSemantics()
        {
            var manifest=BehaviourCatalog.Manifest();
            Assert.That(manifest["facts"].Count(),Is.EqualTo(BehaviourProgram.Facts.Count));
            foreach(var entry in manifest["facts"]) Assert.That(BehaviourProgram.Facts[(string)entry["id"]].ToString().ToLowerInvariant(),Is.EqualTo((string)entry["type"]));
            foreach(var entry in manifest["events"])
            {
                var kind=BehaviourCatalog.Events.Single(x=>x.Id==(string)entry["id"]).Kind;
                if(!kind.HasValue)continue;
                Assert.That(RuleDocument.Activity(kind.Value),Is.EqualTo((string)entry["activity"]));
                Assert.That(RuleDocument.IsObjectEvent(kind.Value),Is.EqualTo((bool)entry["objectEvent"]));
            }
            Assert.That((string)BehaviourCatalog.Action("time.wait").InputSchema["properties"]["seconds"]["type"],Is.EqualTo("number"));
            Assert.That(BehaviourCatalog.Action("time.wait").InputSchema["properties"]["engineCode"],Is.Null);
        }
        [Test] public void NamedArgumentsRoundTripThroughEveryExistingNativeHandlerAdapter()
        {
            foreach(RuleActionKind kind in Enum.GetValues(typeof(RuleActionKind))) {
                var capability=BehaviourCatalog.Action(kind);
                var original=new RuleStep {action=LegacyCapabilityAdapters.Kind(capability.Id).Value,targetId=RuleDocument.IsInstant(LegacyCapabilityAdapters.Kind(capability.Id).Value)?Guid.NewGuid().ToString("N"):"maestro",seconds=LegacyCapabilityAdapters.Kind(capability.Id).Value==RuleActionKind.ThrowRecording||RuleDocument.IsInstant(LegacyCapabilityAdapters.Kind(capability.Id).Value)?0:1,
                    gesture=RuleGesture.Greeting,clipModelHash=new string('a',64),clipIndex=2,motionId=Guid.NewGuid().ToString("N")};
                if(LegacyCapabilityAdapters.Kind(capability.Id).Value==RuleActionKind.CreateRecipe) original.creationRecipe=RecipeTemplates.BoxRobot(true);
                Assert.That(LegacyCapabilityAdapters.TryCall(original,out var call,out var error),Is.True,error);
                var args=call.Arguments;
                Assert.That(BehaviourCatalog.TryInvocation(call.Definition.Id,1,args,out var step,out error),Is.True,call.Definition.Id+": "+error);
                Assert.That(step.action,Is.EqualTo(LegacyCapabilityAdapters.Kind(capability.Id).Value));Assert.That(step.seconds,Is.EqualTo(original.seconds));
                Assert.That(JToken.DeepEquals(CapabilityArguments.FromStep(step),CapabilityArguments.FromStep(original)),Is.True,capability.Id);
                Assert.That(BehaviourCatalog.TryInvocation(call.Definition.Id,2,args,out _,out _),Is.False);
                args["engineCode"]="anything";Assert.That(BehaviourCatalog.TryInvocation(call.Definition.Id,1,args,out _,out _),Is.False);
            }
            Assert.That(BehaviourCatalog.TryInvocation("unknown.action",1,new JObject(),out _,out _),Is.False);
        }
        [Test] public void CapabilityArgumentsUseNamedGesturesAndPreserveDomainPropValidation()
        {
            var step=new RuleStep {action=RuleActionKind.Gesture,seconds=2,gesture=RuleGesture.Pointing,propId=Guid.NewGuid().ToString("N"),propHand=PropHand.Left,
                propRelease=PropRelease.Throw,propReleaseAt=.5f,propOffset=new Vector3(.1f,.2f,0),propRotation=Quaternion.identity};
            Assert.That(LegacyCapabilityAdapters.TryCall(step,out var call,out _),Is.True);var args=call.Arguments;Assert.That((string)args["source"]["gesture"],Is.EqualTo("pointing"));
            Assert.That(BehaviourCatalog.TryInvocation("animation.play",1,args,out var restored,out var error),Is.True,error);
            Assert.That(restored.propId,Is.EqualTo(step.propId));Assert.That(restored.propHand,Is.EqualTo(PropHand.Left));Assert.That(restored.propRelease,Is.EqualTo(PropRelease.Throw));Assert.That(restored.propOffset,Is.EqualTo(step.propOffset));
            args["source"]["gesture"]=1;Assert.That(BehaviourCatalog.TryInvocation("animation.play",1,args,out _,out _),Is.False);args["source"]["gesture"]="greeting";
            args["prop"]["offset"]=JObject.Parse("{\"x\":1,\"y\":1,\"z\":1}");
            Assert.That(BehaviourCatalog.TryInvocation("animation.play",1,args,out _,out _),Is.False,"Component bounds do not replace the native distance constraint");
            args.Remove("prop");args["seconds"]="2";Assert.That(BehaviourCatalog.TryInvocation("animation.play",1,args,out _,out _),Is.False);
            args["seconds"]=2;args["target"]="book";Assert.That(BehaviourCatalog.TryInvocation("animation.play",1,args,out _,out _),Is.False);
        }
        [Test] public void AReturnedSchemaCannotWeakenTheRegisteredArgumentContract()
        {
            var definition=BehaviourCatalog.Action("time.wait");var schema=definition.InputSchema;
            schema["properties"]["seconds"]["maximum"]=1000000;
            Assert.That(BehaviourCatalog.TryInvocation("time.wait",1,new JObject {["seconds"]=31},out _,out _),Is.False);
            Assert.That(BehaviourCatalog.TryInvocation("time.wait",1,new JObject {["seconds"]=.1},out _,out _),Is.True);
            Assert.That(BehaviourCatalog.TryInvocation("time.wait",1,new JObject {["seconds"]=double.NaN},out _,out _),Is.False);
            Assert.That(BehaviourCatalog.TryInvocation("time.wait",1,new JObject(),out _,out _),Is.False);
        }
    }
}

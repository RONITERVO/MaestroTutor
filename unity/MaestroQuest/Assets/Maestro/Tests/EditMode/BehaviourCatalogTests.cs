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
            var pages=new System.Collections.Generic.List<JObject>{first,second};
            while(pages.Sum(x=>x["entries"].Count())<(int)first["total"])pages.Add(Query(new JObject {["operation"]="search",["category"]="events",["query"]="",["offset"]=pages.Sum(x=>x["entries"].Count())}.ToString()));
            var ids=pages.SelectMany(x=>x["entries"]).Select(x=>(string)x["id"]).ToArray();
            Assert.That(ids,Is.EqualTo(BehaviourCatalog.Events.Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal)));
            pages.Last()["entries"][0]["label"]="Changed by caller";
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
        [Test] public void CurrentInputMappingsReferenceRegisteredFactFieldsAndActionGuards()
        {
            int count=0;
            foreach(var action in BehaviourCatalog.Actions)foreach(var schema in action.InputSchema.DescendantsAndSelf().OfType<JObject>()){
                if(schema["x-current"] is not JObject mapping)continue;count++;
                var fact=BehaviourCatalog.Fact((string)mapping["fact"]);Assert.That(fact,Is.Not.Null,action.Id);
                Assert.That((int)mapping["version"],Is.EqualTo(fact.Version));
                var props=(JObject)schema["properties"];var args=(JObject)mapping["arguments"];
                foreach(var entry in args.Properties()){
                    Assert.That(props.ContainsKey((string)entry.Value),Is.True);
                    Assert.That(fact.Input?["properties"]?[entry.Name],Is.Not.Null);
                }
                foreach(var required in (JArray)(fact.Input?["required"]??new JArray()))Assert.That(args.ContainsKey((string)required),Is.True);
                foreach(var field in ((JObject)mapping["fields"]).Properties()){
                    Assert.That(props.ContainsKey(field.Name),Is.True);JToken type=fact.ToJson()["type"];
                    Assert.That(field.Value.Count(),Is.InRange(1,4));
                    foreach(var part in (JArray)field.Value)type=type?["record"]?[(string)part];
                    Assert.That(AcceptsCurrentType((JObject)props[field.Name],type),Is.True,action.Id+"."+field.Name);
                    Assert.That(args.Properties().Any(x=>(string)x.Value==field.Name),Is.False,"A query cannot overwrite its input target");
                }
                foreach(var guard in (JArray)mapping["guards"])Assert.That(mapping["fields"][(string)guard],Is.Not.Null);
            }
            Assert.That(count,Is.EqualTo(82));
        }
        static bool AcceptsCurrentType(JObject schema,JToken type,int depth=0){
            if(schema==null||type==null||depth>4||schema["oneOf"]!=null||(bool?)schema["x-static"]==true)return false;
            string expected=(string)schema["type"];if(expected=="integer")expected="number";if(expected=="string")expected="text";
            if(type.Type==JTokenType.String)return (string)type==expected;
            if(type["list"]!=null)return expected=="array"&&AcceptsCurrentType(schema["items"] as JObject,type["list"],depth+1);
            return expected=="object"&&schema["properties"] is JObject fields&&type["record"] is JObject record&&fields.Count==record.Count&&record.Properties().All(p=>AcceptsCurrentType(fields[p.Name] as JObject,p.Value,depth+1));
        }
        sealed class ChangingCurrentInputs:IProgramFacts
        {
            public int Reads;
            public bool TryRead(string name,out ProgramValue value){
                Reads++;var source=JObject.Parse("{\"revision\":101,\"distance\":1.8,\"speed\":0.4,\"temporary\":false,\"live\":{\"active\":false,\"mode\":\"stopped\",\"status\":\"Ready\",\"canLook\":true,\"lookReason\":\"\",\"canFollow\":true,\"followReason\":\"\"}}");
                source["revision"]=100+Reads;source["distance"]=1.7+Reads*.1;value=ProgramValue.Literal(source,BehaviourCatalog.Fact(name).Type);return true;
            }
        }
        [Test] public void BookGeneratedCurrentInputsReadOnceAndNeverFallBackWhenUnavailable()
        {
            var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/current-input-program.json"));
            Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);
            var facts=new ChangingCurrentInputs();var machine=new ProgramMachine(program,facts);
            Assert.That(machine.Advance(out var action,256),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(facts.Reads,Is.EqualTo(1));
            Assert.That(action.Definition.Id,Is.EqualTo("avatar.movement.configure"));Assert.That((int)action.Arguments["revision"],Is.EqualTo(101));
            Assert.That((double)action.Arguments["distance"],Is.EqualTo(1.8).Within(.0001));Assert.That((double)action.Arguments["speed"],Is.EqualTo(.9));
            var unavailable=new ProgramMachine(program,null);Assert.That(unavailable.Advance(out var missing,256),Is.EqualTo(ProgramYield.Failed));Assert.That(missing,Is.Null);Assert.That(unavailable.Error,Does.Contain("fact unavailable"));
        }
        sealed class MemberFacts:IProgramFacts,IProgramFactQueries {
            public int Reads;public bool MissingSecond;
            public bool TryRead(string name,out ProgramValue value){value=default;return false;}
            public bool TryRead(string name,int version,JObject args,out ProgramValue value){
                Reads++;Assert.That(name,Is.EqualTo("object.definition"));Assert.That(version,Is.EqualTo(1));
                Assert.That((string)args["target"],Is.EqualTo(new string(Reads==1?'a':'b',32)));
                var record=JObject.Parse("{\"target\":\"\",\"revision\":1,\"kind\":\"block\",\"name\":\"Part\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":1,\"content\":{\"points\":0,\"parts\":0,\"frames\":0,\"modelHash\":\"\",\"recipePlaying\":false}}");
                record["target"]=args["target"].DeepClone();record["revision"]=100+Reads;value=ProgramValue.Literal(record,BehaviourCatalog.Fact(name).Type);return !MissingSecond||Reads!=2;
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void BookGeneratedMemberReadsBindCurrentGuardsOrFailBeforeAnyAction(bool missing) {
            var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/current-members-program.json"));
            Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);var facts=new MemberFacts {MissingSecond=missing};var machine=new ProgramMachine(program,facts);
            Assert.That(machine.Advance(out var action,256),Is.EqualTo(missing?ProgramYield.Failed:ProgramYield.Action),machine.Error);Assert.That(facts.Reads,Is.EqualTo(2));
            if(missing){Assert.That(action,Is.Null);Assert.That(machine.Error,Does.Contain("fact unavailable"));return;}
            Assert.That((int)action.Arguments["members"][0]["revision"],Is.EqualTo(101));Assert.That((int)action.Arguments["members"][1]["revision"],Is.EqualTo(102));
            Assert.That(action.Resources,Is.EquivalentTo(new[]{new string('a',32),new string('b',32)}));Assert.That((int)JObject.Parse(source)["functions"][0]["body"][2]["arguments"]["members"][0]["revision"],Is.EqualTo(1));
        }
        [Test] public void IndexedBindingsRejectInvalidPathsAndDoNotShiftOrAuthorizeOtherMembers() {
            var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/current-members-program.json")));
            var action=(JObject)source["functions"][0]["body"][2];var args=(JObject)action["arguments"];var schema=BehaviourCatalog.Action((string)action["capability"]).InputSchema;
            foreach(string path in new[]{"members.01.target","members.-1.target","members.2.target","members.0.unknown","members.1e0.target","members.0.target.x"})Assert.That(CapabilitySchema.BindingType(schema,path,args),Is.Null,path);
            var copy=(JObject)args.DeepClone();CapabilitySchema.Remove(copy,"members.0");Assert.That(copy["members"].Count(),Is.EqualTo(2));Assert.That((string)copy["members"][1]["target"],Is.EqualTo(new string('b',32)));
            Assert.Throws<ProgramFault>(()=>CapabilitySchema.Set(copy,"members.2.revision",new JValue(2)));
            // A bound literal placeholder does not grant authority to its computed replacement.
            action["bindings"]["members.0.target"]=new JObject {["value"]=new string('c',32)};
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);var machine=new ProgramMachine(program,new MemberFacts());
            Assert.That(machine.Advance(out var call,256),Is.EqualTo(ProgramYield.Failed));Assert.That(call,Is.Null);Assert.That(machine.Error,Does.Contain("declared or created"));
            action["bindings"]["members.0"]=new JObject {["value"]=args["members"][0].DeepClone()};Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
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
            Assert.That(ready.Type.Kind,Is.EqualTo(ProgramType.Boolean));Assert.That(ready.Boolean,Is.False);
            Assert.That(BehaviourCatalog.TryRead("maestro.state",new BehaviourCatalog.FactContext("speaking"),out var state),Is.True);
            Assert.That(state.Text,Is.EqualTo("speaking"));
        }
        [Test] public void ExportAndNativeValidationShareTheRegisteredTypesAndEventSemantics()
        {
            var manifest=BehaviourCatalog.Manifest();
            Assert.That(manifest["facts"].Count(),Is.EqualTo(BehaviourProgram.Facts.Count));
            foreach(var entry in manifest["facts"]) Assert.That(BehaviourProgram.Facts[(string)entry["id"]],Is.EqualTo(ProgramDataType.Read(entry["type"])));
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

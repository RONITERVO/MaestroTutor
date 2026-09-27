// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class BehaviourCatalogTests
    {
        [Test] public void NativeDefinitionsEqualTheCommittedWebManifest()
        {
            string path=Environment.GetEnvironmentVariable("MAESTRO_BEHAVIOUR_CATALOG");
            if(string.IsNullOrEmpty(path))path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../shared/generated/behaviourCatalog.json"));
            Assert.That(File.Exists(path),Is.True,"Run Verify-Quest with the shared catalog path, or open the source project.");
            var expected=JObject.Parse(File.ReadAllText(path));expected.Remove("sources");
            // Compare the serialized contract: a nullable C# string becomes JSON null.
            var actual=JObject.Parse(BehaviourCatalog.Manifest().ToString());
            Assert.That(JToken.DeepEquals(actual,expected),Is.True,"Regenerate the reviewed manifest from native registrations.");
            Assert.That(BehaviourCatalog.Actions.All(x=>x.Duration=="timed"),Is.True);
            Assert.That(BehaviourCatalog.Actions.Where(x=>x.Kind!=RuleActionKind.Wait).All(x=>x.Channels.SequenceEqual(new[] {"wholeTarget"})),Is.True);
        }
        [Test] public void EveryExistingAdapterHasExactlyOneStableRegistration()
        {
            Assert.That(BehaviourCatalog.Actions.Select(x=>x.Kind),Is.EquivalentTo(Enum.GetValues(typeof(RuleActionKind))));
            Assert.That(BehaviourCatalog.Events.Select(x=>x.Kind),Is.EquivalentTo(Enum.GetValues(typeof(RuleEventKind))));
            var ids=BehaviourCatalog.Actions.Select(x=>x.Id).Concat(BehaviourCatalog.Events.Select(x=>x.Id)).Concat(BehaviourCatalog.Facts.Select(x=>x.Id)).ToArray();
            Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length));
            Assert.That(ids.All(id=>System.Text.RegularExpressions.Regex.IsMatch(id,@"^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$")),Is.True);
            Assert.That(BehaviourCatalog.HasAction((RuleActionKind)999),Is.False);
            Assert.That(BehaviourCatalog.Event((RuleEventKind)999),Is.Null);
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
                Assert.That(RuleDocument.Activity(kind),Is.EqualTo((string)entry["activity"]));
                Assert.That(RuleDocument.IsObjectEvent(kind),Is.EqualTo((bool)entry["objectEvent"]));
            }
            Assert.That((string)BehaviourCatalog.Action("time.wait").InputSchema["properties"]["seconds"]["type"],Is.EqualTo("number"));
            Assert.That(BehaviourCatalog.Action("time.wait").InputSchema["properties"]["engineCode"],Is.Null);
        }
        [Test] public void NamedArgumentsRoundTripThroughEveryExistingNativeHandlerAdapter()
        {
            foreach(var capability in BehaviourCatalog.Actions) {
                var original=new RuleStep {action=capability.Kind,targetId="maestro",seconds=capability.Kind==RuleActionKind.ThrowRecording?0:1,
                    gesture=RuleGesture.Greeting,clipModelHash=new string('a',64),clipIndex=2,motionId=Guid.NewGuid().ToString("N")};
                var args=CapabilityArguments.FromStep(original);
                Assert.That(BehaviourCatalog.TryInvocation(capability.Id,1,args,out var step,out var error),Is.True,capability.Id+": "+error);
                Assert.That(step.action,Is.EqualTo(capability.Kind));Assert.That(step.seconds,Is.EqualTo(original.seconds));
                Assert.That(JToken.DeepEquals(CapabilityArguments.FromStep(step),args),Is.True,capability.Id);
                Assert.That(BehaviourCatalog.TryInvocation(capability.Id,2,args,out _,out _),Is.False);
                args["engineCode"]="anything";Assert.That(BehaviourCatalog.TryInvocation(capability.Id,1,args,out _,out _),Is.False);
            }
            Assert.That(BehaviourCatalog.TryInvocation("unknown.action",1,new JObject(),out _,out _),Is.False);
        }
        [Test] public void CapabilityArgumentsUseNamedGesturesAndPreserveDomainPropValidation()
        {
            var step=new RuleStep {action=RuleActionKind.Gesture,seconds=2,gesture=RuleGesture.Pointing,propId=Guid.NewGuid().ToString("N"),propHand=PropHand.Left,
                propRelease=PropRelease.Throw,propReleaseAt=.5f,propOffset=new Vector3(.1f,.2f,0),propRotation=Quaternion.identity};
            var args=CapabilityArguments.FromStep(step);Assert.That((string)args["gesture"],Is.EqualTo("pointing"));
            Assert.That(BehaviourCatalog.TryInvocation("avatar.gesture.play",1,args,out var restored,out var error),Is.True,error);
            Assert.That(restored.propId,Is.EqualTo(step.propId));Assert.That(restored.propHand,Is.EqualTo(PropHand.Left));Assert.That(restored.propRelease,Is.EqualTo(PropRelease.Throw));Assert.That(restored.propOffset,Is.EqualTo(step.propOffset));
            args["gesture"]=1;Assert.That(BehaviourCatalog.TryInvocation("avatar.gesture.play",1,args,out _,out _),Is.False);args["gesture"]="greeting";
            args["prop"]["offset"]=JObject.Parse("{\"x\":1,\"y\":1,\"z\":1}");
            Assert.That(BehaviourCatalog.TryInvocation("avatar.gesture.play",1,args,out _,out _),Is.False,"Component bounds do not replace the native distance constraint");
            args.Remove("prop");args["seconds"]="2";Assert.That(BehaviourCatalog.TryInvocation("avatar.gesture.play",1,args,out _,out _),Is.False);
            args["seconds"]=2;args["target"]="book";Assert.That(BehaviourCatalog.TryInvocation("avatar.gesture.play",1,args,out _,out _),Is.False);
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

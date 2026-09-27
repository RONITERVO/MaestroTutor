// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class BehaviourProgramTests
    {


        [Test] public void RecipeCreationContractChecksHierarchyTracksCollectionsAndDetachedExamples()
        {
            var definition=BehaviourCatalog.Action("object.create");var args=((JObject)definition.InputSchema["oneOf"][1]["examples"][0]);
            var call=new JObject {["id"]=definition.Id,["version"]=1,["arguments"]=args};
            Assert.That(Maestro.Quest.Creation.RoomCapabilityCatalog.ValidCall(call),Is.True);
            Assert.That(BehaviourCatalog.TryInvocation(definition.Id,1,args,out var step,out var error),Is.True,error);
            Assert.That(step.creationRecipe.parts.Length,Is.GreaterThanOrEqualTo(17));Assert.That(step.creationRecipe.playing,Is.False);
            var copy=step.Copy();copy.creationRecipe.parts[0].size.x=1;
            Assert.That(step.creationRecipe.parts[0].size.x,Is.Not.EqualTo(1));
            Assert.That(CapabilityArguments.Resources(args,definition.InputSchema),Is.Empty);
            void Invalid(Action<JObject> change) {
                var value=((JObject)definition.InputSchema["oneOf"][1]["examples"][0]);change(value);
                Assert.That(BehaviourCatalog.TryInvocation(definition.Id,1,value,out _,out _),Is.False);
            }
            Invalid(x=>x["recipe"]["parts"][0]["parent"]="Head");
            Invalid(x=>x["recipe"]["parts"][1]["id"]=x["recipe"]["parts"][0]["id"].DeepClone());
            Invalid(x=>x["recipe"]["parts"][0]["color"]["a"]=.5);
            Invalid(x=>x["recipe"]["tracks"][0]["keys"][1]["time"]=0);
            Invalid(x=>x["recipe"]["tracks"][0]["part"]="missing");
            Invalid(x=>((JArray)x["recipe"]["tracks"][0]["keys"]).RemoveAt(0));
            Invalid(x=>x["recipe"]["parts"][0]["script"]="run code");
            Invalid(x=>x["recipe"]["parts"]=new JArray());
            Invalid(x=>x["recipe"]["duration"]=31);
            Invalid(x=>x["recipe"]["parts"][0]["position"]=new JObject {["x"]=2,["y"]=2,["z"]=2});
            Assert.That((string)((JObject)definition.InputSchema["oneOf"][1]["examples"][0])["recipe"]["parts"][0]["id"],Is.EqualTo("Hips"));
        }
        [Test] public void RecipeCreationProgramsRetainArraysAndUseReturnedIdForAnimation()
        {
            var definition=BehaviourCatalog.Action("object.create");
            var source=CreationProgram();source["functions"][0]["body"][0]["capability"]=definition.Id;
            source["functions"][0]["body"][0]["arguments"]=((JObject)definition.InputSchema["oneOf"][1]["examples"][0]);
            source["functions"][0]["locals"][0]["name"]="robot";source["functions"][0]["body"][0]["results"]["objectId"]="robot";
            var play=source["functions"][0]["body"][1];play["id"]="animate";play["capability"]="animation.play";play["bindings"]["target"]["var"]="robot";
            play["arguments"]=new JObject {["target"]=new string('0',32),["seconds"]=.6,["loop"]=true,["source"]=new JObject {["kind"]="recipe"},["channel"]="wholeTarget"};
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);
            var machine=new ProgramMachine(program,null);
            Assert.That(machine.Advance(out var create),Is.EqualTo(ProgramYield.Action));Assert.That(EditorStep(create).creationRecipe.tracks.Length,Is.EqualTo(2));
            string id=Guid.NewGuid().ToString("N");Assert.That(machine.CompleteAction(new JObject {["objectId"]=id},out error),Is.True,error);
            Assert.That(machine.Advance(out var animation),Is.EqualTo(ProgramYield.Action));Assert.That(EditorStep(animation).targetId,Is.EqualTo(id));Assert.That(EditorStep(animation).action,Is.EqualTo(RuleActionKind.RecipeAnimation));
            string output=Environment.GetEnvironmentVariable("MAESTRO_RECIPE_CREATION_EVIDENCE");
            if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"program.json"),source.ToString());}
        }

        [Test] public void ObjectEditsUseTypedScopedInstantContractsAndProtectBuiltins()
        {
            string target=Guid.NewGuid().ToString("N");
            foreach(var step in new[] {
                new RuleStep {action=RuleActionKind.MoveObject,targetId="book",editPosition=Vector3.up},
                new RuleStep {action=RuleActionKind.ResizeObject,targetId="maestro",editScale=.5f},
                new RuleStep {action=RuleActionKind.PaintObject,targetId=target,editColor=Color.red},
                new RuleStep {action=RuleActionKind.DeleteObject,targetId=target}
            }) {
                var definition=BehaviourCatalog.Actions.Single(x=>LegacyCapabilityAdapters.Kind(x.Id)==step.action);
                Assert.That(definition.Duration,Is.EqualTo("instant"));
                Assert.That(definition.Channels,Is.EqualTo(new[]{"wholeTarget"}));
                var args=CapabilityArguments.FromStep(step);
                Assert.That(BehaviourCatalog.TryInvocation(definition.Id,1,args,out var restored,out var error),Is.True,error);
                Assert.That(CapabilityArguments.Resources(args,definition.InputSchema),Is.EqualTo(new[]{step.targetId}));
                Assert.That(JToken.DeepEquals(args,CapabilityArguments.FromStep(restored)),Is.True);
                args["seconds"]=1;Assert.That(BehaviourCatalog.TryInvocation(definition.Id,1,args,out _,out _),Is.False);
            }
            Assert.That(BehaviourCatalog.TryInvocation("object.position.set",1,new JObject {["target"]=target,["x"]=25,["y"]=25,["z"]=25},out _,out _),Is.False);
            Assert.That(BehaviourCatalog.TryInvocation("object.delete",1,new JObject {["target"]="maestro"},out _,out _),Is.False);
            Assert.That(BehaviourCatalog.TryInvocation("object.color.set",1,new JObject {["target"]="book",["red"]=1,["green"]=0,["blue"]=0},out _,out _),Is.False);
        }

        static JObject CreationProgram()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json")));
        [Test] public void TypedNativeResultsAuthorizeOnlyCreatedObjectsAndNeverFlattenToSimpleSteps()
        {
            var json=CreationProgram();Assert.That(BehaviourProgram.TryParse(json.ToString(),out var program,out var error),Is.True,error);
            Assert.That(program.SimpleSteps(),Is.Null);
            var machine=new ProgramMachine(program,null);
            Assert.That(machine.Advance(out var create),Is.EqualTo(ProgramYield.Action));Assert.That(EditorStep(create).action,Is.EqualTo(RuleActionKind.CreatePrimitive));
            Assert.That(machine.CompleteAction(new JObject {["objectId"]="maestro"},out error),Is.False,"A builtin cannot be invented by a creation result");
            string id=Guid.NewGuid().ToString("N");Assert.That(machine.CompleteAction(new JObject {["objectId"]=id},out error),Is.True,error);
            Assert.That(machine.Locals["ball"].Text,Is.EqualTo(id));
            Assert.That(machine.Advance(out var push),Is.EqualTo(ProgramYield.Action));Assert.That(EditorStep(push).targetId,Is.EqualTo(id));
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));
            json=CreationProgram();json["functions"][0]["body"][1]["bindings"]["target"]=new JObject {["value"]=Guid.NewGuid().ToString("N")};
            machine=new ProgramMachine(Compile(json.ToString()),null);machine.Advance(out _);Assert.That(machine.CompleteAction(new JObject {["objectId"]=id},out error),Is.True,error);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("declared or created"));
            json=CreationProgram();json["functions"][0]["locals"][0]["initial"]=0;Assert.That(BehaviourProgram.TryParse(json.ToString(),out _,out _),Is.False);
            json=CreationProgram();json["functions"][0]["body"][0]["results"]["unknown"]="ball";Assert.That(BehaviourProgram.TryParse(json.ToString(),out _,out _),Is.False);
            json=CreationProgram();json["version"]=2;((JObject)json).Remove("state");((JObject)json).Remove("events");Assert.That(BehaviourProgram.TryParse(json.ToString(),out _,out _),Is.False);
        }
        [Test] public void CreationLimitStopsBeforeASeventeenthEffectAndDoesNotRenewInstructionBudget()
        {
            var json=CreationProgram();var create=json["functions"][0]["body"][0].DeepClone();
            json["functions"][0]["body"]=new JArray(new JObject {["id"]="loop",["op"]="forever",["body"]=new JArray(create)});
            var machine=new ProgramMachine(Compile(json.ToString()),null);int previous=0;
            for(int i=0;i<16;i++) {
                Assert.That(machine.Advance(out var action),Is.EqualTo(ProgramYield.Action));Assert.That(EditorStep(action).action,Is.EqualTo(RuleActionKind.CreatePrimitive));
                Assert.That(machine.Instructions,Is.GreaterThan(previous));previous=machine.Instructions;
                Assert.That(machine.CompleteAction(new JObject {["objectId"]=Guid.NewGuid().ToString("N")},out var error),Is.True,error);
            }
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("16 created"));
        }

        [Test] public void SchemaResourcesAndDomainConstraintsMatchNativeHandlers()
        {
            foreach(RuleActionKind kind in Enum.GetValues(typeof(RuleActionKind))) {
                var step=new RuleStep {action=kind,targetId=RuleDocument.IsInstant(kind)?Guid.NewGuid().ToString("N"):"maestro",seconds=kind==RuleActionKind.ThrowRecording||RuleDocument.IsInstant(kind)?0:1};
                if(kind==RuleActionKind.CreateRecipe)step.creationRecipe=Maestro.Quest.Creation.RecipeTemplates.BoxRobot(true);
                void Check() {
                    var schema=CapabilityArguments.Schema(kind);var args=CapabilityArguments.FromStep(step);
                    Assert.That(CapabilityArguments.Validate(args,schema,out var error),Is.True,error);
                    Assert.That(CapabilityArguments.Resources(args,schema),Is.EquivalentTo(RuleDocument.Targets(step)));
                }
                Check();
                if(kind==RuleActionKind.RecordedAnimation||kind==RuleActionKind.Gesture||kind==RuleActionKind.ImportedClip||kind==RuleActionKind.LibraryMotion) {
                    step.propId=Guid.NewGuid().ToString("N");Check();
                    var args=CapabilityArguments.FromStep(step);var schema=CapabilityArguments.Schema(kind);
                    args["prop"]["rotation"]["w"]=0;Assert.That(CapabilityArguments.Validate(args,schema,out _),Is.False);
                    args=CapabilityArguments.FromStep(step);args["prop"]["offset"]=new JObject {["x"]=1,["y"]=1,["z"]=1};
                    Assert.That(CapabilityArguments.Validate(args,schema,out _),Is.False);
                    args=CapabilityArguments.FromStep(step);args["target"]="book";Assert.That(CapabilityArguments.Validate(args,schema,out _),Is.False);
                }
            }
        }
        [Test] public void CatalogRequestsPreserveStructuredArgumentsAndRejectAmbiguousFields()
        {
            var query=new JObject {["operation"]="check",["call"]=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=1}}};
            var command=new JObject {["action"]="catalog",["catalog"]=query};
            var wire=new JObject {["version"]=2,["commands"]=new JArray(command)};
            Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(wire.ToString()),Is.True);
            var request=JsonUtility.FromJson<Maestro.Quest.Creation.RoomAgentRequest>(wire.ToString());
            Assert.That(request.commands[0].catalog,Is.Null);
            Assert.That(Maestro.Quest.Creation.RoomAgentWire.PopulateStructured(request,wire),Is.True);
            Assert.That(JToken.DeepEquals(request.commands[0].catalog,query),Is.True);
            request.commands[0].catalog["call"]["arguments"]["seconds"]=2;
            Assert.That((int)query["call"]["arguments"]["seconds"],Is.EqualTo(1),"Hydration must be detached");
            command["action"]="play";Assert.That(Maestro.Quest.Creation.RoomCapabilityCatalog.ValidWire(command),Is.False);command["action"]="catalog";
            command["catalog"]["extra"]=true;Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(wire.ToString()),Is.False);((JObject)command["catalog"]).Remove("extra");
            ((JArray)wire["commands"]).Add(new JObject {["action"]="stop",["target"]="maestro"});Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(wire.ToString()),Is.False);
            var tooDeep=new JObject();var cursor=tooDeep;for(int i=0;i<14;i++){var next=new JObject();cursor["nested"]=next;cursor=next;}
            query["call"]["arguments"]=tooDeep;Assert.That(Maestro.Quest.Creation.RoomCapabilityCatalog.ValidRequest(query),Is.False);
        }
        [Test] public void OneOffWireRequiresExactOperationsAndStructuredCalls()
        {
            var command=new JObject {["action"]="execution",["execution"]=new JObject {["operation"]="start",["call"]=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=1}}}};
            var raw=new JObject {["version"]=2,["commands"]=new JArray(command)};
            Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(raw.ToString()),Is.True);
            var typed=JsonUtility.FromJson<Maestro.Quest.Creation.RoomAgentRequest>(raw.ToString());
            Assert.That(Maestro.Quest.Creation.RoomAgentWire.PopulateStructured(typed,raw),Is.True);
            Assert.That(JToken.DeepEquals(typed.commands[0].execution,command["execution"]),Is.True);
            command["execution"]["runId"]=Guid.NewGuid().ToString("N");Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(raw.ToString()),Is.True);
            command["execution"]["runId"]="invalid";Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(raw.ToString()),Is.False);
            command["execution"]=new JObject {["operation"]="cancel",["runId"]="bad"};Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(raw.ToString()),Is.False);
            command["execution"]["runId"]=Guid.NewGuid().ToString("N");Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(raw.ToString()),Is.True);
            command["execution"]["operation"]=new JObject();Assert.That(Maestro.Quest.Creation.RoomExecutions.ValidRequest((JObject)command["execution"]),Is.False);
            command["execution"]=new JObject {["operation"]="inspect",["runId"]=Guid.NewGuid().ToString("N")};
            ((JArray)raw["commands"]).Add(new JObject {["action"]="stop",["target"]="book"});Assert.That(Maestro.Quest.Creation.RoomControls.ValidWire(raw.ToString()),Is.False);
        }
        static RuleStep EditorStep(CapabilityCall call) {Assert.That(call.TryStep(out var step,out var error),Is.True,error);return step;}
        static string Example()=>File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-prime.json"));
        static BehaviourProgram Compile(string source) {Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);return program;}
        sealed class Facts:IProgramFacts {public bool TryRead(string name,out ProgramValue value) {value=new ProgramValue("speaking");return name=="maestro.state";}}
        [TestCase(1,RuleGesture.Idle)] [TestCase(2,RuleGesture.Greeting)] [TestCase(7,RuleGesture.Greeting)] [TestCase(12,RuleGesture.Idle)]
        public void FunctionsReturnThroughBranchesAndLoopsToTheSameNativeActions(int candidate,RuleGesture expected)
        {
            var json=JObject.Parse(Example());json["functions"][0]["body"][0]["args"][0]["value"]=candidate;
            var program=Compile(json.ToString());var machine=new ProgramMachine(program,new Facts());
            ProgramYield result;CapabilityCall action;int ticks=0;
            do {result=machine.Advance(out action,4);Assert.That(++ticks,Is.LessThan(100));}while(result==ProgramYield.Yield);
            Assert.That(result,Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(EditorStep(action).gesture,Is.EqualTo(expected));Assert.That(action.NodeId,Is.EqualTo(machine.NodeId));
            Assert.That(machine.NodeId,Is.EqualTo(expected==RuleGesture.Greeting?"prime_wave":"composite_idle"));Assert.That(machine.Function,Is.EqualTo("main"));
            Assert.That(machine.Locals["answer"].Boolean,Is.EqualTo(expected==RuleGesture.Greeting));Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed),"A completed program is never replayed");
        }
        sealed class Actions:IRuleActions {
            public int Starts,Stops;public RuleGesture Gesture;
            public bool CanRun(CapabilityCall step,out string error) {error=null;return true;}
            public bool Start(string run,CapabilityCall invocation,out float seconds,out string error) { invocation.TryStep(out var step,out _);Starts++;Gesture=step.gesture;seconds=step.seconds;error=null;return true;}
            public void Stop(string run,bool preserve) {Stops++;}
        }
        [Test] public void ProgramUsesExistingTriggerOwnershipStopAndCompletionInsteadOfAnotherRuntime()
        {
            string id=Guid.NewGuid().ToString("N");var sequence=new RuleSequence {id=id,name="Prime check",program=Example()};
            var document=new RuleDocument {sequences=new[] {sequence},bindings=new[] {new RuleBinding {id=Guid.NewGuid().ToString("N"),sequenceId=id,trigger=RuleEventKind.Speaking}}};
            Assert.That(document.Validate(out var error),Is.True,error);var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.Configure(document);
            scheduler.SetActivity("idle",0);scheduler.SetActivity("speaking",1);for(int i=0;i<20&&actions.Starts==0;i++)scheduler.Tick(1+i*.01f);
            Assert.That(actions.Starts,Is.EqualTo(1));Assert.That(actions.Gesture,Is.EqualTo(RuleGesture.Greeting));
            var view=scheduler.ObserveRuns().Single();Assert.That(view.nodeId,Is.EqualTo("prime_wave"));Assert.That(view.locals.Single(x=>x.name=="answer").value,Is.EqualTo("True"));
            scheduler.Tick(3);Assert.That(scheduler.RunningCount,Is.Zero);Assert.That(scheduler.Outcomes.Last().phase,Is.EqualTo("completed"));
            Assert.That(scheduler.Trigger(id,4),Is.True);scheduler.StopTarget("maestro",true);Assert.That(scheduler.Outcomes.Last().phase,Is.EqualTo("cancelled"));
            int before=actions.Starts;scheduler.Tick(100);Assert.That(actions.Starts,Is.EqualTo(before),"Stop never resumes program instructions");
        }
        [Test] public void ProgramSourceAndNodeIdsSurviveVersionedStorageAndBackup()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroProgram-"+Guid.NewGuid().ToString("N"));
            try {
                var document=new RuleDocument {sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Prime",program=Example()}}};
                var storage=new RuleStorage(directory);Assert.That(storage.Save(document,out var error),Is.True,error);
                var loaded=new RuleStorage(directory).Load(out error);Assert.That(loaded.version,Is.EqualTo(2));Assert.That(loaded.sequences[0].program,Is.EqualTo(Example()));
                loaded.sequences[0].name="Renamed";Assert.That(storage.Save(loaded,out error),Is.True,error);File.WriteAllText(Path.Combine(directory,"behaviours.v2.json"),"broken");
                loaded=new RuleStorage(directory).Load(out error);Assert.That(error,Does.Contain("backup"));Assert.That(loaded.sequences[0].program,Is.EqualTo(Example()));
                File.WriteAllText(Path.Combine(directory,"behaviours.v2.json"),"{\"version\":6}");storage=new RuleStorage(directory);storage.Load(out _);Assert.That(storage.ReadOnly,Is.True);
            }finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        sealed class VisualFacts:IProgramFacts {
            readonly string activity;public VisualFacts(string activity){this.activity=activity;}
            public bool TryRead(string name,out ProgramValue value){value=new ProgramValue(activity);return name=="maestro.state";}
        }
        [TestCase("speaking",2)] [TestCase("idle",1)]
        public void VisuallyAuthoredBranchFixtureExecutesTheExactNestedActions(string activity,int expected)
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-visual.json"));
            var machine=new ProgramMachine(Compile(source),new VisualFacts(activity));int actions=0,ticks=0;ProgramYield result;
            do {
                result=machine.Advance(out var action,4);Assert.That(++ticks,Is.LessThan(50),machine.Error);
                if(result==ProgramYield.Action){
                    actions++;Assert.That(action.NodeId,Is.EqualTo(activity=="speaking"?"block_3":"block_4"));
                    Assert.That(EditorStep(action).action,Is.EqualTo(activity=="speaking"?RuleActionKind.Gesture:RuleActionKind.Wait));
                    if(activity=="speaking"){Assert.That(EditorStep(action).gesture,Is.EqualTo(RuleGesture.Greeting));Assert.That(EditorStep(action).seconds,Is.EqualTo(.2f));}
                    else Assert.That(EditorStep(action).seconds,Is.EqualTo(1));
                }
            }while(result==ProgramYield.Action||result==ProgramYield.Yield);
            Assert.That(result,Is.EqualTo(ProgramYield.Completed),machine.Error);Assert.That(actions,Is.EqualTo(expected));
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));
        }
        [Test] public void SharedWebAndNativeProgramFixturesAgree()
        {
            var fixtures=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-contract.json")));
            foreach(var item in fixtures["cases"])Assert.That(BehaviourProgram.TryParse((string)item["source"],out _,out var error),Is.EqualTo((bool)item["valid"]),(string)item["name"]+": "+error);
        }
        [Test] public void SimpleControlsEditOnlyTheCanonicalProgramAndPreserveBlockIdentities()
        {
            var step=new RuleStep {id="wave",action=RuleActionKind.Gesture,seconds=1};
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Wave",program=BehaviourProgram.FromSteps(step)};
            var original=sequence.program;var view=sequence.SimpleSteps();view[0].seconds=2;
            Assert.That(sequence.program,Is.EqualTo(original),"The editable view must be detached");
            var copy=sequence.Copy();copy.SetSimpleSteps(view);
            Assert.That(copy.SimpleSteps()[0].id,Is.EqualTo("wave"));Assert.That(copy.SimpleSteps()[0].seconds,Is.EqualTo(2));
            Assert.That(sequence.SimpleSteps()[0].seconds,Is.EqualTo(1));
            Assert.That(JObject.Parse(JsonUtility.ToJson(copy)).ContainsKey("steps"),Is.False);
            var machine=new ProgramMachine(copy.Compile(out _),null);Assert.That(machine.Advance(out var action),Is.EqualTo(ProgramYield.Action));
            Assert.That(EditorStep(action).seconds,Is.EqualTo(2));Assert.That(machine.NodeId,Is.EqualTo("wave"));
            Assert.That(Compile(Example()).SimpleSteps(),Is.Null);Assert.Throws<ArgumentException>(()=>Compile(Example()).WithSimpleSteps(view));
        }
        [Test] public void SimpleEditsUpdateResourceOwnershipWithoutFlatteningComplexPrograms()
        {
            var step=new RuleStep {action=RuleActionKind.RecordedAnimation,targetId="book",seconds=1};
            var root=JObject.Parse(BehaviourProgram.FromSteps(step));((JArray)root["resources"]).Add("maestro");
            root["entry"]="start";root["functions"][0]["name"]="start";
            var program=Compile(root.ToString());var view=program.SimpleSteps();view[0].targetId=Guid.NewGuid().ToString("N");
            var edited=Compile(program.WithSimpleSteps(view));Assert.That(edited.Entry,Is.EqualTo("start"));
            Assert.That(edited.Resources,Is.EquivalentTo(new[] {"maestro",view[0].targetId}));Assert.That(edited.SimpleSteps()[0].id,Is.EqualTo(step.id));
            root=JObject.Parse(BehaviourProgram.FromSteps(step));root["functions"][0]["body"][0]["bindings"]["seconds"]=JObject.Parse("{\"value\":2}");
            program=Compile(root.ToString());Assert.That(program.SimpleSteps(),Is.Null,"An expression must not be overwritten by the literal-only editor");
        }
        [Test] public void NativeWireRejectsLegacyMixedAndMistypedSavedPrograms()
        {
            var sequence=new RuleSequence {id="",name="Wait",program=BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.Wait,seconds=1})};
            var value=JObject.Parse(JsonUtility.ToJson(sequence));
            bool Wire()=>Maestro.Quest.Creation.RoomControls.ValidWire(new JObject {["commands"]=new JArray(new JObject {["action"]="rules",["rule"]=new JObject {["action"]="edit",["revision"]=1,["edits"]=new JArray(new JObject {["kind"]="save",["reference"]="wait",["sequence"]=value})}})}.ToString());
            Assert.That(Wire(),Is.True);value["steps"]=new JArray();Assert.That(Wire(),Is.False);value.Remove("steps");
            value.Remove("program");Assert.That(Wire(),Is.False);value["program"]=12;Assert.That(Wire(),Is.False);
        }
        [Test] public void NewerProgramVersionIsPreservedIndividuallyWithoutRollingBackToItsBackup()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroFutureProgram-"+Guid.NewGuid().ToString("N"));
            try {
                var document=new RuleDocument {sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Future",program=Example()}}};
                var storage=new RuleStorage(directory);Assert.That(storage.Save(document,out _),Is.True);Assert.That(storage.Save(document,out _),Is.True);
                var program=JObject.Parse(document.sequences[0].program);program["version"]=4;document.sequences[0].program=program.ToString();
                string path=Path.Combine(directory,"behaviours.v2.json"),future=JsonUtility.ToJson(document);File.WriteAllText(path,future);
                storage=new RuleStorage(directory);var loaded=storage.Load(out var message);Assert.That(storage.ReadOnly,Is.False,message);Assert.That(message,Does.Contain("unavailable"));
                Assert.That(loaded.sequences.Single().program,Is.EqualTo(document.sequences[0].program));
                Assert.That(loaded.ProgramError(loaded.sequences[0]),Is.Not.Empty);Assert.That(File.ReadAllText(path),Is.EqualTo(future));
                Assert.That(storage.Save(loaded,out var error),Is.True,error);
            }finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [TestCase("avatar.gesture.play",2)] [TestCase("future.capability",1)]
        public void NewerCapabilityContractDoesNotRollBackToAnOlderBackup(string capability,double version)
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroFutureCapability-"+Guid.NewGuid().ToString("N"));
            try {
                var document=new RuleDocument {sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Future",program=Example()}}};
                var storage=new RuleStorage(directory);Assert.That(storage.Save(document,out _),Is.True);Assert.That(storage.Save(document,out _),Is.True);
                var program=JObject.Parse(document.sequences[0].program);var node=program["functions"][0]["body"][1]["then"][0];
                node["capability"]=capability;node["version"]=version;document.sequences[0].program=program.ToString();
                string path=Path.Combine(directory,"behaviours.v2.json"),original=JsonUtility.ToJson(document);File.WriteAllText(path,original);
                storage=new RuleStorage(directory);var loaded=storage.Load(out _);Assert.That(storage.ReadOnly,Is.False);
                Assert.That(loaded.sequences.Single().program,Is.EqualTo(document.sequences[0].program));
                Assert.That(loaded.ProgramError(loaded.sequences[0]),Is.Not.Empty);Assert.That(File.ReadAllText(path),Is.EqualTo(original));
                Assert.That(storage.Save(loaded,out var error),Is.True,error);
            }finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        static RuleSequence Available(string name="Usable")=>new() {id=Guid.NewGuid().ToString("N"),name=name,program=BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.Gesture,seconds=1})};
        [TestCase("unknownCapability")] [TestCase("unknownVersion")] [TestCase("malformed")]
        public void UnavailableProgramDoesNotBlockOtherEditsTriggersOrUndo(string kind)
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroIsolated-"+Guid.NewGuid().ToString("N"));
            try {
                var good=Available();var bad=Available("Needs repair");var source=JObject.Parse(bad.program);
                if(kind=="unknownCapability")source["functions"][0]["body"][0]["capability"]="future.capability";
                if(kind=="unknownVersion")source["version"]=99;
                bad.program=kind=="malformed"?" { preserve incomplete source ":source.ToString();string original=bad.program;
                var document=new RuleDocument {sequences=new[]{good,bad},bindings=new[]{good,bad}.Select(x=>new RuleBinding {id=Guid.NewGuid().ToString("N"),sequenceId=x.id,trigger=RuleEventKind.Speaking}).ToArray()};
                Directory.CreateDirectory(directory);string path=Path.Combine(directory,"behaviours.v2.json");File.WriteAllText(path,JsonUtility.ToJson(document));
                var storage=new RuleStorage(directory);var loaded=storage.Load(out _);
                Assert.That(storage.ReadOnly,Is.False);Assert.That(loaded.Validate(out _,true),Is.True);Assert.That(loaded.Validate(out _),Is.False);
                var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.Configure(loaded);
                scheduler.SetActivity("idle",0);scheduler.SetActivity("speaking",1);
                Assert.That(actions.Starts,Is.EqualTo(1));Assert.That(scheduler.LastError,Is.Null);
                Assert.That(scheduler.Trigger(bad.id,2),Is.False);Assert.That(scheduler.RunningCount,Is.EqualTo(1),"Failed starts cannot interrupt a usable run");
                Assert.That(actions.Stops,Is.Zero);
                loaded.sequences[0].name="Edited valid";
                Assert.That(storage.Save(loaded,out var error),Is.True,error);
                Assert.That(new RuleStorage(directory).Load(out _).sequences[1].program,Is.EqualTo(original));
                var rejected=loaded.Copy();rejected.sequences[1].name="Changed invalid";Assert.That(storage.Save(rejected,out _),Is.False);
                rejected=loaded.Copy();rejected.sequences=rejected.sequences.Append(new RuleSequence {id=Guid.NewGuid().ToString("N"),name="New invalid",program=original}).ToArray();
                Assert.That(storage.Save(rejected,out _),Is.False);
                var repaired=loaded.Copy();repaired.sequences[1].program=good.program;
                Assert.That(repaired.sequences[1].Compile(out _),Is.Not.Null,"Repair must invalidate a cached failure");
                Assert.That(storage.Save(repaired,out error),Is.True,error);
                Assert.That(storage.Save(loaded,out error),Is.True,error,"Undo may restore a preserved original");
                storage.RetainsMotion(Guid.NewGuid().ToString("N"),out bool uncertain,true);Assert.That(uncertain,Is.True);
                Assert.That(bad.UsesMotion(Guid.NewGuid().ToString("N")),Is.True);
                Assert.That(storage.Save(new RuleDocument(),out error),Is.True,error,"Unavailable programs remain deletable");
                storage.RetainsMotion(Guid.NewGuid().ToString("N"),out uncertain,true);Assert.That(uncertain,Is.True,"Recovery backups still protect unknown references");
            }finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void EventTypeConflictsDisableOnlyAffectedProgramsAndCannotBeIntroducedByEdits()
        {
            var first=Available("Number event");var second=Available("Text event");var good=Available();
            var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-events.json")));
            first.program=source.ToString();source["events"][0]["type"]="text";second.program=source.ToString();
            var previous=new RuleDocument {sequences=new[]{first,good}};
            var conflict=new RuleDocument {sequences=new[]{first,second,good}};
            Assert.That(conflict.ValidateEdit(previous,out _),Is.False);
            Assert.That(conflict.ProgramError(first),Does.Contain("different payload"));Assert.That(conflict.ProgramError(second),Is.Not.Null);
            Assert.That(conflict.ProgramError(good),Is.Null);
            var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.Configure(conflict);
            Assert.That(scheduler.Trigger(first.id,0),Is.False);Assert.That(scheduler.Trigger(second.id,0),Is.False);Assert.That(scheduler.Trigger(good.id,0),Is.True);
            Assert.That(actions.Starts,Is.EqualTo(1));
            var repaired=conflict.Copy();repaired.sequences[1].program=first.program;Assert.That(repaired.ValidateEdit(conflict,out var error),Is.True,error);
        }
        [Test] public void CompatibilityIsolationDoesNotRelaxCollectionStructureOrLimits()
        {
            var good=Available();var document=new RuleDocument {sequences=new[]{good}};
            good.program="bad";Assert.That(document.Validate(out _,true),Is.True);
            good.program=new string('x',24001);Assert.That(document.Validate(out _,true),Is.False);
            good.program="bad";good.id="not-an-id";Assert.That(document.Validate(out _,true),Is.False);
            good.id=Guid.NewGuid().ToString("N");document.sequences=new[]{good,good.Copy()};Assert.That(document.Validate(out _,true),Is.False);
            document.sequences=new[]{good};document.buttons=new[]{new RuleButtonData {id=Guid.NewGuid().ToString("N"),sequenceId=Guid.NewGuid().ToString("N")}};
            Assert.That(document.Validate(out _,true),Is.False);
        }
        [TestCase("greeting",true)] [TestCase("unknown",false)]
        public void ComputedNamedGesturesAreValidatedBeforeTheyReachTheHandler(string gesture,bool valid)
        {
            var program=JObject.Parse(BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.Gesture,seconds=1}));
            program["functions"][0]["body"][0]["bindings"]["source.gesture"]=new JObject {["value"]=gesture};
            var machine=new ProgramMachine(Compile(program.ToString()),null);
            Assert.That(machine.Advance(out var step),Is.EqualTo(valid?ProgramYield.Action:ProgramYield.Failed));
            if(valid)Assert.That(EditorStep(step).gesture,Is.EqualTo(RuleGesture.Greeting));else Assert.That(step,Is.Null);
        }
        [Test] public void ComputedTargetAndIndexMustMeetTheirContractAndResourceReservation()
        {
            var program=JObject.Parse(BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.ImportedClip,targetId="maestro",seconds=1}));
            var bindings=(JObject)program["functions"][0]["body"][0]["bindings"];
            bindings["target"]=new JObject {["value"]="book"};
            var machine=new ProgramMachine(Compile(program.ToString()),null);
            Assert.That(machine.Advance(out var step),Is.EqualTo(ProgramYield.Failed));Assert.That(step,Is.Null);
            ((JArray)program["resources"]).Add("book");
            machine=new ProgramMachine(Compile(program.ToString()),null);Assert.That(machine.Advance(out step),Is.EqualTo(ProgramYield.Action));Assert.That(EditorStep(step).targetId,Is.EqualTo("book"));
            bindings["source.clipIndex"]=new JObject {["value"]=.5};
            machine=new ProgramMachine(Compile(program.ToString()),null);Assert.That(machine.Advance(out step),Is.EqualTo(ProgramYield.Failed));Assert.That(step,Is.Null);
        }
        [Test] public void NewerCollectionFilenamesPreventRollbackSavingAndMotionRemoval()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroFutureFiles-"+Guid.NewGuid().ToString("N"));
            try {
                var document=new RuleDocument {sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Current",program=Example()}}};
                var storage=new RuleStorage(directory);Assert.That(storage.Save(document,out var error),Is.True,error);
                string current=Path.Combine(directory,"behaviours.v2.json"),original=File.ReadAllText(current);
                File.WriteAllText(Path.Combine(directory,"behaviours.v3.json.notes"),"unrelated notes");
                File.WriteAllText(Path.Combine(directory,"room.v999.json"),"another collection");
                storage=new RuleStorage(directory);Assert.That(storage.Load(out _).sequences.Length,Is.EqualTo(1));Assert.That(storage.ReadOnly,Is.False);
                foreach(string name in new[] {"behaviours.v3.json","behaviours.v3.json.backup","behaviours.v3.json.pending","behaviours.v3.json.unreadable","behaviours.v999999999999.json"}) {
                    // The newer writer may appear after this process has loaded.
                    var running=new RuleStorage(directory);running.Load(out _);
                    string path=Path.Combine(directory,name),future="Unknown newer format, preserve every byte";File.WriteAllText(path,future);
                    var reopened=new RuleStorage(directory);var loaded=reopened.Load(out var message);
                    Assert.That(reopened.ReadOnly,Is.True,name);Assert.That(loaded.sequences,Is.Empty,"Never roll back to the older primary or backup");
                    Assert.That(message,Does.Contain("different app version"));
                    Assert.That(running.Save(new RuleDocument(),out _),Is.False,name);Assert.That(running.ReadOnly,Is.True);
                    running.RetainsMotion(Guid.NewGuid().ToString("N"),out bool uncertain);Assert.That(uncertain,Is.True,"Unknown retained references protect downloads");
                    Assert.That(File.ReadAllText(current),Is.EqualTo(original));Assert.That(File.ReadAllText(path),Is.EqualTo(future));File.Delete(path);
                }
            }finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void ParserRejectsRecursiveMistypedUnknownAndUndeclaredPrograms()
        {
            void Invalid(Action<JObject> change) {var json=JObject.Parse(Example());change(json);Assert.That(BehaviourProgram.TryParse(json.ToString(),out _,out _),Is.False);}
            Invalid(x=>x["functions"][0]["body"][0]["function"]="main");
            Invalid(x=>x["functions"][0]["body"][0]["args"][0]["value"]="7");
            Invalid(x=>x["functions"][0]["body"][0]["op"]="eval");
            Invalid(x=>x["resources"]=new JArray());
            Invalid(x=>x["functions"][1]["body"][0]["id"]="call_prime");
            Invalid(x=>x["functions"][0]["body"][1]["test"]=JObject.Parse("{\"fact\":\"api.key\"}"));
            Invalid(x=>x["functions"][0]["body"][1]["then"][0]["arguments"]["seconds"]="0.1");
            Invalid(x=>x["functions"][0]["body"][1]["then"][0]["bindings"]["arbitraryField"]=JObject.Parse("{\"value\":1}"));
            Assert.That(BehaviourProgram.TryParse(Example()+"{}",out _,out _),Is.False);
            Assert.That(BehaviourProgram.TryParse(Example().Replace("\"version\": 2","\"version\": 2, \"version\": 2"),out _,out _),Is.False);
        }
        [Test] public void ShortCircuitSkipsUnavailableFactsAndInvalidComputedActionsNeverEscape()
        {
            var json=JObject.Parse(Example());json["functions"][0]["body"][1]["test"]=JObject.Parse("{\"op\":\"or\",\"args\":[{\"value\":true},{\"op\":\"eq\",\"args\":[{\"fact\":\"maestro.state\"},{\"value\":\"speaking\"}]}]}");
            var machine=new ProgramMachine(Compile(json.ToString()),null);Assert.That(machine.Advance(out var step,256),Is.EqualTo(ProgramYield.Action));Assert.That(EditorStep(step).gesture,Is.EqualTo(RuleGesture.Greeting));
            json["functions"][0]["body"][1]["then"][0]["bindings"]["seconds"]=JObject.Parse("{\"value\":100}");
            machine=new ProgramMachine(Compile(json.ToString()),null);Assert.That(machine.Advance(out step,256),Is.EqualTo(ProgramYield.Failed));Assert.That(step,Is.Null);
        }
        [Test] public void YieldedTraceKeepsTheNodeAndItsFunctionScopeTogether()
        {
            var source=JObject.Parse(Example());var owners=source["functions"].SelectMany(f=>((JObject)f).Descendants().OfType<JObject>().Where(n=>n.ContainsKey("id")).Select(n=>new {id=(string)n["id"],name=(string)f["name"]})).ToDictionary(x=>x.id,x=>x.name);
            var machine=new ProgramMachine(Compile(Example()),new Facts());ProgramYield result;int ticks=0;
            do {
                result=machine.Advance(out _,1);Assert.That(++ticks,Is.LessThan(300));
                if(machine.NodeId!=null)Assert.That(machine.Function,Is.EqualTo(owners[machine.NodeId]),"A frame unwind must not relabel the last executed node with its caller's scope");
            }while(result==ProgramYield.Yield||result==ProgramYield.Action);
            Assert.That(result,Is.EqualTo(ProgramYield.Completed));
        }
        [Test] public void NestedWorkYieldsAndTerminatesAtItsTotalInstructionBudget()
        {
            var json=JObject.Parse(Example());json["functions"]=new JArray(JObject.Parse("{\"name\":\"main\",\"returns\":\"void\",\"parameters\":[],\"locals\":[{\"name\":\"n\",\"initial\":0}],\"body\":[{\"id\":\"outer\",\"op\":\"repeat\",\"count\":{\"value\":10000},\"body\":[{\"id\":\"inner\",\"op\":\"repeat\",\"count\":{\"value\":10000},\"body\":[{\"id\":\"set\",\"op\":\"set\",\"variable\":\"n\",\"value\":{\"value\":1}}]}]}]}"));
            var machine=new ProgramMachine(Compile(json.ToString()),null);Assert.That(machine.Advance(out _,32),Is.EqualTo(ProgramYield.Yield));Assert.That(machine.Instructions,Is.InRange(32,34));
            ProgramYield result;int ticks=0;do {result=machine.Advance(out _,256);Assert.That(++ticks,Is.LessThan(300));}while(result==ProgramYield.Yield);
            Assert.That(result,Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("budget"));
        }
    }
}

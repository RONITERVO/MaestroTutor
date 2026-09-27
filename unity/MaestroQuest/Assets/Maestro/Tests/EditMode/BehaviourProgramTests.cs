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
        static string Example()=>File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-prime.json"));
        static BehaviourProgram Compile(string source) {Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);return program;}
        sealed class Facts:IProgramFacts {public bool TryRead(string name,out ProgramValue value) {value=new ProgramValue("speaking");return name=="maestro.state";}}
        [TestCase(1,RuleGesture.Idle)] [TestCase(2,RuleGesture.Greeting)] [TestCase(7,RuleGesture.Greeting)] [TestCase(12,RuleGesture.Idle)]
        public void FunctionsReturnThroughBranchesAndLoopsToTheSameNativeActions(int candidate,RuleGesture expected)
        {
            var json=JObject.Parse(Example());json["functions"][0]["body"][0]["args"][0]["value"]=candidate;
            var program=Compile(json.ToString());var machine=new ProgramMachine(program,new Facts());
            ProgramYield result;RuleStep action;int ticks=0;
            do {result=machine.Advance(out action,4);Assert.That(++ticks,Is.LessThan(100));}while(result==ProgramYield.Yield);
            Assert.That(result,Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(action.gesture,Is.EqualTo(expected));
            Assert.That(machine.NodeId,Is.EqualTo(expected==RuleGesture.Greeting?"prime_wave":"composite_idle"));Assert.That(machine.Function,Is.EqualTo("main"));
            Assert.That(machine.Locals["answer"].Boolean,Is.EqualTo(expected==RuleGesture.Greeting));Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed),"A completed program is never replayed");
        }
        sealed class Actions:IRuleActions {
            public int Starts,Stops;public RuleGesture Gesture;
            public bool CanRun(RuleStep step,out string error) {error=null;return true;}
            public bool Start(string run,RuleStep step,out float seconds,out string error) {Starts++;Gesture=step.gesture;seconds=step.seconds;error=null;return true;}
            public void Stop(string run,bool preserve) {Stops++;}
        }
        [Test] public void ProgramUsesExistingTriggerOwnershipStopAndCompletionInsteadOfAnotherRuntime()
        {
            string id=Guid.NewGuid().ToString("N");var sequence=new RuleSequence {id=id,name="Prime check",program=Example(),steps=Array.Empty<RuleStep>()};
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
                var document=new RuleDocument {sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Prime",program=Example(),steps=Array.Empty<RuleStep>()}}};
                var storage=new RuleStorage(directory);Assert.That(storage.Save(document,out var error),Is.True,error);
                var loaded=new RuleStorage(directory).Load(out error);Assert.That(loaded.version,Is.EqualTo(5));Assert.That(loaded.sequences[0].program,Is.EqualTo(Example()));
                loaded.sequences[0].name="Renamed";Assert.That(storage.Save(loaded,out error),Is.True,error);File.WriteAllText(Path.Combine(directory,"rules.v5.json"),"broken");
                loaded=new RuleStorage(directory).Load(out error);Assert.That(error,Does.Contain("backup"));Assert.That(loaded.sequences[0].program,Is.EqualTo(Example()));
                File.WriteAllText(Path.Combine(directory,"rules.v5.json"),"{\"version\":6}");storage=new RuleStorage(directory);storage.Load(out _);Assert.That(storage.ReadOnly,Is.True);
            }finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void SharedWebAndNativeProgramFixturesAgree()
        {
            var fixtures=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-contract.json")));
            foreach(var item in fixtures["cases"])Assert.That(BehaviourProgram.TryParse((string)item["source"],out _,out var error),Is.EqualTo((bool)item["valid"]),(string)item["name"]+": "+error);
        }
        [Test] public void VersionFourMigrationKeepsStableStepIdsAndOriginalFile()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroV4-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {
                var document=new RuleDocument {version=4,sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Wait",steps=new[] {new RuleStep {action=RuleActionKind.Wait,seconds=1}}}}};
                string original=JsonUtility.ToJson(document),path=Path.Combine(directory,"rules.v4.json");File.WriteAllText(path,original);
                var storage=new RuleStorage(directory);var loaded=storage.Load(out var error);Assert.That(loaded.version,Is.EqualTo(5),error);Assert.That(loaded.sequences[0].steps[0].id,Is.EqualTo(document.sequences[0].steps[0].id));
                Assert.That(storage.Save(loaded,out error),Is.True,error);Assert.That(File.ReadAllText(path),Is.EqualTo(original));
            }finally {Directory.Delete(directory,true);}
        }
        [Test] public void NewerProgramVersionIsPreservedReadOnlyWithoutRollingBackToItsBackup()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroFutureProgram-"+Guid.NewGuid().ToString("N"));
            try {
                var document=new RuleDocument {sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Future",program=Example()}}};
                var storage=new RuleStorage(directory);Assert.That(storage.Save(document,out _),Is.True);Assert.That(storage.Save(document,out _),Is.True);
                var program=JObject.Parse(document.sequences[0].program);program["version"]=2;document.sequences[0].program=program.ToString();
                string path=Path.Combine(directory,"rules.v5.json"),future=JsonUtility.ToJson(document);File.WriteAllText(path,future);
                storage=new RuleStorage(directory);storage.Load(out var message);Assert.That(storage.ReadOnly,Is.True,message);Assert.That(message,Does.Contain("different app version"));
                Assert.That(storage.Save(new RuleDocument(),out _),Is.False);Assert.That(File.ReadAllText(path),Is.EqualTo(future));
            }finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void NewerCollectionFilenamesPreventRollbackSavingAndMotionRemoval()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroFutureFiles-"+Guid.NewGuid().ToString("N"));
            try {
                var document=new RuleDocument {sequences=new[] {new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Current",program=Example()}}};
                var storage=new RuleStorage(directory);Assert.That(storage.Save(document,out var error),Is.True,error);
                string current=Path.Combine(directory,"rules.v5.json"),original=File.ReadAllText(current);
                File.WriteAllText(Path.Combine(directory,"rules.v6.json.notes"),"unrelated notes");
                File.WriteAllText(Path.Combine(directory,"room.v999.json"),"another collection");
                storage=new RuleStorage(directory);Assert.That(storage.Load(out _).sequences.Length,Is.EqualTo(1));Assert.That(storage.ReadOnly,Is.False);
                foreach(string name in new[] {"rules.v6.json","rules.v6.json.backup","rules.v6.json.pending","rules.v6.json.unreadable","rules.v999999999999.json"}) {
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
            Invalid(x=>x["functions"][0]["body"][1]["then"][0]["step"]["seconds"]="0.1");
            Invalid(x=>x["functions"][0]["body"][1]["then"][0]["bindings"]["arbitraryField"]=JObject.Parse("{\"value\":1}"));
            Assert.That(BehaviourProgram.TryParse(Example()+"{}",out _,out _),Is.False);
            Assert.That(BehaviourProgram.TryParse(Example().Replace("\"version\": 1","\"version\": 1, \"version\": 1"),out _,out _),Is.False);
        }
        [Test] public void ShortCircuitSkipsUnavailableFactsAndInvalidComputedActionsNeverEscape()
        {
            var json=JObject.Parse(Example());json["functions"][0]["body"][1]["test"]=JObject.Parse("{\"op\":\"or\",\"args\":[{\"value\":true},{\"op\":\"eq\",\"args\":[{\"fact\":\"maestro.state\"},{\"value\":\"speaking\"}]}]}");
            var machine=new ProgramMachine(Compile(json.ToString()),null);Assert.That(machine.Advance(out var step,256),Is.EqualTo(ProgramYield.Action));Assert.That(step.gesture,Is.EqualTo(RuleGesture.Greeting));
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

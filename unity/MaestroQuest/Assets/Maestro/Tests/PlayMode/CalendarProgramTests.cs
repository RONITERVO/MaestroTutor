// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
 public sealed class CalendarProgramTests
 {
  sealed class Clock:IProgramClock {public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-02T17:59:50Z");public DateTimeOffset UtcNow=>Now;public TimeZoneInfo LocalZone=>TimeZoneInfo.Utc;}
  GameObject root;RoomEditor editor;RuleWorkshop rules;RoomRules runtime;Clock clock;string directory;
  [UnitySetUp] public IEnumerator Setup(){
   directory=Path.Combine(Path.GetTempPath(),"MaestroCalendar-"+Guid.NewGuid().ToString("N"));root=new GameObject("Calendar room");root.AddComponent<XRInteractionManager>();var room=root.AddComponent<RoomInteraction>();
   RoomItem Item(string name){var obj=new GameObject(name);obj.transform.SetParent(root.transform,false);var collider=obj.AddComponent<BoxCollider>();var item=obj.AddComponent<RoomItem>();item.Configure(new Collider[]{collider});room.Register(item);return item;}
   var book=Item("book");var maestro=Item("maestro");editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,maestro,directory);var animations=root.AddComponent<AnimationWorkshop>();animations.Initialize(editor);rules=root.AddComponent<RuleWorkshop>();rules.Initialize(editor,directory);clock=new Clock();runtime=root.AddComponent<RoomRules>();runtime.Initialize(rules,editor,animations,null,room,null,clock:clock);yield return null;
  }
  JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-calendar.json")));
  string Save(JObject program){var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=rules.Revision,edits=new[]{new RuleEdit{kind="save",reference="calendar",sequence=new RuleSequence{id="",name="Weekday practice ball",program=program.ToString(Newtonsoft.Json.Formatting.None)}}}}}}},out var error,out var ids),Is.True,error);return ids.Single();}
  static IEnumerator Until(Func<bool> ready){float until=Time.realtimeSinceStartup+10;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Assert.That(ready(),Is.True,"Calendar program did not reach the expected state");}
  bool State(string name,string value)=>runtime.Scheduler.ObserveRuns().Any(r=>r.waiting&&r.state.Any(v=>v.name==name&&v.value==value));
  [UnityTest] public IEnumerator CalendarEventRunsTheSameSavedProgramAndCreatesOnlyForAnOnTimeOccurrence(){
   var program=Source();string id=Save(program);int count=editor.Snapshot().objects.Length;var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var capture=new JObject{["program"]=program};
   void Capture(string phase){var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=rules.Observe(true);capture[phase]=JObject.Parse(RoomAgentWire.Serialize(state));Assert.That(runtime.TryReadFact("clock.now",out var value),Is.True);capture[phase+"Clock"]=JObject.FromObject(value.Value);}
   Capture("saved");Assert.That(runtime.Trigger(id),Is.True,runtime.Scheduler.LastError);yield return null;Capture("waiting");Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
   clock.Now=DateTimeOffset.Parse("2026-10-02T18:00:00Z");yield return Until(()=>State("created","1"));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));Capture("fired");
   clock.Now=clock.Now.AddDays(28);yield return Until(()=>State("missed","1"));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));Capture("missed");
   runtime.StopAll();Capture("stopped");clock.Now=clock.Now.AddDays(7);yield return new WaitForSecondsRealtime(.2f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
   string folder=Environment.GetEnvironmentVariable("MAESTRO_CALENDAR_EVIDENCE");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"calendar.json"),capture.ToString());}
  }
  [UnityTest] public IEnumerator PauseDoesNotResumeAMissedDeadlineAndLateFailureCannotCreate(){
   var program=Source();var wait=program["functions"][0]["body"][0]["body"][0];wait["arguments"]=new JObject{["kind"]="once",["at"]="2026-10-02T18:00:00Z",["missed"]="fail",["graceSeconds"]=1};wait["bindings"]=new JObject();string id=Save(program);int count=editor.Snapshot().objects.Length;
   Assert.That(runtime.Trigger(id),Is.True);runtime.SendMessage("OnApplicationPause",true);clock.Now=clock.Now.AddDays(7);runtime.SendMessage("OnApplicationPause",false);yield return new WaitForSecondsRealtime(.2f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
   Assert.That(runtime.Trigger(id),Is.True);yield return Until(()=>runtime.Scheduler.RunningCount==0);Assert.That(runtime.Scheduler.LastError,Does.Contain("missed"));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("failed"));
  }
  [UnityTearDown] public IEnumerator Cleanup(){if(root)UnityEngine.Object.Destroy(root);yield return null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }
}

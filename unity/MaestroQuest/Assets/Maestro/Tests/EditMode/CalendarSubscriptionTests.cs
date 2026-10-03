// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class CalendarSubscriptionTests
 {
  sealed class ClockWorld:IProgramEventWorld,IProgramClockWorld,IProgramClock {
   public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-02T10:00:00Z");public TimeZoneInfo Zone=TimeZoneInfo.Utc;
   public IProgramClock Clock=>this;public DateTimeOffset UtcNow=>Now;public TimeZoneInfo LocalZone=>Zone;
   public bool TryPosition(string id,out Vector3 position){position=default;return false;}
  }
  static JObject Once(string at="2026-10-02T13:00:01+03:00",string missed="fail")=>new(){["kind"]="once",["at"]=at,["missed"]=missed,["graceSeconds"]=1};
  static JObject Weekly(int hour=9,int minute=0,string day="fri")=>new(){["kind"]="weekly",["hour"]=hour,["minute"]=minute,["weekdays"]=new JArray(day),["zone"]="device",["ambiguous"]="earlier",["missed"]="report",["graceSeconds"]=60};
  [Test] public void OneOffUsesExplicitOffsetsAndWallClockWhileDisposalPreventsReplay(){
   var w=new ClockWorld();using var watch=new CalendarSubscription(w,Once(),0);
   w.Now=w.Now.AddSeconds(.5);Assert.That(watch.Poll(.11f,out _,out _,out var error),Is.False);Assert.That(error,Is.Null);
   w.Now=w.Now.AddHours(-1);Assert.That(watch.Poll(.22f,out _,out _,out error),Is.False);Assert.That(error,Is.Null);
   w.Now=DateTimeOffset.Parse("2026-10-02T10:00:01Z");Assert.That(watch.Poll(.33f,out var value,out var fields,out error),Is.True);Assert.That(value.Text,Is.EqualTo("due"));Assert.That((double)fields["lateSeconds"],Is.Zero);Assert.That((string)fields["scheduledUtc"],Is.EqualTo(CalendarSubscription.Stamp(w.Now)));
   Assert.That(BehaviourCatalog.Event("clock.scheduled").ValidFields(fields),Is.True);watch.Dispose();Assert.That(watch.Poll(100,out _,out _,out _),Is.False);
  }
  [Test] public void LatePolicyReportsTruthfulCappedLatenessOrFails(){
   var w=new ClockWorld();using var report=new CalendarSubscription(w,Once("2020-01-01T00:00:00Z","report"),0);Assert.That(report.Poll(.11f,out var value,out var fields,out _),Is.True);Assert.That(value.Text,Is.EqualTo("late"));Assert.That((bool)fields["late"],Is.True);Assert.That((double)fields["lateSeconds"],Is.EqualTo(1000000));Assert.That((bool)fields["lateSecondsCapped"],Is.True);Assert.That((string)fields["observedUtc"],Is.EqualTo(CalendarSubscription.Stamp(w.Now)));
   using var fail=new CalendarSubscription(w,Once("2020-01-01T00:00:00Z"),0);Assert.That(fail.Poll(.11f,out _,out _,out var error),Is.False);Assert.That(error,Does.Contain("missed"));
  }
  [Test] public void WeeklyChoosesNextOccurrenceWithoutRepeatingTheCurrentOrMissedWeeks(){
   var w=new ClockWorld();using var watch=new CalendarSubscription(w,Weekly(),0);w.Now=DateTimeOffset.Parse("2026-10-09T09:00:00Z");Assert.That(watch.Poll(.11f,out _,out var fields,out _),Is.True);Assert.That((string)fields["scheduledUtc"],Is.EqualTo(CalendarSubscription.Stamp(w.Now)));
   using var next=new CalendarSubscription(w,Weekly(),.11f);Assert.That(next.Poll(.22f,out _,out _,out _),Is.False);w.Now=w.Now.AddDays(28);Assert.That(next.Poll(.33f,out _,out fields,out _),Is.True);Assert.That((string)fields["scheduledUtc"],Is.EqualTo("2026-10-16T09:00:00.0000000Z"));
   using var future=new CalendarSubscription(w,Weekly(),.33f);Assert.That(future.Poll(.44f,out _,out _,out _),Is.False);
  }
  static TimeZoneInfo TestZone(){
   var start=TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1,1,1,2,0,0),3,5,DayOfWeek.Sunday);
   var end=TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1,1,1,3,0,0),10,5,DayOfWeek.Sunday);
   return TimeZoneInfo.CreateCustomTimeZone("Test DST",TimeSpan.FromHours(2),"Test DST","Standard","Summer",new[]{TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2020,1,1),new DateTime(2030,12,31),TimeSpan.FromHours(1),start,end)});
  }
  [Test] public void WeeklySkipsNonexistentLocalTimeAndSelectsRepeatedTimeExplicitly(){
   var w=new ClockWorld{Zone=TestZone(),Now=DateTimeOffset.Parse("2026-03-28T00:00:00Z")};using var gap=new CalendarSubscription(w,Weekly(2,30,"sun"),0);w.Now=DateTimeOffset.Parse("2026-03-29T01:00:00Z");Assert.That(gap.Poll(.11f,out _,out _,out _),Is.False);w.Now=DateTimeOffset.Parse("2026-04-04T23:30:00Z");Assert.That(gap.Poll(.22f,out _,out var fields,out _),Is.True);Assert.That((string)fields["scheduledUtc"],Is.EqualTo(CalendarSubscription.Stamp(w.Now)));
   w.Now=DateTimeOffset.Parse("2026-10-24T00:00:00Z");var args=Weekly(2,30,"sun");using var earlier=new CalendarSubscription(w,args,0);args["ambiguous"]="later";using var later=new CalendarSubscription(w,args,0);w.Now=DateTimeOffset.Parse("2026-10-24T23:30:00Z");Assert.That(earlier.Poll(.11f,out _,out _,out _),Is.True);Assert.That(later.Poll(.11f,out _,out _,out _),Is.False);w.Now=w.Now.AddHours(1);Assert.That(later.Poll(.22f,out _,out _,out _),Is.True);
  }
  [Test] public void ExactDatesInputsAndSelectedVariantBindingsAreValidated(){
   var w=new ClockWorld();Assert.Throws<ProgramFault>(()=>new CalendarSubscription(w,Once("2026-02-30T12:00:00Z"),0));var args=Weekly();args["weekdays"]=new JArray("fri","fri");Assert.Throws<ProgramFault>(()=>new CalendarSubscription(w,args,0));
   string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-calendar.json"));Assert.That(BehaviourProgram.TryParse(source,out _,out var error),Is.True,error);var p=JObject.Parse(source);var wait=p["functions"][0]["body"][0]["body"][0];wait["bindings"]["kind"]=new JObject{["value"]="once"};Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False);((JObject)wait["bindings"]).Remove("kind");wait["arguments"]=Once();Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False,"A weekly-only hour binding cannot enter once");
   Assert.That(BehaviourCatalog.Event("clock.scheduled").ValidArguments(1,Once("2026-10-02T10:00:00"),out _),Is.False);
  }
  [Test] public void ClockFactKeepsFullTimestampsWithinProgramValueLimits(){
   var w=new ClockWorld{Zone=TimeZoneInfo.CreateCustomTimeZone("Test plus three",TimeSpan.FromHours(3),"plus three","plus three")};Assert.That(BehaviourCatalog.TryRead("clock.now",1,null,new BehaviourCatalog.FactContext(world:w),out var value),Is.True);var result=JObject.FromObject(value.Value);Assert.That((string)result["utc"],Is.EqualTo(CalendarSubscription.Stamp(w.Now)));Assert.That((string)result["local"],Does.EndWith("+03:00"));Assert.That((double)result["offsetMinutes"],Is.EqualTo(180));Assert.That((string)result["weekday"],Is.EqualTo("fri"));
  }
 }
}

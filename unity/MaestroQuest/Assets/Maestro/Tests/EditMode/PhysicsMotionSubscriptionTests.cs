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
 public sealed class PhysicsMotionSubscriptionTests
 {
  sealed class World:IProgramEventWorld,IProgramPhysicsWorld {
   public int Reads;public bool Exists=true;public PhysicsMotionSample Sample=new(1,1,true,0,0);
   public bool TryPosition(string id,out Vector3 position){position=default;return Exists;}
   public bool TryPhysicsMotion(string id,out PhysicsMotionSample sample){Reads++;sample=Sample;return Exists;}
   public void Set(float speed=0,float spin=0,bool available=true,uint revision=1,int instance=1)=>Sample=new(instance,revision,available,speed,spin);
  }
  static JObject Args(string initial="report",string transition="either")=>new() {["target"]=new string('b',32),["speedThreshold"]=.05,["angularThreshold"]=.1,["quietSeconds"]=.3,["transition"]=transition,["initial"]=initial};
  [Test] public void ReportingInitialRestRequiresSustainedLinearAndAngularQuiet(){
   var w=new World();using var watch=new PhysicsMotionSubscription(w,Args(),0);
   Assert.That(watch.Poll(.11f,out _,out _,out _),Is.False);w.Set(spin:.4f);
   Assert.That(watch.Poll(.22f,out var value,out var fields,out var error),Is.True);Assert.That((bool)fields["settled"],Is.False);Assert.That(error,Is.Null);Assert.That((string)value.Value,Is.EqualTo(new string('b',32)));
   w.Set();Assert.That(watch.Poll(.33f,out _,out _,out _),Is.False);Assert.That(watch.Poll(.55f,out _,out _,out _),Is.False);
   Assert.That(watch.Poll(.66f,out _,out fields,out _),Is.True);Assert.That((bool)fields["settled"],Is.True);Assert.That((float)fields["quietSeconds"],Is.GreaterThanOrEqualTo(.3));Assert.That(watch.Poll(.77f,out _,out _,out _),Is.False);
  }
  [Test] public void BaselineAndHysteresisSuppressInitialStateAndJitterButSeeSpin(){
   var w=new World();using var watch=new PhysicsMotionSubscription(w,Args("baseline"),0);
   for(int i=1;i<8;i++){w.Set(.075f,.15f);Assert.That(watch.Poll(i*.11f,out _,out _,out _),Is.False);}
   w.Set(.01f,.21f);Assert.That(watch.Poll(.88f,out _,out var fields,out _),Is.True);Assert.That((bool)fields["settled"],Is.False);
   w.Set();Assert.That(watch.Poll(.99f,out _,out _,out _),Is.False);Assert.That(watch.Poll(1.21f,out _,out _,out _),Is.False);Assert.That(watch.Poll(1.32f,out _,out fields,out _),Is.True);Assert.That((bool)fields["settled"],Is.True);
  }
  [TestCase("unavailable")] [TestCase("revision")] [TestCase("replacement")] [TestCase("gap")]
  public void OwnershipPhysicsPlacementAndSamplingGapsCannotReuseOldQuietTime(string reason){
   var w=new World();using var watch=new PhysicsMotionSubscription(w,Args(),0);watch.Poll(.22f,out _,out _,out _);
   float at=.33f;
   if(reason=="unavailable"){w.Set(available:false);Assert.That(watch.Poll(at,out _,out _,out _),Is.False);w.Set();at=.44f;}
   if(reason=="revision")w.Set(revision:2);if(reason=="replacement")w.Set(instance:2);if(reason=="gap")at=2;
   Assert.That(watch.Poll(at,out _,out _,out _),Is.False);Assert.That(watch.Poll(at+.22f,out _,out _,out _),Is.False);Assert.That(watch.Poll(at+.33f,out _,out var fields,out _),Is.True);Assert.That((bool)fields["settled"],Is.True);
  }
  [Test] public void SamplingIsBoundedAndUnavailableNeverMeansZeroSpeedRest(){
   var w=new World();w.Set(available:false);using var watch=new PhysicsMotionSubscription(w,Args(),0);
   for(int i=0;i<100;i++)Assert.That(watch.Poll(i*.0001f,out _,out _,out _),Is.False);Assert.That(w.Reads,Is.EqualTo(1));
   Assert.That(watch.Poll(20,out _,out _,out _),Is.False);Assert.That(w.Reads,Is.EqualTo(2));w.Set();Assert.That(watch.Poll(20.11f,out _,out _,out _),Is.False);
   watch.Dispose();Assert.That(watch.Poll(40,out _,out _,out _),Is.False);Assert.That(w.Reads,Is.EqualTo(3));
  }
  [Test] public void FilteredInitialMotionStillAllowsARealSettledResult(){
   var w=new World();w.Set(1);using var watch=new PhysicsMotionSubscription(w,Args(transition:"settled"),0);
   Assert.That(watch.Poll(.11f,out _,out _,out _),Is.False);w.Set();watch.Poll(.22f,out _,out _,out _);watch.Poll(.44f,out _,out _,out _);
   Assert.That(watch.Poll(.55f,out _,out var fields,out _),Is.True);Assert.That((bool)fields["settled"],Is.True);
  }
  [Test] public void MissingAndNonfiniteReadingsFailInsteadOfManufacturingAnEvent(){
   var w=new World {Exists=false};Assert.Throws<ProgramFault>(()=>new PhysicsMotionSubscription(w,Args(),0));w.Exists=true;
   using var watch=new PhysicsMotionSubscription(w,Args(),0);w.Set(float.NaN);Assert.That(watch.Poll(.11f,out _,out _,out var error),Is.False);Assert.That(error,Does.Contain("invalid reading"));
  }
  [Test] public void SharedProgramAndComputedInputsUseTheNativeCatalogContract(){
   string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-physics-motion.json"));Assert.That(BehaviourProgram.TryParse(source,out _,out var error),Is.True,error);
   var p=JObject.Parse(source);var wait=p["functions"][0]["body"][0];wait["arguments"]["quietSeconds"]=0;Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False);
   wait["arguments"]["quietSeconds"]=.3;wait["bindings"]=new JObject {["speedThreshold"]=new JObject {["value"]=2}};Assert.That(BehaviourProgram.TryParse(p.ToString(),out var program,out error),Is.True,error);
   var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("speedThreshold"));
  }
 }
}

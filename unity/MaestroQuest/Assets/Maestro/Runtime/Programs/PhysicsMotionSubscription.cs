// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
 public readonly struct PhysicsMotionSample {
  public readonly int Instance;public readonly uint Revision;public readonly bool Available;public readonly float Speed,AngularSpeed;
  public PhysicsMotionSample(int instance,uint revision,bool available,float speed,float angularSpeed){Instance=instance;Revision=revision;Available=available;Speed=speed;AngularSpeed=angularSpeed;}
 }
 // Optional read-only facet of the same native world. Observation never owns an object.
 public interface IProgramPhysicsWorld {bool TryPhysicsMotion(string id,out PhysicsMotionSample sample);}
 public sealed class PhysicsMotionSubscription:IProgramEventWatch
 {
  public const float SampleInterval=.1f,MaximumSampleGap=.3f;
  enum MotionState {Unknown,Moving,Settled}
  readonly IProgramPhysicsWorld world;readonly string target,transition,initial;
  readonly float speedLimit,spinLimit,quietTime;
  MotionState state;bool disposed,hasSample;int instance;uint revision;float nextSample,lastSample,quietSince=-1;
  static JObject Field(JObject schema,string title,string description){schema["title"]=title;schema["description"]=description;return schema;}
  public static BehaviourCatalog.EventDefinition Definition()=>new("object.motion.changed","Object physics motion changed",
   "Observes an explicit solid/bouncy room object's physical velocity, at most 10 samples/second. Source filter must be empty. Settled requires both speed <= speedThreshold (metres/second) and angular speed <= angularThreshold (radians/second) for quietSeconds. After settling, moving starts at twice either threshold; this hysteresis suppresses jitter. Transition selects settled, moving or either. Initial baseline suppresses the current state; report emits the first qualifying state (settled still requires the quiet period). Missing/disabled/non-dynamic objects or invalid readings fail. Held, carried, animation-owned, unready or paused physics emits nothing. Each new eligible window after ownership/physics/placement changes, replacement or a sample gap over 0.3 seconds resets the baseline and quiet period, applying the initial policy again. No catch-up or old quiet time is replayed. Value is the object ID; fields are settled, speed, angularSpeed and quietSeconds measured at delivery. Sampling can miss motion between samples; settled is a threshold observation, not proof of support/contact or a permanent rest. It grants no edit authority.",
   Object(new JObject {["settled"]=new JObject {["type"]="boolean"},["speed"]=Number(0,1000000),["angularSpeed"]=Number(0,1000000),["quietSeconds"]=Number(0,3600)}),
   input:Object(new JObject {["target"]=Field(Resource(Text("^[a-fA-F0-9]{32}$",32)),"Object","Choose a creation with solid or bouncy physics."),["speedThreshold"]=Field(Number(.005,1),"Speed threshold (m/s)","Motion below this speed can qualify as settled. Moving again uses twice this threshold."),["angularThreshold"]=Field(Number(.01,5),"Spin threshold (rad/s)","Rotation must also stay below this limit. Moving again uses twice this threshold."),["quietSeconds"]=Field(Number(.1,10),"Quiet period (seconds)","Both speeds must remain low for this long. Held, paused or interrupted time does not count."),["transition"]=Field(Choice("settled","moving","either"),"Detect","Choose which qualifying motion state should wake the program."),["initial"]=Field(Choice("baseline","report"),"When watching starts","Baseline waits for a later change. Report also reports the current state; settling still needs the full quiet period. This choice applies again after interrupted observation.")}),
   example:new JObject {["target"]=new string('0',32),["speedThreshold"]=.05,["angularThreshold"]=.1,["quietSeconds"]=.6,["transition"]="settled",["initial"]="report"},
   watch:(world,args,now)=>new PhysicsMotionSubscription(world,args,now));
  public PhysicsMotionSubscription(IProgramEventWorld world,JObject args,float now){
   this.world=world as IProgramPhysicsWorld;target=(string)args["target"];transition=(string)args["transition"];initial=(string)args["initial"];
   speedLimit=(float)args["speedThreshold"];spinLimit=(float)args["angularThreshold"];quietTime=(float)args["quietSeconds"];
   if(!Read(out var sample))throw new ProgramFault("Physics motion target is missing, disabled, non-dynamic or has an invalid reading");
   if(sample.Available)Reset(sample,now);lastSample=now;nextSample=now+SampleInterval;
  }
  bool Read(out PhysicsMotionSample sample){sample=default;return world!=null&&world.TryPhysicsMotion(target,out sample)&&float.IsFinite(sample.Speed)&&sample.Speed>=0&&sample.Speed<=1000000&&float.IsFinite(sample.AngularSpeed)&&sample.AngularSpeed>=0&&sample.AngularSpeed<=1000000;}
  bool Quiet(PhysicsMotionSample sample)=>sample.Speed<=speedLimit&&sample.AngularSpeed<=spinLimit;
  void Reset(PhysicsMotionSample sample,float now){
   hasSample=true;instance=sample.Instance;revision=sample.Revision;bool quiet=Quiet(sample);
   state=initial=="report"?MotionState.Unknown:quiet?MotionState.Settled:MotionState.Moving;quietSince=quiet?now:-1;
  }
  public bool Poll(float now,out ProgramValue value,out JObject fields,out string error){
   value=default;fields=null;error=null;if(disposed||now<nextSample)return false;nextSample=now+SampleInterval;
   if(!Read(out var sample)){error="Physics motion target is missing, disabled, non-dynamic or has an invalid reading";return false;}
   bool gap=now-lastSample>MaximumSampleGap;lastSample=now;
   if(!sample.Available){hasSample=false;quietSince=-1;return false;}
   if(!hasSample||sample.Instance!=instance||sample.Revision!=revision||gap)Reset(sample,now);
   var previous=state;
   if(state==MotionState.Settled){if(sample.Speed>=speedLimit*2||sample.AngularSpeed>=spinLimit*2){state=MotionState.Moving;quietSince=-1;}}
   else if(Quiet(sample)){if(quietSince<0)quietSince=now;if(now-quietSince+1e-6f>=quietTime)state=MotionState.Settled;}
   else {quietSince=-1;state=MotionState.Moving;}
   if(state==previous||state==MotionState.Unknown||transition!="either"&&transition!=(state==MotionState.Settled?"settled":"moving"))return false;
   value=new ProgramValue(target);fields=new JObject {["settled"]=state==MotionState.Settled,["speed"]=sample.Speed,["angularSpeed"]=sample.AngularSpeed,["quietSeconds"]=state==MotionState.Settled?Math.Max(0,now-quietSince):0};return true;
  }
  public void Dispose(){disposed=true;}
 }
}

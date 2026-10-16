// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
 internal static partial class SimulationWorkSchedule {
  // A charge is an opportunity allowance, not measured operations or engine time.
  // Consuming twice retains the larger allowance; it does not authorize another
  // automatic quantity tick. Quantity/contact clocks separately enforce cadence.
  internal readonly struct WorkCharge {
   internal readonly long Discovery,Force,Flow,Contact;
   internal readonly int Bodies,Rooms,Samplers;
   internal WorkCharge(long discovery,long force,long flow,long contact,int bodies,int rooms,int samplers){Discovery=discovery;Force=force;Flow=flow;Contact=contact;Bodies=bodies;Rooms=rooms;Samplers=samplers;}
   internal bool NonNegative=>Discovery>=0&&Force>=0&&Flow>=0&&Contact>=0&&Bodies>=0&&Rooms>=0&&Samplers>=0;
   internal bool Fits=>NonNegative&&Discovery<=FlowDiscoveryWork.MaximumUnits&&Force<=PhysicsWorkResources.MaximumUnits&&Flow<=FlowWorkResources.MaximumUnits&&Contact<=ContactWorkResources.MaximumUnits&&Bodies<=PhysicsWorkResources.MaximumReservations&&Rooms<=FlowWorkResources.MaximumExecutions&&Samplers<=ContactWorkResources.MaximumExecutions;
   internal static WorkCharge Request(Claim c)=>new(c.DiscoveryUnits,c.ForceUnits,c.FlowUnits,c.ContactUnits,c.Bodies,c.Rooms,c.Samplers);
   public static WorkCharge operator +(WorkCharge a,WorkCharge b)=>new(a.Discovery+b.Discovery,a.Force+b.Force,a.Flow+b.Flow,a.Contact+b.Contact,a.Bodies+b.Bodies,a.Rooms+b.Rooms,a.Samplers+b.Samplers);
   internal static WorkCharge Max(WorkCharge a,WorkCharge b)=>new(System.Math.Max(a.Discovery,b.Discovery),System.Math.Max(a.Force,b.Force),System.Math.Max(a.Flow,b.Flow),System.Math.Max(a.Contact,b.Contact),System.Math.Max(a.Bodies,b.Bodies),System.Math.Max(a.Rooms,b.Rooms),System.Math.Max(a.Samplers,b.Samplers));
   internal JObject Observe()=>new(){["discoveryUnits"]=Discovery,["physicsUnits"]=Force,["liquidUnits"]=Flow,["contactUnits"]=Contact,["physicsBodies"]=Bodies,["liquidPasses"]=Rooms,["contactPasses"]=Samplers};
  }
  static WorkCharge retiredConsumption;
  static WorkCharge Committed(Group candidate=null){
   var cost=retiredConsumption;
   foreach(var c in claims)cost+=c.Granted||candidate!=null&&ReferenceEquals(c.Group,candidate)?WorkCharge.Max(WorkCharge.Request(c),c.Consumed):c.Consumed;
   return cost;
  }
  internal static bool Consume(Claim claim){
   // Consumption cannot grant a request which was only queued. Revalidate the
   // complete live allowance in case its requested cost changed since admission.
   if(!Ready(claim)||!TryAdmit(claim))return false;
   claim.Consumed=WorkCharge.Max(claim.Consumed,WorkCharge.Request(claim));return true;
  }
  internal static JObject ObserveResource(string resource){
   var ledger=ObserveLedger();
   return new JObject{["step"]=ledger["step"],["resource"]=resource,["committed"]=ledger["committed"][resource],["consumed"]=ledger["consumed"][resource],["retired"]=ledger["retired"][resource],["limit"]=ledger["limits"][resource]};
  }
  internal static JObject ObserveLedger(){
   var consumed=retiredConsumption;foreach(var c in claims)consumed+=c.Consumed;
   var limits=new WorkCharge(FlowDiscoveryWork.MaximumUnits,PhysicsWorkResources.MaximumUnits,FlowWorkResources.MaximumUnits,ContactWorkResources.MaximumUnits,PhysicsWorkResources.MaximumReservations,FlowWorkResources.MaximumExecutions,ContactWorkResources.MaximumExecutions);
   return new JObject{["step"]=Step,["committed"]=Committed().Observe(),["consumed"]=consumed.Observe(),["retired"]=retiredConsumption.Observe(),["limits"]=limits.Observe()};
  }
 }
}

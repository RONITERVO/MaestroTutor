// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class CatchGeometryTests {
  [Test] public void RelativeSweepsDetectBetweenStepContactWithoutInventingStationaryContact(){
   Assert.That(CatchGeometry.Crosses(Vector3.left,Vector3.right,Vector3.zero,Vector3.zero,.1f),Is.True);
   Assert.That(CatchGeometry.Crosses(Vector3.zero,Vector3.zero,Vector3.left,Vector3.right,.1f),Is.True);
   Assert.That(CatchGeometry.Crosses(Vector3.left,Vector3.left,Vector3.zero,Vector3.zero,.1f),Is.False);
   Assert.That(CatchGeometry.Crosses(Vector3.left+Vector3.up,Vector3.right+Vector3.up,Vector3.zero,Vector3.zero,.1f),Is.False);
   Assert.That(CatchGeometry.Crosses(new Vector3(float.NaN,0,0),Vector3.right,Vector3.zero,Vector3.zero,.1f),Is.False);
  }
  [Test] public void TwoBoneReachKeepsBothLengthsAcrossGoalsPolesAndDegenerateDirections(){
   var shoulder=new Vector3(.3f,1,.1f);var elbow=shoulder+Vector3.right*.3f;var hand=elbow+Vector3.right*.25f;
   foreach(var goal in new[]{shoulder,hand,shoulder+Vector3.forward*.3f,shoulder+Vector3.up*5,shoulder-Vector3.right*.4f})foreach(var pole in new[]{Vector3.zero,Vector3.down,Vector3.forward}){
    Assert.That(CatchGeometry.TwoBone(shoulder,elbow,hand,goal,pole,out var e,out var h),Is.True);
    Assert.That(Vector3.Distance(e,shoulder),Is.EqualTo(.3f).Within(.0001f));Assert.That(Vector3.Distance(h,e),Is.EqualTo(.25f).Within(.0001f));Assert.That(Vector3.Distance(h,shoulder),Is.LessThanOrEqualTo(.55f));
   }
   Assert.That(CatchGeometry.TwoBone(shoulder,shoulder,hand,hand,Vector3.up,out _,out _),Is.False);
   Assert.That(CatchGeometry.TwoBone(shoulder,elbow,hand,new Vector3(float.PositiveInfinity,0,0),Vector3.up,out _,out _),Is.False);
  }
  [Test] public void CatchContractAuthorizesBothObjectsButReservesOnlyHolderUntilContact(){
   var definition=BehaviourCatalog.Action("object.physics.catch");Assert.That(definition.TryCall(definition.Example,out var call,out var error),Is.True,error);
   Assert.That(call.Resources,Is.EquivalentTo(new[]{new string('0',32),"maestro"}));Assert.That(call.Claims.Length,Is.EqualTo(2));foreach(var claim in call.Claims)Assert.That(claim.Target,Is.EqualTo("maestro"));Assert.That(call.AwaitCompletion,Is.True);
   var bad=definition.Example;bad["timeout"]=16;Assert.That(definition.TryCall(bad,out _,out _),Is.False);
   bad=definition.Example;bad["holder"]["avatarHash"]="stale";Assert.That(definition.TryCall(bad,out _,out _),Is.False);
   var result=JObject.Parse(@"{""target"":""00000000000000000000000000000000"",""holder"":""maestro"",""phase"":""missed"",""caught"":false,""dropped"":false,""reason"":""No contact""}");
   Assert.That(CapabilityArguments.Validate(result,definition.OutputSchema,out error),Is.True,error);
  }
 }
}

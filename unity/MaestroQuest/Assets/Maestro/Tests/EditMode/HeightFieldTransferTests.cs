// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class HeightFieldTransferTests {
  static RoomHeightField Field(int cells,float w,float d,float h)=>new(){cells=cells,width=w,depth=d,heights=Enumerable.Repeat(h,(cells+1)*(cells+1)).ToArray()};
  [Test] public void DifferentGridsAndBoundaryFootprintsConserveTheirActualTriangleVolumes(){
   foreach(int cells in new[]{4,8,16})foreach(float edge in new[]{0,.5f}){
    var a=Field(cells,1,1,.08f);var b=Field(16,2,.5f,.02f);double av=a.VolumeLitres,bv=b.VolumeLitres;
    Assert.That(HeightFieldTransfer.Apply(a,new Vector2(edge,edge),.3f,b,new Vector2(-1,.25f),.4f,1,out var r,out var error),Is.True,error);
    Assert.That(av-a.VolumeLitres,Is.EqualTo(r.RemovedLitres).Within(1e-10));Assert.That(b.VolumeLitres-bv,Is.EqualTo(r.AddedLitres).Within(1e-10));
    Assert.That(Math.Abs(r.RoundingLitres),Is.LessThanOrEqualTo(HeightFieldTransfer.Tolerance(r.RemovedLitres)));Assert.That(r.RemovedLitres,Is.GreaterThan(0).And.LessThanOrEqualTo(1.000001));
    Assert.That(a.heights.All(h=>h>=0&&h<=a.maxHeight)&&b.heights.All(h=>h>=0&&h<=b.maxHeight),Is.True);
    Assert.That(a.heights[0],Is.EqualTo(.08f),"Unselected vertices stay exact");
   }
  }
  [Test] public void CapacityAndAvailabilityBoundTheTransferWithoutNegativeHeights(){
   var a=Field(4,.1f,.1f,.001f);var b=Field(16,.1f,.1f,.2499f);
   double av=a.VolumeLitres,bv=b.VolumeLitres;
   Assert.That(HeightFieldTransfer.Apply(a,Vector2.zero,2,b,Vector2.zero,2,8000,out var r,out var error),Is.True,error);
   Assert.That(r.RemovedLitres,Is.LessThan(av));Assert.That(b.VolumeLitres,Is.LessThanOrEqualTo(.1*.1*.25*1000+.000001));
   Assert.That(av+bv-a.VolumeLitres-b.VolumeLitres,Is.EqualTo(-r.RoundingLitres).Within(1e-10));
  }
  [Test] public void InvalidEmptyIncompatibleOrUnresolvableFootprintsDoNotMutateEitherEndpoint(){
   var a=Field(4,1,1,.1f);var b=Field(4,1,1,.1f);string before=JsonUtility.ToJson(a),other=JsonUtility.ToJson(b);
   foreach(var c in new[]{new Vector2(2,0),new Vector2(float.NaN,0),new Vector2(.125f,.125f)}){
    Assert.That(HeightFieldTransfer.Apply(a,c,.005f,b,Vector2.zero,.2f,1,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(a),Is.EqualTo(before));Assert.That(JsonUtility.ToJson(b),Is.EqualTo(other));
   }
   Assert.That(HeightFieldTransfer.Apply(a,Vector2.zero,.2f,a,Vector2.zero,.2f,1,out _,out _),Is.False);
   foreach(double q in new[]{0,double.NaN,double.PositiveInfinity,8001})Assert.That(HeightFieldTransfer.Apply(a,Vector2.zero,.2f,b,Vector2.zero,.2f,q,out _,out _),Is.False);
   foreach(var mismatch in new[]{Field(4,1,1,.25f),Field(4,1,1,0)}){if(mismatch.heights[0]==0)mismatch.material="Sand";Assert.That(HeightFieldTransfer.Apply(a,Vector2.zero,.2f,mismatch,Vector2.zero,.2f,1,out _,out _),Is.False);}
   b.color=Color.gray;Assert.That(HeightFieldTransfer.Apply(a,Vector2.zero,.2f,b,Vector2.zero,.2f,1,out _,out _),Is.False);
   Assert.That(HeightFieldTransfer.Apply(Field(4,1,1,0),Vector2.zero,.2f,a,Vector2.zero,.2f,1,out _,out _),Is.False);
   Assert.That(JsonUtility.ToJson(a),Is.EqualTo(before));
  }
  [Test] public void BroadDeterministicInputsEitherPublishBoundedConservationOrPreserveBothSources(){
   var random=new System.Random(937);int accepted=0;
   for(int n=0;n<200;n++){
    var a=Field(new[]{4,8,16}[n%3],.1f+(float)random.NextDouble()*3.9f,.1f+(float)random.NextDouble()*3.9f,.1f);
    var b=Field(new[]{16,4,8}[n%3],.1f+(float)random.NextDouble()*3.9f,.1f+(float)random.NextDouble()*3.9f,.1f);
    for(int i=0;i<a.heights.Length;i++)a.heights[i]=(float)random.NextDouble()*.25f;
    for(int i=0;i<b.heights.Length;i++)b.heights[i]=(float)random.NextDouble()*.25f;
    var originalA=a.heights.ToArray();var originalB=b.heights.ToArray();double total=a.VolumeLitres+b.VolumeLitres;
    bool ok=HeightFieldTransfer.Apply(a,Vector2.zero,2,b,Vector2.zero,2,n%2==0?.001:10,out var r,out _);
    if(ok){accepted++;Assert.That(Math.Abs(r.RoundingLitres),Is.LessThanOrEqualTo(HeightFieldTransfer.Tolerance(r.RemovedLitres)));Assert.That(a.VolumeLitres+b.VolumeLitres-total,Is.EqualTo(r.RoundingLitres).Within(.00000001));}
    else {Assert.That(a.heights,Is.EqualTo(originalA));Assert.That(b.heights,Is.EqualTo(originalB));}
   }
   Assert.That(accepted,Is.GreaterThan(100),"Ordinary representable transfers must succeed, not merely refuse all rounding cases");
  }
  [Test] public void SharedContractChecksBothTargetsAndProgramFeatureRequirements(){
   foreach(var row in JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/height-field-transfer-contract.json")))){
    var args=(JObject)row["arguments"];bool valid=(bool)row["valid"];Assert.That(BehaviourCatalog.TryCall("object.field.transfer",1,args,out var call,out var error),Is.EqualTo(valid),(string)row["name"]+": "+error);if(!valid)continue;
    Assert.That(call.Resources,Is.EquivalentTo(new[]{(string)args["source"]["target"],(string)args["destination"]["target"]}));
    Assert.That(BehaviourProgram.TryParse(BehaviourProgram.FromInvocation(new JObject{["id"]="object.field.transfer",["version"]=1,["arguments"]=args}),out _,out error),Is.True,error);
   }
  }
 }
}

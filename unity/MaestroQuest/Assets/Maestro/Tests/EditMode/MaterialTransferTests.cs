// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;using System.IO;using System.Linq;
using Maestro.Quest.Creation;using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;using NUnit.Framework;using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class MaterialTransferTests {
  static RoomHeightField Field(float height=.08f,int cells=16)=>new(){cells=cells,heights=Enumerable.Repeat(height,(cells+1)*(cells+1)).ToArray()};
  static MaterialTransfer.Endpoint Surface(RoomHeightField f)=>MaterialTransfer.Endpoint.Surface(f,Vector2.zero,.3f);
  static MaterialTransfer.Endpoint Store(RoomMaterialStore s)=>MaterialTransfer.Endpoint.Stored(s);
  [Test] public void AStoredScoopCanReturnMeasuredMaterialToAnotherSurface(){
   var a=Field();var b=Field(0,8);var store=new RoomMaterialStore{capacityLitres=.2};double before=a.VolumeLitres;
   Assert.That(MaterialTransfer.Apply(Surface(a),Store(store),.5,out var taken,out var error),Is.True,error);
   Assert.That(taken.RemovedLitres,Is.EqualTo(before-a.VolumeLitres).Within(1e-10));Assert.That(store.amountLitres,Is.EqualTo(taken.AddedLitres));Assert.That(store.amountLitres,Is.GreaterThan(.19).And.LessThanOrEqualTo(.2));
   double carried=store.amountLitres;Assert.That(MaterialTransfer.Apply(Store(store),Surface(b),carried,out var placed,out error),Is.True,error);
   Assert.That(carried-store.amountLitres,Is.EqualTo(placed.RemovedLitres));Assert.That(b.VolumeLitres,Is.EqualTo(placed.AddedLitres));Assert.That(store.amountLitres,Is.GreaterThanOrEqualTo(0));
   Assert.That(Math.Abs(placed.RoundingLitres),Is.LessThanOrEqualTo(HeightFieldTransfer.Tolerance(placed.RemovedLitres)));
   Assert.That(before-a.VolumeLitres-b.VolumeLitres-store.amountLitres,Is.EqualTo(-taken.RoundingLitres-placed.RoundingLitres).Within(1e-10));
  }
  [Test] public void StoreAndFieldCapacityBoundActualQuantitiesWithoutChangingDefinitions(){
   var a=new RoomMaterialStore{capacityLitres=4,amountLitres=3};var b=new RoomMaterialStore{capacityLitres=1,amountLitres=.9};
   Assert.That(MaterialTransfer.Apply(Store(a),Store(b),2,out var result,out var error),Is.True,error);Assert.That(b.amountLitres,Is.EqualTo(1));Assert.That(a.amountLitres,Is.EqualTo(2.9).Within(1e-12));Assert.That(result.RemovedLitres,Is.EqualTo(.1).Within(1e-12));Assert.That(a.capacityLitres,Is.EqualTo(4));
   var field=Field(.2499f,4);double volume=field.VolumeLitres,carried=a.amountLitres;
   Assert.That(MaterialTransfer.Apply(Store(a),Surface(field),2,out result,out error),Is.True,error);Assert.That(result.AddedLitres,Is.LessThan(2));Assert.That(field.heights.All(x=>x<=field.maxHeight),Is.True);Assert.That(carried-a.amountLitres,Is.EqualTo(field.VolumeLitres-volume).Within(1e-6));
  }
  [Test] public void InvalidMismatchedFullEmptyAndAliasedInputsRemainUnchanged(){
   var field=Field();var store=new RoomMaterialStore();string original=JsonUtility.ToJson(field),saved=JsonUtility.ToJson(store);
   foreach(double amount in new[]{0,-1,double.NaN,double.PositiveInfinity,8001})Assert.That(MaterialTransfer.Apply(Surface(field),Store(store),amount,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(Surface(field),Surface(field),1,out _,out _),Is.False);Assert.That(MaterialTransfer.Apply(Store(store),Store(store),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(MaterialTransfer.Endpoint.Surface(field,new Vector2(2,0),.2f),Store(store),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(MaterialTransfer.Endpoint.Surface(field,new Vector2(.03f,.03f),.005f),Store(store),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(Store(store),Surface(field),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(Surface(field),Store(new RoomMaterialStore{amountLitres=1}),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(Surface(field),Store(new RoomMaterialStore{material="Sand"}),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(Surface(field),Store(new RoomMaterialStore{color=Color.white}),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(Store(new RoomMaterialStore{amountLitres=1}),Surface(Field(.25f)),1,out _,out _),Is.False);
   Assert.That(MaterialTransfer.Apply(default,Store(store),1,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(field),Is.EqualTo(original));Assert.That(JsonUtility.ToJson(store),Is.EqualTo(saved));
  }
  [Test] public void AllFourEndpointRoutesPreserveBoundedMeasuredBalances(){
   var random=new System.Random(573);int accepted=0;
   for(int i=0;i<160;i++){
    var a=Field((float)(.04+random.NextDouble()*.12),new[]{4,8,16}[i%3]);var b=Field(.01f,new[]{16,4,8}[i%3]);var c=new RoomMaterialStore{capacityLitres=4,amountLitres=2};var d=new RoomMaterialStore{capacityLitres=2};
    var from=i%4<2?Surface(a):Store(c);var to=i%2==0?Surface(b):Store(d);double av=from.Amount,bv=to.Amount;
    if(MaterialTransfer.Apply(from,to,i%3==0?.001:.7,out var result,out var error)){
     accepted++;Assert.That(from.Amount,Is.GreaterThanOrEqualTo(0));Assert.That(result.RemovedLitres,Is.EqualTo(av-from.Amount));Assert.That(result.AddedLitres,Is.EqualTo(to.Amount-bv));Assert.That(Math.Abs(result.RoundingLitres),Is.LessThanOrEqualTo(HeightFieldTransfer.Tolerance(result.RemovedLitres)));
    }else{Assert.That(from.Amount,Is.EqualTo(av),error);Assert.That(to.Amount,Is.EqualTo(bv),error);}
   }
   Assert.That(accepted,Is.GreaterThan(100));
  }
  [Test] public void SharedContractGuardsBothResourcesAndProgramFeatures(){
   foreach(var row in JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/material-transfer-contract.json")))){
    var args=(JObject)row["arguments"];bool valid=(bool)row["valid"];Assert.That(BehaviourCatalog.TryCall("object.material.transfer",1,args,out var call,out var error),Is.EqualTo(valid),(string)row["name"]+": "+error);if(!valid)continue;
    Assert.That(call.Resources,Is.EquivalentTo(new[]{(string)args["source"]["target"],(string)args["destination"]["target"]}));
    Assert.That(BehaviourProgram.TryParse(BehaviourProgram.FromInvocation(new JObject{["id"]="object.material.transfer",["version"]=1,["arguments"]=args}),out _,out error),Is.True,error);
   }
  }
 }
}

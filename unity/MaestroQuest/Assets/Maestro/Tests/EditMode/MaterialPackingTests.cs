// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;using System.IO;using System.Linq;using Maestro.Quest.Creation;using Maestro.Quest.Programs;using Newtonsoft.Json.Linq;using NUnit.Framework;using UnityEngine;
namespace Maestro.Quest.Tests {public sealed class MaterialPackingTests {
 static RoomHeightField Field(int cells=16)=>new(){cells=cells,width=1,depth=1,heights=Enumerable.Repeat(.08f,(cells+1)*(cells+1)).ToArray()};
 static RoomObjectData Ball()=>new(){id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Ball,materialStores=new[]{new RoomMaterialStore{capacityLitres=.5,amountLitres=.32123456789}}};
 [Test] public void ExtractionMeasuresRepresentableLossWithoutExceedingRequest(){
  foreach(int cells in new[]{4,8,16})foreach(float edge in new[]{0,.5f})foreach(double request in new[]{.002,.5,20}){
   var f=Field(cells);double before=f.VolumeLitres;var untouched=f.heights.ToArray();
   Assert.That(HeightFieldTransfer.Extract(f,new Vector2(edge,edge),.3f,request,out var removed,out var error),Is.True,error);
   Assert.That(removed,Is.GreaterThanOrEqualTo(.001).And.LessThanOrEqualTo(request));Assert.That(before-f.VolumeLitres,Is.EqualTo(removed).Within(1e-10));Assert.That(f.heights.All(h=>h>=0&&h<=.08f),Is.True);Assert.That(f.heights[0],Is.EqualTo(untouched[0]));
  }
 }
 [Test] public void ExtractionRefusalsPreserveInput(){
  var f=Field(4);var before=f.heights.ToArray();foreach(double amount in new[]{0,double.NaN,double.PositiveInfinity,8001})Assert.That(HeightFieldTransfer.Extract(f,Vector2.zero,.2f,amount,out _,out _),Is.False);
  foreach(var point in new[]{new Vector2(.125f,.125f),new Vector2(2,0),new Vector2(float.NaN,0)})Assert.That(HeightFieldTransfer.Extract(f,point,.005f,1,out _,out _),Is.False);
  Assert.That(f.heights,Is.EqualTo(before));f.heights=new float[25];Assert.That(HeightFieldTransfer.Extract(f,Vector2.zero,.3f,1,out var removed,out _),Is.False);Assert.That(removed,Is.Zero);Assert.That(f.heights.All(h=>h==0),Is.True);
 }
 [Test] public void MaterialBoundsCopiesAndPrototypesKeepQuantityIndependentOfGeometry(){
  var b=Ball();var store=b.materialStores[0];Assert.That(store.Validate(out var error),Is.True,error);
  var copy=b.Copy();copy.materialStores[0].amountLitres=.1;Assert.That(store.amountLitres,Is.EqualTo(.32123456789));
  var prototype=CreationPrototype.Capture(b);Assert.That(prototype.Validate(out error),Is.True,error);var spawned=prototype.Instantiate("Scaled contents",Vector3.zero,Quaternion.identity,2);spawned.materialStores[0].material="Sand";Assert.That(prototype.materialStores[0].material,Is.EqualTo("Snow"));Assert.That(spawned.materialStores[0].amountLitres,Is.EqualTo(store.amountLitres));
  foreach(double n in new[]{double.NaN,-.1,.6,double.PositiveInfinity}){var bad=store.Copy();bad.amountLitres=n;Assert.That(bad.Validate(out _),Is.False);}
  foreach(double n in new[]{0,8001,double.NaN}){var bad=store.Copy();bad.capacityLitres=n;Assert.That(bad.Validate(out _),Is.False);}
  var future=store.Copy();future.version=2;Assert.That(future.Validate(out _),Is.False);b.materialStores=new[]{store,store.Copy()};Assert.That(RoomMaterialStore.ValidateCollection(b,out _),Is.False);b.kind=RoomObjectKind.Maestro;b.materialStores=new[]{store};Assert.That(RoomMaterialStore.ValidateCollection(b,out _),Is.False);
  Assert.That(CreationPrototype.ValidateObjects(Enumerable.Range(0,16).Select(_=>Ball()).ToArray(),out error),Is.True,error);Assert.That(CreationPrototype.ValidateObjects(Enumerable.Range(0,17).Select(_=>Ball()).ToArray(),out _),Is.False);
 }
 [Test] public void OldCleanRoomAndUnknownStoreVersionPreserveOriginalFiles(){
  string dir=Path.Combine(Path.GetTempPath(),"MaestroMaterial-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{var room=new RoomDocument{version=16,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};string old=Path.Combine(dir,"room.v16.json"),bytes=JsonUtility.ToJson(room);File.WriteAllText(old,bytes);var storage=new RoomStorage(dir);Assert.That(storage.Load(out var error),Is.Not.Null,error);Assert.That(File.ReadAllText(old),Is.EqualTo(bytes));Assert.That(File.Exists(Path.Combine(dir,RoomStorage.FileName)),Is.False);
   room.version=RoomDocument.CurrentVersion;room.objects=room.objects.Append(Ball()).ToArray();room.objects.Last().materialStores[0].version=2;string path=Path.Combine(dir,RoomStorage.FileName),future=JsonUtility.ToJson(room);File.WriteAllText(path,future);storage=new RoomStorage(dir);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(future));
  }finally{Directory.Delete(dir,true);}
 }
 [Test] public void MixedEditCreateBaselineCoversOnlyExistingMembersAndUndoKeepsTheirObservedPose(){
  var source=Ball();var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},source}};var journal=new RoomJournal(room);var changed=source.Copy();changed.materialStores[0].amountLitres=.1;changed.position=Vector3.right;var added=Ball();var before=new RoomLayout{placements=new[]{new ObjectPlacement{target=source.id,position=Vector3.right,rotation=Quaternion.identity,scale=1}}};
  Assert.That(journal.EditBaseline(new[]{changed,added},Array.Empty<string>(),before,out _,out var error),Is.True,error);Assert.That(journal.Apply(new[]{changed,added},Array.Empty<string>(),out error,before),Is.True,error);Assert.That(journal.Undo(),Is.True);Assert.That(journal.Read(added.id),Is.Null);Assert.That(journal.Read(source.id).position,Is.EqualTo(Vector3.right));Assert.That(journal.Read(source.id).materialStores[0].amountLitres,Is.EqualTo(source.materialStores[0].amountLitres));
  before.placements[0].target=added.id;Assert.That(journal.EditBaseline(new[]{changed,added},Array.Empty<string>(),before,out _,out _),Is.False);Assert.That(journal.Apply(new[]{changed,added},Array.Empty<string>(),out _,before),Is.False);Assert.That(journal.Read(added.id),Is.Null);
 }
 [Test] public void SharedContractValidatesQuantityFeaturesAndCreationBudget(){
  foreach(var row in JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/material-packing-contract.json")))){
   string id=(string)row["id"];var args=(JObject)row["arguments"];bool valid=(bool)row["valid"];Assert.That(BehaviourCatalog.TryCall(id,1,args,out var call,out var error),Is.EqualTo(valid),(string)row["name"]+": "+error);if(!valid)continue;
   Assert.That(call.Resources,Is.EqualTo(new[]{new string('a',32)}));var program=BehaviourProgram.FromInvocation(new JObject{["id"]=id,["version"]=1,["arguments"]=args});Assert.That(BehaviourProgram.TryParse(program,out _,out error),Is.True,error);
   if(id=="object.material.pack"){Assert.That(new PackMaterialCapability().MaximumCreatedObjects(args),Is.EqualTo(1));Assert.That(new PackMaterialCapability().InputSchema["x-features"].Values<string>(),Does.Contain("materialPacking.v1"));}
  }
 }
}}

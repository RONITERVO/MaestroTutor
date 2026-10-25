// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class RoomMaterialPrecisionTests {
  // The actual packed quantity that previously changed by one ULP on disk load.
  const double Reported=0.49999987822957331;
  string directory;
  [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"maestro-material-precision-"+Guid.NewGuid().ToString("N"));}
  [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
  static double Adjacent(double value,int offset)=>BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value)+offset);
  static RoomDocument Full(int offset=0){
   double value=Adjacent(Reported,offset);
   var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{
    new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},
    new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Ball,materialStores=new[]{new RoomMaterialStore{capacityLitres=value,amountLitres=value}}}}};
   Assert.IsTrue(room.Validate(out var error),error);return room;
  }
  static RoomMaterialStore Store(RoomDocument room)=>room.objects[2].materialStores[0];
  static void Same(RoomMaterialStore expected,RoomMaterialStore actual){
   Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected.amountLitres),BitConverter.DoubleToInt64Bits(actual.amountLitres),"Exact carried amount");
   Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected.capacityLitres),BitConverter.DoubleToInt64Bits(actual.capacityLitres),"Exact carried capacity");
   Assert.AreEqual(expected.material,actual.material);
  }
  [TestCase(-1)][TestCase(0)][TestCase(1)]public void MaterialDefinitionAndPrototypeKeepExactMeasuredNumbers(int offset){
   var room=Full(offset);var store=Store(room);var wire=MaterialStoreCapability.Definition(store);
   Assert.IsTrue(CapabilityArguments.Validate(wire,MaterialStoreCapability.DefinitionSchema(),out var error),error);
   Assert.AreEqual(store.amountLitres,(double)wire["amountLitres"]);Same(store,MaterialStoreCapability.ReadDefinition(wire));
   var adjacent=store.Copy();adjacent.amountLitres=Adjacent(store.amountLitres,-1);
   RoomLiquidCodec.Write(adjacent,wire);Assert.AreEqual(adjacent.amountLitres,(double)JObject.Parse(wire.ToString())["amountLitres"]);
   Same(adjacent,MaterialStoreCapability.ReadDefinition(wire));
   var prototype=CreationPrototype.Capture(room.objects[2]);var encoded=CreationPrototypeSchema.Encode(prototype);
   Assert.IsTrue(CapabilityArguments.Validate(encoded,CreationPrototypeSchema.Schema(),out error),error);
   var decoded=CreationPrototype.Read(encoded);Assert.IsTrue(decoded.Validate(out error),error);Same(store,decoded.materialStores[0]);
   Same(store,decoded.Instantiate("Copied material",Vector3.zero,Quaternion.identity,1).materialStores[0]);
  }
  [TestCase(-1,false)][TestCase(0,false)][TestCase(1,false)]
  [TestCase(-1,true)][TestCase(0,true)][TestCase(1,true)]
  public void MaterialSurvivesSynchronousAndPreparedSaveExactly(int offset,bool prepared){
   var room=Full(offset);var storage=new RoomStorage(directory);
   Assert.IsTrue(storage.Save(room,out var error),error);
   var restored=new RoomStorage(directory).Load(out error);Assert.IsNotNull(restored,error);Same(Store(room),Store(restored));
   Store(room).amountLitres=Adjacent(Store(room).amountLitres,-1);
   if(prepared){using var candidate=storage.PrepareSaveAsync(room).GetAwaiter().GetResult();Assert.IsTrue(storage.PublishPrepared(candidate,out error),error);}
   else Assert.IsTrue(storage.Save(room,out error),error);
   var reopened=new RoomStorage(directory);restored=reopened.Load(out error);Assert.IsNotNull(restored,error);Same(Store(room),Store(restored));
   Assert.IsTrue(reopened.Save(restored,out error),error);
   var resaved=new RoomStorage(directory).Load(out error);Assert.IsNotNull(resaved,error);Same(Store(room),Store(resaved));
  }
  [TestCase(-1)][TestCase(0)][TestCase(1)]public void MaterialSurvivesPairedMemoryPublicationExactly(int offset){
   var room=Full(offset);var snapshot=RoomSnapshotTransaction.FromDocuments(room,ProgramMemoryDocument.Empty());
   RoomSnapshotTransaction.Publish(directory,RoomSnapshotTransaction.Capture(directory),snapshot);
   Assert.AreEqual(snapshot.Identity,RoomSnapshotTransaction.Capture(directory).Identity);
   var restored=new RoomStorage(directory).Load(out var error);Assert.IsNotNull(restored,error);Same(Store(room),Store(restored));
  }
  [TestCase(false)][TestCase(true)]public void MeasuredTemplateEntriesAndTheirPrototypesKeepExactDetachedQuantities(bool liquid){
   string template=liquid?"cup":"material-scoop",collection=liquid?"containers":"materialStores";
   string amountKey=liquid?"amountMl":"amountLitres",capacityKey=liquid?"capacityMl":"capacityLitres";
   double amount=liquid?Reported*10000:Reported,capacity=Adjacent(amount,1);
   var source=CreationTemplates.All.Single(t=>t.Id==template).Source;var wire=(JObject)source["definition"][collection][0];
   wire.Remove(amountKey);wire.Add(amountKey,amount);wire.Remove(capacityKey);wire.Add(capacityKey,capacity);
   var entry=new CreationTemplates.Entry(Encoding.UTF8.GetBytes(source.ToString()));
   var item=new RoomObjectData{id=new string('b',32),kind=RoomObjectKind.Assembly,recipe=entry.Recipe,collision=entry.Collision,
    surfaces=entry.Surfaces,drawingTips=entry.DrawingTips,snapPoints=entry.SnapPoints,heightFields=entry.HeightFields,
    sculptTips=entry.SculptTips,materialStores=entry.MaterialStores,containers=entry.Containers};
   Assert.IsTrue(RoomControls.SetPhysics(item,entry.Physics,out var error),error);
   if(liquid){Assert.AreEqual(amount,item.containers[0].amountMl);Assert.AreEqual(capacity,item.containers[0].capacityMl);}
   else{Assert.AreEqual(amount,item.materialStores[0].amountLitres);Assert.AreEqual(capacity,item.materialStores[0].capacityLitres);}
   var encoded=CreationPrototypeSchema.Encode(CreationPrototype.Capture(item));
   Assert.IsTrue(CapabilityArguments.Validate(encoded,CreationPrototypeSchema.Schema(),out error),error);
   var decoded=CreationPrototype.Read(encoded);Assert.IsTrue(decoded.Validate(out error),error);
   if(liquid){Assert.AreEqual(amount,decoded.containers[0].amountMl);Assert.AreEqual(capacity,decoded.containers[0].capacityMl);item.containers[0].amountMl=0;Assert.AreEqual(amount,entry.Containers[0].amountMl);}
   else{Assert.AreEqual(amount,decoded.materialStores[0].amountLitres);Assert.AreEqual(capacity,decoded.materialStores[0].capacityLitres);item.materialStores[0].amountLitres=0;Assert.AreEqual(amount,entry.MaterialStores[0].amountLitres);}
   Assert.AreEqual(amount,(double)entry.Source["definition"][collection][0][amountKey]);
  }
  [Test]public void MaterialOneUlpOverCapacityIsRejectedWithoutClampingOrFallback(){
   var room=Full();var store=Store(room);double excessive=Adjacent(store.capacityLitres,1);
   var wire=MaterialStoreCapability.Definition(store);wire.Remove("amountLitres");wire.Add("amountLitres",excessive);
   var decoded=MaterialStoreCapability.ReadDefinition(wire);Assert.AreEqual(excessive,decoded.amountLitres);Assert.IsFalse(decoded.Validate(out _));
   Assert.IsFalse(CapabilityArguments.Validate(wire,MaterialStoreCapability.DefinitionSchema(),out _));
   var storage=new RoomStorage(directory);Assert.IsTrue(storage.Save(room,out var error),error);
   string path=Path.Combine(directory,RoomStorage.FileName);
   var saved=JObject.Parse(File.ReadAllText(path));var stored=(JObject)saved["objects"][2]["materialStores"][0];
   stored.Remove("amountLitres");stored.Add("amountLitres",excessive);string invalid=saved.ToString();File.WriteAllText(path,invalid);
   storage=new RoomStorage(directory);Assert.IsNull(storage.Load(out _));Assert.IsTrue(storage.ReadOnly);Assert.AreEqual(invalid,File.ReadAllText(path));
  }
  [Test]public void MaterialQuantityWireRejectsMissingNonNumericAndNonfiniteValues(){
   foreach(string key in new[]{"capacityLitres","amountLitres"}){
    var missing=MaterialStoreCapability.Definition(Store(Full()));missing.Remove(key);
    Assert.Throws<ArgumentException>(()=>RoomLiquidCodec.Read(new RoomMaterialStore(),missing));
    foreach(var bad in new JToken[]{JValue.CreateNull(),new JValue(true),new JValue("0.5"),new JValue(double.NaN),new JValue(double.PositiveInfinity)}){
     var wire=MaterialStoreCapability.Definition(Store(Full()));wire.Remove(key);wire.Add(key,bad);
     Assert.Throws<ArgumentException>(()=>RoomLiquidCodec.Read(new RoomMaterialStore(),wire),key+" "+bad.Type);
    }
   }
  }
  [Test]public void MaterialExistingFormat44ReadsWithoutRewritingItsNumbers(){
   var room=Full();var wire=JObject.Parse(JsonUtility.ToJson(room));wire["version"]=44;
   RoomLiquidCodec.Write(Store(room),(JObject)wire["objects"][2]["materialStores"][0]);
   string original=wire.ToString();Directory.CreateDirectory(directory);string path=Path.Combine(directory,RoomStorage.FileName);File.WriteAllText(path,original);
   var restored=new RoomStorage(directory).Load(out var error);Assert.IsNotNull(restored,error);Same(Store(room),Store(restored));Assert.AreEqual(original,File.ReadAllText(path));
  }
  [TestCase(false)][TestCase(true)]public void MaterialNewerRoomOrStoreVersionRemainsPreservedAndReadOnly(bool component){
   var room=Full();Assert.IsTrue(new RoomStorage(directory).Save(room,out var error),error);
   string path=Path.Combine(directory,RoomStorage.FileName);var wire=JObject.Parse(File.ReadAllText(path));
   if(component)wire["objects"][2]["materialStores"][0]["version"]=2;else wire["version"]=RoomDocument.CurrentVersion+1;
   string future=wire.ToString();File.WriteAllText(path,future);var storage=new RoomStorage(directory);
   Assert.IsNull(storage.Load(out _));Assert.IsTrue(storage.ReadOnly);Assert.AreEqual(future,File.ReadAllText(path));
  }
 }
}

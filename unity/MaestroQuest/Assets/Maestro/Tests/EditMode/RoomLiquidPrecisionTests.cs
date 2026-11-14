// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class RoomLiquidPrecisionTests {
  string directory;
  [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"maestro-liquid-precision-"+Guid.NewGuid().ToString("N"));}
  [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
  static RoomDocument Full(){
   var terrain=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,physics=ItemPhysics.Fixed,
    heightFields=new[]{TerrainSpillTests.Bed()},containers=new[]{new RoomContainer{version=4,height=.5f}}};
   Assert.IsTrue(RoomTerrainReservoir.Normalize(terrain,out var error),error);
   terrain.containers[0].amountMl=terrain.containers[0].capacityMl;
   var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{
    new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},terrain}};
   Assert.IsTrue(room.Validate(out error),error);return room;
  }
  static RoomContainer Liquid(RoomDocument room)=>room.objects[2].containers[0];
  static void Same(RoomContainer expected,RoomContainer actual){
   Assert.AreEqual(expected.amountMl,actual.amountMl);Assert.AreEqual(expected.capacityMl,actual.capacityMl);
   Assert.IsTrue(actual.SpillAtTerrainEdge);
  }
  [Test]public void FullTerrainDefinitionAndPrototypeKeepExactMeasuredNumbers(){
   var room=Full();var c=Liquid(room);var wire=ContainerCapability.Definition(c,true,true);
   Assert.AreEqual(c.amountMl,(double)wire["amountMl"]);Same(c,ContainerCapability.ReadDefinition(wire));
   var adjacent=c.Copy();adjacent.amountMl=BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(c.amountMl)-1);
   RoomLiquidCodec.Write(adjacent,wire);Assert.AreEqual(adjacent.amountMl,(double)JObject.Parse(wire.ToString())["amountMl"]);
   Same(adjacent,ContainerCapability.ReadDefinition(wire));
   var prototype=CreationPrototype.Capture(room.objects[2]);var encoded=CreationPrototypeSchema.Encode(prototype);
   Assert.IsTrue(CapabilityArguments.Validate(encoded,CreationPrototypeSchema.Schema(),out var error),error);
   var decoded=CreationPrototype.Read(encoded);Assert.IsTrue(decoded.Validate(out error),error);Same(c,decoded.containers[0]);
  }
  [TestCase(false)][TestCase(true)]public void FullTerrainSurvivesSynchronousAndPreparedSave(bool prepared){
   var room=Full();var storage=new RoomStorage(directory);
   Assert.IsTrue(storage.Save(room,out var error),error);Same(Liquid(room),Liquid(new RoomStorage(directory).Load(out error)));
   Liquid(room).amountMl-=.01337;
   if(prepared){using var candidate=storage.PrepareSaveAsync(room).GetAwaiter().GetResult();Assert.IsTrue(storage.PublishPrepared(candidate,out error),error);}
   else Assert.IsTrue(storage.Save(room,out error),error);
   var restored=new RoomStorage(directory).Load(out error);Assert.IsNotNull(restored,error);Same(Liquid(room),Liquid(restored));
  }
  [Test]public void FullTerrainSurvivesPairedMemoryPublication(){
   var room=Full();var snapshot=RoomSnapshotTransaction.FromDocuments(room,ProgramMemoryDocument.Empty());
   RoomSnapshotTransaction.Publish(directory,RoomSnapshotTransaction.Capture(directory),snapshot);
   Assert.AreEqual(snapshot.Identity,RoomSnapshotTransaction.Capture(directory).Identity);
   var restored=new RoomStorage(directory).Load(out var error);Assert.IsNotNull(restored,error);Same(Liquid(room),Liquid(restored));
  }
  [Test]public void OneUlpOverCapacityIsRejectedWithoutClampingOrFallback(){
   var room=Full();var c=Liquid(room);double excessive=BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(c.capacityMl)+1);
   var wire=ContainerCapability.Definition(c,true,true);wire.Remove("amountMl");wire.Add("amountMl",excessive);
   var decoded=ContainerCapability.ReadDefinition(wire);Assert.AreEqual(excessive,decoded.amountMl);Assert.IsFalse(decoded.Validate(out _));
   Assert.IsTrue(new RoomStorage(directory).Save(room,out var error),error);
   string path=Path.Combine(directory,RoomStorage.FileName);var saved=JObject.Parse(File.ReadAllText(path));
   var stored=(JObject)saved["objects"][2]["containers"][0];stored.Remove("amountMl");stored.Add("amountMl",excessive);string invalid=saved.ToString();File.WriteAllText(path,invalid);
   var storage=new RoomStorage(directory);Assert.IsNull(storage.Load(out _));Assert.IsTrue(storage.ReadOnly);Assert.AreEqual(invalid,File.ReadAllText(path));
  }
 }
}

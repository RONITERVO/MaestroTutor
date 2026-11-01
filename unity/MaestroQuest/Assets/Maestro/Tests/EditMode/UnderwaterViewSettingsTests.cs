// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class UnderwaterViewSettingsTests {
  [Test]public void UnderwaterSettingsHaveIndependentCopiedPresentationWithoutChangingFluidIdentity(){
   var a=new RoomContainer{amountMl=200};var b=a.Copy();
   b.underwater.enabled=true;b.underwater.tint="#103060";b.underwater.densityPerMetre=1;
   Assert.That(a.underwater.enabled,Is.False);Assert.That(a.SameLiquid(b),Is.True);
   Assert.That(RoomContainer.Transfer(a,b,10,out var moved,out var error),Is.True,error);Assert.That(moved,Is.EqualTo(10));
   Assert.That(a.underwater.enabled,Is.False);Assert.That(b.underwater.enabled,Is.True);
   Assert.That(UnderwaterViewSettings.Effective(new(){version=0,tint=null,densityPerMetre=0}).enabled,Is.False);
  }
  [Test]public void UnderwaterWireRejectsMalformedAndFutureSettings(){
   var good=JObject.FromObject(new UnderwaterViewSettings());Assert.That(UnderwaterViewSettings.Wire(good),Is.True);
   foreach(var change in new[]{new JObject{["version"]=2},new JObject{["enabled"]="true"},new JObject{["densityPerMetre"]=-1},new JObject{["densityPerMetre"]=9},new JObject{["tint"]="#12345678"},new JObject{["surprise"]=true}}){
    var bad=(JObject)good.DeepClone();bad.Merge(change);Assert.That(UnderwaterViewSettings.Wire(bad),Is.False,bad.ToString());
   }
  }
  [Test]public void UnderwaterStorageRetainsLegacyDefaultsAndProtectsNewerSettings(){
   var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),name="Water",kind=RoomObjectKind.Block,containers=new[]{new RoomContainer{amountMl=100,underwater=new(){enabled=true,tint="#204060",densityPerMetre=.6f}}}}}};
   var dir=Path.Combine(Path.GetTempPath(),"MaestroUnderwater-"+Guid.NewGuid().ToString("N"));
   try{
    var store=new RoomStorage(dir);Assert.IsTrue(store.Save(room,out var error),error);var loaded=store.Load(out error);Assert.IsNotNull(loaded,error);
    Assert.AreEqual("#204060",loaded.objects[2].containers[0].underwater.tint);Assert.IsNotNull(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()));
    var wire=JObject.Parse(RoomLiquidCodec.Encode(room));((JObject)wire["objects"][2]["containers"][0]).Remove("underwater");Assert.IsFalse(UnderwaterViewSettings.ValidWire(wire));
    wire["version"]=44;var legacy=Path.Combine(dir,"legacy");Directory.CreateDirectory(legacy);var retained=Path.Combine(legacy,"room.v44.json");var legacyBytes=wire.ToString();File.WriteAllText(retained,legacyBytes);
    store=new RoomStorage(legacy);loaded=store.Load(out error);Assert.IsNotNull(loaded,error);Assert.IsFalse(loaded.objects[2].containers[0].underwater.enabled);
    Assert.IsTrue(store.Save(loaded,out error),error);Assert.AreEqual(legacyBytes,File.ReadAllText(retained));Assert.IsTrue(File.Exists(Path.Combine(legacy,RoomStorage.FileName)));
    var old=new VersionedRoomFile<RoomDocument>(legacy,"room",4*1024*1024,r=>r.Validate(out _),r=>r.Copy(),RoomStorage.Normalize,r=>r.version=44,version:44);
    Assert.IsNull(old.Load(out _));Assert.IsTrue(old.ReadOnly,"An older client must not roll back and overwrite a room with newer settings");
    wire=JObject.Parse(RoomLiquidCodec.Encode(room));wire["objects"][2]["containers"][0]["underwater"]["version"]=2;var raw=wire.ToString();
    File.WriteAllText(Path.Combine(dir,RoomStorage.FileName),raw);store=new RoomStorage(dir);Assert.IsNull(store.Load(out _));Assert.IsTrue(store.ReadOnly);Assert.AreEqual(raw,File.ReadAllText(Path.Combine(dir,RoomStorage.FileName)));
   }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
  }
  [Test]public void UnderwaterCapabilityAndPortableDefinitionExposeTheSameSettingsWithoutChangingOldFacts(){
   Assert.That(BehaviourCatalog.Action("object.container.view.configure"),Is.Not.Null);
   Assert.That(BehaviourCatalog.Fact("object.container.view"),Is.Not.Null);
   var data=new RoomContainer();data.underwater.enabled=true;
   Assert.That(ContainerCapability.Definition(data,true,true)["underwater"],Is.Null,"Old typed container facts remain stable");
   var wire=JObject.Parse(UnityEngine.JsonUtility.ToJson(data));RoomLiquidCodec.Write(data,wire);
   var copy=ContainerCapability.ReadDefinition(wire);Assert.That(copy.underwater.enabled,Is.True);
   wire.Remove("underwater");copy=ContainerCapability.ReadDefinition(wire);Assert.That(copy.underwater.enabled,Is.False);
   Assert.That(ContainerCapability.SavedSchema()["properties"]["underwater"]["x-features"].ToString(),Does.Contain("underwaterView.v1"));
  }
 }
}

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
 public sealed class WaterAudioSettingsTests {
  [Test]public void SoundTreatmentCopiesIndependentlyAndDoesNotPourOrChangeLiquidIdentity(){
   var a=new RoomContainer{amountMl=200};var b=a.Copy();b.waterAudio.enabled=true;b.waterAudio.attenuationDbPerMetre=3;
   Assert.IsFalse(a.waterAudio.enabled);Assert.IsTrue(a.SameLiquid(b));
   Assert.IsTrue(RoomContainer.Transfer(a,b,10,out var moved,out var error),error);Assert.AreEqual(10,moved);
   Assert.IsTrue(b.waterAudio.enabled);Assert.AreEqual(3,b.waterAudio.attenuationDbPerMetre);Assert.IsFalse(a.waterAudio.enabled);
   Assert.IsFalse(WaterAudioSettings.Effective(new(){version=0,attenuationDbPerMetre=0,mufflingPerMetre=0}).enabled);
  }
  [Test]public void SoundWireRejectsMalformedAndFutureSettings(){
   var good=JObject.FromObject(new WaterAudioSettings());Assert.IsTrue(WaterAudioSettings.Wire(good));
   foreach(var change in new[]{new JObject{["version"]=2},new JObject{["enabled"]="true"},new JObject{["attenuationDbPerMetre"]=-1},new JObject{["attenuationDbPerMetre"]=25},new JObject{["mufflingPerMetre"]=9},new JObject{["mufflingPerMetre"]="1"},new JObject{["surprise"]=true}}){
    var bad=(JObject)good.DeepClone();bad.Merge(change);Assert.IsFalse(WaterAudioSettings.Wire(bad),bad.ToString());
   }
  }
  [Test]public void SoundStoragePreservesLegacyDefaultsAndProtectsNewerSettings(){
   var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),name="Water",kind=RoomObjectKind.Block,containers=new[]{new RoomContainer{amountMl=100,waterAudio=new(){enabled=true,attenuationDbPerMetre=3,mufflingPerMetre=2}}}}}};
   var dir=Path.Combine(Path.GetTempPath(),"MaestroWaterAudio-"+Guid.NewGuid().ToString("N"));
   try{
    var store=new RoomStorage(dir);Assert.IsTrue(store.Save(room,out var error),error);var loaded=store.Load(out error);Assert.IsNotNull(loaded,error);
    Assert.AreEqual(3,loaded.objects[2].containers[0].waterAudio.attenuationDbPerMetre);Assert.IsNotNull(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()));
    var wire=JObject.Parse(RoomLiquidCodec.Encode(room));((JObject)wire["objects"][2]["containers"][0]).Remove("waterAudio");Assert.IsFalse(WaterAudioSettings.ValidWire(wire));
    wire["version"]=45;var legacy=Path.Combine(dir,"legacy");Directory.CreateDirectory(legacy);var retained=Path.Combine(legacy,"room.v45.json");var raw=wire.ToString();File.WriteAllText(retained,raw);
    store=new RoomStorage(legacy);loaded=store.Load(out error);Assert.IsNotNull(loaded,error);Assert.IsFalse(loaded.objects[2].containers[0].waterAudio.enabled);
    Assert.IsTrue(store.Save(loaded,out error),error);Assert.AreEqual(raw,File.ReadAllText(retained));
    var old=new VersionedRoomFile<RoomDocument>(legacy,"room",4*1024*1024,r=>r.Validate(out _),r=>r.Copy(),RoomStorage.Normalize,r=>r.version=45,version:45);
    Assert.IsNull(old.Load(out _));Assert.IsTrue(old.ReadOnly);
    wire=JObject.Parse(RoomLiquidCodec.Encode(room));wire["objects"][2]["containers"][0]["waterAudio"]["version"]=2;raw=wire.ToString();
    File.WriteAllText(Path.Combine(dir,RoomStorage.FileName),raw);store=new RoomStorage(dir);Assert.IsNull(store.Load(out _));Assert.IsTrue(store.ReadOnly);Assert.AreEqual(raw,File.ReadAllText(Path.Combine(dir,RoomStorage.FileName)));
   }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
  }
  [Test]public void PortableContainersCarrySoundSettingsWithoutChangingOldTypedContainerFacts(){
   Assert.IsNotNull(BehaviourCatalog.Action("object.container.audio.configure"));Assert.IsNotNull(BehaviourCatalog.Fact("world.audio.water"));
   var data=new RoomContainer();data.waterAudio.enabled=true;
   Assert.IsNull(ContainerCapability.Definition(data,true,true)["waterAudio"]);
   var wire=JObject.Parse(UnityEngine.JsonUtility.ToJson(data));RoomLiquidCodec.Write(data,wire);
   Assert.IsTrue(ContainerCapability.ReadDefinition(wire).waterAudio.enabled);
   wire.Remove("waterAudio");Assert.IsFalse(ContainerCapability.ReadDefinition(wire).waterAudio.enabled);
   Assert.That(ContainerCapability.SavedSchema()["properties"]["waterAudio"]["x-features"].ToString(),Does.Contain("waterAudio.v1"));
  }
 }
}

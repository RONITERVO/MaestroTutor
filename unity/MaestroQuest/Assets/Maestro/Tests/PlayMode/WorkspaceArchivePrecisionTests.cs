// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  static double ArchiveAdjacent(double value,int offset)=>BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value)+offset);
  MovementControls ArchivePrecisionControls(){
   var controls=root.AddComponent<MovementControls>();controls.Initialize(root.GetComponent<RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,directory:directory);
   workshop.Modules.Flush();return controls;
  }
  RoomObjectData ArchivePrecisionObject(int offset){
   var data=editor.Read(editor.Identity(block));
   double material=ArchiveAdjacent(.49999987822957331,offset),liquid=ArchiveAdjacent(.49999987822957331*10000,offset);
   data.materialStores=new[]{new RoomMaterialStore{capacityLitres=ArchiveAdjacent(material,1),amountLitres=material}};
   data.containers=new[]{new RoomContainer{capacityMl=ArchiveAdjacent(liquid,1),amountMl=liquid}};
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),error);
   var accepted=editor.Read(data.id);ArchiveExactStores(data,accepted);return accepted;
  }
  static void ArchiveExact(double expected,double actual,string name)=>Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected),BitConverter.DoubleToInt64Bits(actual),name);
  static void ArchiveExactStores(RoomObjectData expected,RoomObjectData actual){
   ArchiveExact(expected.materialStores[0].amountLitres,actual.materialStores[0].amountLitres,"Archived material amount");
   ArchiveExact(expected.materialStores[0].capacityLitres,actual.materialStores[0].capacityLitres,"Archived material capacity");
   ArchiveExact(expected.containers[0].amountMl,actual.containers[0].amountMl,"Archived liquid amount");
   ArchiveExact(expected.containers[0].capacityMl,actual.containers[0].capacityMl,"Archived liquid capacity");
  }
  static Dictionary<string,byte[]> ArchivePrecisionEntries(byte[] archive){
   using var stream=new MemoryStream(archive);using var zip=new ZipArchive(stream,ZipArchiveMode.Read);
   return zip.Entries.ToDictionary(entry=>entry.FullName,entry=>{using var input=entry.Open();using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();});
  }
  static byte[] ArchivePrecisionZip(Dictionary<string,byte[]> entries){
   using var output=new MemoryStream();
   using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))foreach(var pair in entries){using var stream=zip.CreateEntry(pair.Key,System.IO.Compression.CompressionLevel.Optimal).Open();stream.Write(pair.Value,0,pair.Value.Length);}
   return output.ToArray();
  }
  [UnityTest]public IEnumerator WorkspaceArchivePrecisionCapturesLiveMaterialAndLiquidThenStagesAndReopensExactly(){
   var controls=ArchivePrecisionControls();Directory.CreateDirectory(Path.Combine(directory,"precision-stage"));
   foreach(int offset in new[]{-1,0,1}){
    var expected=ArchivePrecisionObject(offset);
    var capture=WorkspaceArchiveCapture.Start(editor,workshop,controls,Path.Combine(directory,"precision-exports"));
    yield return TestLifecycleDrain.Wait(capture);var result=capture.GetAwaiter().GetResult();var archive=File.ReadAllBytes(result.Path);
    var entries=ArchivePrecisionEntries(archive);var json=JObject.Parse(Encoding.UTF8.GetString(entries[RoomStorage.FileName]));
    var wire=json["objects"].Single(value=>(string)value["id"]==expected.id);
    ArchiveExact(expected.materialStores[0].amountLitres,(double)wire["materialStores"][0]["amountLitres"],"Captured material JSON amount");
    ArchiveExact(expected.materialStores[0].capacityLitres,(double)wire["materialStores"][0]["capacityLitres"],"Captured material JSON capacity");
    ArchiveExact(expected.containers[0].amountMl,(double)wire["containers"][0]["amountMl"],"Captured liquid JSON amount");
    ArchiveExact(expected.containers[0].capacityMl,(double)wire["containers"][0]["capacityMl"],"Captured liquid JSON capacity");
    using(var input=new MemoryStream(archive))using(var staged=WorkspaceArchive.Stage(input,Path.Combine(directory,"precision-stage"))){
     Assert.AreEqual(result.Receipt.ManifestHash,staged.Receipt.ManifestHash);
     var storage=new RoomStorage(staged.DirectoryPath);var restored=storage.Load(out var error);Assert.IsNotNull(restored,error);
     ArchiveExactStores(expected,restored.objects.Single(value=>value.id==expected.id));
     Assert.IsTrue(storage.Save(restored,out error),error);
     var reopened=new RoomStorage(staged.DirectoryPath).Load(out error);Assert.IsNotNull(reopened,error);
     ArchiveExactStores(expected,reopened.objects.Single(value=>value.id==expected.id));
    }
    ArchiveExactStores(expected,editor.Read(expected.id));
   }
  }
  [UnityTest]public IEnumerator WorkspaceArchivePrecisionRejectsAdjacentOverCapacityMaterialAndLiquidWithValidManifest(){
   var controls=ArchivePrecisionControls();var expected=ArchivePrecisionObject(0);
   var capture=WorkspaceArchiveCapture.Start(editor,workshop,controls,Path.Combine(directory,"precision-exports"));
   yield return TestLifecycleDrain.Wait(capture);var result=capture.GetAwaiter().GetResult();var original=File.ReadAllBytes(result.Path);
   string destination=Path.Combine(directory,"precision-rejection");Directory.CreateDirectory(destination);
   foreach(bool liquid in new[]{false,true}){
    var entries=ArchivePrecisionEntries(original);var room=JObject.Parse(Encoding.UTF8.GetString(entries[RoomStorage.FileName]));
    var item=room["objects"].Single(value=>(string)value["id"]==expected.id);
    string collection=liquid?"containers":"materialStores",amount=liquid?"amountMl":"amountLitres",capacity=liquid?"capacityMl":"capacityLitres";
    var store=(JObject)item[collection][0];double over=ArchiveAdjacent((double)store[capacity],1);store.Remove(amount);store.Add(amount,over);
    entries[RoomStorage.FileName]=Encoding.UTF8.GetBytes(room.ToString(Formatting.None));
    // Keep ZIP integrity valid: this must fail semantic quantity validation,
    // not an unrelated stale digest or byte-count guard.
    var manifest=JObject.Parse(Encoding.UTF8.GetString(entries["manifest.json"]));
    var record=manifest["entries"].Single(value=>(string)value["path"]==RoomStorage.FileName);
    record["bytes"]=entries[RoomStorage.FileName].Length;record["sha256"]=ModelLibrary.Hash(entries[RoomStorage.FileName]);
    entries["manifest.json"]=Encoding.UTF8.GetBytes(manifest.ToString(Formatting.None));
    using var invalid=new MemoryStream(ArchivePrecisionZip(entries));
    var error=Assert.Throws<InvalidDataException>(()=>{using var unexpected=WorkspaceArchive.Stage(invalid,destination);});
    StringAssert.Contains(liquid?"container":"material store",error.Message);
    Assert.That(Directory.EnumerateFileSystemEntries(destination),Is.Empty,"A refused archive must not leave an import generation behind");
    Assert.That(File.ReadAllBytes(result.Path),Is.EqualTo(original));ArchiveExactStores(expected,editor.Read(expected.id));
   }
  }
 }
}

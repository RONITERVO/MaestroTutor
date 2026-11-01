// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class SavedWorldCapacityTests {
  internal static RoomDocument Document(int count)=>new(){version=RoomDocument.CurrentVersion,objects=new[]{
   new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}
  }.Concat(Enumerable.Range(1,count).Select(i=>new RoomObjectData{id=i.ToString("x32"),kind=RoomObjectKind.Block,name="Saved block "+i,position=new Vector3(i%16*.3f,1,i/16*.3f)})).ToArray()};
  [Test]public void SavedCapacityIsIndependentFromNativeCapacityAndKeepsOlderFilesBounded(){
   var room=Document(RoomDocument.MaximumObjects);Assert.That(RoomDocument.MaximumObjects,Is.EqualTo(256));
   Assert.That(NativeObjectResources.MaximumInstances,Is.EqualTo(128));Assert.That(room.Validate(out var error),Is.True,error);
   Assert.That(Document(257).Validate(out _),Is.False);room.version=41;Assert.That(room.Validate(out _),Is.False);
   room=Document(64);room.version=41;Assert.That(room.Validate(out error),Is.True,error);
   Assert.That(new RoomJournal(room).Snapshot().version,Is.EqualTo(RoomDocument.CurrentVersion));
  }
  [Test]public void SavedCapacityPagesEveryIdentityAndKeepsFrequentContextBounded(){
   var room=new RoomJournal(Document(256));var ids=new HashSet<string>();string cursor="";int pages=0;
   do{
    var page=room.SearchObjects(new string('a',32),"","all",cursor);Assert.That((int)page["total"],Is.EqualTo(258));
    foreach(var entry in (JArray)page["entries"])Assert.That(ids.Add((string)entry["id"]),Is.True);
    Assert.That(((JArray)page["entries"]).Count,Is.InRange(1,3));cursor=(string)page["next"];pages++;
   }while(cursor!=""&&pages<100);
   Assert.That(ids.Count,Is.EqualTo(258));Assert.That(pages,Is.EqualTo(86));
   var context=room.ObserveObjectContext("1".PadLeft(32,'0'),"100".PadLeft(32,'0'));Assert.That(context.Length,Is.EqualTo(4));
   Assert.That(room.CanUndo,Is.False);
  }
  [Test]public void FullSavedWorldRoundTripsAndFailedReplacementKeepsItsLastCheckpoint(){
   var directory=Path.Combine(Path.GetTempPath(),"MaestroSavedCapacity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
   try{
    var document=Document(256);var store=new RoomStorage(directory);store.Load(out _);
    Assert.That(store.Save(document,out var error),Is.True,error);
    string path=Path.Combine(directory,RoomStorage.FileName);var bytes=File.ReadAllBytes(path);Assert.That(bytes.Length,Is.LessThan(4*1024*1024));
    Assert.That(new RoomStorage(directory).Load(out error).objects.Length,Is.EqualTo(258),error);
    Assert.That(store.Save(Document(257),out _),Is.False);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
    Directory.CreateDirectory(path+".pending");document.objects[2].name="Uncommitted";
    try{Assert.That(store.Save(document,out _),Is.False);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));}
    finally{Directory.Delete(path+".pending");}
    Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==document.objects[2].id).name,Is.Not.EqualTo("Uncommitted"));
   }finally{Directory.Delete(directory,true);}
  }
 }
}

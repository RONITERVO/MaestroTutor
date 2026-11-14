// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomWindowTests
    {
        static RoomObjectData Object()=>new(){id=new string('a',32),kind=RoomObjectKind.Block,surfaces=new[]{new DrawingSurface{id="Canvas"}},windows=new[]{new RoomWindow{reveal=.5f}}};
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},Object()}};
        [Test]public void WindowsAreIndependentSavedComponentsWithProtectedSurfaceReferences(){
            var doc=Room();Assert.That(doc.Validate(out var error),Is.True,error);var obj=doc.objects[2];
            obj.surfaces[0].enabled=false;Assert.That(doc.Validate(out error),Is.True,error);
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,-.001f,1.001f}){obj.windows[0].reveal=value;Assert.That(doc.Validate(out _),Is.False);}
            obj.windows[0].reveal=1;obj.windows[0].surface="Absent";Assert.That(doc.Validate(out _),Is.False);
            obj.windows[0].surface="Canvas";obj.surfaces[0].shape="cylinder";obj.surfaces[0].version=2;obj.surfaces[0].curvatureRadius=1;Assert.That(doc.Validate(out _),Is.False);
            obj.surfaces[0]=new DrawingSurface{id="Canvas"};obj.windows=new[]{new RoomWindow(),new RoomWindow{id="Other"}};Assert.That(doc.Validate(out _),Is.False,"Two masks on one plane are ambiguous");
        }
        [Test]public void WindowFormatDoesNotSilentlyDowngradeOrFillMissingFields(){
            var doc=Room();var wire=JObject.Parse(JsonUtility.ToJson(doc));Assert.That(RoomWindow.ValidWire(wire),Is.True);
            foreach(var field in new[]{"version","id","surface","shape","reveal"}){var bad=(JObject)wire.DeepClone();((JObject)bad["objects"][2]["windows"][0]).Remove(field);Assert.That(RoomWindow.ValidWire(bad),Is.False,field);}
            var future=(JObject)wire.DeepClone();future["objects"][2]["windows"][0]["unexpected"]=1;Assert.That(RoomWindow.ValidWire(future),Is.False);
            ((JObject)wire["objects"][0]).Remove("windows");Assert.That(RoomWindow.ValidWire(wire),Is.False);
            doc.version=33;Assert.That(doc.Validate(out _),Is.False);doc.objects[2].windows=Array.Empty<RoomWindow>();Assert.That(doc.Validate(out var error),Is.True,error);
        }
        [Test]public void SavedWindowsRoundTripUndoForkAndTransactionalSnapshot(){
            string path=Path.Combine(Path.GetTempPath(),"MaestroWindows-"+Guid.NewGuid().ToString("N"));
            try{
                Assert.That(RoomStorage.FileName,Is.EqualTo("room.v"+RoomDocument.CurrentVersion+".json"));
                var doc=Room();var storage=new RoomStorage(path);Assert.That(storage.Save(doc,out var error),Is.True,error);var loaded=storage.Load(out error);Assert.That(loaded.objects[2].windows[0].reveal,Is.EqualTo(.5f),error);
                Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                var journal=new RoomJournal(loaded);var copy=journal.Read(new string('a',32));copy.windows[0].reveal=0;Assert.That(journal.Apply(new[]{copy},Array.Empty<string>(),out error),Is.True,error);Assert.That(journal.Undo(),Is.True);Assert.That(journal.Read(copy.id).windows[0].reveal,Is.EqualTo(.5f));
                var fork=journal.Fork();copy.windows[0].shape="ellipse";Assert.That(fork.Apply(new[]{copy},Array.Empty<string>(),out error),Is.True,error);Assert.That(journal.Read(copy.id).windows[0].shape,Is.EqualTo("rectangle"));
            }finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test]public void ConstructionPrototypeRetainsWindowMeaningWithoutWorldOrPhysicalIdentities(){
            var original=Object();var p=CreationPrototype.Capture(original);Assert.That(p.version,Is.EqualTo(4));Assert.That(p.Validate(out var error),Is.True,error);
            var encoded=CreationPrototypeSchema.Encode(p);
            var decoded=CreationPrototype.Read(encoded);Assert.That(decoded.Validate(out error),Is.True,error);var first=decoded.Instantiate("Window",Vector3.zero,Quaternion.identity,1);var second=decoded.Instantiate("Window",Vector3.one,Quaternion.identity,1);
            first.windows[0].reveal=0;Assert.That(second.windows[0].reveal,Is.EqualTo(.5f));Assert.That(second.scanAnchors,Is.Empty);Assert.That(second.id,Is.Not.EqualTo(first.id));
        }
        [Test]public void NewerWindowVersionIsPreservedAsReadOnly(){
            string path=Path.Combine(Path.GetTempPath(),"MaestroWindows-"+Guid.NewGuid().ToString("N"));
            try{Directory.CreateDirectory(path);var doc=Room();doc.objects[2].windows[0].version=2;string raw=JsonUtility.ToJson(doc);File.WriteAllText(Path.Combine(path,RoomStorage.FileName),raw);var storage=new RoomStorage(path);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(path,RoomStorage.FileName)),Is.EqualTo(raw));}
            finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}

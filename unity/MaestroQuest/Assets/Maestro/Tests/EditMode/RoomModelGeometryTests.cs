// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomModelGeometryTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{
            new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},
            new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.ImportedModel,modelHash=new string('a',64),physics=ItemPhysics.Fixed,
                modelGeometry=new(){scaleMode="source",pivot="base",meshCollision=true,walkable=true}}}};
        [Test] public void RigidGeometryRequiresAnExplicitStaticCompatibleModel()
        {
            var doc=Room();var data=doc.objects[2];Assert.That(doc.Validate(out var error),Is.True,error);
            foreach(var mode in new[]{ItemPhysics.Solid,ItemPhysics.Bouncy}) {data.physics=mode;Assert.That(doc.Validate(out _),Is.False);}data.physics=ItemPhysics.Fixed;
            data.collisionShape=ItemCollider.Box;Assert.That(doc.Validate(out _),Is.False);data.collisionShape=ItemCollider.Automatic;
            data.motion=new RoomMotion{frames=new[]{new MotionFrame()}};Assert.That(doc.Validate(out _),Is.False);data.motion=null;
            data.kind=RoomObjectKind.Block;Assert.That(doc.Validate(out _),Is.False);data.kind=RoomObjectKind.ImportedModel;
            foreach(float scale in new[]{0f,float.NaN,float.PositiveInfinity,101}){data.modelGeometry.metresPerUnit=scale;Assert.That(doc.Validate(out _),Is.False);}
            data.modelGeometry.metresPerUnit=1;data.modelGeometry.meshCollision=false;Assert.That(doc.Validate(out _),Is.False);
            data.modelGeometry.walkable=false;Assert.That(doc.Validate(out error),Is.True,error);
        }
        [Test] public void WireSettingsCannotLoseFieldsOrMasqueradeAsAnOlderRoom()
        {
            var doc=Room();var wire=JObject.Parse(JsonUtility.ToJson(doc));Assert.That(RoomModelGeometry.ValidWire(wire),Is.True);
            foreach(var field in new[]{"version","scaleMode","metresPerUnit","pivot","meshCollision","walkable"}) {
                var bad=(JObject)wire.DeepClone();((JObject)bad["objects"][2]["modelGeometry"]).Remove(field);Assert.That(RoomModelGeometry.ValidWire(bad),Is.False,field);
            }
            var extra=(JObject)wire.DeepClone();extra["objects"][2]["modelGeometry"]["unknown"]=0;Assert.That(RoomModelGeometry.ValidWire(extra),Is.False);
            wire["version"]=34;Assert.That(RoomModelGeometry.ValidWire(wire),Is.False);doc.version=34;Assert.That(doc.Validate(out _),Is.False);
            foreach(var item in (JArray)wire["objects"])((JObject)item).Remove("modelGeometry");Assert.That(RoomModelGeometry.ValidWire(wire),Is.True);
        }
        [Test] public void GeometryRoundTripsAcrossStorageSnapshotsUndoAndIndependentCopies()
        {
            string path=Path.Combine(Path.GetTempPath(),"MaestroGeometry-"+Guid.NewGuid().ToString("N"));
            try {
                var doc=Room();var storage=new RoomStorage(path);Assert.That(storage.Save(doc,out var error),Is.True,error);
                var loaded=storage.Load(out error);Assert.That(loaded.objects[2].modelGeometry.Same(doc.objects[2].modelGeometry),Is.True,error);
                Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                var journal=new RoomJournal(loaded);var changed=journal.Read(doc.objects[2].id);changed.modelGeometry.pivot="source";
                Assert.That(journal.Apply(new[]{changed},Array.Empty<string>(),out error),Is.True,error);Assert.That(journal.Undo(),Is.True);
                Assert.That(journal.Read(changed.id).modelGeometry.pivot,Is.EqualTo("base"));
                var copy=loaded.objects[2].Copy();copy.modelGeometry.walkable=false;Assert.That(loaded.objects[2].modelGeometry.walkable,Is.True);
            } finally {if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test] public void PortableModelRetainsGeometryAlongsideWindowsAndRejectsRootMotion()
        {
            var obj=Room().objects[2];obj.surfaces=new[]{new DrawingSurface{id="Canvas"}};obj.windows=new[]{new RoomWindow()};
            var prototype=CreationPrototype.Capture(obj);Assert.That(prototype.version,Is.EqualTo(5));Assert.That(prototype.Validate(out var error),Is.True,error);
            var decoded=CreationPrototype.Read(CreationPrototypeSchema.Encode(prototype));Assert.That(decoded.Validate(out error),Is.True,error);
            var first=decoded.Instantiate("Building",Vector3.zero,Quaternion.identity,1);var second=decoded.Instantiate("Building",Vector3.one,Quaternion.identity,1);
            first.modelGeometry.walkable=false;Assert.That(second.modelGeometry.walkable,Is.True);Assert.That(second.windows.Length,Is.EqualTo(1));
            decoded.version=4;Assert.That(decoded.Validate(out _),Is.False);decoded.version=5;
            decoded.motion=new PrototypeMotion{frames=new[]{new PrototypeFrame()}};Assert.That(decoded.Validate(out _),Is.False);
        }
        [Test] public void FutureModelGeometryIsReadOnlyAndPreservesOriginalBytes()
        {
            string path=Path.Combine(Path.GetTempPath(),"MaestroGeometry-"+Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(path);var doc=Room();doc.objects[2].modelGeometry.version=2;
                string raw=JsonUtility.ToJson(doc);File.WriteAllText(Path.Combine(path,RoomStorage.FileName),raw);var storage=new RoomStorage(path);
                Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(path,RoomStorage.FileName)),Is.EqualTo(raw));
            }finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}

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
    public sealed class StaticSupportPersistenceTests
    {
        static RoomObjectData Support()=>new(){id=new string('a',32),kind=RoomObjectKind.Block,physics=ItemPhysics.Fixed,staticSupport=new(){enabled=true}};
        static RoomDocument Room(RoomObjectData item=null)=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},item??Support()}};
        static CollisionRecipe Collision(params string[] kinds)=>new(){shapes=kinds.Select((shape,i)=>new CollisionShape{id="Shape"+i,shape=shape,segments=shape is "cylinder" or "ring"?8:0,innerRadius=shape=="ring"?.2f:0}).ToArray()};
        [Test] public void SupportSelectsOnlyCompatibleCompletePrimaryCollision()
        {
            var item=Support();Assert.That(RoomStaticSupport.Validate(item,out var error),Is.True,error);
            item.kind=RoomObjectKind.Cylinder;Assert.That(RoomStaticSupport.Validate(item,out _),Is.False,"Automatic primitive cylinder is not a native mesh support provider");
            item.collisionShape=ItemCollider.Box;Assert.That(RoomStaticSupport.Validate(item,out error),Is.True,error);
            item.collisionShape=ItemCollider.Sphere;Assert.That(RoomStaticSupport.Validate(item,out _),Is.False);
            item.collisionShape=ItemCollider.Automatic;item.collision=Collision("box","cylinder","ring");Assert.That(RoomStaticSupport.Validate(item,out error),Is.True,error);
            item.collision.shapes[1].shape="sphere";item.collision.shapes[1].segments=0;Assert.That(RoomStaticSupport.Validate(item,out _),Is.False,"One incompatible selected shape must refuse the entire source");
            item.collision=Collision("box");item.collision.shapes[0].size=Vector3.zero;Assert.That(RoomStaticSupport.Validate(item,out _),Is.False,"Malformed source must not be admitted from its shape label");
            item.collision=Collision("sphere");item.collisionShape=ItemCollider.Box;Assert.That(RoomStaticSupport.Validate(item,out error),Is.True,error+" Explicit Box selects the box provider instead of custom shapes");
            item.collisionShape=ItemCollider.Automatic;item.collision=new();Assert.That(RoomStaticSupport.Validate(item,out _),Is.False);
            foreach(var kind in new[]{RoomObjectKind.Block,RoomObjectKind.Drawing,RoomObjectKind.Assembly}){item.kind=kind;Assert.That(RoomStaticSupport.Validate(item,out error),Is.True,error);}
        }
        [Test] public void ImportedSupportNeedsExplicitCollisionAndNeverOverridesLegacyMeshOwnership()
        {
            var item=Support();item.kind=RoomObjectKind.ImportedModel;item.modelHash=new string('b',64);
            Assert.That(RoomStaticSupport.Validate(item,out _),Is.False,"No imported aggregate bounding proxy");
            item.collision=Collision("box","ring");Assert.That(Room(item).Validate(out var error),Is.True,error);
            item.collisionShape=ItemCollider.Box;Assert.That(RoomStaticSupport.Validate(item,out _),Is.False);item.collisionShape=ItemCollider.Automatic;
            item.modelGeometry.meshCollision=true;item.modelGeometry.walkable=true;Assert.That(RoomStaticSupport.Validate(item,out _),Is.False);
            item.staticSupport.enabled=false;item.collision=null;Assert.That(Room(item).Validate(out error),Is.True,error+" Legacy imported mesh/walkable settings remain independent");
        }
        [Test] public void EnabledSupportNeedsAuthoredFixedOwnershipWithoutRootMotionOrScanAnchors()
        {
            foreach(var kind in new[]{RoomObjectKind.Book,RoomObjectKind.Maestro}){var builtIn=Support();builtIn.kind=kind;Assert.That(RoomStaticSupport.Validate(builtIn,out _),Is.False);}
            foreach(var mode in new[]{ItemPhysics.Solid,ItemPhysics.Bouncy}){var moving=Support();moving.physics=mode;Assert.That(RoomStaticSupport.Validate(moving,out _),Is.False);}
            var item=Support();item.motion=new RoomMotion{frames=new[]{new MotionFrame()}};Assert.That(RoomStaticSupport.Validate(item,out _),Is.False);
            item.motion=null;item.scanAnchors=new[]{new ScanDrawingAnchor()};Assert.That(RoomStaticSupport.Validate(item,out _),Is.False);
            item.scanAnchors=Array.Empty<ScanDrawingAnchor>();item.heightFields=new[]{new RoomHeightField()};Assert.That(Room(item).Validate(out var error),Is.True,error);
            item.staticSupport.enabled=false;Assert.That(Room(item).Validate(out error),Is.True,error+" Terrain retains its own policy when ordinary support is disabled");
        }
        [Test] public void CurrentWireRequiresAnExactTypedComponentAndLegacyWireCannotEnableIt()
        {
            var wire=JObject.Parse(JsonUtility.ToJson(Room()));Assert.That(RoomStaticSupport.ValidWire(wire),Is.True);
            foreach(var value in new JToken[]{JValue.CreateNull(),new JObject(),new JObject{["version"]=1},new JObject{["version"]=1,["enabled"]="true"},new JObject{["version"]=1,["enabled"]=true,["future"]=0},new JObject{["version"]=2,["enabled"]=false}}){var bad=(JObject)wire.DeepClone();bad["objects"][2]["staticSupport"]=value;Assert.That(RoomStaticSupport.ValidWire(bad),Is.False,value.ToString());}
            var missing=(JObject)wire.DeepClone();((JObject)missing["objects"][2]).Remove("staticSupport");Assert.That(RoomStaticSupport.ValidWire(missing),Is.False);
            wire["version"]=46;Assert.That(RoomStaticSupport.ValidWire(wire),Is.False);wire["objects"][2]["staticSupport"]["enabled"]=false;Assert.That(RoomStaticSupport.ValidWire(wire),Is.True);
            foreach(var item in wire["objects"].Cast<JObject>())item.Remove("staticSupport");Assert.That(RoomStaticSupport.ValidWire(wire),Is.True);
        }
        [Test] public void SupportRoundTripsStoragePairedSnapshotUndoRedoAndIndependentCopies()
        {
            string path=Path.Combine(Path.GetTempPath(),"MaestroSupport-"+Guid.NewGuid().ToString("N"));
            try{
                var doc=Room();var storage=new RoomStorage(path);Assert.That(storage.Save(doc,out var error),Is.True,error);var loaded=storage.Load(out error);Assert.That(loaded.objects[2].staticSupport.enabled,Is.True,error);
                Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                var journal=new RoomJournal(loaded);var changed=journal.Read(doc.objects[2].id);changed.staticSupport.enabled=false;Assert.That(journal.Apply(new[]{changed},Array.Empty<string>(),out error),Is.True,error);
                Assert.That(journal.Undo(),Is.True);Assert.That(journal.Read(changed.id).staticSupport.enabled,Is.True);Assert.That(journal.Redo(),Is.True);Assert.That(journal.Read(changed.id).staticSupport.enabled,Is.False);
                var copy=loaded.objects[2].Copy();copy.staticSupport.enabled=false;Assert.That(loaded.objects[2].staticSupport.enabled,Is.True);
            }finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test] public void OldSavedRoomDefaultsOffWithoutRewritingAndFutureSupportCannotRollBackToBackup()
        {
            string path=Path.Combine(Path.GetTempPath(),"MaestroSupport-"+Guid.NewGuid().ToString("N"));
            try{
                Directory.CreateDirectory(path);var wire=JObject.Parse(JsonUtility.ToJson(Room()));wire["version"]=46;foreach(var item in wire["objects"].Cast<JObject>())item.Remove("staticSupport");string old=wire.ToString();string legacy=Path.Combine(path,"room.v46.json");File.WriteAllText(legacy,old);
                var storage=new RoomStorage(path);var loaded=storage.Load(out var error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects.All(o=>o.staticSupport.Default),Is.True);Assert.That(File.ReadAllText(legacy),Is.EqualTo(old));Assert.That(File.Exists(Path.Combine(path,RoomStorage.FileName)),Is.False);
                Assert.That(storage.Save(loaded,out error),Is.True,error);string current=Path.Combine(path,RoomStorage.FileName);File.Copy(current,current+".backup",true);var newer=JObject.Parse(File.ReadAllText(current));newer["objects"][2]["staticSupport"]["version"]=2;string raw=newer.ToString();File.WriteAllText(current,raw);
                storage=new RoomStorage(path);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(current),Is.EqualTo(raw));Assert.That(File.ReadAllText(legacy),Is.EqualTo(old));
            }finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test] public void PortableSupportUsesVersionElevenAndPreservesIndependentComponents()
        {
            var item=Support();item.heightFields=new[]{new RoomHeightField()};var prototype=CreationPrototype.Capture(item);Assert.That(prototype.version,Is.EqualTo(11));Assert.That(prototype.Validate(out var error),Is.True,error);
            var encoded=CreationPrototypeSchema.Encode(prototype);Assert.That((bool)encoded["staticSupport"]["enabled"],Is.True);Assert.That(CapabilityArguments.Validate(encoded,CreationPrototypeSchema.Schema(),out error),Is.True,error);
            foreach(var invalid in new JToken[]{JValue.CreateNull(),new JObject{["version"]=2,["enabled"]=true},new JObject{["version"]=1,["enabled"]=true,["extra"]=1}}){var bad=(JObject)encoded.DeepClone();bad["staticSupport"]=invalid;Assert.That(CapabilityArguments.Validate(bad,CreationPrototypeSchema.Schema(),out _),Is.False);}
            var decoded=CreationPrototype.Read(encoded);Assert.That(decoded.Validate(out error),Is.True,error);
            var first=decoded.Instantiate("First",Vector3.zero,Quaternion.identity,1);var second=decoded.Instantiate("Second",Vector3.one,Quaternion.identity,1);first.staticSupport.enabled=false;Assert.That(second.staticSupport.enabled,Is.True);Assert.That(second.heightFields.Length,Is.EqualTo(1));
            decoded.version=10;Assert.That(decoded.Validate(out _),Is.False);decoded.version=11;decoded.motion=new(){frames=new[]{new PrototypeFrame()}};Assert.That(decoded.Validate(out _),Is.False,"Root motion cannot be erased before validating support eligibility");
            item.staticSupport.enabled=false;item.heightFields=Array.Empty<RoomHeightField>();prototype=CreationPrototype.Capture(item);Assert.That(prototype.version,Is.EqualTo(1));encoded=CreationPrototypeSchema.Encode(prototype);Assert.That(encoded.ContainsKey("staticSupport"),Is.False);decoded=CreationPrototype.Read(encoded);Assert.That(decoded.staticSupport.Default,Is.True);Assert.That(decoded.Validate(out error),Is.True,error);
        }
    }
}

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
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class WaterTraversalTests {
        [Test]public void DefaultsSeparateMaestroFromOrdinaryPropsAndCopiesDoNotAlias(){
            var actor=new RoomObjectData{kind=RoomObjectKind.Maestro};
            Assert.That(RoomWaterTraversal.Effective(actor).mode,Is.EqualTo("avoid"));
            var prop=new RoomObjectData{kind=RoomObjectKind.Block};Assert.That(RoomWaterTraversal.Effective(prop).mode,Is.EqualTo("ignore"));
            actor.waterTraversal.mode="wade";var copy=actor.Copy();copy.waterTraversal.maxDepthMetres=.7f;
            Assert.That(actor.waterTraversal.maxDepthMetres,Is.EqualTo(.25f));
        }
        [TestCase(false)][TestCase(true)]public void ContinuousSweepFindsThinWaterBetweenDryEndpoints(bool rectangular){
            var go=new GameObject("Thin liquid");
            try{
                var item=go.AddComponent<RoomItem>();var c=new RoomContainer{height=.4f,radius=.005f,capacityMl=100,amountMl=50};
                if(rectangular){c.version=2;c.rectangle=new(){width=.01f,depth=.01f};}
                var liquid=new LiquidMediumGeometry("water",item,c,Vector3.up,null);var from=new Vector3(-4,0,0);var to=new Vector3(4,0,0);
                Assert.That(liquid.Sample(from,out _)||liquid.Sample(to,out _),Is.False);
                Assert.That(liquid.Sweep(from,to,.02f,1.7f,out var depth,out _),Is.True);Assert.That(depth,Is.EqualTo(.2f).Within(.001));
                Assert.That(liquid.Sweep(from+Vector3.up,to+Vector3.up,.02f,1.7f,out _,out _),Is.False,"Above the entire liquid");
                Assert.That(liquid.Sweep(from+Vector3.back,to+Vector3.back,.02f,1.7f,out _,out _),Is.False);
                Assert.That(liquid.Sweep(from+Vector3.down*2,to+Vector3.down*2,.02f,1,out _,out _),Is.False,"Entire body below the cavity");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void SweepUsesTheLiveFillPlaneForRotatedScaledCavitiesAndBodyFootprint(){
            var go=new GameObject("Tilted liquid");
            try{
                var item=go.AddComponent<RoomItem>();go.transform.rotation=Quaternion.Euler(0,35,15);go.transform.localScale=Vector3.one*2;
                var c=new RoomContainer{version=2,rectangle=new(){width=.2f,depth=.3f},height=.5f,amountMl=125};
                var liquid=new LiquidMediumGeometry("water",item,c,Vector3.up,null);
                Assert.That(liquid.Sweep(new Vector3(-2,0,0),new Vector3(2,0,0),.1f,1.7f,out float depth,out _),Is.True);
                Assert.That(depth,Is.EqualTo(liquid.Level).Within(.001));
                c.amountMl=0;liquid=new("water",item,c,Vector3.up,null);
                Assert.That(liquid.Sweep(Vector3.left,Vector3.right,.1f,1.7f,out _,out _),Is.False);
                go.transform.rotation=Quaternion.identity;go.transform.localScale=Vector3.one;c.amountMl=125;
                liquid=new("water",item,c,Vector3.up,null);
                Assert.That(liquid.Sweep(new Vector3(.19f,0,-1),new Vector3(.19f,0,1),.1f,1.7f,out _,out _),Is.True,"Body edge reaches water although centre does not");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void TraversalStorageRequiresExactCurrentWireAndProtectsUnknownFuturePolicy(){
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro,waterTraversal=new(){mode="wade",maxDepthMetres=.3f}}}};
            var wire=JObject.Parse(JsonUtility.ToJson(room));Assert.That(RoomWaterTraversal.ValidWire(wire),Is.True);
            wire["objects"][1]["waterTraversal"]["unknown"]=true;Assert.That(RoomWaterTraversal.ValidWire(wire),Is.False);
            ((JObject)wire["objects"][1]).Remove("waterTraversal");Assert.That(RoomWaterTraversal.ValidWire(wire),Is.False);
            wire["version"]=30;Assert.That(RoomWaterTraversal.ValidWire(wire),Is.True);
            string dir=Path.Combine(Path.GetTempPath(),"MaestroWater-"+Guid.NewGuid().ToString("N"));
            try{
                var store=new RoomStorage(dir);Assert.That(store.Save(room,out var e),Is.True,e);var loaded=store.Load(out e);Assert.That(loaded,Is.Not.Null,e);
                Assert.That(loaded.objects[1].waterTraversal.mode,Is.EqualTo("wade"));Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                wire=JObject.Parse(JsonUtility.ToJson(room));wire["objects"][1]["waterTraversal"]["version"]=2;string raw=wire.ToString();
                File.WriteAllText(Path.Combine(dir,RoomStorage.FileName),raw);store=new(dir);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(dir,RoomStorage.FileName)),Is.EqualTo(raw));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test]public void CapturedBlueprintRetainsPolicyAndOldPrototypesGetOrdinaryPropDefault(){
            var prop=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,waterTraversal=new(){mode="wade",maxDepthMetres=.15f}};
            var value=CreationPrototypeSchema.Encode(CreationPrototype.Capture(prop));Assert.That(value["waterTraversal"]["mode"].Value<string>(),Is.EqualTo("wade"));
            var decoded=CreationPrototype.Read(value);Assert.That(decoded.Validate(out var e),Is.True,e);var copy=decoded.Instantiate("NPC",Vector3.zero,Quaternion.identity,1);
            Assert.That(copy.waterTraversal.maxDepthMetres,Is.EqualTo(.15f));((JObject)value).Remove("waterTraversal");decoded=CreationPrototype.Read(value);
            Assert.That(decoded.waterTraversal.Default,Is.True);Assert.That(CreationPrototypeSchema.Encode(decoded)["waterTraversal"],Is.Null);
        }
    }
}

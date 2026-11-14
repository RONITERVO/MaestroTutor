// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class LiquidMediumTests {
        [Test]public void RecordFieldBudgetIsExplicitAndRetainsTheValueSizeCeiling(){
            var value=new JObject();for(int i=0;i<ProgramDataType.MaximumRecordFields;i++)value["f"+i]=i;
            Assert.That(ProgramValue.Literal(value).Type.Kind,Is.EqualTo(ProgramType.Record));value["overflow"]=1;Assert.Throws<ProgramFault>(()=>ProgramValue.Literal(value));value.Remove("overflow");foreach(var key in System.Linq.Enumerable.ToArray(value.Properties()))key.Value=new string('x',128);Assert.Throws<ProgramFault>(()=>ProgramValue.Literal(value));
        }
        [Test]public void PropertiesTravelWithLiquidAndIncompatiblePropertiesCannotMix(){
            var a=new RoomContainer{amountMl=200,fluid=new(){densityKgM3=850,linearDrag=4}};var b=new RoomContainer();
            Assert.That(RoomContainer.Transfer(a,b,100,out var moved,out var e),Is.True,e);Assert.That(moved,Is.EqualTo(100));Assert.That(b.fluid.densityKgM3,Is.EqualTo(850));Assert.That(b.fluid,Is.Not.SameAs(a.fluid));
            a.fluid.linearDrag=6;Assert.That(RoomContainer.Transfer(a,b,10,out _,out e),Is.False);Assert.That(a.amountMl+b.amountMl,Is.EqualTo(200));
            foreach(float n in new[]{float.NaN,float.PositiveInfinity,0,20001}){a.fluid.densityKgM3=n;Assert.That(a.Validate(out _),Is.False);}
        }
        [Test]public void MediumWireAndStoragePreserveExplicitPropertiesAndProtectFutureVersions(){
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),name="Liquid",kind=RoomObjectKind.Block,containers=new[]{new RoomContainer{amountMl=100,fluid=new(){densityKgM3=900,linearDrag=3}}}}}};
            var wire=JObject.Parse(JsonUtility.ToJson(room));Assert.That(RoomFluid.ValidWire(wire),Is.True);((JObject)wire["objects"][2]["containers"][0]["fluid"]).Remove("linearDrag");Assert.That(RoomFluid.ValidWire(wire),Is.False);
            ((JObject)wire["objects"][2]["containers"][0]).Remove("fluid");Assert.That(RoomFluid.ValidWire(wire),Is.False);wire["version"]=29;Assert.That(RoomFluid.ValidWire(wire),Is.True);
            string dir=Path.Combine(Path.GetTempPath(),"MaestroMedium-"+Guid.NewGuid().ToString("N"));
            try{var store=new RoomStorage(dir);Assert.That(store.Save(room,out var e),Is.True,e);var loaded=store.Load(out e);Assert.That(loaded,Is.Not.Null,e);Assert.That(loaded.objects[2].containers[0].fluid.densityKgM3,Is.EqualTo(900));Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                wire=JObject.Parse(JsonUtility.ToJson(room));wire["objects"][2]["containers"][0]["fluid"]["version"]=2;var raw=wire.ToString();File.WriteAllText(Path.Combine(dir,RoomStorage.FileName),raw);store=new RoomStorage(dir);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(dir,RoomStorage.FileName)),Is.EqualTo(raw));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
    }
}

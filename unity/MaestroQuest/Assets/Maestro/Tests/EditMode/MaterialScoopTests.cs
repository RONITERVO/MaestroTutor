// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class MaterialScoopTests {
        [Test] public void MaterialScoopTemplateIsEditableVersionedAndCopiesKeepIndependentBalances(){
            var template=CreationTemplates.All.Single(t=>t.Id=="material-scoop");var data=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Assembly,recipe=template.Recipe,sculptTips=template.SculptTips,materialStores=template.MaterialStores};
            Assert.That(SculptTip.ValidateCollection(data,out var error),Is.True,error);Assert.That(data.sculptTips[0].IsMaterial,Is.True);Assert.That(data.sculptTips[0].height,Is.Zero);
            var encoded=CreationPrototypeSchema.Encode(CreationPrototype.Capture(data));Assert.That(CapabilityArguments.Validate(encoded,CreationPrototypeSchema.Schema(),out error),Is.True,error);
            var prototype=CreationPrototype.Read(encoded);Assert.That(prototype.Validate(out error),Is.True,error);var instance=prototype.Instantiate("Scoop",Vector3.one,Quaternion.identity,1);instance.materialStores[0].amountLitres=.1;instance.sculptTips[0].amountLitres=.05;Assert.That(prototype.materialStores[0].amountLitres,Is.Zero);Assert.That(data.sculptTips[0].amountLitres,Is.EqualTo(.25));
            var room=new RoomDocument{objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},data}};room.version=17;Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;Assert.That(room.Validate(out error),Is.True,error);
            string directory=Path.Combine(Path.GetTempPath(),"MaestroScoop-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{Assert.That(new RoomStorage(directory).Save(room,out error),Is.True,error);var loaded=new RoomStorage(directory).Load(out error);Assert.That(loaded.objects[2].sculptTips[0].IsMaterial,Is.True,error);Assert.That(loaded.objects[2].materialStores[0].capacityLitres,Is.EqualTo(.25));data.sculptTips[0].version=3;string wire=JsonUtility.ToJson(room);File.WriteAllText(Path.Combine(directory,RoomStorage.FileName),wire);var storage=new RoomStorage(directory);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(wire));}finally{Directory.Delete(directory,true);}
        }
        [TestCase(16),TestCase(17),TestCase(18)] public void MissingSculptTipIsRejectedWithoutThrowingForEverySupportedTipFormat(int version){
            var room=new RoomDocument{version=version,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,sculptTips=new SculptTip[]{null}}}};
            Assert.That(room.Validate(out var error),Is.False);Assert.That(error,Is.Not.Empty);
        }
        [Test] public void MaterialScoopSchemasAndRuntimeRejectMixedModesAndUnboundedAmounts(){
            var owner=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block};var tip=new SculptTip{version=2,mode="scoop",height=0,amountLitres=.25};Assert.That(tip.Validate(owner,out var error),Is.True,error);
            var definition=SculptTipCapability.Definition(tip);Assert.That(definition["height"],Is.Null);Assert.That(CapabilityArguments.Validate(definition,SculptTipCapability.DefinitionSchema(),out error),Is.True,error);
            foreach(double amount in new[]{0,.0001,20.01,double.NaN,double.PositiveInfinity}){tip.amountLitres=amount;Assert.That(tip.Validate(owner,out _),Is.False);}
            tip.amountLitres=.25;tip.height=.03f;Assert.That(tip.Validate(owner,out _),Is.False);tip.height=0;tip.version=1;Assert.That(tip.Validate(owner,out _),Is.False);
            definition["height"]=.03;Assert.That(CapabilityArguments.Validate(definition,SculptTipCapability.DefinitionSchema(),out _),Is.False);definition.Remove("height");definition["amountLitres"]=21;Assert.That(CapabilityArguments.Validate(definition,SculptTipCapability.DefinitionSchema(),out _),Is.False);
            var legacy=new SculptTip();Assert.That(CapabilityArguments.Validate(JObject.Parse(JsonUtility.ToJson(legacy)),SculptTipCapability.SavedSchema(),out error),Is.True,error);legacy.amountLitres=.1;Assert.That(legacy.Validate(owner,out _),Is.False);
        }
    }
}

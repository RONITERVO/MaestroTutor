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
    public sealed class RoomContainerTests {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,containers=new[]{new RoomContainer{amountMl=200}}}}};
        [Test] public void ContainersDeepCopyAndPreserveAmountsAcrossPrototypeCaptureAndResizing(){
            var cup=CreationTemplates.All.Single(t=>t.Id=="cup");Assert.That(cup.Containers.Single().capacityMl,Is.EqualTo(500));Assert.That(cup.Containers.Single().amountMl,Is.Zero);
            var room=Room();Assert.That(room.Validate(out var error),Is.True,error);var copy=room.Copy();copy.objects[2].containers[0].frame.position=Vector3.up;copy.objects[2].containers[0].amountMl=50;Assert.That(room.objects[2].containers[0].amountMl,Is.EqualTo(200));Assert.That(room.objects[2].containers[0].frame.position,Is.EqualTo(Vector3.zero));
            var prototype=CreationPrototype.Capture(room.objects[2]);var wire=CreationPrototypeSchema.Encode(prototype);Assert.That(CapabilityArguments.Validate(wire,CreationPrototypeSchema.Schema(),out error),Is.True,error);var restored=CreationPrototype.Read(wire);Assert.That(restored.Validate(out error),Is.True,error);var item=restored.Instantiate("Small cup",Vector3.zero,Quaternion.identity,.1f);Assert.That(item.containers.Single().amountMl,Is.EqualTo(200));Assert.That(item.containers.Single().capacityMl,Is.EqualTo(250));
        }
        [Test] public void RoomNinePreservesContentsAndRefusesUnknownComponentsWithoutFallback(){
            var room=Room();room.version=8;Assert.That(room.Validate(out _),Is.False);room.objects[2].containers=Array.Empty<RoomContainer>();Assert.That(room.Validate(out var error),Is.True,error);
            string directory=Path.Combine(Path.GetTempPath(),"container-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{room=Room();var storage=new RoomStorage(directory);Assert.That(storage.Save(room,out error),Is.True,error);Assert.That(storage.Load(out _).objects[2].containers.Single().amountMl,Is.EqualTo(200));room.objects[2].containers[0].version=2;string path=Path.Combine(directory,RoomStorage.FileName),wire=JsonUtility.ToJson(room);File.WriteAllText(path,wire);storage=new RoomStorage(directory);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(wire));}finally{Directory.Delete(directory,true);}
        }
        [Test] public void RoomAndComponentBudgetsRejectInvalidOrBuiltinContainers(){
            foreach(var bad in new[]{new RoomContainer{amountMl=251},new RoomContainer{amountMl=double.NaN},new RoomContainer{capacityMl=double.PositiveInfinity},new RoomContainer{liquid="Water\n"},new RoomContainer{radius=0},new RoomContainer{height=3},new RoomContainer{color=new Color(1,1,1,0)},new RoomContainer{frame=new ConnectionFrame{rotation=new Quaternion()}}}){var room=Room();room.objects[2].containers=new[]{bad};Assert.That(room.Validate(out _),Is.False);}
            var many=Room();many.objects[0].containers=new[]{new RoomContainer()};Assert.That(many.Validate(out _),Is.False);many=Room();many.objects[2].containers=new[]{new RoomContainer(),new RoomContainer()};Assert.That(many.Validate(out _),Is.False);
            many=Room();many.objects=many.objects.Take(2).Concat(Enumerable.Range(0,16).Select(i=>new RoomObjectData{id=i.ToString("x32"),kind=RoomObjectKind.Block,containers=new[]{new RoomContainer()}})).ToArray();Assert.That(many.Validate(out var error),Is.True,error);many.objects=many.objects.Append(new RoomObjectData{id=new string('f',32),kind=RoomObjectKind.Block,containers=new[]{new RoomContainer()}}).ToArray();Assert.That(many.Validate(out _),Is.False);
        }
        [Test] public void TransfersConserveClampAndAdoptOnlyIntoEmptyMatchingContainers(){
            var from=new RoomContainer{amountMl=200};var to=new RoomContainer{capacityMl=100,amountMl=20};Assert.That(RoomContainer.Transfer(from,to,150,out var moved,out var error),Is.True,error);Assert.That(moved,Is.EqualTo(80));Assert.That(from.amountMl+to.amountMl,Is.EqualTo(220));Assert.That(RoomContainer.Transfer(from,to,1,out _,out _),Is.False);to.amountMl=0;to.liquid="Juice";to.color=Color.magenta;Assert.That(RoomContainer.Transfer(from,to,50,out moved,out error),Is.True,error);Assert.That(to.liquid,Is.EqualTo(from.liquid));Assert.That(to.color,Is.EqualTo(from.color));
            to.liquid="Juice";var savedFrom=from.amountMl;Assert.That(RoomContainer.Transfer(from,to,10,out _,out _),Is.False);Assert.That(from.amountMl,Is.EqualTo(savedFrom));to.liquid=from.liquid;to.color=new Color(from.color.r+1e-7f,from.color.g,from.color.b,1);Assert.That(RoomContainer.Transfer(from,to,10,out _,out _),Is.False);
        }
        [Test] public void RepeatedFractionalTransfersRemainBoundedAndDoNotCreateLiquid(){
            var from=new RoomContainer{amountMl=249.123456789};var to=new RoomContainer();double total=from.amountMl;for(int i=0;i<10000;i++){Assert.That(RoomContainer.Transfer(from,to,.01337,out _,out var error),Is.True,error);Assert.That(RoomContainer.Transfer(to,from,.01337,out _,out error),Is.True,error);}Assert.That(from.amountMl+to.amountMl,Is.EqualTo(total).Within(1e-9));Assert.That(to.amountMl,Is.InRange(0,250));Assert.That(RoomContainer.Transfer(from,to,double.NaN,out _,out _),Is.False);Assert.That(RoomContainer.Transfer(from,from,10,out _,out _),Is.False);
        }
        [Test] public void TiltedDisplayLevelIsFiniteSymmetricAndDoesNotChangeSavedAmounts(){
            foreach(var normal in new[]{Vector3.up,Vector3.down,Vector3.right,new Vector3(.5f,.7f,.2f).normalized}){float half=ContainerFillView.Level(normal,.04f,.1f,.5f);Assert.That(half,Is.EqualTo(0).Within(.0001));float low=ContainerFillView.Level(normal,.04f,.1f,.2f),high=ContainerFillView.Level(normal,.04f,.1f,.8f);Assert.That(low,Is.LessThan(high));Assert.That(low,Is.EqualTo(-high).Within(.0001));}
        }
        [Test] public void SharedContainerContractsValidateAndCompileWithExactObjectClaims(){
            foreach(var item in JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/containers-contract.json")))){var args=(JObject)item["arguments"];bool expected=(bool)item["valid"];string id=(string)item["capability"];Assert.That(BehaviourCatalog.TryCall(id,1,args,out var call,out var error),Is.EqualTo(expected),(string)item["name"]+": "+error);if(!expected)continue;Assert.That(BehaviourProgram.TryParse(BehaviourProgram.FromInvocation(new JObject{["id"]=id,["version"]=1,["arguments"]=args}),out _,out error),Is.True,error);Assert.That(call.Claims.Select(c=>c.Target),Is.EquivalentTo(call.Resources));}
        }
    }
}

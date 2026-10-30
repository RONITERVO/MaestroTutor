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
namespace Maestro.Quest.Tests
{
    public sealed class RoomConnectionTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,connections=new[]{new RoomConnection{connected=new string('b',32)}}},new RoomObjectData{id=new string('b',32),kind=RoomObjectKind.Block}}};
        [Test] public void ConnectionDefinitionsAreDeepCopiedBoundedAndNeverRebindMissingIdentities()
        {
            var room=Room();Assert.That(room.Validate(out var error),Is.True,error);var copy=room.Copy();copy.objects[2].connections[0].drive.speed=45;Assert.That(room.objects[2].connections[0].drive.speed,Is.EqualTo(90));
            copy.objects=copy.objects.Take(3).ToArray();Assert.That(copy.Validate(out error),Is.True,error);
            copy.objects[2].connections[0].connected=copy.objects[2].id;Assert.That(copy.Validate(out _),Is.False);
            copy=Room();copy.objects[3].connections=new[]{new RoomConnection{connected=copy.objects[2].id}};Assert.That(copy.Validate(out _),Is.False);
            foreach(var invalid in new[]{new RoomConnection{connected=new string('b',32),version=2},new RoomConnection{connected="book"},new RoomConnection{connected=new string('B',32)},new RoomConnection{connected=new string('b',32),drive=new HingeDrive{speed=float.NaN}},new RoomConnection{connected=new string('b',32),ownerFrame=new ConnectionFrame{rotation=default}},new RoomConnection{connected=new string('b',32),limits=new HingeLimits{minimum=90,maximum=-90}}}){copy=Room();copy.objects[2].connections=new[]{invalid};Assert.That(copy.Validate(out _),Is.False);}
        }
        [Test] public void HingeBoundaryUsesSharedResourcesAndRejectsInvalidDriveCombinations()
        {
            var module=new ConnectionCapability();var args=module.Example;Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out var call,out var error),Is.True,error);Assert.That(call.Resources.Length,Is.EqualTo(2));Assert.That(call.Claims.Select(x=>x.Target),Is.EquivalentTo(call.Resources));
            Assert.That(BehaviourProgram.TryParse(BehaviourProgram.FromInvocation(new JObject{["id"]=module.Id,["version"]=1,["arguments"]=args}),out _,out error),Is.True,error);
            args["definition"]["limits"]["enabled"]=true;args["definition"]["drive"]["mode"]="spring";args["definition"]["drive"]["target"]=100;Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out _,out _),Is.False);
        }
        [Test] public void RoomSevenRoundTripsAndFutureHingeDataPreservesOriginals()
        {
            var room=Room();room.version=5;Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;
            string dir=Path.Combine(Path.GetTempPath(),"hinge-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try{var store=new RoomStorage(dir);Assert.That(store.Save(room,out var error),Is.True,error);Assert.That(store.Load(out _).objects[2].connections[0].connected,Is.EqualTo(new string('b',32)));
                room.objects[2].connections[0].version=2;string wire=JsonUtility.ToJson(room),path=Path.Combine(dir,RoomStorage.FileName);File.WriteAllText(path,wire);store=new RoomStorage(dir);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(wire));
            }finally{Directory.Delete(dir,true);}
        }
        [Test] public void FixedSettingsUseTheSameGraphAndRejectInvalidBreakLimits(){
            var room=Room();room.objects[2].connections[0].kind="fixed";room.objects[2].connections[0].breakForce=25;Assert.That(room.Validate(out var error),Is.True,error);
            var copy=room.Copy();copy.objects[2].connections[0].breakForce=50;Assert.That(room.objects[2].connections[0].breakForce,Is.EqualTo(25));
            foreach(float force in new[]{-1f,10001f,float.NaN,float.PositiveInfinity}){copy=room.Copy();copy.objects[2].connections[0].breakForce=force;Assert.That(copy.Validate(out _),Is.False);}
            var args=new ConnectionCapability().Example;args["definition"]=ConnectionCapability.Definition(room.objects[2].connections[0]);Assert.That(((JObject)args["definition"]).ContainsKey("drive"),Is.False);Assert.That(BehaviourCatalog.TryCall("object.connection.edit",1,args,out _,out error),Is.True,error);
            foreach(var op in new[]{"attach","align","rearm"}){
                var call=new JObject {["operation"]=op,["target"]=new string('a',32),["connected"]=new string('b',32),["revision"]=1};if(op=="attach"){call["breakForce"]=0;call["breakTorque"]=0;}if(op=="align")call["angle"]=0;
                Assert.That(CapabilityArguments.Validate(call,new ConnectionCapability().InputSchema,out error),Is.True,error);call["connected"]=call["target"].DeepClone();Assert.That(CapabilityArguments.Validate(call,new ConnectionCapability().InputSchema,out _),Is.False,"Shared schema must reject self-links for "+op);
            }
        }
        [Test] public void SliderSettingsAreDeepCopiedAndRejectNarrowTravelAndInvalidDrives(){
            var room=Room();var link=room.objects[2].connections[0];link.kind="slider";link.slide=new SliderSettings{minimum=-.03f,maximum=.12f,mode="spring",target=.04f};Assert.That(room.Validate(out var error),Is.True,error);
            var copy=room.Copy();copy.objects[2].connections[0].slide.target=0;Assert.That(link.slide.target,Is.EqualTo(.04f));
            var module=new ConnectionCapability();var args=module.Example;args["definition"]=ConnectionCapability.Definition(link);Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out _,out error),Is.True,error);
            foreach(var patch in new[]{new JObject{["maximum"]=-.028},new JObject{["target"]=.13},new JObject{["speed"]=.51},new JObject{["force"]=101},new JObject{["spring"]=501},new JObject{["damper"]=-1}}){var bad=(JObject)args.DeepClone();((JObject)bad["definition"]["slide"]).Merge(patch);Assert.That(CapabilityArguments.Validate(bad,module.InputSchema,out _),Is.False);}
            var definition=(JObject)args["definition"];Assert.That(definition.ContainsKey("drive"),Is.False);Assert.That(definition.ContainsKey("limits"),Is.False);
            var data=room.objects[2];data.position=Vector3.right*.2f;var other=room.objects[3];other.scale=2;Assert.That(link.Aligned(data,other,out error),Is.True,error);data.position=Vector3.right*.26f;Assert.That(link.Aligned(data,other,out _),Is.False);data.position=new Vector3(.2f,.04f,0);Assert.That(link.Aligned(data,other,out _),Is.False);
        }
        [Test] public void PreviousHingeOnlyRoomIsPreservedWithoutSilentlyDroppingConnections(){
            string dir=Path.Combine(Path.GetTempPath(),"previous-connection-room-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try{var room=Room();var raw=JObject.Parse(JsonUtility.ToJson(room));raw["version"]=6;foreach(JObject value in (JArray)raw["objects"]){value["hinges"]=value["connections"];value.Remove("connections");}string text=raw.ToString(),path=Path.Combine(dir,"room.v6.json");File.WriteAllText(path,text);var store=new RoomStorage(dir);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(text));Assert.That(File.Exists(Path.Combine(dir,RoomStorage.FileName)),Is.False);}finally{Directory.Delete(dir,true);}
        }
    }
}

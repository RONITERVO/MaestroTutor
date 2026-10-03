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
    public sealed class RoomHingeTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,hinges=new[]{new RoomHinge{connected=new string('b',32)}}},new RoomObjectData{id=new string('b',32),kind=RoomObjectKind.Block}}};
        [Test] public void HingeDefinitionsAreDeepCopiedBoundedAndNeverRebindMissingIdentities()
        {
            var room=Room();Assert.That(room.Validate(out var error),Is.True,error);var copy=room.Copy();copy.objects[2].hinges[0].drive.speed=45;Assert.That(room.objects[2].hinges[0].drive.speed,Is.EqualTo(90));
            copy.objects=copy.objects.Take(3).ToArray();Assert.That(copy.Validate(out error),Is.True,error);
            copy.objects[2].hinges[0].connected=copy.objects[2].id;Assert.That(copy.Validate(out _),Is.False);
            copy=Room();copy.objects[3].hinges=new[]{new RoomHinge{connected=copy.objects[2].id}};Assert.That(copy.Validate(out _),Is.False);
            foreach(var invalid in new[]{new RoomHinge{connected=new string('b',32),version=2},new RoomHinge{connected="book"},new RoomHinge{connected=new string('B',32)},new RoomHinge{connected=new string('b',32),drive=new HingeDrive{speed=float.NaN}},new RoomHinge{connected=new string('b',32),ownerFrame=new HingeFrame{rotation=default}},new RoomHinge{connected=new string('b',32),limits=new HingeLimits{minimum=90,maximum=-90}}}){copy=Room();copy.objects[2].hinges=new[]{invalid};Assert.That(copy.Validate(out _),Is.False);}
        }
        [Test] public void HingeBoundaryUsesSharedResourcesAndRejectsInvalidDriveCombinations()
        {
            var module=new HingeCapability();var args=module.Example;Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out var call,out var error),Is.True,error);Assert.That(call.Resources.Length,Is.EqualTo(2));Assert.That(call.Claims.Select(x=>x.Target),Is.EquivalentTo(call.Resources));
            Assert.That(BehaviourProgram.TryParse(BehaviourProgram.FromInvocation(new JObject{["id"]=module.Id,["version"]=1,["arguments"]=args}),out _,out error),Is.True,error);
            args["definition"]["limits"]["enabled"]=true;args["definition"]["drive"]["mode"]="spring";args["definition"]["drive"]["target"]=100;Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out _,out _),Is.False);
        }
        [Test] public void RoomSixRoundTripsAndFutureHingeDataPreservesOriginals()
        {
            var room=Room();room.version=5;Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;
            string dir=Path.Combine(Path.GetTempPath(),"hinge-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try{var store=new RoomStorage(dir);Assert.That(store.Save(room,out var error),Is.True,error);Assert.That(store.Load(out _).objects[2].hinges[0].connected,Is.EqualTo(new string('b',32)));
                room.objects[2].hinges[0].version=2;string wire=JsonUtility.ToJson(room),path=Path.Combine(dir,RoomStorage.FileName);File.WriteAllText(path,wire);store=new RoomStorage(dir);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(wire));
            }finally{Directory.Delete(dir,true);}
        }
    }
}

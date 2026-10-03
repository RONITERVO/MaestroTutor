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
    public sealed class RoomSnapPointTests {
        static RoomSnapPoint Point(string id="Top")=>new(){id=id,name="Top",family="Brick",frame=new ConnectionFrame{position=new Vector3(0,.05f,0)}};
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,snapPoints=new[]{Point()}}}};
        [Test] public void SnapContractsMatchSharedFixturesAndProgramCompilation(){
            foreach(var item in JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/snap-points-contract.json")))){
                var id=(string)item["capability"];var args=(JObject)item["arguments"];bool expected=(bool)item["valid"];
                Assert.That(BehaviourCatalog.TryCall(id,1,args,out var call,out var error),Is.EqualTo(expected),(string)item["name"]+": "+error);
                if(!expected)continue;
                Assert.That(BehaviourProgram.TryParse(BehaviourProgram.FromInvocation(new JObject{["id"]=id,["version"]=1,["arguments"]=args}),out _,out error),Is.True,error);
                Assert.That(call.Claims.Select(c=>c.Target),Is.EquivalentTo(call.Resources));
            }
        }
        [Test] public void PointsAreDeepCopiedBoundedUniqueAndForbiddenOnBuiltins(){
            var room=Room();Assert.That(room.Validate(out var error),Is.True,error);var copy=room.Copy();copy.objects[2].snapPoints[0].frame.position=Vector3.one;Assert.That(room.objects[2].snapPoints[0].frame.position.y,Is.EqualTo(.05f));
            foreach(var bad in new[]{new RoomSnapPoint{id="Top",name=" ",family="Brick"},new RoomSnapPoint{id="Top",name="Top",family="a b"},new RoomSnapPoint{id="Top",name="Top",family="Brick",version=2},new RoomSnapPoint{id="Top",name="Top",family="Brick",frame=new ConnectionFrame{position=new Vector3(8,8,0)}}}){copy=Room();copy.objects[2].snapPoints=new[]{bad};Assert.That(copy.Validate(out _),Is.False);}
            copy=Room();copy.objects[0].snapPoints=new[]{Point()};Assert.That(copy.Validate(out _),Is.False);
            copy=Room();copy.objects[2].snapPoints=new[]{Point(),Point()};Assert.That(copy.Validate(out _),Is.False);
            copy=Room();copy.objects[2].snapPoints=Enumerable.Range(0,64).Select(i=>Point("Point"+i)).ToArray();Assert.That(copy.Validate(out error),Is.True,error);
            copy.objects[2].snapPoints=copy.objects[2].snapPoints.Append(Point("Overflow")).ToArray();Assert.That(copy.Validate(out _),Is.False);
            copy=Room();copy.objects=copy.objects.Take(2).Concat(Enumerable.Range(0,5).Select(i=>new RoomObjectData{id=i.ToString("x32"),kind=RoomObjectKind.Block,snapPoints=Enumerable.Range(0,64).Select(n=>Point("Point"+n)).ToArray()})).ToArray();Assert.That(copy.Validate(out _),Is.False);
        }
        [Test] public void RoomEightPreservesPointsAndRefusesFutureComponentsWithoutBackupFallback(){
            var room=Room();room.version=7;Assert.That(room.Validate(out _),Is.False);room.objects[2].snapPoints=Array.Empty<RoomSnapPoint>();Assert.That(room.Validate(out var error),Is.True,error);
            string dir=Path.Combine(Path.GetTempPath(),"snap-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try{room=Room();var storage=new RoomStorage(dir);Assert.That(storage.Save(room,out error),Is.True,error);Assert.That(storage.Load(out _).objects[2].snapPoints.Single().id,Is.EqualTo("Top"));
                room.objects[2].snapPoints[0].version=2;string path=Path.Combine(dir,RoomStorage.FileName),wire=JsonUtility.ToJson(room);File.WriteAllText(path,wire);storage=new RoomStorage(dir);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(wire));
            }finally{Directory.Delete(dir,true);}
        }
        [Test] public void SnapFramesProjectInRoomSpaceWithDistinctScalesAndQuarterTurn(){
            var request=new RoomSnapPlacement{members=new[]{new TransformMember{target=new string('a',32),revision=1}},point="Bottom",destination=new SnapDestination{target=new string('b',32),revision=1,point="Top"},turn=90};
            var source=new RoomLayout{placements=new[]{new ObjectPlacement{target=request.members[0].target,position=new Vector3(1,1,1),scale=2}}};
            var from=Point("Bottom");from.frame.position=Vector3.down*.05f;
            var other=new RoomObjectData{position=new Vector3(4,2,3),rotation=Quaternion.Euler(0,0,90),scale=1.5f};var to=Point();
            var projection=request.Projection(source,from,other,to);var expected=other.position+other.rotation*(Vector3.up*.075f);
            Assert.That(Vector3.Distance(projection.position+projection.rotation*(from.frame.position*2),expected),Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(projection.rotation,other.rotation*Quaternion.Euler(0,90,0)),Is.LessThan(.01f));Assert.That(projection.scale,Is.EqualTo(1));
        }
        [Test] public void ProximityUsesMatchingFamilyBoundedDistanceAndFrameTiltWithFreeOrSteppedTwist(){
            var moving=new ObjectPlacement{position=new Vector3(0,.1f,0),rotation=Quaternion.Euler(0,73,0)};
            var from=Point("Bottom");from.frame.position=Vector3.down*.05f;var to=Point();var destination=new RoomObjectData{rotation=Quaternion.identity};
            Assert.That(ConstructionSnapMath.Match(moving,from,destination,to,.08f,90,out var distance,out var turn),Is.True);Assert.That(distance,Is.EqualTo(0).Within(.00001));Assert.That(turn,Is.EqualTo(90).Within(.001));
            Assert.That(ConstructionSnapMath.Match(moving,from,destination,to,.08f,0,out _,out turn),Is.True);Assert.That(turn,Is.EqualTo(73).Within(.001));
            moving.position.x=.081f;Assert.That(ConstructionSnapMath.Match(moving,from,destination,to,.08f,90,out _,out _),Is.False);
            moving.position.x=0;moving.rotation=Quaternion.Euler(31,0,0);Assert.That(ConstructionSnapMath.Match(moving,from,destination,to,.08f,90,out _,out _),Is.False);
            moving.rotation=Quaternion.identity;to.family="Other";Assert.That(ConstructionSnapMath.Match(moving,from,destination,to,.08f,90,out _,out _),Is.False);
        }
        [Test] public void ScaledSnapKeepsPointAndAllRelativePiecePlacementsAligned(){
            var first=new string('a',32);var second=new string('c',32);var request=new RoomSnapPlacement{members=new[]{new TransformMember{target=first,revision=1},new TransformMember{target=second,revision=1}},point="Bottom",destination=new SnapDestination{target=new string('b',32),revision=1,point="Top"},scale=2};
            var source=new RoomLayout{placements=new[]{new ObjectPlacement{target=first,position=Vector3.one},new ObjectPlacement{target=second,position=Vector3.one+Vector3.right*.2f}}};var result=new RoomLayout{placements=new[]{new ObjectPlacement{target=first},new ObjectPlacement{target=second}}};
            var from=Point("Bottom");from.frame.position=Vector3.down*.05f;var to=Point();var other=new RoomObjectData{position=new Vector3(2,1,0),rotation=Quaternion.Euler(0,90,0)};
            Assert.That(request.Projection(source,from,other,to).Project(source,result,out var error),Is.True,error);
            Assert.That(Vector3.Distance(result.placements[0].position+result.placements[0].rotation*(from.frame.position*2),other.position+Vector3.up*.05f),Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(result.placements[1].position-result.placements[0].position,new Vector3(0,0,-.4f)),Is.LessThan(.00001f));Assert.That(result.placements[1].scale,Is.EqualTo(2));
        }
        [Test] public void PrototypeAndBundledBrickRetainEditablePoints(){
            var data=Room().objects[2];var prototype=CreationPrototype.Capture(data);var wire=CreationPrototypeSchema.Encode(prototype);Assert.That(CapabilityArguments.Validate(wire,CreationPrototypeSchema.Schema(),out var error),Is.True,error);
            var decoded=CreationPrototype.Read(wire);Assert.That(decoded.Validate(out error),Is.True,error);var clone=decoded.Instantiate("Copy",Vector3.one,Quaternion.identity,1);clone.snapPoints[0].frame.position=Vector3.zero;Assert.That(data.snapPoints[0].frame.position.y,Is.EqualTo(.05f));
            var brick=CreationTemplates.All.Single(t=>t.Id=="brick");Assert.That(brick.SnapPoints.Select(p=>p.id),Is.EqualTo(new[]{"Top","Bottom"}));Assert.That(brick.SnapPoints.All(p=>p.family=="Brick"),Is.True);
        }
    }
}

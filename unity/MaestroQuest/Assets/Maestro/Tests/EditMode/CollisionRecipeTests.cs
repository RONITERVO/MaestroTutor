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
    public sealed class CollisionRecipeTests
    {
        public static CollisionRecipe Cup()=>JsonUtility.FromJson<CollisionRecipe>(JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/collision-contract.json")))[0]["collision"].ToString());
        [Test] public void WebAndNativeShareBoundedCollisionRecipeContract(){
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/collision-contract.json")));
            foreach(var c in cases)Assert.That(CapabilityArguments.Validate(c["collision"],CollisionCapability.RecipeSchema(),out var error),Is.EqualTo((bool)c["valid"]),(string)c["name"]+": "+error);
        }
        [Test] public void CopiesAndRoomAdmissionIncludeInactiveCollisionRecipes(){
            var cup=Cup();var copy=cup.Copy();copy.shapes[0].position=Vector3.one;Assert.That(cup.shapes[0].position,Is.Not.EqualTo(Vector3.one));
            var heavy=new CollisionRecipe {shapes=Enumerable.Range(0,8).Select(i=>new CollisionShape {id="Wall"+i,shape="ring",size=Vector3.one*.2f,segments=8,innerRadius=.4f}).ToArray()};Assert.That(heavy.Validate(out _),Is.True);Assert.That(heavy.Pieces,Is.EqualTo(64));
            var builtins=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro}};
            var room=new RoomDocument {version=2,objects=builtins.Concat(Enumerable.Range(0,8).Select(i=>new RoomObjectData {id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block,collision=heavy.Copy(),collisionShape=Maestro.Quest.Interaction.ItemCollider.Box})).ToArray()};
            Assert.That(room.Validate(out var error),Is.True,error);room.objects=room.objects.Append(new RoomObjectData {id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block}).ToArray();Assert.That(room.Validate(out error),Is.False);StringAssert.Contains("collision-piece",error);
        }
        [Test] public void RoomRoundTripPreservesCustomShapesAndAbsentBuiltinsButRejectsFutureVersions(){
            string directory=Path.Combine(Path.GetTempPath(),"MaestroCollision-"+Guid.NewGuid().ToString("N"));
            try{
                var room=new RoomDocument {version=2,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData {id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block,collision=Cup()}}};
                var storage=new RoomStorage(directory);Assert.That(storage.Save(room,out var error),Is.True,error);
                var loaded=new RoomStorage(directory).Load(out error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects[0].collision,Is.Null);Assert.That(loaded.objects[1].collision,Is.Null);Assert.That(loaded.objects[2].collision.Pieces,Is.EqualTo(13));
                foreach(int version in new[]{0,1,2}){
                    var wire=JObject.Parse(JsonUtility.ToJson(room));wire["objects"][2]["collision"]=new JObject {["version"]=version,["shapes"]=new JArray()};
                    var decoded=JsonUtility.FromJson<RoomDocument>(wire.ToString());RoomStorage.Normalize(decoded);Assert.That(decoded.Validate(out _),Is.EqualTo(version!=2));
                    if(version==2)Assert.That(decoded.objects[2].collision.version,Is.EqualTo(2));else Assert.That(decoded.objects[2].collision,Is.Null);
                }
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void ConvexPrismsHaveClosedOutwardNondegenerateFaces(){
            var mesh=CollisionGeometry.Prism(new[]{new Vector2(.4f,0),new Vector2(.5f,0),new Vector2(.4330127f,.25f),new Vector2(.3464102f,.2f)});
            try{var v=mesh.vertices;var t=mesh.triangles;double volume=0;for(int i=0;i<t.Length;i+=3){var cross=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);Assert.That(cross.sqrMagnitude,Is.GreaterThan(1e-12));volume+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6;}Assert.That(volume,Is.GreaterThan(0));Assert.That(v.Length,Is.EqualTo(8));}finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}

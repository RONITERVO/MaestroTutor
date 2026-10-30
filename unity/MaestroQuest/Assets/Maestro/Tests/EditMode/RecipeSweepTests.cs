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
    public sealed class RecipeSweepTests {
        internal static JArray Cases()=>JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/sweep-contract.json")));
        internal static RoomRecipe Handle()=>JsonUtility.FromJson<RoomRecipe>(Cases()[0]["recipe"].ToString());
        [Test] public void SweepSharedCasesMatchNativeAdmissionAndMesh(){
            foreach(var row in Cases()){
                var recipe=JsonUtility.FromJson<RoomRecipe>(row["recipe"].ToString());Assert.That(recipe.Validate(out _),Is.EqualTo((bool)row["valid"]),(string)row["name"]);
                Assert.That(CapabilityArguments.Validate(row["recipe"],CapabilitySchema.RecipeSchema(),out var error),Is.EqualTo((bool)row["valid"]),(string)row["name"]+": "+error);
                if(!(bool)row["valid"])Assert.Throws<ArgumentException>(()=>RecipeSweep.Build(recipe.parts[0]));else CheckMesh(recipe.parts[0]);
            }
        }
        static void CheckMesh(RecipePart part){
            var mesh=RecipeSweep.Build(part);try{
                var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;
                Assert.That(v.Length,Is.EqualTo(part.profile.Length*(2+4*(part.path.Length-1))));Assert.That(v.Length,Is.EqualTo(RecipeGeometry.VertexCost(new RoomRecipe{parts=new[]{part}})));Assert.That(mesh.uv.Length,Is.EqualTo(v.Length));
                Assert.That(v.All(p=>RoomRecipe.Finite(p)&&Mathf.Abs(p.x)<=.50002f&&Mathf.Abs(p.y)<=.50002f&&Mathf.Abs(p.z)<=.50002f),Is.True);
                Assert.That(n.All(p=>RoomRecipe.Finite(p)&&Mathf.Abs(p.magnitude-1)<.0001f),Is.True);double volume=0;
                for(int i=0;i<t.Length;i+=3){var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];var cross=Vector3.Cross(b-a,c-a);Assert.That(cross.sqrMagnitude,Is.GreaterThan(1e-16));Assert.That(Vector3.Dot(cross,n[t[i]]+n[t[i+1]]+n[t[i+2]]),Is.GreaterThan(0));volume+=Vector3.Dot(a,Vector3.Cross(b,c))/6d;}
                Assert.That(volume,Is.GreaterThan(0));Assert.That(mesh.uv.All(p=>p.x>=0&&p.x<=1.00001&&p.y>=0&&p.y<=1.00001),Is.True);
                if(part.path.Length==2){double area=0;for(int i=0;i<part.profile.Length;i++){var a=part.profile[i];var c=part.profile[(i+1)%part.profile.Length];area+=(double)a.x*c.y-(double)c.x*a.y;}Assert.That(volume,Is.EqualTo(area*.5*Vector3.Distance(part.path[0],part.path[1])).Within(1e-6),"Both ends must be capped with outward winding");}
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void SweepSeededSpatialPathsKeepFiniteOutwardGeometry(){
            var random=new System.Random(4517);for(int sample=0;sample<60;sample++){
                var part=Handle().parts[0];int count=2+sample%15;float angle=(float)(random.NextDouble()*Math.PI*2);
                part.path=Enumerable.Range(0,count).Select(i=>{float u=i/(float)(count-1);return new Vector3(.2f*Mathf.Cos(angle+u*2),-.3f+u*.6f,.2f*Mathf.Sin(angle+u*2));}).ToArray();
                Assert.That(RecipeSweep.Valid(part),Is.True,"Sample "+sample);CheckMesh(part);
            }
        }
        [Test] public void SweepUsesRoomGeometryBudgetAndOtherShapesCannotHideAPath(){
            var recipe=Handle();var part=recipe.parts[0];part.path=Enumerable.Range(0,16).Select(i=>new Vector3(0,0,-.4f+i*.8f/15)).ToArray();part.profile=Enumerable.Range(0,32).Select(i=>new Vector2(Mathf.Cos(i*Mathf.PI/16)*.04f,Mathf.Sin(i*Mathf.PI/16)*.04f)).ToArray();
            recipe.parts=Enumerable.Range(0,32).Select(i=>{var clone=JsonUtility.FromJson<RecipePart>(JsonUtility.ToJson(part));clone.id="Part"+i;return clone;}).ToArray();Assert.That(recipe.Validate(out var error),Is.True,error);Assert.That(RecipeGeometry.VertexCost(recipe),Is.EqualTo(63488));
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}.Concat(Enumerable.Range(0,4).Select(i=>new RoomObjectData{id=i.ToString("x32"),kind=RoomObjectKind.Assembly,recipe=recipe.Copy()})).ToArray()};Assert.That(room.Validate(out error),Is.True,error);
            room.objects=room.objects.Append(new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Assembly,recipe=recipe.Copy()}).ToArray();Assert.That(room.Validate(out _),Is.False);
            part.shape="box";part.profile=Array.Empty<Vector2>();Assert.That(new RoomRecipe{parts=new[]{part}}.Validate(out _),Is.False);
        }
        [Test] public void SweepCurrentFormatPreservesCleanEarlierRoomsAndPreventsDowngradeFallback(){
            string directory=Path.Combine(Path.GetTempPath(),"MaestroSweep-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{
                var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Assembly,recipe=Handle()}}};Assert.That(room.Validate(out var error),Is.True,error);
                var copy=room.Copy();copy.version=10;Assert.That(copy.Validate(out _),Is.False);copy.objects[2].recipe.parts[0].shape="box";copy.objects[2].recipe.parts[0].profile=Array.Empty<Vector2>();copy.objects[2].recipe.parts[0].path=Array.Empty<Vector3>();string earlier=JsonUtility.ToJson(copy);File.WriteAllText(Path.Combine(directory,"room.v10.json"),earlier);
                var storage=new RoomStorage(directory);Assert.That(storage.Load(out _).version,Is.EqualTo(RoomDocument.CurrentVersion));Assert.That(storage.Save(room,out error),Is.True,error);Assert.That(File.ReadAllText(Path.Combine(directory,"room.v10.json")),Is.EqualTo(earlier));Assert.That(storage.Load(out _).objects[2].recipe.parts[0].shape,Is.EqualTo("sweep"));
                var older=new VersionedRoomFile<RoomDocument>(directory,"room",4*1024*1024,r=>r.Validate(out _),r=>r.Copy(),RoomStorage.Normalize,r=>r.version=10,version:10);Assert.That(older.Load(out _),Is.Null);Assert.That(older.ReadOnly,Is.True);
            }finally{Directory.Delete(directory,true);}
        }
    }
}

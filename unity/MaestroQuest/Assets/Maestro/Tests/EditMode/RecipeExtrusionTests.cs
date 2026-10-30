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
    public sealed class RecipeExtrusionTests {
        internal static JArray Cases()=>JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/extrusion-contract.json")));
        internal static RoomRecipe Bracket()=>JsonUtility.FromJson<RoomRecipe>(Cases()[0]["recipe"].ToString());
        [Test] public void SharedOutlineCasesMatchNativeGeometryAndCatalogAdmission(){
            foreach(var row in Cases()){
                var recipe=JsonUtility.FromJson<RoomRecipe>(row["recipe"].ToString());Assert.That(recipe.Validate(out _),Is.EqualTo((bool)row["valid"]),(string)row["name"]);
                Assert.That(CapabilityArguments.Validate(row["recipe"],CapabilitySchema.RecipeSchema(),out var error),Is.EqualTo((bool)row["valid"]),(string)row["name"]+": "+error);
                if(!(bool)row["valid"])Assert.Throws<ArgumentException>(()=>RecipeExtrusion.Build(recipe.parts[0]));else CheckMesh(recipe.parts[0]);
            }
        }
        static void CheckMesh(RecipePart part){
            var mesh=RecipeExtrusion.Build(part);try{
                var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;Assert.That(v.Length,Is.EqualTo(6*part.profile.Length));Assert.That(mesh.uv.Length,Is.EqualTo(v.Length));Assert.That(v.Length,Is.LessThanOrEqualTo(RecipeGeometry.VertexCost(new RoomRecipe{parts=new[]{part}})));
                Assert.That(n.All(p=>RoomRecipe.Finite(p)&&Mathf.Abs(p.magnitude-1)<.0001f),Is.True);double volume=0;
                for(int i=0;i<t.Length;i+=3){var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];var cross=Vector3.Cross(b-a,c-a);Assert.That(cross.sqrMagnitude,Is.GreaterThan(1e-16));Assert.That(Vector3.Dot(cross,n[t[i]]+n[t[i+1]]+n[t[i+2]]),Is.GreaterThan(0));volume+=Vector3.Dot(a,Vector3.Cross(b,c))/6d;}
                double area=0;for(int i=0;i<part.profile.Length;i++){var a=part.profile[i];var b=part.profile[(i+1)%part.profile.Length];area+=(double)a.x*b.y-(double)b.x*a.y;}
                Assert.That(volume,Is.EqualTo(area*.5).Within(1e-6),"Concave notches must remain outside the solid");Assert.That(mesh.bounds.size.z,Is.EqualTo(1));Assert.That(mesh.uv.All(p=>p.x>=0&&p.x<=1.00001&&p.y>=0&&p.y<=1),Is.True);
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void BoundedSeededConcaveOutlinesHaveClosedOutwardGeometry(){
            var random=new System.Random(7139);for(int sample=0;sample<80;sample++){int count=3+sample%30;var part=Bracket().parts[0];part.profile=Enumerable.Range(0,count).Select(i=>{double angle=i*Math.PI*2/count;float r=(float)(.15+random.NextDouble()*.34);return new Vector2(r*(float)Math.Cos(angle),r*(float)Math.Sin(angle));}).ToArray();Assert.That(RecipeExtrusion.Valid(part),Is.True);CheckMesh(part);}
        }
        [Test] public void CurrentRoomsPreserveOlderFilesAndRefuseExtrusionsMislabeledAsOlderData(){
            string directory=Path.Combine(Path.GetTempPath(),"MaestroExtrusion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{
                var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Assembly,recipe=Bracket()}}};Assert.That(room.Validate(out var error),Is.True,error);
                var copy=room.Copy();copy.version=9;Assert.That(copy.Validate(out _),Is.False);copy.objects[2].recipe.parts[0].shape="box";copy.objects[2].recipe.parts[0].profile=Array.Empty<Vector2>();string earlier=JsonUtility.ToJson(copy);File.WriteAllText(Path.Combine(directory,"room.v9.json"),earlier);
                var storage=new RoomStorage(directory);Assert.That(storage.Load(out _).version,Is.EqualTo(RoomDocument.CurrentVersion));Assert.That(storage.Save(room,out error),Is.True,error);Assert.That(File.ReadAllText(Path.Combine(directory,"room.v9.json")),Is.EqualTo(earlier));Assert.That(storage.Load(out _).objects[2].recipe.parts[0].shape,Is.EqualTo("extrude"));
                var older=new VersionedRoomFile<RoomDocument>(directory,"room",4*1024*1024,r=>r.Validate(out _),r=>r.Copy(),RoomStorage.Normalize,r=>r.version=9,version:9);Assert.That(older.Load(out _),Is.Null);Assert.That(older.ReadOnly,Is.True,"An older reader must not roll back to its valid earlier room");
            }finally{Directory.Delete(directory,true);}
        }
    }
}

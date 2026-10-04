// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class CurvedDrawingSurfaceTests {
        static DrawingSurface Patch(string kind)=>new(){id="Paint",version=2,shape=kind,curvatureRadius=.2f,width=.8f,height=.4f};
        [Test] public void CurvedSurfaceContractRejectsBadGeometryBeforeDispatch(){
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/curved-surfaces-contract.json")));
            foreach(var c in cases)Assert.That(CapabilityArguments.Validate(c["definition"],DrawingSurfaceCapability.DefinitionSchema(),out _),Is.EqualTo((bool)c["valid"]),(string)c["name"]);
        }
        [Test] public void CurvedSurfaceThinInkOnLargeRadiusStaysAboveTheShell(){
            var s=Patch("sphere");s.curvatureRadius=4;s.width=s.height=4;var path=DrawingSurfaceGeometry.Path(s,new[]{new Vector3(-1.8f,-1,0),new Vector3(1.8f,1,0)},.001f);
            for(int i=1;i<path.Length;i++)Assert.That(Vector3.Distance((path[i-1]+path[i])*.5f,new Vector3(0,0,4)),Is.GreaterThan(4.0005f));
        }
        [TestCase("cylinder")][TestCase("sphere")]
        public void CurvedSurfaceRaysAndMappedInkAgreeAcrossThePatch(string kind){
            var s=Patch(kind);Assert.That(s.Validate(new RoomObjectData(),out var error),Is.True,error);
            foreach(float x in new[]{-.35f,0,.35f})foreach(float y in new[]{-.15f,0,.15f}){
                var uv=new Vector3(x,y,0);var p=DrawingSurfaceGeometry.Point(s,uv);var n=(p-new Vector3(0,kind=="cylinder"?y:0,.2f)).normalized;
                Assert.That(DrawingSurfaceGeometry.Hit(s,p+n*.1f,-n,.2f,.003f,out var found,out var distance),Is.True);
                Assert.That(Vector3.Distance(found,uv),Is.LessThan(.00001));Assert.That(distance,Is.EqualTo(.1f).Within(.00001));
                Assert.That(Vector3.Distance(DrawingSurfaceGeometry.Point(s,uv,.003f),p),Is.EqualTo(.003f).Within(.00001));
                Assert.That(DrawingSurfaceGeometry.Hit(s,p-n*.03f,n,.2f,.003f,out _,out _),Is.False,"Inside/back-facing rays cannot paint through the shell");
                Assert.That(DrawingSurfaceGeometry.Hit(s,p+n*.1f,-n,.05f,.003f,out _,out _),Is.False);
            }
        }
        [TestCase("cylinder")][TestCase("sphere")]
        public void CurvedSurfaceInkSubdividesOnTheShellAndBudgetsRenderedPoints(string kind){
            var s=Patch(kind);var points=new[]{new Vector3(-.3f,-.1f,0),new Vector3(.3f,.1f,0)};var path=DrawingSurfaceGeometry.Path(s,points,.003f);
            Assert.That(path.Length,Is.GreaterThan(30));Assert.That(path.Length,Is.EqualTo(DrawingSurfaceGeometry.PointCount(s,points)));
            foreach(var p in path)Assert.That(Vector3.Distance(p,new Vector3(0,kind=="cylinder"?p.y:0,.2f)),Is.EqualTo(.203f).Within(.00001));
            s.strokes=new[]{new SurfaceStroke{id=new string('a',32),points=points}};var owner=new RoomObjectData{surfaces=new[]{s}};Assert.That(DrawingSurface.PointCount(owner),Is.EqualTo(path.Length));
            s.strokes[0].points=Enumerable.Range(0,512).Select(i=>points[i%2]).ToArray();Assert.That(s.Validate(owner,out _),Is.False);Assert.Throws<ArgumentException>(()=>DrawingSurfaceGeometry.Path(s,s.strokes[0].points,.003f));
        }
        [Test] public void CurvedSurfaceBoundsAvoidSeamOverlapPolesAndSilentPlaneReinterpretation(){
            var s=Patch("sphere");var owner=new RoomObjectData();s.height=.6f;Assert.That(s.Validate(owner,out _),Is.False);s.height=.4f;s.width=1.3f;Assert.That(s.Validate(owner,out _),Is.False);
            s.width=.8f;s.version=1;Assert.That(s.Validate(owner,out _),Is.False);s.version=2;s.curvatureRadius=float.NaN;Assert.That(s.Validate(owner,out _),Is.False);
            s=Patch("unknown");Assert.That(s.Validate(owner,out _),Is.False);s=new DrawingSurface{id="Plane"};Assert.That(s.Validate(owner,out _),Is.True);s.curvatureRadius=.2f;Assert.That(s.Validate(owner,out _),Is.False);
        }
        [Test] public void CurvedSurfaceStoragePreservesCoordinatesAndOlderReadersCannotFallBack(){
            string dir=Path.Combine(Path.GetTempPath(),"curved-surface-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var s=Patch("sphere");s.strokes=new[]{new SurfaceStroke{id=new string('b',32),points=new[]{Vector3.left*.1f,Vector3.up*.05f,Vector3.right*.1f}}};
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('c',32),kind=RoomObjectKind.Ball,surfaces=new[]{s}}}};
            try{
                var old=room.Copy();old.version=11;Assert.That(old.Validate(out _),Is.False);old.objects[2].surfaces=Array.Empty<DrawingSurface>();string oldBytes=JsonUtility.ToJson(old);File.WriteAllText(Path.Combine(dir,"room.v11.json"),oldBytes);
                var store=new RoomStorage(dir);Assert.That(store.Save(room,out var error),Is.True,error);var restored=store.Load(out error);Assert.That(restored,Is.Not.Null,error);Assert.That(restored.objects[2].surfaces[0].strokes[0].points,Is.EqualTo(s.strokes[0].points));Assert.That(restored.objects[2].surfaces[0].Kind,Is.EqualTo("sphere"));
                var reader=new VersionedRoomFile<RoomDocument>(dir,"room",4*1024*1024,r=>r.Validate(out _),r=>r.Copy(),RoomStorage.Normalize,r=>r.version=11,version:11);Assert.That(reader.Load(out _),Is.Null);Assert.That(reader.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(dir,"room.v11.json")),Is.EqualTo(oldBytes));
            }finally{Directory.Delete(dir,true);}
        }
    }
}

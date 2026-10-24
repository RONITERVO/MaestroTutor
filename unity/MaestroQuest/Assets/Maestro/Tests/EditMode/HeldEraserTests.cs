// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class HeldEraserTests {
        static SurfaceStroke Mark(char id,Vector3 a,Vector3 b)=>new(){id=new string(id,32),radius=.003f,points=new[]{a,b}};
        [Test] public void HeldEraserSweepsBetweenSamplesWithoutSelectingDistantInk(){
            var s=new DrawingSurface{id="Front",width=.2f,height=.2f,strokes=new[]{Mark('a',new(-.03f,-.04f,0),new(-.03f,.04f,0)),Mark('b',new(.03f,-.04f,0),new(.03f,.04f,0)),Mark('c',new(-.02f,.07f,0),new(.02f,.07f,0))}};
            var erase=new SurfaceEraseSelection(s,.002f);erase.Sample(new(-.049f,0,0));Assert.That(erase.Count,Is.Zero);erase.Sample(new(.049f,0,0));Assert.That(erase.Removed,Is.EqualTo(new[]{new string('a',32),new string('b',32)}));
            Assert.That(erase.Sample(new(.8f,0,0)),Is.False);Assert.That(s.strokes.Length,Is.EqualTo(3));
        }
        [TestCase("cylinder")][TestCase("sphere")] public void HeldEraserFollowsCurvedArcRatherThanAnInteriorChord(string kind){
            var s=new DrawingSurface{id="Front",version=2,shape=kind,curvatureRadius=.065f,width=.28f,height=.12f,strokes=new[]{Mark('a',new(0,-.03f,0),new(0,.03f,0))}};
            var erase=new SurfaceEraseSelection(s,.001f);erase.Sample(new(-.1f,0,0));Assert.That(erase.Count,Is.Zero);erase.Sample(new(.1f,0,0));Assert.That(erase.Count,Is.EqualTo(1));
        }
        [Test] public void HeldEraserVersionCannotBecomeDrawingInAnOlderRoom(){
            var tip=new DrawingTip{mode="erase",version=2};var item=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,drawingTips=new[]{tip}};
            Assert.That(tip.Validate(item,out var error),Is.True,error);var copy=item.Copy();Assert.That(copy.drawingTips.Single().Mode,Is.EqualTo("erase"));copy.drawingTips[0].mode="draw";Assert.That(copy.drawingTips[0].Validate(copy,out _),Is.False);
            var room=new RoomDocument{version=12,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},item}};Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;Assert.That(room.Validate(out error),Is.True,error);
            string dir=Path.Combine(Path.GetTempPath(),"eraser-room-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try {var store=new RoomStorage(dir);Assert.That(store.Save(room,out error),Is.True,error);Assert.That(store.Load(out error).objects.Last().drawingTips.Single().Mode,Is.EqualTo("erase"));
                var old=new VersionedRoomFile<RoomDocument>(dir,"room",4*1024*1024,r=>r.Validate(out _),r=>r.Copy(),RoomStorage.Normalize,r=>r.version=12,version:12);Assert.That(old.Load(out _),Is.Null);Assert.That(old.ReadOnly,Is.True);
            }finally{Directory.Delete(dir,true);}
        }
        [Test] public void HeldEraserAndDrawingDefaultsAreEditableOrdinaryComponents(){
            foreach(string id in new[]{"pencil","brush","eraser"}){var t=CreationTemplates.All.Single(x=>x.Id==id);Assert.That(t.Recipe.Validate(out var error),Is.True,error);Assert.That(t.Collision.Validate(out error),Is.True,error);var tip=t.DrawingTips.Single();Assert.That(tip.part,Is.Empty);Assert.That(tip.Mode,Is.EqualTo(id=="eraser"?"erase":"draw"));Assert.That(tip.Validate(new RoomObjectData{recipe=t.Recipe},out error),Is.True,error);Assert.That(t.Physics.mode,Is.EqualTo("solid"));}
        }
    }
}

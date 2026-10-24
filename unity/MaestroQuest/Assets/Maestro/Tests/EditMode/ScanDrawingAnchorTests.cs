// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class ScanDrawingAnchorTests
    {
        static ScanDrawingAnchor Anchor()=>new(){roomId=new string('a',32),anchorId=new string('b',32)};
        static RoomObjectData Layer()=>new(){id=new string('c',32),kind=RoomObjectKind.Drawing,points=Array.Empty<Vector3>(),scanAnchors=new[]{Anchor()},surfaces=new[]{new DrawingSurface{id="Canvas",strokes=new[]{new SurfaceStroke{id=new string('d',32),points=new[]{Vector3.left*.03f,Vector3.right*.03f}}}}}};
        static RoomDocument Room(RoomObjectData layer)=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},layer}};
        [Test] public void LayerSourceRoundTripsAndCopiesIndependentlyWithoutFreeSpaceStroke()
        {
            var layer=Layer();var room=Room(layer);Assert.That(room.Validate(out var error),Is.True,error);var copy=layer.Copy();copy.scanAnchors[0].x=.1f;copy.surfaces[0].strokes[0].points[0]=Vector3.zero;Assert.That(layer.scanAnchors[0].x,Is.Zero);Assert.That(layer.surfaces[0].strokes[0].points[0],Is.Not.EqualTo(Vector3.zero));
            string dir=Path.Combine(Path.GetTempPath(),"scan-ink-"+Guid.NewGuid().ToString("N"));
            try{var store=new RoomStorage(dir);Assert.That(store.Save(room,out error),Is.True,error);var read=store.Load(out error);Assert.That(read,Is.Not.Null,error);var saved=read.objects.Single(o=>o.id==layer.id);Assert.That(JsonUtility.ToJson(saved),Is.EqualTo(JsonUtility.ToJson(layer)));Assert.That(saved.points,Is.Empty);}finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
            room.version=19;Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;layer.scanAnchors=Array.Empty<ScanDrawingAnchor>();Assert.That(room.Validate(out _),Is.False,"An ordinary 3D stroke still needs at least two points");
        }
        [TestCase("room")][TestCase("anchor")][TestCase("version")][TestCase("nan")][TestCase("duplicate")][TestCase("kind")][TestCase("physics")][TestCase("motion")][TestCase("scale")][TestCase("surface")][TestCase("curve")][TestCase("position")][TestCase("rotation")][TestCase("offset")][TestCase("points")]
        public void InvalidBindingsCannotBecomeLooseOrDynamicObjects(string fault)
        {
            var layer=Layer();switch(fault){case "room":layer.scanAnchors[0].roomId=new string('0',32);break;case "anchor":layer.scanAnchors[0].anchorId="bad";break;case "version":layer.scanAnchors[0].version=2;break;case "nan":layer.scanAnchors[0].angle=float.NaN;break;case "duplicate":layer.scanAnchors=new[]{Anchor(),Anchor()};break;case "kind":layer.kind=RoomObjectKind.Block;break;case "physics":layer.physics=ItemPhysics.Solid;break;case "motion":layer.motion=new RoomMotion();break;case "scale":layer.scale=2;break;case "surface":layer.surfaces[0].id="Other";break;case "curve":layer.surfaces[0].shape="cylinder";break;case "position":layer.position=Vector3.one;break;case "rotation":layer.rotation=Quaternion.Euler(0,20,0);break;case "offset":layer.surfaces[0].position=Vector3.right;break;case "points":layer.points=new[]{Vector3.zero,Vector3.up};break;}
            Assert.That(Room(layer).Validate(out _),Is.False,fault);
        }
        [Test] public void RotatedInkMustFitRectangleAndConcaveBoundaryIncludingVertexCrossings()
        {
            var anchor=Anchor();var surface=new ScannedSurface{Plane=new Rect(-1,-1,2,2)};
            Assert.That(anchor.Fits(surface,2,2),Is.True);anchor.angle=45;Assert.That(anchor.Fits(surface,2,2),Is.False);Assert.That(anchor.Fits(surface,1,1),Is.True);anchor.angle=0;
            // Notch reaches below the proposed ink's top edge, with both intersections at vertices.
            surface.Boundary=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(.2f,1),new Vector2(.2f,.5f),new Vector2(0,0),new Vector2(-.2f,.5f),new Vector2(-.2f,1),new Vector2(-1,1)};
            Assert.That(anchor.Fits(surface,1,1),Is.False);anchor.y=-.5f;Assert.That(anchor.Fits(surface,1,.5f),Is.True);Assert.That(anchor.Fits(surface,float.NaN,1),Is.False);
            surface.Boundary=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};anchor.y=0;Assert.That(anchor.Fits(surface,2,2),Is.True,"Coincident boundary is allowed");
        }
    }
}

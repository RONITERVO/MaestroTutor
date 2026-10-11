// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class DrawingSurfaceTests
    {
        static RoomObjectData Owner()=>new(){id=new string('a',32),kind=RoomObjectKind.Block,surfaces=new[]{new DrawingSurface {id="Front",strokes=new[]{new SurfaceStroke {id=new string('b',32),points=new[]{Vector3.left*.05f,Vector3.right*.05f}}}}}};
        [Test] public void SurfaceCopyPreservesIndependentEditableInkAndRejectsUnknownAnchors()
        {
            var owner=Owner();Assert.That(DrawingSurface.ValidateCollection(owner,out var error),Is.True,error);var copy=owner.Copy();copy.surfaces[0].strokes[0].points[0]=Vector3.zero;Assert.That(owner.surfaces[0].strokes[0].points[0],Is.Not.EqualTo(Vector3.zero));
            copy.surfaces[0].part="Missing";Assert.That(DrawingSurface.ValidateCollection(copy,out _),Is.False);copy=owner.Copy();copy.surfaces[0].version=2;Assert.That(DrawingSurface.ValidateCollection(copy,out _),Is.False);
        }
        [Test] public void SurfaceRejectsOutOfBoundsNonPlanarAndDuplicateInkWithoutCropping()
        {
            var owner=Owner();owner.surfaces[0].width=.08f;Assert.That(DrawingSurface.ValidateCollection(owner,out _),Is.False);owner=Owner();owner.surfaces[0].strokes[0].points[0].z=.01f;Assert.That(DrawingSurface.ValidateCollection(owner,out _),Is.False);
            owner=Owner();owner.surfaces[0].strokes=new[]{owner.surfaces[0].strokes[0],owner.surfaces[0].strokes[0].Copy()};Assert.That(DrawingSurface.ValidateCollection(owner,out _),Is.False);owner=Owner();owner.surfaces[0].strokes[0].radius=float.NaN;Assert.That(DrawingSurface.ValidateCollection(owner,out _),Is.False);
        }
        [Test] public void SurfaceAndFreeSpaceInkConsumeTheSameRoomPointBudget()
        {
            var objects=new System.Collections.Generic.List<RoomObjectData>{new(){id="book",kind=RoomObjectKind.Book},new(){id="maestro",kind=RoomObjectKind.Maestro}};
            for(int i=0;i<16;i++)objects.Add(new RoomObjectData {id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Drawing,points=Enumerable.Range(0,RoomDocument.MaximumStrokePoints).Select(n=>new Vector3(n*.001f,0,0)).ToArray()});
            var room=new RoomDocument {version=RoomDocument.CurrentVersion,objects=objects.ToArray()};Assert.That(room.Validate(out var error),Is.True,error);
            objects.Add(Owner());room.objects=objects.ToArray();Assert.That(room.Validate(out error),Is.False);Assert.That(error,Does.Contain("drawing limit"));
            objects[2].points=objects[2].points.Skip(2).ToArray();Assert.That(room.Validate(out error),Is.True,error);
        }
        [Test] public void SurfacePointsShareRoomDrawingBudgetAndRoundTripThroughStorage()
        {
            var owner=Owner();var room=new RoomDocument {version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro},owner}};
            Assert.That(room.Validate(out var error),Is.True,error);string dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"surface-"+Guid.NewGuid().ToString("N"));
            try{var store=new RoomStorage(dir);Assert.That(store.Save(room,out error),Is.True,error);var read=store.Load(out error);Assert.That(read.objects.Single(x=>x.id==owner.id).surfaces[0].strokes[0].points,Is.EqualTo(owner.surfaces[0].strokes[0].points));}finally{if(System.IO.Directory.Exists(dir))System.IO.Directory.Delete(dir,true);}
            room.version=3;Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;room.objects[0].surfaces=owner.surfaces;Assert.That(room.Validate(out _),Is.False);
        }
    }
}

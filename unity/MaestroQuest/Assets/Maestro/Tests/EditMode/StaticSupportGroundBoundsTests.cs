// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class StaticSupportGroundBoundsTests {
        static RoomObjectData Block()=>new(){id=new string('a',32),kind=RoomObjectKind.Block,physics=ItemPhysics.Fixed,staticSupport=new(){enabled=true}};
        static RoomJournal Journal(params RoomObjectData[] values)=>new(new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{
            new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}
        }.Concat(values).ToArray()});
        static RoomHeightField[] Terrain()=>new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
        static void Vector(Vector3 actual,Vector3 expected)=>Assert.That(UnityEngine.Vector3.Distance(actual,expected),Is.LessThan(.0001f));
        [Test]public void StaticSupportGroundProjectionKeepsIndependentProviderKindsAndExactIdentity(){
            var primary=Block();primary.heightFields=Terrain();
            var rigid=new RoomObjectData{id=new string('b',32),kind=RoomObjectKind.ImportedModel,physics=ItemPhysics.Fixed,modelHash=new string('1',64),modelGeometry=new(){meshCollision=true,walkable=true},heightFields=Terrain()};
            var journal=Journal(primary,rigid);var source=JsonUtility.ToJson(journal.Snapshot());
            Assert.That(journal.GroundCandidate(primary.id,out var a),Is.True);Assert.That(a.target,Is.EqualTo(primary.id));
            Assert.That(a.ordinary&&a.terrain&&!a.imported,Is.True);
            Assert.That(journal.GroundCandidate(rigid.id,out var b),Is.True);Assert.That(b.target,Is.EqualTo(rigid.id));
            Assert.That(!b.ordinary&&b.terrain&&b.imported,Is.True);
            CollectionAssert.AreEqual(new[]{primary.id,rigid.id},journal.GroundCandidates().Select(x=>x.target));
            var held=journal.GroundCandidates();var edit=journal.Read(primary.id);edit.staticSupport.enabled=false;
            Assert.That(journal.Apply(new[]{edit},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(journal.GroundCandidate(primary.id,out var changed)&&changed.terrain&&!changed.ordinary,Is.True);
            Assert.That(held[0].ordinary,Is.True,"Old descriptors are immutable values");
            journal.Undo();Assert.That(journal.GroundCandidate(primary.id,out changed)&&changed.ordinary,Is.True);
            Assert.That(JsonUtility.ToJson(journal.Snapshot()),Is.EqualTo(source));
        }
        [Test]public void StaticSupportColdProxyBoundsIncludeSelectedOffsetRotationAndPlacement(){
            var data=Block();data.position=new(4,1,-3);data.rotation=Quaternion.Euler(0,90,0);data.scale=2;
            data.collision=new(){shapes=new[]{new CollisionShape{id="platform",position=Vector3.right*1.5f,size=new(.6f,.1f,.2f),rotation=Quaternion.Euler(0,90,0)}}};
            var journal=Journal(data);string before=JsonUtility.ToJson(journal.Snapshot());
            var extent=journal.GroundSupportExtent(data.id);Assert.That(extent.Known&&extent.HasBounds,Is.True);
            Vector(extent.Bounds.center,new(4,1,-6));Vector(extent.Bounds.size,new(1.2f,.2f,.4f));
            Assert.That(extent.Bounds.Contains(data.position),Is.False,"The visual root cannot stand in for an offset collision source");
            var retained=journal.GroundSupportExtent(data.id,Matrix4x4.TRS(new(-2,3,1),Quaternion.identity,Vector3.one));
            Vector(retained.Bounds.center,new(-.5f,3,1));Vector(retained.Bounds.size,new(.2f,.1f,.6f));
            Assert.That(JsonUtility.ToJson(journal.Snapshot()),Is.EqualTo(before),"Projection is passive");
        }
        [TestCase("cylinder")] [TestCase("ring")]
        public void StaticSupportColdMeshProxyEnvelopeIncludesWholeSelectedShape(string shape){
            var data=Block();data.collision=new(){shapes=new[]{new CollisionShape{id="mesh",shape=shape,size=new(.8f,.2f,.6f),position=new(1,0,0),rotation=Quaternion.Euler(30,20,10),segments=12,innerRadius=shape=="ring"?.25f:0}}};
            Assert.That(RoomStaticSupport.Validate(data,out var error),Is.True,error);
            var extent=RoomGroundBounds.Ordinary(data);Assert.That(extent.Known&&extent.HasBounds,Is.True);
            var p=data.collision.shapes[0];
            for(int level=0;level<2;level++)for(int segment=0;segment<12;segment++){
                float a=segment*Mathf.PI/6;var point=new Vector3(Mathf.Cos(a)*p.size.x*.5f,(level==0?-.5f:.5f)*p.size.y,Mathf.Sin(a)*p.size.z*.5f);
                var conservative=extent.Bounds;conservative.Expand(.0001f);Assert.That(conservative.Contains(p.position+p.rotation*point),Is.True);
            }
        }
        [Test]public void StaticSupportBoundsFollowSelectedBoxRatherThanIgnoredCustomRecipe(){
            var data=Block();data.collisionShape=ItemCollider.Box;
            data.collision=new(){shapes=new[]{new CollisionShape{id="unused",position=Vector3.right*1.5f,size=Vector3.one*.4f}}};
            var extent=RoomGroundBounds.Ordinary(data);Assert.That(extent.Known,Is.True);Vector(extent.Bounds.center,Vector3.zero);Vector(extent.Bounds.size,Vector3.one*.13f);
        }
        [Test]public void StaticSupportAssemblyRestBoundsDoNotTreatDecorativeAnimationAsRootMotion(){
            var data=Block();data.kind=RoomObjectKind.Assembly;
            data.recipe=new(){
                parts=new[]{
                    new RecipePart{id="root",position=Vector3.right,size=Vector3.one*.2f,rotation=Quaternion.Euler(0,90,0)},
                    new RecipePart{id="child",parent="root",position=Vector3.right*.5f,size=Vector3.one*.2f}
                },
                tracks=new[]{
                    new RecipeTrack{part="root",keys=new[]{new RecipeKey{time=0},new RecipeKey{time=2,rotation=Quaternion.Euler(0,180,0)}}}
                },
                playing=true
            };
            var extent=RoomGroundBounds.Ordinary(data);Assert.That(extent.Known,Is.True);Vector(extent.Bounds.center,new(1,0,-.25f));Vector(extent.Bounds.size,new(.2f,.2f,.7f));
            Assert.That(data.recipe.playing,Is.True);Assert.That(data.motion,Is.Null);
        }
        [Test]public void StaticSupportDrawingEnvelopeIncludesStrokeRadiusAndAutomaticColliderPadding(){
            var data=Block();data.kind=RoomObjectKind.Drawing;data.radius=.02f;data.points=new[]{Vector3.zero,new Vector3(.8f,.2f,-.4f)};
            var extent=RoomGroundBounds.Ordinary(data);Assert.That(extent.Known,Is.True);Vector(extent.Bounds.center,new(.4f,.1f,-.2f));Vector(extent.Bounds.size,new(.858f,.258f,.458f));
        }
        [Test]public void StaticSupportUnknownAndMixedSourceBoundsStayConservative(){
            var data=Block();data.collision=new(){shapes=new[]{new CollisionShape{id="valid"},new CollisionShape{id="unsupported",shape="sphere"}}};
            Assert.That(RoomGroundBounds.Ordinary(data).Known,Is.False,"Never salvage a supported subset");
            var known=new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=new Bounds(Vector3.right*3,Vector3.one)};
            Assert.That(RoomGroundBounds.Union(known,default).Known,Is.False);
            var terrain=new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=new Bounds(Vector3.left*3,Vector3.one)};
            var combined=RoomGroundBounds.Union(known,terrain);Assert.That(combined.Known,Is.True);Assert.That(combined.Bounds.Contains(known.Bounds.max)&&combined.Bounds.Contains(terrain.Bounds.min),Is.True);
        }
    }
}

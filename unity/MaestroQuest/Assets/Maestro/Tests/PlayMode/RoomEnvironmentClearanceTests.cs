// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed class RoomEnvironmentClearanceTests
    {
        GameObject root,staging;RoomPhysicsWorld world;BoxCollider wall;
        readonly RoomEnvironmentClearance clearance=new();
        [SetUp]public void SetUp(){
            RoomPhysicsLayers.Configure();root=new GameObject("Environment clearance");
            root.transform.position=new Vector3(30,10,-20);root.AddComponent<XRInteractionManager>();
            world=root.AddComponent<RoomPhysicsWorld>();world.SetSurfaces(true,"Aligned");world.Contains=_=>true;
            staging=new GameObject("Unpublished");staging.transform.SetParent(root.transform,false);staging.SetActive(false);
            var scanned=new GameObject("Scanned wall");scanned.transform.SetParent(root.transform,false);scanned.layer=RoomPhysicsLayers.Scanned;wall=scanned.AddComponent<BoxCollider>();
        }
        [TearDown]public void TearDown(){Object.DestroyImmediate(root);}
        RoomItem Item(string shape,bool unpublished=false){
            var value=new GameObject(shape);value.transform.SetParent(unpublished?staging.transform:root.transform,false);Collider collider;
            if(shape=="sphere")collider=value.AddComponent<SphereCollider>();
            else if(shape=="capsule")collider=value.AddComponent<CapsuleCollider>();
            else if(shape=="convex"||shape=="concave"){
                var source=GameObject.CreatePrimitive(PrimitiveType.Cube);
                var mesh=value.AddComponent<MeshCollider>();mesh.sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;mesh.convex=shape=="convex";collider=mesh;Object.DestroyImmediate(source);
            }else collider=value.AddComponent<BoxCollider>();
            var item=value.AddComponent<RoomItem>();item.Configure(new[]{collider});return item;
        }
        [TestCase("box")][TestCase("sphere")][TestCase("capsule")][TestCase("convex")]
        public void InactiveOverlapsRemainUnprovenUntilShapesAreEnabled(string shape){
            var item=Item(shape);var own=item.Grab.colliders[0];Physics.SyncTransforms();
            Assert.That(Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,wall,wall.transform.position,wall.transform.rotation,out _,out float activeDepth),Is.True);
            Assert.That(activeDepth,Is.GreaterThan(.003f));Assert.That(clearance.Check(world,item,true,true,out _),Is.False);
            item.transform.SetParent(staging.transform,false);Physics.SyncTransforms();
            Assert.That(own.bounds.size,Is.EqualTo(Vector3.zero),"The test must exercise Unity's inactive-bounds case");
            Assert.That(Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,wall,wall.transform.position,wall.transform.rotation,out _,out _),Is.False,"Pinned Unity does not prove overlap for inactive native shapes");
            Assert.That(clearance.Check(world,item,true,true,out var pending),Is.False);Assert.That(pending,Does.Contain("not ready"));
            item.transform.localPosition=Vector3.right*3;
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
            Assert.That(item.gameObject.activeInHierarchy,Is.False,"A proof must not publish the candidate");
        }
        [Test]public void ConservativeSphereEnvelopeDoesNotTurnAnAabbOverlapIntoAPenetration(){
            var item=Item("sphere");item.transform.SetParent(staging.transform,false);
            wall.size=Vector3.one*.1f;wall.transform.localPosition=new Vector3(.7f,.7f,0);
            var envelope=RoomEnvironmentClearance.Envelope(item.Grab.colliders[0]);
            Physics.SyncTransforms();Assert.That(envelope.Bounds.Intersects(wall.bounds),Is.True);
            Assert.That(clearance.Check(world,item,true,true,out var pending),Is.False);Assert.That(pending,Does.Contain("not ready"));
            item.transform.SetParent(root.transform,false);
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
        }
        [Test]public void UnsupportedConcaveOverlapIsUnavailableButItsEmptyRegionCanBeProven(){
            var item=Item("concave");
            Assert.That(clearance.Check(world,item,true,true,out _),Is.False);
            wall.transform.localPosition=Vector3.right*3;
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
        }
        [Test]public void SaturatedScannedQueryCannotGuessThatTheCandidateIsClear(){
            var item=Item("sphere");
            wall.transform.localPosition=Vector3.right*3;
            BoxCollider NewPeer(int index){
                var peer=new GameObject("Scanned "+index);peer.transform.SetParent(root.transform,false);
                peer.transform.localPosition=new Vector3(.45f+index*.0001f,.45f,0);peer.layer=RoomPhysicsLayers.Scanned;
                var shape=peer.AddComponent<BoxCollider>();shape.size=Vector3.one*.01f;return shape;
            }
            for(int i=0;i<63;i++)NewPeer(i);
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
            var final=NewPeer(63);
            Assert.That(clearance.Check(world,item,true,true,out _),Is.False);
            final.enabled=false;
            Assert.That(clearance.Check(world,item,true,true,out error),Is.True,error);
        }
        [Test]public void MissingGroundAndPolicyAreIndependentOfVisibility(){
            var item=Item("box");item.transform.SetParent(staging.transform,false);
            world.SetSurfaces(false,"Lost tracking");
            Assert.That(clearance.Check(world,item,true,true,out _),Is.False);
            Assert.That(clearance.Check(world,item,true,false,out var error),Is.True,error); // Paused saved edits retain their existing semantics.
            world.SetSurfaces(true,"Aligned");
            Assert.That(clearance.Check(world,item,false,false,out error),Is.True,error);
            Assert.That(clearance.Check(world,item,false,true,out _),Is.False,"Ignoring the scan does not invent virtual terrain");
            Assert.That(clearance.Check(world,item,true,true,out _),Is.False,"Visibility does not opt an actor out of the real room");
        }
        [Test]public void AWallMovedAfterPreparationIsReadAgainAtPublication(){
            var item=Item("box");item.transform.SetParent(staging.transform,false);wall.transform.localPosition=Vector3.right*3;
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
            wall.transform.localPosition=Vector3.zero;
            Assert.That(clearance.Check(world,item,true,true,out _),Is.False);
            Assert.That(item.gameObject.activeInHierarchy,Is.False);
        }
        [Test]public void IntentionallyDisabledChildShapesAreNotPromotedByStagedInspection(){
            var item=Item("box");item.Grab.colliders[0].enabled=false;
            var child=new GameObject("Hidden collision");child.transform.SetParent(item.transform,false);var shape=child.AddComponent<BoxCollider>();item.Grab.colliders.Add(shape);child.SetActive(false);
            item.transform.SetParent(staging.transform,false);
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
            child.SetActive(true);
            Assert.That(clearance.Check(world,item,true,true,out _),Is.False);
        }
        [TestCase("box")][TestCase("sphere")][TestCase("capsule")][TestCase("convex")]
        public void StagedEnvelopeContainsTheActualActiveShapeUnderNestedNonuniformTransforms(string type){
            root.transform.localScale=new Vector3(1.4f,.8f,1.1f);root.transform.localRotation=Quaternion.Euler(0,27,0);
            var item=Item(type);item.transform.localRotation=Quaternion.Euler(13,31,7);item.transform.localScale=new Vector3(2,.7f,1.4f);
            var shape=item.Grab.colliders[0];Physics.SyncTransforms();var before=shape.bounds;
            item.transform.SetParent(staging.transform,false);
            var extent=RoomEnvironmentClearance.Envelope(shape);Assert.That(extent.Known&&extent.HasBounds,Is.True);
            var envelope=extent.Bounds;envelope.Expand(.0001f);
            Assert.That(envelope.Contains(before.min)&&envelope.Contains(before.max),Is.True);
        }
        [TestCase("box")][TestCase("sphere")][TestCase("capsule")][TestCase("convex")]
        public void UnpublishedShapesAreOnlyClearedWhenTheirEnvelopeIsEmpty(string shape){
            var item=Item(shape,true);Assert.That(item.gameObject.activeInHierarchy,Is.False);
            Assert.That(clearance.Check(world,item,true,true,out _),Is.False);
            wall.transform.localPosition=Vector3.right*4;
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
            Assert.That(item.gameObject.activeInHierarchy,Is.False);
        }
    }
}

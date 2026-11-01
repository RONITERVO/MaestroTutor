// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Maestro.Quest.Tests
{
    public sealed class PointerColliderInteractionTests
    {
        GameObject root;
        XRInteractionManager manager;
        RoomPhysicsWorld world;
        [SetUp] public void SetUp(){
            RoomPhysicsLayers.Configure();
            root=new GameObject("Pointer collision tests");root.transform.position=new Vector3(30,10,-20);
            manager=root.AddComponent<XRInteractionManager>();world=root.AddComponent<RoomPhysicsWorld>();
            world.Contains=p=>new Bounds(root.transform.position,Vector3.one*20).Contains(p);
        }
        [TearDown] public void TearDown(){if(root)Object.DestroyImmediate(root);}
        RoomItem Item(Vector3 position,out BoxCollider solid){
            var value=new GameObject("Solid owner");value.transform.SetParent(root.transform,false);value.transform.localPosition=position;
            value.layer=RoomPhysicsLayers.Item;solid=value.AddComponent<BoxCollider>();solid.size=new Vector3(.2f,.4f,.2f);
            var item=value.AddComponent<RoomItem>();item.Configure(new Collider[]{solid});
            // Hold the physics pivot constant while testing query-role bounds;
            // arbitrary-layer pointer colliders can still affect PhysX mass.
            item.GetComponent<Rigidbody>().centerOfMass=Vector3.zero;return item;
        }
        BoxCollider Child(RoomItem item,Vector3 position,Vector3 size){
            var value=new GameObject("Extra query target");value.transform.SetParent(item.transform,false);value.transform.localPosition=position;
            // Deliberately inside the query mask: these tests prove exact-hit
            // filtering, not PhysX contact suppression for arbitrary layers.
            value.layer=RoomPhysicsLayers.Environment;var collider=value.AddComponent<BoxCollider>();collider.size=size;return collider;
        }
        (Bounds Scanned,Vector3 Centre,float Volume,float Radius,float Height,Vector3 Foot,Vector3 Leader,Bounds Contact,Bounds Motion,Bounds Ground) Measure(RoomItem item){
            Physics.SyncTransforms();
            Assert.That(ScannedRoom.Bounds(item,out var scanned),Is.True);
            Assert.That(RoomCollisionVolume.Read(item,.5f,out var volume,out var error),Is.True,error);
            Assert.That(GroundedMovement.Body(item,out var radius,out var height,out var foot,out error),Is.True,error);
            Assert.That(GroundedMovement.LeaderFoot(item,out var leader),Is.True);
            Assert.That(RoomContactSweep.Item(item,new Pose(item.transform.position,item.transform.rotation),item.transform.localScale,out var contact),Is.True);
            Assert.That(RoomPhysicsSweep.Bounds(item,item.GetComponent<Rigidbody>(),.02f,out var motion,out var ground),Is.True);
            return(scanned,volume.LocalCentre,volume.Radius,radius,height,foot,leader,contact,motion,ground);
        }
        [Test] public void PointerTargetsDoNotEnlargePhysicalBoundsOrSweepsWhenHiddenOrDestroyed(){
            var item=Item(Vector3.up,out _);var before=Measure(item);
            var pointer=Child(item,new Vector3(4,0,0),Vector3.one*2);
            Assert.That(item.RegisterPointerSurface(pointer),Is.True);
            Assert.That(Measure(item),Is.EqualTo(before));
            pointer.gameObject.SetActive(false);
            Assert.That(Measure(item),Is.EqualTo(before),"Hidden page targets are still pointer targets");
            Object.DestroyImmediate(pointer.gameObject);
            Assert.That(Measure(item),Is.EqualTo(before),"Destroying a pointer target must not create a missing solid");
        }
        [Test] public void MissingOrDisabledSolidStillRefusesContactAndPhysicsSweeps(){
            var item=Item(Vector3.up,out var solid);var pointer=Child(item,Vector3.right*4,Vector3.one);
            Assert.That(item.RegisterPointerSurface(pointer),Is.True);pointer.gameObject.SetActive(false);
            var pose=new Pose(item.transform.position,item.transform.rotation);var body=item.GetComponent<Rigidbody>();
            solid.enabled=false;
            Assert.That(RoomContactSweep.Item(item,pose,Vector3.one,out _),Is.False);
            Assert.That(RoomPhysicsSweep.Bounds(item,body,.02f,out _,out _),Is.False);
            solid.enabled=true;item.Grab.colliders.Add(null);
            Assert.That(RoomContactSweep.Item(item,pose,Vector3.one,out _),Is.False);
            Assert.That(RoomPhysicsSweep.Bounds(item,body,.02f,out _,out _),Is.False);
            item.Grab.colliders.RemoveAt(item.Grab.colliders.Count-1);Object.DestroyImmediate(solid);
            Assert.That(RoomContactSweep.Item(item,pose,Vector3.one,out _),Is.False);
            Assert.That(RoomPhysicsSweep.Bounds(item,body,.02f,out _,out _),Is.False);
        }
        [Test] public void ScannedClearanceIgnoresPointerPenetrationButRejectsSolidPenetration(){
            var item=Item(Vector3.up,out var solid);
            var wallObject=new GameObject("Accepted scanned wall");wallObject.transform.SetParent(root.transform,false);
            wallObject.transform.localPosition=new Vector3(2,1,0);wallObject.layer=RoomPhysicsLayers.Scanned;
            var wall=wallObject.AddComponent<BoxCollider>();wall.size=Vector3.one*.4f;
            world.SetSurfaces(true,"Synthetic accepted scan and bounds");
            Assert.That(world.RealCollisions,Is.True);
            var pointer=Child(item,Vector3.right*2,Vector3.one*.4f);
            Assert.That(item.RegisterPointerSurface(pointer),Is.True);Physics.SyncTransforms();
            Assert.That(Physics.ComputePenetration(pointer,pointer.transform.position,pointer.transform.rotation,wall,wall.transform.position,wall.transform.rotation,out _,out _),Is.True);
            var clearance=new RoomEnvironmentClearance();
            Assert.That(clearance.Check(world,item,true,true,out var error),Is.True,error);
            solid.center=Vector3.right*2;Physics.SyncTransforms();
            Assert.That(clearance.Check(world,item,true,true,out error),Is.False);
            StringAssert.Contains("clear of scanned surfaces",error);
        }
        [Test] public void NestedIndependentItemCannotSupplyParentsPhysicalShape(){
            var item=Item(Vector3.up,out _);var nested=Item(Vector3.right,out var nestedSolid);
            nested.transform.SetParent(item.transform,false);nested.gameObject.SetActive(false);
            item.Grab.colliders.Add(nestedSolid);world.SetSurfaces(true,"Synthetic accepted scan and bounds");
            Assert.That(new RoomEnvironmentClearance().Check(world,item,true,true,out var error),Is.False);
            StringAssert.Contains("not owned",error);
        }
        [Test] public void PublishedGroundAndObstacleMarkersCannotPromotePointerTargets(){
            var item=Item(Vector3.up,out _);var pointer=Child(item,Vector3.right*2,Vector3.one);
            var ground=pointer.gameObject.AddComponent<RoomWalkableSurface>();ground.Publish(pointer);
            Assert.That(ground.Available,Is.True);Assert.That(RoomGroundQuery.Accepted(pointer,false,out _),Is.True);
            Assert.That(item.RegisterPointerSurface(pointer),Is.True);
            Assert.That(ground.Available,Is.False);Assert.That(RoomGroundQuery.Accepted(pointer,false,out _),Is.False);
            var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);var mesh=temporary.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temporary);
            var obstacleObject=new GameObject("Published query mesh");obstacleObject.transform.SetParent(item.transform,false);
            var collision=obstacleObject.AddComponent<MeshCollider>();collision.sharedMesh=mesh;collision.convex=false;
            var obstacle=obstacleObject.AddComponent<RoomNavigationObstacle>();obstacle.Publish(collision);
            Assert.That(obstacle.Available,Is.True);Assert.That(item.RegisterPointerSurface(collision),Is.True);
            Assert.That(obstacle.Available,Is.False);
        }
        [Test] public void WalkingAndCatchQueriesIgnorePointerHitsAndStillStopAtSolids(){
            var item=Item(new Vector3(5,1,5),out var solid);
            var pointer=Child(item,new Vector3(-5,0,-5),new Vector3(.2f,1,.2f));
            var from=root.transform.position+new Vector3(-1,1,0);var to=from+Vector3.right*2;
            var motor=new RoomGroundMotor();int mask=(1<<RoomPhysicsLayers.Environment)|(1<<RoomPhysicsLayers.Item);
            Physics.SyncTransforms();
            Assert.That(motor.ClearExact(from,to,.1f,1,mask,_=>true),Is.False);
            Assert.That(motor.Blocker,Is.SameAs(pointer));
            var boxMotor=new RoomBoxMotor();var swept=new RoomSweptBody(new Bounds(Vector3.zero,Vector3.one*.2f),Vector3.one,from,Quaternion.identity,to,Quaternion.identity);
            Assert.That(boxMotor.Clear(swept,mask,_=>true),Is.False);
            Assert.That(item.RegisterPointerSurface(pointer),Is.True);Physics.SyncTransforms();
            Assert.That(motor.ClearExact(from,to,.1f,1,mask,_=>true),Is.True);
            Assert.That(boxMotor.Clear(swept,mask,_=>true),Is.True);
            Assert.That(RoomWalkingObstacle.Blocks(pointer,null,null),Is.False);
            var ball=Item(new Vector3(-1,1,0),out _);var holder=Item(new Vector3(1,1,0),out _);
            var viewer=new GameObject("Viewer");viewer.transform.SetParent(root.transform,false);viewer.transform.localPosition=new Vector3(0,4,3);
            var catchClearance=new CatchClearance();
            Assert.That(catchClearance.Segment(ball,holder,viewer.transform,from,to,.1f),Is.True);
            solid.center=new Vector3(-5,0,-5);Physics.SyncTransforms();
            Assert.That(catchClearance.Segment(ball,holder,viewer.transform,from,to,.1f),Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void WorldTransferIgnoresPointerShapesOnMovingAndStationaryOwners(bool movingPointer){
            var content=new GameObject("Moving content");content.transform.SetParent(root.transform,false);
            var moving=Item(movingPointer?new Vector3(5,1,5):Vector3.up,out _);moving.transform.SetParent(content.transform,true);
            var stationary=Item(movingPointer?new Vector3(0,1,1):new Vector3(5,1,5),out _);
            var pointer=Child(movingPointer?moving:stationary,movingPointer?new Vector3(-5,0,-5):new Vector3(-5,0,-4),Vector3.one*.2f);
            var bodies=new Dictionary<Rigidbody,bool>{{moving.GetComponent<Rigidbody>(),false}};
            world.SetSurfaces(true,"Synthetic accepted scan and bounds");world.StartPhysics();Assert.That(world.Running,Is.True);
            var sweep=new RoomWorldSweep();var target=content.transform.position+Vector3.forward*2;Physics.SyncTransforms();
            Assert.That(sweep.CanMove(content.transform,world,bodies,target,Quaternion.identity,out var error),Is.False);
            StringAssert.Contains("clearance",error);
            var owner=movingPointer?moving:stationary;Assert.That(owner.RegisterPointerSurface(pointer),Is.True);
            Physics.SyncTransforms();Assert.That(sweep.CanMove(content.transform,world,bodies,target,Quaternion.identity,out error),Is.True,error);
            Assert.That(owner.UnregisterPointerSurface(pointer),Is.True);Physics.SyncTransforms();
            Assert.That(sweep.CanMove(content.transform,world,bodies,target,Quaternion.identity,out _),Is.False,"An ordinary unregistered collider remains a solid obstacle");
        }
        [Test] public void SavedViewpointPlacementIgnoresPointerTargetButRetainsSolidBlocker(){
            var content=new GameObject("Viewpoint content");content.transform.SetParent(root.transform,false);
            var owner=Item(new Vector3(5,.8f,5),out var solid);owner.transform.SetParent(content.transform,true);
            var pointer=Child(owner,new Vector3(-5,0,-5),new Vector3(.2f,1,.2f));
            var head=new GameObject("Tracked view");head.transform.SetParent(root.transform,false);head.transform.localPosition=new Vector3(0,1.6f,-3);
            var view=root.AddComponent<VirtualRoomView>();view.Initialize(content.transform,root.transform,head.AddComponent<Camera>(),null,world);
            var destination=new RoomViewpoint{active=true,position=Vector3.zero,yaw=0};
            Assert.That(view.CanEnter,Is.True);Assert.That(view.CanPlaceViewpoint(destination,out var error),Is.False);
            StringAssert.Contains("occupied",error);
            Assert.That(owner.RegisterPointerSurface(pointer),Is.True);
            Assert.That(view.CanPlaceViewpoint(destination,out error),Is.True,error);
            solid.center=new Vector3(-5,0,-5);
            Assert.That(view.CanPlaceViewpoint(destination,out error),Is.False);StringAssert.Contains("occupied",error);
        }
        [UnityTest] public IEnumerator RegisteringPointerWhileSelectedRefusesWithoutDroppingTheItem(){
            var item=Item(Vector3.forward,out _);yield return null;
            var hand=new GameObject("Test hand");hand.SetActive(false);hand.transform.SetParent(root.transform,false);hand.transform.position=item.transform.position;
            var ray=hand.AddComponent<XRRayInteractor>();ray.enableUIInteraction=false;ray.interactionManager=manager;
            ray.keepSelectedTargetValid=true;ray.manipulateAttachTransform=false;ray.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;
            ray.selectInput=new XRInputButtonReader{inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};
            hand.SetActive(true);manager.SelectEnter((IXRSelectInteractor)ray,item.Grab);
            Assert.That(item.Grab.isSelected,Is.True);
            var pointer=Child(item,Vector3.right,Vector3.one*.1f);
            Assert.That(item.RegisterPointerSurface(pointer),Is.False);
            Assert.That(item.Grab.isSelected,Is.True);Assert.That(item.Grab.enabled,Is.True);
            Assert.That(item.Grab.colliders,Has.No.Member(pointer));Assert.That(RoomItem.IsPointerOnly(pointer),Is.False);
            ray.selectInput.manualPerformed=false;ray.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)ray,item.Grab);
            Assert.That(item.Grab.isSelected,Is.False);Assert.That(item.RegisterPointerSurface(pointer),Is.True);
            Assert.That(item.Grab.colliders,Has.Member(pointer));
        }
    }
}

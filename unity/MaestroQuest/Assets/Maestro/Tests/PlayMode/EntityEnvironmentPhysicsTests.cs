// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed class EntityEnvironmentPhysicsTests
    {
        GameObject root;RoomPhysicsWorld world;RoomItem virtualBall,realBall;float priorDelta;
        [UnitySetUp]public IEnumerator Setup() {
            priorDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/72;
            root=new GameObject("Entity environment test");root.AddComponent<XRInteractionManager>();RoomPhysicsLayers.Configure();
            world=root.AddComponent<RoomPhysicsWorld>();world.Contains=p=>p.y>=0&&Mathf.Abs(p.x)<4&&Mathf.Abs(p.z)<4;
            Floor("Real floor",-.1f,RoomPhysicsLayers.Scanned,false);Floor("Downhill virtual ground",-2.1f,RoomPhysicsLayers.Environment,true);
            virtualBall=Ball(new Vector3(-1,.8f,0),false);realBall=Ball(new Vector3(1,.8f,0),true);
            world.SetSurfaces(true,"Scan ready");world.StartPhysics();Physics.SyncTransforms();yield return null;
        }
        GameObject Floor(string name,float height,int layer,bool authored) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(0,height,0);go.transform.localScale=new Vector3(8,.2f,8);go.layer=layer;
            if(authored)go.AddComponent<RoomWalkableSurface>().Publish(go.GetComponent<Collider>());return go;
        }
        RoomItem Ball(Vector3 position,bool real) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.transform.SetParent(root.transform,false);go.transform.position=position;go.transform.localScale=Vector3.one*.2f;go.layer=RoomPhysicsLayers.Item;
            var item=go.AddComponent<RoomItem>();item.Configure(new[]{go.GetComponent<Collider>()});var rigid=go.AddComponent<RigidRoomItem>();rigid.Initialize(item);
            go.AddComponent<RoomEnvironmentBinding>().Apply(world,item,real);rigid.Configure(world,ItemPhysics.Solid,.5f);return item;
        }
        [UnityTest]public IEnumerator DifferentActorsLandOnTheirOwnGroundInTheSamePhysicsWorld() {
            for(int i=0;i<180;i++)yield return new WaitForFixedUpdate();
            Assert.That(world.Running,Is.True);Assert.That(realBall.transform.position.y,Is.EqualTo(.1f).Within(.06f));
            Assert.That(virtualBall.transform.position.y,Is.EqualTo(-1.9f).Within(.06f));
            Assert.That(world.CanSimulate(virtualBall.transform.position,virtualBall),Is.True);
            Assert.That(world.CanSimulate(virtualBall.transform.position,realBall),Is.False);
        }
        [UnityTest]public IEnumerator ScanLossSuspendsPhysicalActorsWithoutStoppingVirtualActors() {
            world.SetSurfaces(false,"Scan unavailable");yield return null;
            Assert.That(world.Running,Is.True);Assert.That(realBall.GetComponent<Rigidbody>().isKinematic,Is.True);
            Assert.That(virtualBall.GetComponent<Rigidbody>().isKinematic,Is.False);
            Assert.That(world.EnvironmentReady(virtualBall),Is.True);Assert.That(world.EnvironmentReady(realBall),Is.False);
            for(int i=0;i<130;i++)yield return new WaitForFixedUpdate();
            Assert.That(virtualBall.transform.position.y,Is.LessThan(-1.7f));Assert.That(world.Running,Is.True);
            world.PausePhysics();Assert.That(virtualBall.GetComponent<Rigidbody>().isKinematic,Is.True);
            Assert.That(world.SetRunning(true,out var error),Is.True,error);
        }
        [UnityTest]public IEnumerator NavigationAndCatchQueriesUseTheSelectedActorsProfileBelowTheScan() {
            virtualBall.transform.position=new Vector3(-1,-1,0);virtualBall.GetComponent<Rigidbody>().position=virtualBall.transform.position;virtualBall.GetComponent<RigidRoomItem>().Teleported();
            var nav=root.AddComponent<RoomNavigation>();nav.Initialize(world);
            Assert.That(nav.Prepare(.15f,1.6f,out var error,virtualBall),Is.True,error);
            Assert.That(nav.Sample(new Vector3(0,-2,0),.2f,out var point),Is.True);Assert.That(point.y,Is.EqualTo(-2).Within(.02f));
            var eye=new GameObject("viewer");eye.transform.SetParent(root.transform);eye.transform.position=new Vector3(3,1.6f,3);
            var from=new Vector3(0,.5f,0);var to=new Vector3(0,-.5f,0);var clearance=new CatchClearance();
            Assert.That(clearance.Segment(virtualBall,realBall,eye.transform,from,to,.05f,world),Is.True);
            Assert.That(clearance.Segment(realBall,virtualBall,eye.transform,from,to,.05f,world),Is.False);
            world.SetSurfaces(false,"Scan unavailable");Assert.That(nav.Ready,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator GlobalOffOverridesProfilesAndProfileChangesClearOnlyTargetVelocity() {
            var rigid=virtualBall.GetComponent<RigidRoomItem>();Assert.That(rigid.Launch(Vector3.right,Vector3.zero),Is.True);
            var velocity=virtualBall.GetComponent<Rigidbody>().linearVelocity;
            realBall.GetComponent<RoomEnvironmentBinding>().Apply(world,realBall,false);
            Assert.That(virtualBall.GetComponent<Rigidbody>().linearVelocity,Is.EqualTo(velocity));
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
            realBall.GetComponent<RoomEnvironmentBinding>().Apply(world,realBall,true);
            Assert.That(world.IncludesRealRoom(realBall),Is.False);Assert.That(realBall.GetComponent<Rigidbody>().excludeLayers.value&(1<<RoomPhysicsLayers.Scanned),Is.Not.Zero);
            Assert.That(world.Running,Is.False);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){Object.Destroy(root);Time.captureDeltaTime=priorDelta;yield return null;yield return null;}
    }
}

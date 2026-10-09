// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest.Tests
{
    // One accepted concave mesh deliberately contains several floors, a roof
    // and walls. Separate floor colliders would conceal the interior admission bug.
    public sealed class InteriorGroundTests
    {
        GameObject root,building;
        RoomPhysicsWorld world;
        RoomItem virtualActor,otherActor;
        MeshCollider collision;
        Mesh geometry;
        float priorDelta;
        [UnitySetUp]public IEnumerator Setup()
        {
            priorDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/72;
            RoomPhysicsLayers.Configure();
            root=new GameObject("Interior ground tests");root.AddComponent<XRInteractionManager>();
            world=root.AddComponent<RoomPhysicsWorld>();
            building=new GameObject("One mesh, three floors and roof");building.transform.SetParent(root.transform,false);building.layer=RoomPhysicsLayers.Environment;
            collision=building.AddComponent<MeshCollider>();building.AddComponent<RoomWalkableSurface>();
            Replace(Building());
            virtualActor=Participant("Virtual actor",false);otherActor=Participant("Second actor",false);
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
            Assert.That(world.SetRunning(true,out error),Is.True,error);Physics.SyncTransforms();yield return null;
        }
        RoomItem Participant(string name,bool real)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);var item=go.AddComponent<RoomItem>();
            go.AddComponent<RoomEnvironmentBinding>().Apply(world,item,real);return item;
        }
        static void Box(List<Vector3> vertices,List<int> triangles,Vector3 centre,Vector3 size)
        {
            int start=vertices.Count;var h=size*.5f;
            foreach(var sign in new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)})vertices.Add(centre+Vector3.Scale(sign,h));
            foreach(int i in new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2})triangles.Add(start+i);
        }
        static Mesh Building(bool hole=false,float roof=7,bool walls=true)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(float y in hole?new[]{-2f}:new[]{-2f,1f,4f})
            {
                if(!hole)Box(vertices,triangles,new Vector3(0,y-.05f,0),new Vector3(6,.1f,6));
                else
                {
                    foreach(float x in new[]{-1.75f,1.75f})Box(vertices,triangles,new Vector3(x,y-.05f,0),new Vector3(2.5f,.1f,6));
                    foreach(float z in new[]{-1.75f,1.75f})Box(vertices,triangles,new Vector3(0,y-.05f,z),new Vector3(1,.1f,2.5f));
                }
            }
            Box(vertices,triangles,new Vector3(0,roof-.05f,0),new Vector3(6,.1f,6));
            if(walls)
            {
                // A doorway separates the two halves of the lowest storey.
                foreach(float z in new[]{-1.825f,1.825f})Box(vertices,triangles,new Vector3(1,-.5f,z),new Vector3(.15f,3,2.35f));
                Box(vertices,triangles,new Vector3(1,.65f,0),new Vector3(.15f,.7f,1.3f));
            }
            var mesh=new Mesh{name="Original multi-storey collision fixture"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void Replace(Mesh value)
        {
            var previous=geometry;geometry=value;collision.sharedMesh=value;building.GetComponent<RoomWalkableSurface>().Publish(collision);
            if(previous)Object.Destroy(previous);Physics.SyncTransforms();
        }
        void Admission(Vector3 point,bool expected)
        {
            Assert.That(world.CanSimulate(point,virtualActor),Is.EqualTo(expected),$"live {point}");
            var queries=new RoomEnvironmentQueries();using var batch=queries.Begin(world);
            Assert.That(batch.CanSimulate(point,virtualActor,otherActor),Is.EqualTo(expected),$"batch {point}");
        }
        [Test]public void InteriorFloorsAndRoofAdmitTheirOwnSupportedAirspace()
        {
            foreach(float height in new[]{-1.9f,-1f,.5f,1.1f,2.5f,4.1f,5.5f,7.1f,22.9f})Admission(new Vector3(-1,height,0),true);
            foreach(var point in new[]{new Vector3(-1,-2.3f,0),new Vector3(-1,23.1f,0),new Vector3(3.1f,0,0),new Vector3(float.NaN,0,0)})Admission(point,false);
        }
        [Test]public void ExistingFloorToleranceAndMaximumAirHeightRemainBounded()
        {
            Replace(Building(roof:20,walls:false));
            Admission(new Vector3(-1,-2.24f,0),true);Admission(new Vector3(-1,-2.26f,0),false);
            Admission(new Vector3(-1,19.9f,0),true);Admission(new Vector3(-1,36.1f,0),false);
            // Remove intermediate floors: the high roof cannot provide support below itself.
            Replace(Building(hole:true,roof:20,walls:false));
            Admission(new Vector3(-1,13.9f,0),true);Admission(new Vector3(-1,14.1f,0),false);
        }
        [Test]public void HolesAreNotFilledByTheBoundsOrTheRoofAbove()
        {
            Replace(Building(hole:true,walls:false));
            Admission(new Vector3(0,-1,0),false);Admission(new Vector3(.8f,-1,0),true);
            Admission(new Vector3(0,7.1f,0),true);
        }
        [Test]public void InteriorAdmissionKeepsTheTwoParticipantsRealRoomPolicies()
        {
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],true,out _,out var error),Is.True,error);
            world.Contains=p=>p.y>=0;world.SetSurfaces(true,"Aligned test scan");world.StartPhysics();
            otherActor.GetComponent<RoomEnvironmentBinding>().Apply(world,otherActor,true);
            var point=new Vector3(-1,-1,0);var queries=new RoomEnvironmentQueries();using var batch=queries.Begin(world);
            Assert.That(batch.CanSimulate(point,virtualActor,virtualActor),Is.True);
            Assert.That(batch.CanSimulate(point,virtualActor,otherActor),Is.False);
            Assert.That(batch.CanSimulate(point,otherActor,virtualActor),Is.False);
            world.SetSurfaces(false,"Lost scan");Assert.That(batch.CanSimulate(point,virtualActor,virtualActor),Is.True);
            Assert.That(batch.CanSimulate(new Vector3(-1,2,0),virtualActor,otherActor),Is.False);
            world.PausePhysics();Assert.That(batch.CanSimulate(point,virtualActor,virtualActor),Is.False);
        }
        [Test]public void MovedOrUnavailableBuildingCannotKeepOldSupport()
        {
            Admission(new Vector3(-1,-1,0),true);
            building.transform.SetPositionAndRotation(new Vector3(10,-3,5),Quaternion.Euler(0,45,0));Physics.SyncTransforms();
            Admission(new Vector3(-1,-1,0),false);var moved=building.transform.TransformPoint(new Vector3(-1,-1,0));Admission(moved,true);
            collision.enabled=false;Admission(moved,false);collision.enabled=true;
            building.GetComponent<RoomWalkableSurface>().Publish(null);Admission(moved,false);
            building.GetComponent<RoomWalkableSurface>().Publish(collision);Admission(moved,true);
        }
        [UnityTest]public IEnumerator NavigationFindsInteriorAndUpperFloorWithoutProjectingOntoRoof()
        {
            var nav=root.AddComponent<RoomNavigation>();nav.Initialize(world);
            Assert.That(nav.Prepare(.15f,1.6f,out var error),Is.True,error);
            foreach(float height in new[]{-2f,1f,4f,7f})
            {
                Assert.That(nav.Sample(new Vector3(-1,height,0),.15f,out var floor),Is.True,$"storey {height}");
                Assert.That(floor.y,Is.EqualTo(height).Within(.01f));
            }
            var path=new NavMeshPath();Assert.That(nav.Path(new Vector3(-1,-2,0),new Vector3(2,-2,0),path),Is.True);
            foreach(var corner in path.corners)Assert.That(corner.y,Is.EqualTo(-2).Within(.06f));
            Assert.That(nav.Path(new Vector3(-1,-2,0),new Vector3(-1,1,0),new NavMeshPath()),Is.False,"No stairs connect these storeys");yield return null;
        }
        [Test]public void InteriorWalkingPassesDoorwayButDoesNotWalkThroughSameMeshWalls()
        {
            var ground=new RoomGroundQuery();ground.Capture(root.transform);var motor=new RoomGroundMotor();int mask=1<<RoomPhysicsLayers.Environment;
            bool Permit(Vector3 point)=>world.ContainsSimulation(point+Vector3.up*.1f,virtualActor);
            Assert.That(motor.Travel(ground,new Vector3(0,-2,0),Vector3.right*2,.15f,1.6f,mask,_=>true,Permit,out var destination,out var error),Is.True,error);
            Assert.That(Vector3.Distance(destination,new Vector3(2,-2,0)),Is.LessThan(.001f));
            var start=new Vector3(0,-2,1.5f);
            Assert.That(motor.Travel(ground,start,Vector3.right*2,.15f,1.6f,mask,_=>true,Permit,out destination,out _),Is.False);
            Assert.That(destination,Is.EqualTo(start));Assert.That(motor.Blocker,Is.SameAs(collision));
        }
        RoomItem Ball(Vector3 position)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.transform.SetParent(root.transform,false);go.transform.position=position;go.transform.localScale=Vector3.one*.2f;go.layer=RoomPhysicsLayers.Item;
            var item=go.AddComponent<RoomItem>();item.Configure(new[]{go.GetComponent<Collider>()});go.AddComponent<RoomEnvironmentBinding>().Apply(world,item,false);
            var body=go.AddComponent<RigidRoomItem>();body.Initialize(item);body.Configure(world,ItemPhysics.Solid,.5f);return item;
        }
        [UnityTest]public IEnumerator RealPhysicsDropsOnEachInteriorFloorAndEmitsContacts()
        {
            var lower=Ball(new Vector3(-1,-1.2f,0));var upper=Ball(new Vector3(-1,1.8f,0));int contacts=0;
            lower.GetComponent<RigidRoomItem>().ContactStarted+=(_,other,point,speed)=>{if(other==collision)contacts++;};
            Assert.That(lower.GetComponent<RigidRoomItem>().Simulating,Is.True);Assert.That(upper.GetComponent<RigidRoomItem>().Simulating,Is.True);
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Assert.That(lower.transform.position.y,Is.EqualTo(-1.9f).Within(.04f));Assert.That(upper.transform.position.y,Is.EqualTo(1.1f).Within(.04f));
            Assert.That(contacts,Is.GreaterThan(0));Assert.That(world.CanSimulate(lower.transform.position,lower),Is.True);
        }
        [UnityTearDown]public IEnumerator Cleanup(){Object.Destroy(root);if(geometry)Object.Destroy(geometry);Time.captureDeltaTime=priorDelta;yield return null;yield return null;}
    }
}

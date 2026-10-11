// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Maestro.Quest.Tests {
    public sealed class EnvironmentQueryTests {
        GameObject root;RoomPhysicsWorld world;RoomItem first,second;
        [UnitySetUp]public IEnumerator Setup(){
            root=new GameObject("Ground query benchmark");world=root.AddComponent<RoomPhysicsWorld>();
            for(int i=0;i<8;i++){
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(-6+(i%4)*4,-.1f,-2+(i/4)*4);go.transform.localScale=new Vector3(4,.2f,4);go.layer=RoomPhysicsLayers.Environment;go.AddComponent<RoomWalkableSurface>().Publish(go.GetComponent<Collider>());
            }
            for(int i=0;i<1000;i++){var go=new GameObject("Decorative child");go.transform.SetParent(root.transform,false);}
            first=Participant("First",false);second=Participant("Second",false);
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);world.StartPhysics();Physics.SyncTransforms();yield return null;
        }
        RoomItem Participant(string name,bool real){var go=new GameObject(name);go.transform.SetParent(root.transform,false);var item=go.AddComponent<RoomItem>();go.AddComponent<RoomEnvironmentBinding>().Apply(world,item,real);return item;}
        static Vector3 Point(int i)=>new(-7.75f+(i%32)*.5f,.5f,-3.75f+((i/32)%16)*.5f);
        bool Direct(Vector3 point)=>world.CanSimulate(point,first)&&world.CanSimulate(point,second);
        [Test]public void BatchMatchesLiveAdmissionAndRejectsUnsupportedPoints(){
            var queries=new RoomEnvironmentQueries();using var batch=queries.Begin(world);
            for(int i=0;i<512;i++)Assert.That(batch.CanSimulate(Point(i),first,second),Is.EqualTo(Direct(Point(i))));
            foreach(var point in new[]{new Vector3(30,1,0),new Vector3(0,-1,0),new Vector3(0,17,0),new Vector3(float.NaN,1,0),new Vector3(float.PositiveInfinity,1,0)}){
                Assert.That(Direct(point),Is.False);Assert.That(batch.CanSimulate(point,first,second),Is.False);
            }
        }
        [Test]public void BatchUsesLivePauseScanAndBothParticipantPolicies(){
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],true,out _,out var error),Is.True,error);
            world.Contains=p=>p.y>=0;world.SetSurfaces(true,"Aligned test scan");world.StartPhysics();second.GetComponent<RoomEnvironmentBinding>().Apply(world,second,true);
            var queries=new RoomEnvironmentQueries();using var batch=queries.Begin(world);var point=new Vector3(-6,.5f,-2);
            Assert.That(batch.CanSimulate(point,first,second),Is.True);world.SetSurfaces(false,"Lost scan");Assert.That(world.Running,Is.True);
            Assert.That(batch.CanSimulate(point,first,second),Is.False);Assert.That(batch.CanSimulate(point,first,first),Is.True);
            world.PausePhysics();Assert.That(batch.CanSimulate(point,first,first),Is.False);world.StartPhysics();Assert.That(batch.CanSimulate(point,first,first),Is.True);
            first.GetComponent<RoomEnvironmentBinding>().Apply(world,first,true);Assert.That(batch.CanSimulate(point,first,first),Is.False);
        }
        [Test]public void EquivalentPhysicalPoliciesDoNotRepeatTheScanPredicate(){
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],true,out _,out var error),Is.True,error);world.SetSurfaces(true,"Aligned test scan");world.StartPhysics();
            first.GetComponent<RoomEnvironmentBinding>().Apply(world,first,true);second.GetComponent<RoomEnvironmentBinding>().Apply(world,second,true);
            int checks=0;world.Contains=p=>{checks++;return p.y>=0;};var queries=new RoomEnvironmentQueries();using var batch=queries.Begin(world);
            Assert.That(batch.CanSimulate(Point(0),first,second),Is.True);Assert.That(checks,Is.EqualTo(1));Assert.That(batch.CanSimulate(new Vector3(0,-1,0),first,second),Is.False);Assert.That(checks,Is.EqualTo(2));
        }
        [Test]public void DisposedOrReplacedBatchCannotReadOrInvalidateTheNewBatch(){
            var queries=new RoomEnvironmentQueries();var old=queries.Begin(world);Assert.That(old.CanSimulate(Point(0),first,second),Is.True);
            var current=queries.Begin(world);Assert.That(old.CanSimulate(Point(0),first,second),Is.False);old.Dispose();Assert.That(current.CanSimulate(Point(0),first,second),Is.True);current.Dispose();Assert.That(current.CanSimulate(Point(0),first,second),Is.False);
        }
        [Test]public void FreshBatchObservesMovedDisabledReplacedAndInvalidGround(){
            var queries=new RoomEnvironmentQueries();var surface=root.GetComponentsInChildren<RoomWalkableSurface>()[0];var point=new Vector3(-6,.5f,-2);
            bool Read(){using var batch=queries.Begin(world);bool value=batch.CanSimulate(point,first,second);Assert.That(value,Is.EqualTo(Direct(point)));return value;}
            Assert.That(Read(),Is.True);surface.transform.position+=Vector3.right*30;Physics.SyncTransforms();Assert.That(Read(),Is.False);
            surface.transform.position-=Vector3.right*30;Physics.SyncTransforms();Assert.That(Read(),Is.True);
            surface.enabled=false;Assert.That(Read(),Is.False);surface.enabled=true;Assert.That(Read(),Is.True);
            var collision=surface.Collision;surface.Publish(null);Assert.That(Read(),Is.False);surface.Publish(collision);Assert.That(Read(),Is.True);
            root.transform.rotation=Quaternion.Euler(10,0,0);Physics.SyncTransforms();Assert.That(Read(),Is.False);
            root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one*2;Physics.SyncTransforms();Assert.That(Read(),Is.False);
        }
        JArray Measure(bool batched){
            var queries=new RoomEnvironmentQueries();using(var warm=queries.Begin(world))for(int i=0;i<128;i++){Assert.That(Direct(Point(i)),Is.True);Assert.That(warm.CanSimulate(Point(i),first,second),Is.True);}
            var trials=new JArray();const int count=4096;
            for(int trial=0;trial<4;trial++){
                int accepted=0;var timer=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();timer.Start();
                if(batched){using var batch=queries.Begin(world);for(int i=0;i<count;i++)if(batch.CanSimulate(Point(i),first,second))accepted++;}
                else for(int i=0;i<count;i++)if(Direct(Point(i)))accepted++;
                timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.That(accepted,Is.EqualTo(count));Assert.That(allocated,Is.Zero,"Warmed environmental query loop should allocate no managed memory");
                trials.Add(new JObject{["milliseconds"]=timer.Elapsed.TotalMilliseconds,["managedBytes"]=allocated});
            }
            return trials;
        }
        [UnityTest]public IEnumerator RepeatedMediumAdmissionMeasurement(){
            var result=new JObject{["boundary"]="Desktop Unity Editor admission-query microbenchmark, not a Quest frame-time or whole-app allocation result",["queriesPerTrial"]=4096,["direct"]=Measure(false),["batched"]=Measure(true),["acceptedGrounds"]=8,["decorativeChildren"]=1000};
            string output=Environment.GetEnvironmentVariable("MAESTRO_ENVIRONMENT_QUERY_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,result.ToString());UnityEngine.Debug.Log("MAESTRO_ENVIRONMENT_QUERY_MEASUREMENT "+result.ToString(Newtonsoft.Json.Formatting.None));yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){Object.Destroy(root);yield return null;yield return null;}
    }
}

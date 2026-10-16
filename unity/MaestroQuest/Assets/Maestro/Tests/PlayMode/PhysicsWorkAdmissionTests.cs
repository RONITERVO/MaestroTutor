// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        // Standalone authored hosts exercise the actual process-wide ledger without
        // consuming another editor's saved/native object quota. Always tear down.
        List<GameObject> FillPhysicsWork(int spare=0)
        {
            var hosts=new List<GameObject>();int demand=PhysicsWorkResources.Demand(1);
            try{
                while(PhysicsWorkResources.MaximumUnits-(int)PhysicsWorkResources.ObserveBudget()["units"]>=(spare+1)*demand){
                    var go=new GameObject("Standalone work owner");hosts.Add(go);go.transform.position=new Vector3(40+hosts.Count,2,40);
                    var collider=go.AddComponent<BoxCollider>();collider.size=Vector3.one*.01f;
                    var item=go.AddComponent<RoomItem>();item.Configure(new Collider[]{collider});
                    var rigid=go.AddComponent<RigidRoomItem>();rigid.Initialize(item);rigid.Configure(physics,ItemPhysics.Solid,1);
                    Assert.That(PhysicsWorkResources.Ready(rigid,out var issue),Is.True,issue);
                    Assert.That(hosts.Count,Is.LessThanOrEqualTo(128));
                }
                return hosts;
            }catch{ReleasePhysicsWork(hosts);throw;}
        }
        static void ReleasePhysicsWork(IEnumerable<GameObject> hosts){foreach(var go in hosts)if(go)UnityEngine.Object.DestroyImmediate(go);PhysicsWorkResources.Collect();}
        JObject[] WorkGrants(string target){var budget=PhysicsWorkResources.ObserveBudget();return Enumerable.Range(0,(int)budget["reservations"]).Select(PhysicsWorkResources.Observe).Where(x=>(string)x["target"]==target).ToArray();}
        [UnityTest]public IEnumerator PhysicsWorkAdmissionQueuesACompleteGroupWithoutEvictingUnrelatedPhysicsAndPreservesMotion()
        {
            var ids=PhysicsIslandChain();var free=PhysicsInputBall(new(-12,3,-8));StartIsland();yield return IslandActive(ids);
            var bodies=ids.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>()).ToArray();
            foreach(var body in bodies){body.GetComponent<Rigidbody>().linearVelocity=Vector3.right*3;PhysicsWorkResources.Release(body);}
            var held=FillPhysicsWork(1);
            try{
                string before=PhysicsWorkResources.ObserveBudget().ToString();foreach(var body in bodies)body.Refresh();
                Assert.That((int)PhysicsWorkResources.ObserveBudget()["units"],Is.EqualTo((int)JObject.Parse(before)["units"]));
                foreach(var id in ids){Assert.That(WorkGrants(id),Has.Length.EqualTo(1));Assert.That((bool)WorkGrants(id)[0]["current"],Is.False);Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);StringAssert.Contains("work capacity",editor.Find(id).GetComponent<RigidRoomItem>().PhysicsInputStatus);}
                Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Simulating,Is.True);
                for(int i=0;i<2;i++){UnityEngine.Object.DestroyImmediate(held[0]);held.RemoveAt(0);}
                foreach(var body in bodies)body.Refresh();yield return IslandActive(ids);
                foreach(var body in bodies){Assert.That(PhysicsWorkResources.Ready(body,out var issue),Is.True,issue);Assert.That(body.GetComponent<Rigidbody>().linearVelocity.x,Is.GreaterThan(2));}
            }finally{ReleasePhysicsWork(held);}physics.PausePhysics();
        }
        [UnityTest]public IEnumerator PhysicsWorkAdmissionScheduledWaitPreservesPendingMomentumButPauseCancelsIt()
        {
            var(_,ids)=PendingPhysicsIsland();var bodies=ids.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>()).ToArray();
            foreach(var body in bodies)PhysicsWorkResources.Release(body);var held=FillPhysicsWork(1);
            try{foreach(var body in bodies)body.Refresh();foreach(var id in ids){Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.True);Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);}physics.PausePhysics();}
            finally{ReleasePhysicsWork(held);}yield return NavigationLoadingDone();
            foreach(var body in bodies)Assert.That(body.Simulating,Is.False);
        }
        [UnityTest]public IEnumerator PhysicsWorkAdmissionShapeGrowthCannotStealAnotherGrantAndShrinkingReusesItsIdentity()
        {
            var(_,prop)=MediumFixture(.1f);var item=editor.Find(prop);var rigid=item.GetComponent<RigidRoomItem>();var initial=WorkGrants(prop).Single();var held=FillPhysicsWork();var extra=new List<Collider>();
            try{
                for(int i=0;i<64-1;i++){var c=item.gameObject.AddComponent<BoxCollider>();c.isTrigger=true;item.Grab.colliders.Add(c);extra.Add(c);}
                Assert.That(PhysicsWorkResources.Ready(rigid,out _),Is.False);string budget=PhysicsWorkResources.ObserveBudget().ToString();rigid.Refresh();
                Assert.That(PhysicsWorkResources.ObserveBudget().ToString(),Is.EqualTo(budget));Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);Assert.That((string)WorkGrants(prop).Single()["reservationId"],Is.EqualTo((string)initial["reservationId"]));
                foreach(var c in extra){item.Grab.colliders.Remove(c);UnityEngine.Object.DestroyImmediate(c);}extra.Clear();rigid.Refresh();
                Assert.That(rigid.Simulating,Is.True);Assert.That((string)WorkGrants(prop).Single()["reservationId"],Is.EqualTo((string)initial["reservationId"]));
            }finally{foreach(var c in extra){item.Grab.colliders.Remove(c);UnityEngine.Object.DestroyImmediate(c);}ReleasePhysicsWork(held);}physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator PhysicsWorkAdmissionDeniedBodyCannotSampleOrReceiveWaterForcesAndResumePreservesWater()
        {
            var(pool,prop)=MediumFixture(.1f);var rigid=editor.Find(prop).GetComponent<RigidRoomItem>();PhysicsWorkResources.Release(rigid);var held=FillPhysicsWork();
            try{
                rigid.Refresh();Assert.That(rigid.Simulating,Is.False);editor.Liquids.TickMedium(.02f);
                var denied=editor.Liquids.ObserveMediumBody(prop);Assert.That((bool)denied["applied"],Is.False);Assert.That((double)denied["displacementM3"],Is.Zero);StringAssert.Contains("work capacity",(string)denied["reason"]);
                ReleasePhysicsWork(held);held.Clear();rigid.Refresh();editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.True);
                Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
                physics.PausePhysics();Assert.That(WorkGrants(prop),Is.Empty);editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);Assert.That(rigid.Simulating,Is.False);
            }finally{ReleasePhysicsWork(held);}yield return null;
        }
        [UnityTest]public IEnumerator PhysicsWorkAdmissionReturningCapacityAndPassiveQueriesCannotRestartPausedPhysics()
        {
            var(_,prop)=MediumFixture(.1f);var rigid=editor.Find(prop).GetComponent<RigidRoomItem>();PhysicsWorkResources.Release(rigid);var held=FillPhysicsWork();
            try{rigid.Refresh();Assert.That(rigid.Simulating,Is.False);physics.PausePhysics();ReleasePhysicsWork(held);held.Clear();
                for(int i=0;i<3;i++){PhysicsWorkResources.ObserveBudget();PhysicsWorkResources.Observe(0);rigid.Refresh();yield return new WaitForFixedUpdate();}
                Assert.That(physics.Running,Is.False);Assert.That(rigid.Simulating,Is.False);Assert.That(WorkGrants(prop),Is.Empty);
                physics.StartPhysics();rigid.Refresh();Assert.That(rigid.Simulating,Is.True);rigid.SetAnimationOwner(this,true);Assert.That(WorkGrants(prop),Is.Empty);rigid.SetAnimationOwner(this,false);Assert.That(WorkGrants(prop),Has.Length.EqualTo(1));
                rigid.gameObject.SetActive(false);Assert.That(WorkGrants(prop),Is.Empty);
            }finally{ReleasePhysicsWork(held);}physics.PausePhysics();
        }
        [UnityTest]public IEnumerator PhysicsWorkAdmissionRetiringNativeBodyCannotMoveOrAcquireWorkBeforeObjectDestruction()
        {
            var(_,prop)=MediumFixture(.1f);var item=editor.Find(prop);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            body.linearVelocity=Vector3.right*3;var before=body.position;Assert.That(WorkGrants(prop),Has.Length.EqualTo(1));
            ((Maestro.Quest.Art.INativeResourceOwner)rigid).ReleaseNativeResources();
            Assert.That(body.isKinematic,Is.True);Assert.That(body.useGravity,Is.False);Assert.That(WorkGrants(prop),Is.Empty);
            Assert.That(rigid.CanReceivePhysicsAction(out _),Is.False);Assert.That(rigid.ApplyImpulse(Vector3.up,out _),Is.False);Assert.That(rigid.ClearMotion(out _),Is.False);
            rigid.Refresh();editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);
            yield return new WaitForFixedUpdate();Assert.That(body.position,Is.EqualTo(before));Assert.That(body.isKinematic,Is.True);Assert.That(WorkGrants(prop),Is.Empty);physics.PausePhysics();
        }
        [UnityTest]public IEnumerator PhysicsWorkAdmissionCapturesDetachedCurrentStaleAndReleasedGrants()
        {
            root.AddComponent<RuntimeDiagnostics>();var(_,prop)=MediumFixture(.1f);var item=editor.Find(prop);var rigid=item.GetComponent<RigidRoomItem>();
            var current=new JArray(WorkGrants(prop));Assert.That(current.Count,Is.EqualTo(1));Assert.That((bool)current[0]["current"],Is.True);
            var extra=item.gameObject.AddComponent<BoxCollider>();extra.isTrigger=true;item.Grab.colliders.Add(extra);
            var stale=new JArray(WorkGrants(prop));Assert.That((bool)stale[0]["current"],Is.False);Assert.That((bool)current[0]["current"],Is.True);
            int revision=editor.Revision;var saved=JsonUtility.ToJson(editor.Snapshot());var budget=CollisionFact("runtime.physicsWorkBudget");
            var detached=CollisionFact("runtime.physicsWorkBudget");detached["units"]=0;Assert.That((int)CollisionFact("runtime.physicsWorkBudget")["units"],Is.GreaterThan(0));
            var index=Enumerable.Range(0,(int)budget["reservations"]).First(i=>(string)PhysicsWorkResources.Observe(i)["target"]==prop);
            Assert.That(CollisionFact("runtime.physicsWorkReservation",new JObject{["index"]=index}).ToString(),Is.EqualTo(stale[0].ToString()));
            rigid.Refresh();var resized=new JArray(WorkGrants(prop));Assert.That((bool)resized[0]["current"],Is.True);Assert.That((int)resized[0]["units"],Is.GreaterThan((int)current[0]["units"]));
            Assert.That((string)resized[0]["reservationId"],Is.EqualTo((string)current[0]["reservationId"]));Assert.That((string)resized[0]["worldId"],Is.EqualTo(editor.WorldIdentity.worldId));
            item.Grab.colliders.Remove(extra);UnityEngine.Object.DestroyImmediate(extra);physics.PausePhysics();var released=new JArray(WorkGrants(prop));Assert.That(released,Is.Empty);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            string path=Environment.GetEnvironmentVariable("MAESTRO_PHYSICS_WORK_EVIDENCE");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,new JObject{["budget"]=budget,["current"]=current,["stale"]=stale,["resized"]=resized,["released"]=released}.ToString());yield return null;
        }
    }
}

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
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        // Independent live samplers reserve the actual scene's complete demand.
        // No fake costs, bypass flags or saved/native quota changes are used.
        List<GameObject> FillContactWork(){
            var hosts=new List<GameObject>();int demand=editor.Liquids.ContactWorkDemand;
            try{while((int)SimulationWorkSchedule.ObserveLedger()["committed"]["contactPasses"]<ContactWorkResources.MaximumExecutions&&ContactWorkResources.MaximumUnits-(int)SimulationWorkSchedule.ObserveLedger()["committed"]["contactUnits"]>=demand){
                var go=new GameObject("Independent contact sampler");hosts.Add(go);var sampler=go.AddComponent<LiquidPouring>();sampler.Initialize(editor);sampler.Synchronize(editor.Snapshot());
                Assert.That(ContactWorkResources.TryAdmit(sampler,out var error),Is.True,error);
            }return hosts;}catch{ReleaseContactWork(hosts);throw;}
        }
        static void ReleaseContactWork(IEnumerable<GameObject> hosts){foreach(var go in hosts)if(go)UnityEngine.Object.DestroyImmediate(go);ContactWorkResources.Collect();}
        int CapturedContactSamples(){
            var bodies=typeof(LiquidPouring).GetField("mediumBodies",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor.Liquids);
            var values=(IEnumerable)bodies.GetType().GetProperty("Values").GetValue(bodies);int total=0;
            foreach(var body in values)total+=((MediumDisplacement)body.GetType().GetField("Displacement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(body)).Count;
            return total;
        }
        JObject ContactGrant(){var budget=ContactWorkResources.ObserveBudget();Assert.That((int)budget["reservations"],Is.EqualTo(1));return ContactWorkResources.Observe(0);}
        [UnityTest]public IEnumerator ContactWorkAdmissionRejectsWholePassWithoutCaptureEventsOrSavedChanges(){
            var(pool,prop)=MediumFixture(.1f);var item=editor.Find(prop);item.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
            ContactWorkResources.Release(editor.Liquids);var held=FillContactWork();var events=new List<LiquidContact>();editor.MediumContact+=events.Add;
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot()),budget=ContactWorkResources.ObserveBudget().ToString();
            try{
                Assert.That(CapturedContactSamples(),Is.Zero);ContactPoint(new Vector3(4,1.4f,0));Assert.That(CapturedContactSamples(),Is.Zero,"Refused contacts must not populate the shared displacement cache");Assert.That((bool)editor.Liquids.ObserveContact(prop)["known"],Is.False);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);
                StringAssert.Contains("sampling capacity",(string)editor.Liquids.ObserveContact(prop)["reason"]);Assert.That(events,Is.Empty);Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.Zero);
                Assert.That((double)editor.Liquids.ObserveMediumBody(prop)["displacementM3"],Is.Zero);Assert.That((int)ContactWorkResources.ObserveBudget()["reservations"],Is.EqualTo((int)JObject.Parse(budget)["reservations"]+1));Assert.That((int)ContactWorkResources.ObserveBudget()["units"],Is.EqualTo((int)JObject.Parse(budget)["units"]));Assert.That(ContactWorkResources.Queued(editor.Liquids),Is.True);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));using(var write=editor.WriteGate.TryWrite(out var error))Assert.That(write,Is.Not.Null,error);
            }finally{ReleaseContactWork(held);}physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator ContactWorkAdmissionRetryEstablishesUnknownBaselineWithoutPhantomCrossings(){
            var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};
            ContactPoint(new Vector3(4,2,0));ContactWorkResources.Release(editor.Liquids);var held=FillContactWork();
            try{ContactPoint(new Vector3(4,1,0));Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);Assert.That(events,Is.Empty);}
            finally{ReleaseContactWork(held);}
            ContactPoint(new Vector3(4,1.4f,0));Assert.That((bool)editor.Liquids.ObserveContact("input:left")["immersed"],Is.True);Assert.That(events,Is.Empty,"Resuming inside water is a baseline, not an entry");
            ContactPoint(new Vector3(4,2,0));Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"exited"}));Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator ContactWorkAdmissionGrowthCannotEvictAnotherSamplerOrKeepAContactPrefix(){
            var(_,prop)=MediumFixture(.1f);var item=editor.Find(prop);item.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);ContactTick();var original=ContactGrant();var held=FillContactWork();var extra=new List<Collider>();
            try{
                foreach(var data in editor.Snapshot().objects){if(data.IsBuiltIn)continue;var target=editor.Find(data.id);for(int i=target.Grab.colliders.Count;i<64;i++){var c=target.gameObject.AddComponent<BoxCollider>();c.isTrigger=true;target.Grab.colliders.Add(c);extra.Add(c);}}
                Assert.That(ContactWorkResources.Ready(editor.Liquids,out _),Is.False);string budget=ContactWorkResources.ObserveBudget().ToString();ContactTick();Assert.That(ContactWorkResources.Ready(editor.Liquids,out _),Is.False);Assert.That(ContactWorkResources.ObserveBudget().ToString(),Is.EqualTo(budget));
                Assert.That((bool)editor.Liquids.ObserveContact(prop)["known"],Is.False);Assert.That((bool)ContactWorkResources.Observe(1)["current"],Is.False,"The changed shared geometry invalidates each copied sampler's old demand");
                foreach(var c in extra){c.GetComponent<RoomItem>().Grab.colliders.Remove(c);UnityEngine.Object.DestroyImmediate(c);}extra.Clear();ContactTick();
                Assert.That(ContactWorkResources.Ready(editor.Liquids,out var error),Is.True,error);Assert.That((string)ContactWorkResources.Observe(0)["reservationId"],Is.EqualTo((string)original["reservationId"]));
            }finally{foreach(var c in extra)if(c){c.GetComponent<RoomItem>().Grab.colliders.Remove(c);UnityEngine.Object.DestroyImmediate(c);}ReleaseContactWork(held);}physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator ContactWorkAdmissionPassiveFactsCannotResumePausedPhysicsOrReserveSampling(){
            root.AddComponent<RuntimeDiagnostics>();var(_,prop)=MediumFixture(.1f);ContactTick();Assert.That((int)CollisionFact("runtime.contactWorkBudget")["reservations"],Is.EqualTo(1));
            physics.PausePhysics();Assert.That((int)CollisionFact("runtime.contactWorkBudget")["reservations"],Is.Zero);ContactTick();Assert.That(physics.Running,Is.False);Assert.That((bool)editor.Liquids.ObserveContact(prop)["known"],Is.False);
            physics.StartPhysics();for(int i=0;i<3;i++)Assert.That((int)CollisionFact("runtime.contactWorkBudget")["reservations"],Is.Zero);ContactTick();Assert.That(ContactGrant()["current"].Value<bool>(),Is.True);
            editor.Liquids.SendMessage("OnApplicationFocus",false);Assert.That((int)CollisionFact("runtime.contactWorkBudget")["reservations"],Is.Zero);editor.Liquids.SendMessage("OnApplicationFocus",true);ContactTick();Assert.That((int)ContactWorkResources.ObserveBudget()["reservations"],Is.Zero,"Returning focus cannot resume paused physics");physics.StartPhysics();ContactTick();Assert.That(ContactGrant()["current"].Value<bool>(),Is.True);
            root.SetActive(false);Assert.That((int)ContactWorkResources.ObserveBudget()["reservations"],Is.Zero);ContactTick();Assert.That((int)ContactWorkResources.ObserveBudget()["reservations"],Is.Zero);root.SetActive(true);physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator ContactWorkAdmissionRoomSynchronizationStopsOldCallbackBatchEvenWithSameIds(){
            var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=new List<LiquidContact>();
            editor.Liquids.SetContactInput(0,new Vector3(4,2,0),true);editor.Liquids.SetContactInput(1,new Vector3(4,2,0),true);ContactTick();
            editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);editor.Liquids.Synchronize(editor.Snapshot());};
            editor.Liquids.SetContactInput(0,new Vector3(4,1.4f,0),true);editor.Liquids.SetContactInput(1,new Vector3(4,1.4f,0),true);ContactTick();
            Assert.That(events.Count,Is.EqualTo(1),"Remaining callbacks belong to the previous synchronized source");ContactTick();Assert.That(events.Count,Is.EqualTo(1));physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator ContactWorkAdmissionCapturesDetachedCurrentStaleResizedAndReleasedFacts(){
            root.AddComponent<RuntimeDiagnostics>();var(_,prop)=MediumFixture(.1f);ContactTick();var current=new JArray(ContactGrant());var item=editor.Find(prop);var extra=item.gameObject.AddComponent<BoxCollider>();extra.isTrigger=true;item.Grab.colliders.Add(extra);
            var stale=new JArray(ContactGrant());Assert.That((bool)stale[0]["current"],Is.False);Assert.That((bool)current[0]["current"],Is.True);int revision=editor.Revision;var saved=JsonUtility.ToJson(editor.Snapshot());var budget=CollisionFact("runtime.contactWorkBudget");
            var detached=CollisionFact("runtime.contactWorkBudget");detached["units"]=0;Assert.That((int)CollisionFact("runtime.contactWorkBudget")["units"],Is.GreaterThan(0));Assert.That(CollisionFact("runtime.contactWorkReservation",new JObject{["index"]=0}).ToString(),Is.EqualTo(stale[0].ToString()));
            ContactTick();var resized=new JArray(ContactGrant());Assert.That((bool)resized[0]["current"],Is.True);Assert.That((int)resized[0]["units"],Is.EqualTo((int)current[0]["units"]+64));Assert.That((string)resized[0]["reservationId"],Is.EqualTo((string)current[0]["reservationId"]));
            item.Grab.colliders.Remove(extra);UnityEngine.Object.DestroyImmediate(extra);physics.PausePhysics();var released=new JArray();Assert.That((int)ContactWorkResources.ObserveBudget()["reservations"],Is.Zero);Assert.That(ContactWorkResources.Observe(0),Is.Null);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            string path=Environment.GetEnvironmentVariable("MAESTRO_CONTACT_WORK_EVIDENCE");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,new JObject{["budget"]=budget,["current"]=current,["stale"]=stale,["resized"]=resized,["released"]=released}.ToString());yield return null;
        }
    }
}

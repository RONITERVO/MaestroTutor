// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        JObject CollisionAvailabilityFact(string target) {
            Assert.That(BehaviourCatalog.TryRead("object.collision.availability",1,new JObject{["target"]=target},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));
            var result=JObject.FromObject(value.Value);
            Assert.That(result.Properties().Select(p=>p.Name),Is.EquivalentTo(new[]{"target","state","reason"}));
            Assert.That((string)result["target"],Is.EqualTo(target));
            Assert.That(new[]{"ready","pending","unavailable"},Does.Contain((string)result["state"]));
            Assert.That(((string)result["reason"]).Length,Is.LessThanOrEqualTo(128));
            if((string)result["state"]=="ready")Assert.That((string)result["reason"],Is.Empty);
            else Assert.That((string)result["reason"],Is.Not.Empty);
            Assert.That(JToken.DeepEquals(result,editor.ObserveCollisionAvailability(target)),Is.True,"The shared fact and native observation must expose the same snapshot");
            return result;
        }
        void CollisionAvailabilityIs(string target,string expected) {
            var fact=CollisionAvailabilityFact(target);Assert.That((string)fact["state"],Is.EqualTo(expected),fact.ToString());
        }
        [UnityTest]public IEnumerator CollisionAvailabilityReadySolidsRemainReadableWhilePhysicsAndRestorationAreHeld() {
            string id=editor.Identity(block);physics.PausePhysics();
            var rigid=block.GetComponent<RigidRoomItem>();var binding=block.GetComponent<RoomEnvironmentBinding>();
            Assert.That(rigid.GeometryReady,Is.True);binding.BeginRestoration(true);binding.FinishRestoration();
            Assert.That(binding.RestorationHeld,Is.True);string restoration=binding.RestorationState;
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            var resources=CollisionFact("runtime.collisionResources");var pose=block.transform.localPosition;
            for(int i=0;i<4;i++)CollisionAvailabilityIs(id,"ready");
            // Readiness is physical query geometry, not permission to grab or simulate.
            block.Grab.enabled=false;CollisionAvailabilityIs(id,"ready");block.Grab.enabled=true;
            using(editor.RuntimeGate.Hold("Review saved content"))CollisionAvailabilityIs(id,"ready");
            Assert.That(physics.Running,Is.False);Assert.That(binding.RestorationHeld,Is.True);
            Assert.That(binding.RestorationState,Is.EqualTo(restoration));Assert.That(block.transform.localPosition,Is.EqualTo(pose));
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            Assert.That(JToken.DeepEquals(CollisionFact("runtime.collisionResources"),resources),Is.True,"Passive reads must not reserve geometry");yield return null;
        }
        [UnityTest]public IEnumerator CollisionAvailabilityRequiresEveryRegisteredSolidAndTheLiveOwner() {
            string id=editor.Identity(block);var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
            data.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="left",position=Vector3.left*.1f},new CollisionShape{id="right",position=Vector3.right*.1f}}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            var item=editor.Find(id);var solids=item.Grab.colliders.Where(c=>!c.isTrigger).ToArray();Assert.That(solids.Length,Is.EqualTo(2));
            CollisionAvailabilityIs(id,"ready");int revision=editor.Revision;
            solids[0].enabled=false;Assert.That(solids[1].enabled,Is.True);CollisionAvailabilityIs(id,"unavailable");solids[0].enabled=true;
            solids[0].gameObject.SetActive(false);CollisionAvailabilityIs(id,"unavailable");solids[0].gameObject.SetActive(true);
            int layer=solids[0].gameObject.layer;solids[0].gameObject.layer=RoomPhysicsLayers.Scanned;CollisionAvailabilityIs(id,"unavailable");solids[0].gameObject.layer=layer;
            item.Grab.colliders.Add(null);CollisionAvailabilityIs(id,"unavailable");item.Grab.colliders.RemoveAt(item.Grab.colliders.Count-1);
            var rigid=item.GetComponent<RigidRoomItem>();rigid.SetGeometryReady(false);CollisionAvailabilityIs(id,"unavailable");rigid.SetGeometryReady(true);
            item.enabled=false;CollisionAvailabilityIs(id,"unavailable");item.enabled=true;
            item.gameObject.SetActive(false);CollisionAvailabilityIs(id,"unavailable");item.gameObject.SetActive(true);
            CollisionAvailabilityIs(id,"ready");Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.NativeActivationPending,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator CollisionAvailabilityCompleteEmptySolidSetIsNotMissingGeometry() {
            string id=editor.Identity(block);var handles=block.Grab.colliders.ToArray();
            // Triggers are deliberately outside the solid-query contract, even if
            // disabled or on a non-solid layer. Null registration is not empty.
            foreach(var collider in handles)collider.isTrigger=true;
            handles[0].enabled=false;int layer=handles[0].gameObject.layer;handles[0].gameObject.layer=0;
            CollisionAvailabilityIs(id,"ready");
            block.Grab.enabled=false;block.Grab.colliders.Clear();CollisionAvailabilityIs(id,"ready");
            var extent=editor.ReadCollisionBounds(id);Assert.That(extent.Known,Is.True);Assert.That(extent.HasBounds,Is.False);
            block.Grab.colliders.Add(null);CollisionAvailabilityIs(id,"unavailable");block.Grab.colliders.Clear();
            block.GetComponent<RigidRoomItem>().SetGeometryReady(false);CollisionAvailabilityIs(id,"unavailable");
            block.GetComponent<RigidRoomItem>().SetGeometryReady(true);CollisionAvailabilityIs(id,"ready");
            // Restore the fixture's real registration before its normal disposal.
            handles[0].gameObject.layer=layer;foreach(var collider in handles){collider.isTrigger=false;collider.enabled=true;}
            block.Grab.colliders.AddRange(handles);block.Grab.enabled=true;yield return null;
        }
        [UnityTest]public IEnumerator CollisionAvailabilityDormantReadsDoNotLoadOrRewriteSavedContent() {
            string id=editor.Identity(block),area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            CollisionAvailabilityIs(id,"unavailable");yield return null;
            Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Dormant));
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.True,"Retained geometry is available but is not native readiness");
            editor.SaveNow();int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            string path=Path.Combine(directory,RoomStorage.FileName),bytes=File.ReadAllText(path);
            var resources=CollisionFact("runtime.collisionResources");
            for(int i=0;i<4;i++)CollisionAvailabilityIs(id,"unavailable");
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));Assert.That(JToken.DeepEquals(CollisionFact("runtime.collisionResources"),resources),Is.True);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));CollisionAvailabilityIs(id,"ready");
        }
        [UnityTest]public IEnumerator CollisionAvailabilityPendingBelongsToTheRequestedTargetAndCancellationDoesNotLoadIt() {
            string id=editor.Identity(block),area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var(group,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(group,out error),Is.True,error);yield return null;
            var first=editor.ActivateNativeArea(group);Assert.That(first.IsCompleted,Is.False,"Keep the single preparation lane occupied");
            Assert.That(editor.NativeActivationPending,Is.True);CollisionAvailabilityIs(id,"unavailable");
            using(var demand=editor.DemandNativeEntities(new[]{id},()=>new[]{id},()=>true)) {
                Assert.That(demand.Preparation.IsCompleted,Is.False);Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Dormant),"This target is queued behind the other active preparation");
                CollisionAvailabilityIs(id,"pending");Assert.That(editor.Find(id),Is.Null);
            }
            yield return NativeAreaCompletion(first);
            float deadline=Time.realtimeSinceStartup+12;
            while(editor.NativeContentPending(id)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeContentPending(id),Is.False);CollisionAvailabilityIs(id,"unavailable");Assert.That(editor.Find(id),Is.Null);
            // A single block can finish inside the four-step frame allowance.
            // Reuse the existing drawing group so this accepted activation must
            // cross a frame boundary, independent of CPU speed or prior timing.
            Assert.That(AssignRegion(id,group,out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(group,out error),Is.True,error);yield return null;
            var activation=editor.ActivateNativeArea(group);Assert.That(activation.IsCompleted,Is.False);
            Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Preparing));
            CollisionAvailabilityIs(id,"pending");yield return NativeAreaCompletion(activation);CollisionAvailabilityIs(id,"ready");
            Assert.That(physics.Running,Is.False);Assert.That(ids.All(target=>editor.Find(target)),Is.True);
        }
        [UnityTest]public IEnumerator CollisionAvailabilityImportedLossRejectsStaleShapesEvenWithALoadingFlag() {
            string id=null;yield return ResidentModelAt(new Vector3(7,2,7),value=>id=value);
            var item=editor.Find(id);var model=item.GetComponent<CreatedRoomObject>();var rigid=item.GetComponent<RigidRoomItem>();
            var solid=item.Grab.colliders.First(c=>!c.isTrigger);CollisionAvailabilityIs(id,"ready");
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.True);Assert.That(editor.ResidentCollisionGuard(id),Is.Null);
            int revision=editor.Revision;model.enabled=false;
            Assert.That(rigid.GeometryReady&&solid.enabled&&solid.gameObject.activeInHierarchy,Is.True);
            CollisionAvailabilityIs(id,"unavailable");Assert.That(editor.ResidentCollisionGuard(id),Is.Null,"Reading must not publish a replacement collision guard");
            model.enabled=true;CollisionAvailabilityIs(id,"ready");
            model.Model.Dispose();Assert.That(model.ModelGeometryReady,Is.False);
            // Fault-inject a stale async-loading flag after real accepted geometry
            // loss. That flag must not turn retained failed geometry into a fresh load.
            var loading=typeof(CreatedRoomObject).GetField("modelLoading",BindingFlags.Instance|BindingFlags.NonPublic);Assert.That(loading,Is.Not.Null);
            loading.SetValue(model,true);
            try {
                Assert.That(model.ModelLoading,Is.True);Assert.That(rigid.GeometryReady&&solid.enabled,Is.True);
                CollisionAvailabilityIs(id,"unavailable");Assert.That(editor.ResidentCollisionGuard(id),Is.Null);
            }finally{loading.SetValue(model,false);}
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));yield return null;
        }
        [UnityTest]public IEnumerator CollisionAvailabilityBuiltInsUseRegisteredShapeReadinessWithoutRequiringRigidPhysics() {
            foreach(var id in new[]{"book","maestro"}) {
                var item=editor.Find(id);Assert.That(item.GetComponent<RigidRoomItem>(),Is.Null);
                item.gameObject.layer=RoomPhysicsLayers.Environment;CollisionAvailabilityIs(id,"ready");
            }
            var book=editor.Find("book");book.gameObject.AddComponent<IllustratedBook>().Build();
            // The fixture's RoomItem.Start has already run. Register the newly
            // built pages through the same production path.
            var pages=book.GetComponentsInChildren<BookPageTarget>().Select(p=>p.GetComponent<Collider>()).ToArray();Assert.That(pages.Length,Is.EqualTo(2));
            book.SendMessage("Start");
            Assert.That(pages.All(p=>p.enabled&&!p.isTrigger&&p.gameObject.layer==0),Is.True);
            CollisionAvailabilityIs("book","ready");
            Assert.That(pages.All(RoomItem.IsPointerOnly),Is.True);
            Assert.That(book.SolidColliderCount,Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator CollisionAvailabilityMissingTargetAndPausedAppUseOuterFactUnavailability() {
            string missing=Guid.NewGuid().ToString("N"),id=editor.Identity(block);
            Assert.That(editor.HasSavedObject(missing),Is.False);Assert.That(editor.ObserveCollisionAvailability(missing),Is.Null);
            Assert.That(BehaviourCatalog.TryRead("object.collision.availability",1,new JObject{["target"]=missing},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            var args=new JObject{["target"]=id};Assert.That(runtime.TryReadFact("object.collision.availability",1,args,out _),Is.True);
            runtime.SendMessage("OnApplicationPause",true);
            try{Assert.That(runtime.TryReadFact("object.collision.availability",1,args,out _),Is.False);}
            finally{runtime.SendMessage("OnApplicationPause",false);}
            Assert.That(runtime.TryReadFact("object.collision.availability",1,args,out _),Is.True);
            editor.enabled=false;
            try{Assert.That(BehaviourCatalog.TryRead("object.collision.availability",1,args,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);}
            finally{editor.enabled=true;}
            CollisionAvailabilityIs(id,"ready");yield return null;
        }
    }
}

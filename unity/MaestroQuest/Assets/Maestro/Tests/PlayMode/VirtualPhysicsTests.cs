// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        JObject GroundFact() {
            Assert.That(BehaviourCatalog.TryRead("physics.environment",1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            return JObject.FromObject(value.Value);
        }
        JObject GroundRequest(bool real)=>new() {["operation"]="start",["runId"]=modeActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="physics.environment.set",["version"]=1,["arguments"]=new JObject {["realCollisions"]=real,["stateId"]=GroundFact()["stateId"].DeepClone()}}};
        IEnumerator GroundPolicy(bool real) {
            Assert.That(modeActions.Execute(GroundRequest(real),out var error),Is.True,error);yield return null;
            Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),modeActions.Observe().ToString());
        }
        [UnityTest] public IEnumerator VirtualPhysicsUsesAuthoredGroundWithoutClaimingAnAlignedScan()
        {
            SharedModes(out _,out _);world.SetSurfaces(false,"No scan");
            var ground=Terrain(new Vector3(8,0,0),.2f,out _);Physics.SyncTransforms();
            Assert.That((bool)GroundFact()["realCollisions"],Is.True);Assert.That((bool)SimulationFact()["canStart"],Is.False);
            yield return GroundPolicy(false);
            Assert.That(world.SurfacesReady,Is.False);Assert.That((bool)GroundFact()["scanReady"],Is.False);
            Assert.That((bool)GroundFact()["authoredReady"],Is.True);Assert.That(world.Running,Is.False);
            yield return ChangeSimulation("start");
            Assert.That(world.CanSimulate(new Vector3(8,2,0)),Is.True);
            Assert.That(world.CanSimulate(new Vector3(0,1,0)),Is.False,"An old scan is not virtual ground");
            Assert.That(world.CanSimulate(new Vector3(8,30,0)),Is.False,"The supported region is vertically bounded");
            Assert.That(navigation.Prepare(.25f,1.7f,out var error),Is.True,error);
            Assert.That(navigation.Sample(new Vector3(8,.2f,0),.08f,out _),Is.True);
            Assert.That(navigation.Sample(Vector3.zero,.08f,out _),Is.False,"Real collision off also removes scanned navigation");
            ground.Apply(null);yield return null;yield return null;
            Assert.That(world.Running,Is.False);Assert.That((bool)SimulationFact()["canStart"],Is.False);
        }
        [UnityTest] public IEnumerator VirtualPhysicsActuallyFallsThroughDisabledRealCollisionOntoAuthoredTerrain()
        {
            SharedModes(out _,out _);world.SetSurfaces(false,"No aligned scan");
            Terrain(Vector3.zero,.1f,out _);
            var shelf=Surface(new Vector3(1,.75f,1),new Vector3(1,.1f,1));
            var id=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            Assert.That(editor.SetItemPhysics(id,new ObjectPhysicsSettings {mode="solid",shape="box",mass=.5f}),Is.True);
            var item=editor.Find(id);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            item.transform.position=new Vector3(1,1.5f,1);rigid.Teleported();Physics.SyncTransforms();
            yield return GroundPolicy(false);yield return ChangeSimulation("start");
            yield return new WaitForSeconds(1.2f);
            Assert.That(body.position.y,Is.LessThan(.65f),"The scanned shelf must not collide when real collisions are off");
            Assert.That(body.position.y,Is.GreaterThan(.1f),"Accepted virtual ground still collides");
            Assert.That(shelf.GetComponent<Collider>().enabled,Is.True,"The original scan stays available to scan queries and acoustics");
            world.SetSurfaces(true,"Aligned scan");yield return GroundPolicy(true);
            Assert.That(world.Running,Is.False);item.transform.position=new Vector3(1,1.5f,1);rigid.Teleported();Physics.SyncTransforms();
            yield return ChangeSimulation("start");yield return new WaitForSeconds(.7f);
            Assert.That(body.position.y,Is.GreaterThan(.8f),"Real collisions must return through the same body");
        }
        [UnityTest] public IEnumerator VirtualPhysicsPolicyRejectsStaleIntentAndDoesNotChangeTheViewOrRestart()
        {
            SharedModes(out var view,out _);Terrain(Vector3.zero,.1f,out _);var stale=GroundRequest(false);
            world.PausePhysics();Assert.That(modeActions.Execute(stale,out _),Is.False);
            yield return GroundPolicy(false);Assert.That(view.Active,Is.False,"Collision policy is independent of passthrough");
            yield return ChangeSimulation("start");var before=SimulationFact();
            yield return GroundPolicy(false);Assert.That(JToken.DeepEquals(before,SimulationFact()),Is.True,"An already-satisfied policy must preserve active motion");
            world.SetSurfaces(false,"Tracking lost");Assert.That(world.Running,Is.True,"Unused scan readiness must not stop authored-ground physics");
            world.SendMessage("OnApplicationFocus",false);world.SendMessage("OnApplicationFocus",true);
            Assert.That(world.Running,Is.False);yield return ChangeSimulation("start");
            yield return GroundPolicy(true);Assert.That(world.Running,Is.False);Assert.That((bool)SimulationFact()["canStart"],Is.False);
        }
        [UnityTest] public IEnumerator VirtualPhysicsLeavesUnsupportedBodiesDormantWhileSupportedBodiesFall()
        {
            SharedModes(out _,out _);Terrain(Vector3.zero,.1f,out _);
            var id=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            Assert.That(editor.SetItemPhysics(id,new ObjectPhysicsSettings {mode="solid",shape="box",mass=.5f}),Is.True);
            var item=editor.Find(id);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            item.transform.position=new Vector3(8,2,0);rigid.Teleported();Physics.SyncTransforms();
            yield return GroundPolicy(false);yield return ChangeSimulation("start");yield return new WaitForSeconds(.2f);
            Assert.That(world.Running,Is.True,"An unsupported dormant body must not stop every other region");
            Assert.That(body.isKinematic,Is.True);Assert.That(body.position.y,Is.EqualTo(2).Within(.001f));
            item.transform.position=new Vector3(0,1.5f,0);rigid.Teleported();Physics.SyncTransforms();yield return new WaitForSeconds(.15f);
            Assert.That(world.Running,Is.True);Assert.That(body.position.y,Is.LessThan(1.45f));
            // Leaving admitted ground while actually simulating still recovers and pauses.
            item.transform.position=new Vector3(8,body.position.y,0);Physics.SyncTransforms();rigid.SendMessage("LateUpdate");yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(world.Running,Is.False);Assert.That(body.position.x,Is.LessThan(2));
        }
        [UnityTest] public IEnumerator VirtualPhysicsGroundLossAndRestoreRequiresAFreshExplicitStart()
        {
            SharedModes(out _,out _);var ground=Terrain(Vector3.zero,.1f,out _);yield return GroundPolicy(false);yield return ChangeSimulation("start");
            var stale=SimulationRequest("start");ground.Collision.enabled=false;yield return null;yield return null;
            Assert.That(world.Running,Is.False);ground.Collision.enabled=true;yield return null;yield return null;
            Assert.That((bool)SimulationFact()["canStart"],Is.True);Assert.That(world.Running,Is.False);
            Assert.That(modeActions.Execute(stale,out _),Is.False);yield return ChangeSimulation("start");
        }
    }
}

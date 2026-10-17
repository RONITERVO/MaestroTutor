// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (string pool,string prop) MediumFixture(float mass){
            editor.Liquids.enabled=false;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Medium vessel",new Vector3(4,1,0),1,Color.white,out var pool,out var e),Is.True,e);
            var c=new RoomContainer{version=2,rectangle=new(){width=1,depth=1},frame=new(){position=new Vector3(0,.2f,0)},height=.6f,capacityMl=600000,amountMl=400000};
            ContainerRun(new RoomAgentExecutor(editor),ContainerCall(pool,c));
            Assert.That(editor.SetItemPhysics(pool,new ObjectPhysicsSettings{mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Floating prop",new Vector3(4,1.4f,0),.5f,Color.white,out var prop,out e),Is.True,e);
            Assert.That(editor.SetItemPhysics(prop,new ObjectPhysicsSettings{mode="solid",shape="box",mass=mass}),Is.True,editor.Status);
            physics.SetSurfaces(true,"Synthetic aligned room");physics.StartPhysics();Physics.SyncTransforms();return(pool,prop);
        }
        [UnityTest]public IEnumerator MediumQueriesUseLiveRainQuantitiesAndTheSavedFluidProperties(){
            var(pool,prop)=MediumFixture(.05f);var a=editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),prop);
            Assert.That((bool)a["found"],Is.True,a.ToString());Assert.That((bool)a["active"],Is.True);Assert.That((double)a["depthMetres"],Is.EqualTo(.2).Within(.001));Assert.That((double)a["densityKgM3"],Is.EqualTo(1000));
            Assert.That(MediumFacts.World().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["position"]=new JObject{["x"]=4,["y"]=1.4,["z"]=0},["target"]=prop},out _),Is.True);
            Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=120},0,out var e),Is.True,e);editor.Liquids.Tick(.1f);
            var current=editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),prop);Assert.That((double)current["depthMetres"],Is.GreaterThan((double)a["depthMetres"]));Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
            physics.PausePhysics();Assert.That((bool)editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),prop)["active"],Is.False);Assert.That(editor.Read(pool).containers[0].amountMl,Is.GreaterThan(400000));yield return null;
        }
        [UnityTest]public IEnumerator MediumBuoyancyAndDragMoveFreePropsWithoutChangingLiquidInventory(){
            var(pool,prop)=MediumFixture(.05f);var body=editor.Find(prop).GetComponent<Rigidbody>();var initial=body.position;
            body.linearVelocity=Vector3.right;editor.Liquids.TickMedium(Time.fixedDeltaTime);var response=editor.Liquids.ObserveMediumBody(prop);Assert.That((bool)response["applied"],Is.True,response.ToString());yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x,Is.LessThan(1));Assert.That(body.position.y,Is.GreaterThan(initial.y));Assert.That((double)response["displacementM3"],Is.GreaterThan(0));
            body.linearVelocity=new Vector3(0,body.linearVelocity.y,0);
            for(int i=0;i<180;i++){editor.Liquids.TickMedium(Time.fixedDeltaTime);yield return new WaitForFixedUpdate();}
            Assert.That(body.position.y,Is.InRange(1.53f,1.71f),"The lightweight prop should stay near the measured surface after its initial rise");Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
            physics.PausePhysics();Assert.That(editor.SetItemPhysics(prop,new ObjectPhysicsSettings{mode="solid",shape="box",mass=20}),Is.True,editor.Status);editor.Find(prop).transform.position=initial;editor.Find(prop).GetComponent<RigidRoomItem>().Teleported();physics.StartPhysics();Physics.SyncTransforms();
            for(int i=0;i<5;i++){editor.Liquids.TickMedium(Time.fixedDeltaTime);yield return new WaitForFixedUpdate();}Assert.That(body.position.y,Is.LessThan(initial.y));physics.PausePhysics();
        }
        [UnityTest]public IEnumerator MediumNeverOverridesPauseAnimationOwnershipOrMissingScan(){
            var(_,prop)=MediumFixture(.05f);var rigid=editor.Find(prop).GetComponent<RigidRoomItem>();var body=editor.Find(prop).GetComponent<Rigidbody>();var owner=new object();rigid.SetAnimationOwner(owner,true);editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);Assert.That(body.isKinematic,Is.True);
            rigid.SetAnimationOwner(owner,false);editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.True);
            physics.SetSurfaces(false,"Scan unavailable");editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);Assert.That((bool)editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),prop)["found"],Is.True);Assert.That((bool)editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),prop)["active"],Is.False);yield return null;
        }
        [UnityTest]public IEnumerator MediumBelowTheScanUsesBothParticipantsEnvironmentProfiles(){
            var(pool,prop)=MediumFixture(.05f);physics.PausePhysics();physics.Contains=p=>p.y>=0;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(root.transform,false);ground.transform.position=new Vector3(4,-2.1f,0);ground.transform.localScale=new Vector3(4,.2f,4);ground.layer=RoomPhysicsLayers.Environment;ground.AddComponent<RoomWalkableSurface>().Publish(ground.GetComponent<Collider>());
            string profile=NewEnvironment();Assert.That(AssignEnvironment(pool,profile,out var e),Is.True,e);Assert.That(AssignEnvironment(prop,profile,out e),Is.True,e);
            foreach(string id in new[]{pool,prop}){var item=editor.Find(id);item.transform.position+=Vector3.down*2.5f;item.GetComponent<RigidRoomItem>().Teleported();}
            Physics.SyncTransforms();physics.SetSurfaces(false,"Scan unavailable");physics.StartPhysics();var point=new Vector3(4,-1.1f,0);
            var virtualResponse=editor.Liquids.ObserveMedium(point,prop);Assert.That((bool)virtualResponse["found"],Is.True);Assert.That((bool)virtualResponse["active"],Is.True,virtualResponse.ToString());
            Assert.That((bool)editor.Liquids.ObserveMedium(point,"maestro")["active"],Is.False,"A real-room actor must not borrow the virtual participant's readiness");
            editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.True);
            physics.PausePhysics();Assert.That(AssignEnvironment(pool,"",out e),Is.True,e);physics.StartPhysics();editor.Liquids.TickMedium(.02f);
            Assert.That((bool)editor.Liquids.ObserveMedium(point,prop)["active"],Is.False,"The medium owner's ground policy also applies");Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);yield return null;
        }
        [UnityTest]public IEnumerator MediumCachedResponseBecomesInactiveImmediatelyDuringAWorkspaceHold(){
            var(_,prop)=MediumFixture(.05f);editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.True);
            using(var hold=editor.WriteGate.TryFreeze(out var e)){Assert.That(hold,Is.Not.Null,e);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);}
            editor.Ownership.Suspend(true);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);editor.Ownership.Suspend(false);yield return null;
        }
        [UnityTest]public IEnumerator MediumFluidEditsShareNativeReceiptsUndoAndTransferCompatibility(){
            var(pool,prop)=MediumFixture(.05f);physics.PausePhysics();var ex=new RoomAgentExecutor(editor);var c=editor.Read(pool).containers[0];c.fluid.densityKgM3=800;c.fluid.linearDrag=4;
            ContainerRun(ex,ContainerCall(pool,c));Assert.That((double)editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),prop)["densityKgM3"],Is.EqualTo(800));editor.Undo();Assert.That((double)editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),prop)["densityKgM3"],Is.EqualTo(1000));editor.Redo();Assert.That(editor.Read(pool).containers[0].fluid.linearDrag,Is.EqualTo(4));yield return null;
        }
    }
}

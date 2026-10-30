// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        JObject HabitatAt(string target,string habitat,Vector3 root)=>editor.ObserveHabitatPath(target,habitat,root);
        [UnityTest]public IEnumerator HabitatPathSharesWholeBodyAndLiveQuantityWithoutStartingPhysics(){
            var(pool,prop)=MediumFixture(.1f);physics.PausePhysics();var item=editor.Find(prop);var initial=item.transform.position;
            var value=HabitatAt(prop,pool,initial);Assert.That((bool)value["contained"],Is.True,value.ToString());
            Assert.That(WaterHabitatFacts.Path().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,
                new JObject{["target"]=prop,["habitat"]=pool,["destination"]=new JObject{["x"]=initial.x,["y"]=initial.y,["z"]=initial.z}},out _),Is.True);
            float high=1.6f-item.Grab.colliders[0].bounds.extents.y*.5f;
            value=HabitatAt(prop,pool,new(4,high,0));Assert.That((bool)value["contained"],Is.False,value.ToString());Assert.That((string)value["reason"],Does.Contain("surface"));
            value=HabitatAt(prop,pool,new(4.6f,1.4f,0));Assert.That((bool)value["contained"],Is.False);
            var c=editor.Read(pool).containers[0];c.amountMl=0;yield return ContainerAction(new RoomAgentExecutor(editor),ContainerCall(pool,c));
            value=HabitatAt(prop,pool,initial);Assert.That((bool)value["contained"],Is.False);Assert.That((string)value["reason"],Does.Contain("no liquid"));
            Assert.That(physics.Running,Is.False);Assert.That(item.transform.position,Is.EqualTo(initial));Assert.That(editor.Read(pool).containers[0].amountMl,Is.Zero);
        }
        [UnityTest]public IEnumerator HabitatPathRequiresTheChosenDistantMissingVesselAndNeverBorrowsAnother(){
            var(pool,prop)=MediumFixture(.1f);physics.PausePhysics();var initial=editor.Find(prop).transform.position;
            Assert.That((bool)HabitatAt(prop,pool,initial)["contained"],Is.True);
            string missing=MissingMedium(new(12,1.3f,0));var value=HabitatAt(prop,missing,initial);
            Assert.That((string)value["bodyId"],Is.EqualTo(missing));Assert.That((bool)value["contained"],Is.False);
            Assert.That((string)value["availability"],Is.EqualTo("unavailable"));Assert.That((int)value["dependencyCount"],Is.EqualTo(1));
            Assert.That((string)value["dependencies"][0]["target"],Is.EqualTo(missing));
            Assert.That(editor.Find(missing).gameObject.activeInHierarchy,Is.False);Assert.That(physics.Running,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator HabitatPathUsesAcceptedTerrainAcrossTheWholePathAndRefusesMissingBed(){
            var(ex,pool)=TerrainReservoir();physics.PausePhysics();var bed=editor.Read(pool).heightFields[0];
            for(int z=0;z<=bed.cells;z++)for(int x=0;x<=bed.cells;x++)bed.heights[z*(bed.cells+1)+x]=x==bed.cells/2?.4f:.05f;
            Assert.That(editor.EditHeightField(pool,editor.ObjectRevision(pool),bed,out var error),Is.True,error);
            var water=editor.Read(pool).containers[0];water.amountMl=water.capacityMl;yield return ContainerAction(ex,ContainerCall(pool,water));
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Aquatic actor",TerrainPoint(pool,-.75f,.2f),.1f,Color.white,out var actor,out error),Is.True,error);
            Assert.That(editor.SetItemPhysics(actor,new(){mode="fixed",shape="box",mass=1}),Is.True);
            Physics.SyncTransforms();
            var from=editor.Frame.PointToRoom(editor.Find(actor).transform.position);
            var to=editor.Frame.PointToRoom(TerrainPoint(pool,.75f,.2f));
            Assert.That((bool)HabitatAt(actor,pool,from)["contained"],Is.True);
            var value=HabitatAt(actor,pool,to);Assert.That((bool)value["contained"],Is.False,value.ToString());Assert.That((string)value["reason"],Does.Contain("terrain"));
            Assert.That(editor.MoveObject(actor,TerrainPoint(pool,-.75f,.46f),out error),Is.True,error);
            to=editor.Frame.PointToRoom(TerrainPoint(pool,.75f,.46f));
            value=HabitatAt(actor,pool,to);Assert.That((bool)value["contained"],Is.True,value.ToString());
            var view=editor.Find(pool).GetComponent<HeightFieldView>();view.Collision.enabled=false;
            value=HabitatAt(actor,pool,to);Assert.That((bool)value["contained"],Is.False);
            Assert.That((string)value["availability"],Is.EqualTo("unavailable"));Assert.That((int)value["dependencyCount"],Is.EqualTo(1));
            Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(water.amountMl));Assert.That(physics.Running,Is.False);view.Collision.enabled=true;
        }
        [UnityTest]public IEnumerator HabitatPathPreservesTheSharedBoundedMissingMediumContract(){
            var(pool,prop)=MediumFixture(.1f);physics.PausePhysics();var initial=editor.Find(prop).transform.position;
            for(int i=0;i<5;i++)MissingMedium(new(4,1.3f,0));
            var value=HabitatAt(prop,pool,initial);Assert.That((bool)value["contained"],Is.False);
            Assert.That((int)value["dependencyCount"],Is.EqualTo(5));Assert.That(value["dependencies"].Count(),Is.EqualTo(4));
            Assert.That(WaterHabitatFacts.Path().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,
                new JObject{["target"]=prop,["habitat"]=pool,["destination"]=new JObject{["x"]=initial.x,["y"]=initial.y,["z"]=initial.z}},out _),Is.True);
            Assert.That(physics.Running,Is.False);yield return null;
        }
    }
}

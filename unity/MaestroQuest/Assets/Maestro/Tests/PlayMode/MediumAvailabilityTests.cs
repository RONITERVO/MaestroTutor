// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        JObject MediumAt(Vector3 point)=>editor.Liquids.ObserveMedium(point,"");
        string MissingMedium(Vector3 position,float amount=800){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Unavailable water",position,1,Color.white,out var id,out var error),Is.True,error);
            ContainerRun(new RoomAgentExecutor(editor),ContainerCall(id,new RoomContainer{radius=.2f,height=.3f,capacityMl=1000,amountMl=amount}));
            editor.Find(id).gameObject.SetActive(false);return id;
        }
        [UnityTest]public IEnumerator MediumAvailabilityDormantCavityCannotBecomeDryAndReadsArePassive(){
            var(pool,prop)=MediumFixture(.1f);physics.PausePhysics();string area=NativeAreaFor(pool);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            editor.SaveNow();var saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;string file=Path.Combine(directory,RoomStorage.FileName),raw=File.ReadAllText(file);
            var value=MediumAt(new(4,1.4f,0));Assert.That((string)value["availability"],Is.EqualTo("unavailable"));Assert.That((bool)value["found"],Is.False);Assert.That((int)value["dependencyCount"],Is.EqualTo(1));Assert.That((string)value["dependencies"][0]["target"],Is.EqualTo(pool));
            Assert.That(editor.Liquids.CheckTraversal(editor.Find("maestro"),new(3,1.4f,0),new(5,1.4f,0),.1f,1.7f,out var hit),Is.False);
            Assert.That(hit.BodyId,Is.Empty,"An absent cavity is not an actual blocking-water contact");Assert.That(hit.Availability.State,Is.EqualTo("unavailable"));
            editor.Find("maestro").transform.position=new(3,1.4f,0);
            Assert.That(WaterTraversalCapability.Path().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]="maestro",["position"]=new JObject{["x"]=5,["y"]=1.4,["z"]=0}},out _),Is.True);
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(pool),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(File.ReadAllText(file),Is.EqualTo(raw));
        }
        [UnityTest]public IEnumerator MediumAvailabilityTracksExplicitPreparationWithoutChangingWaterOrStartingPhysics(){
            var(pool,_)=MediumFixture(.1f);physics.PausePhysics();Assert.That(editor.RetireNativeArea(NativeAreaFor(pool),out var error),Is.True,error);yield return null;
            var(group,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(group,out error),Is.True,error);yield return null;
            var first=editor.ActivateNativeArea(group);Assert.That(first.IsCompleted,Is.False,"Keep the single preparation lane occupied");
            using var demand=editor.DemandNativeEntities(new[]{pool},()=>new[]{pool},()=>true);
            var value=MediumAt(new(4,1.4f,0));Assert.That((string)value["availability"],Is.EqualTo("pending"));Assert.That((bool)value["found"],Is.False);
            float deadline=Time.realtimeSinceStartup+12;while(!demand.Preparation.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(demand.Preparation.IsCompleted,Is.True);Assert.That(demand.Error,Is.Null);Assert.That(first.IsCompleted,Is.True);Assert.That(first.Result,Is.Null);
            value=MediumAt(new(4,1.4f,0));Assert.That((string)value["availability"],Is.EqualTo("ready"));Assert.That((bool)value["found"],Is.True);Assert.That((bool)value["active"],Is.False);Assert.That(physics.Running,Is.False);Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
        }
        [UnityTest]public IEnumerator MediumAvailabilityUsesCavityFrameAndRetainedPoseRatherThanArtwork(){
            var(pool,_)=MediumFixture(.1f);physics.PausePhysics();var c=editor.Read(pool).containers[0];c.frame.position=new(3,.2f,0);c.rectangle.width=2;ContainerRun(new RoomAgentExecutor(editor),ContainerCall(pool,c));
            string area=NativeAreaFor(pool);editor.Find(pool).transform.position=new(8,1,0);editor.Find(pool).transform.rotation=Quaternion.Euler(0,90,0);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            Assert.That((string)MediumAt(new(8,1.4f,-3))["availability"],Is.EqualTo("unavailable"));Assert.That((string)MediumAt(new(7,1.4f,0))["availability"],Is.EqualTo("ready"),"The obsolete saved placement is not the retained idle pose");
            root.transform.SetPositionAndRotation(new(3,2,5),Quaternion.Euler(0,30,0));root.transform.localScale=Vector3.one*1.5f;
            Assert.That((string)MediumAt(new(8,1.4f,-3))["availability"],Is.EqualTo("unavailable"),"Authored queries and native cavities share the workspace transform");
        }
        [UnityTest]public IEnumerator MediumAvailabilityExcludesDistantAndEmptyVesselsButBoundsTheSharedFact(){
            var(_,prop)=MediumFixture(.1f);physics.PausePhysics();var point=new Vector3(4,1.4f,0);
            MissingMedium(new(12,1.3f,0));MissingMedium(new(4,1.3f,0),0);Assert.That((string)MediumAt(point)["availability"],Is.EqualTo("ready"));
            var ids=new List<string>();for(int i=0;i<5;i++)ids.Add(MissingMedium(new(4,1.3f,0)));
            var value=MediumAt(point);Assert.That((bool)value["found"],Is.False);Assert.That((int)value["dependencyCount"],Is.EqualTo(5));Assert.That(value["dependencies"].Count(),Is.EqualTo(4));Assert.That(value["dependencies"].Select(x=>(string)x["target"]),Is.EqualTo(ids.OrderBy(x=>x,StringComparer.Ordinal).Take(4)));
            Assert.That(MediumFacts.World().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["position"]=new JObject{["x"]=4,["y"]=1.4,["z"]=0},["target"]=prop},out _),Is.True,"The actual multi-dependency result must fit the shared program value budget");
            editor.Find("maestro").WaterTraversal=new(){mode="ignore"};Assert.That(editor.Liquids.CheckTraversal(editor.Find("maestro"),new(3,1.4f,0),new(5,1.4f,0),.1f,1.7f,out _),Is.True,"Explicit non-walking policy remains intentional");yield return null;
        }
        [UnityTest]public IEnumerator MediumAvailabilityMissingInnerWaterCannotBorrowOuterForcesOrContactEdges(){
            var(outer,prop)=MediumFixture(.1f);physics.PausePhysics();string inner=MissingMedium(new(4,1.3f,0));var innerItem=editor.Find(inner);innerItem.gameObject.SetActive(true);Assert.That(editor.SetItemPhysics(inner,new ObjectPhysicsSettings{mode="fixed",shape="box",mass=1}),Is.True);physics.StartPhysics();
            var events=new List<LiquidContact>();editor.MediumContact+=events.Add;var point=new Vector3(4,1.4f,0);ContactPoint(point);Assert.That((string)editor.Liquids.ObserveContact("input:left")["bodyId"],Is.EqualTo(inner));events.Clear();
            editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.True);
            innerItem.gameObject.SetActive(false);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);var value=MediumAt(point);Assert.That((bool)value["found"],Is.False);Assert.That((string)value["availability"],Is.EqualTo("unavailable"));
            editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False,"No partial outer-water force while another cavity is missing");
            ContactPoint(point);ContactPoint(new(4,2,0));Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.True);Assert.That(events.Where(x=>x.Participant=="input:left"),Is.Empty,"Losing a cavity cannot manufacture exit/crossing events");
            innerItem.gameObject.SetActive(true);ContactPoint(point);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["immersed"],Is.True);Assert.That(editor.Read(outer).containers[0].amountMl,Is.EqualTo(400000));yield return null;
        }
        [UnityTest]public IEnumerator MediumAvailabilityInvalidatesCachedOuterContactWhenAMissingCavityMovesIntoIt(){
            var(outer,_)=MediumFixture(.1f);physics.PausePhysics();string inner=MissingMedium(new(8,1.3f,0));physics.StartPhysics();
            var point=new Vector3(4,1.4f,0);ContactPoint(point);Assert.That((string)editor.Liquids.ObserveContact("input:left")["bodyId"],Is.EqualTo(outer));
            editor.Find(inner).transform.position=new(4,1.3f,0);
            Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False,"The previous outer-water contact cannot prove a newly missing inner cavity safe");yield return null;
        }
        [UnityTest]public IEnumerator MediumAvailabilityDoesNotMistakeUnsavedRainForAnEmptyInactiveVessel(){
            var(pool,prop)=MediumFixture(.1f);physics.PausePhysics();var c=editor.Read(pool).containers[0];c.amountMl=0;ContainerRun(new RoomAgentExecutor(editor),ContainerCall(pool,c));
            Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=120},0,out var error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.1f);
            Assert.That(editor.Read(pool).containers[0].amountMl,Is.Zero);editor.Find(pool).gameObject.SetActive(false);
            var value=MediumAt(new(4,1.2001f,0));Assert.That((string)value["availability"],Is.EqualTo("unavailable"));Assert.That((int)value["dependencyCount"],Is.EqualTo(1));Assert.That(editor.Read(pool).containers[0].amountMl,Is.Zero);
            editor.Liquids.TickMedium(.02f);Assert.That((string)editor.Liquids.ObserveMediumBody(prop)["reason"],Does.Contain("Liquid geometry is unavailable"));yield return null;
        }
    }
}

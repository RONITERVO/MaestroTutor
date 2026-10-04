// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        void LiquidPose(string id,Vector3 position,Quaternion rotation){var item=editor.Find(id);item.transform.SetPositionAndRotation(position,rotation);item.GetComponent<RigidRoomItem>().Teleported();}
        void DrainLiquid(string id){
            LiquidPose(id,new Vector3(5,2,0),Quaternion.Euler(0,0,180));
            for(int i=0;i<40&&Liquid(id)>0;i++)editor.Liquids.Tick(.05f);
            Assert.That(Liquid(id),Is.Zero);Assert.That(editor.Liquids.Active,Is.True,"Drain stays inside the live episode");
        }
        (string water,string other,string vessel) IdentityBasins(bool colourOnly=false){
            editor.Liquids.enabled=false;var ex=new RoomAgentExecutor(editor);
            string Create(string kind,Vector3 position)=>(string)TemplateRun(ex,TemplateCall(kind,position))["selected"]["output"]["objectId"];
            var water=Create("basin",new Vector3(4,2,0));var other=Create("basin",new Vector3(6,2,0));var vessel=Create("bucket",new Vector3(4,2,0));
            var contents=editor.Read(other).containers[0];contents.liquid=colourOnly?"Water":"Milk";contents.color=Color.white;ContainerRun(ex,ContainerCall(other,contents));
            physics.SetSurfaces(true,"Synthetic room ready");physics.StartPhysics();return(water,other,vessel);
        }
        [UnityTest] public IEnumerator FlowIdentityScoopRefillPublishesWaterBeforeMilkAndUndoKeepsThemSeparate(){
            var(water,milk,vessel)=IdentityBasins();var scoops=new List<(double amount,string liquid)>();var pours=new List<(double amount,string liquid)>();
            editor.ContainerScooped+=(id,amount,_,liquid)=>{if(id==vessel)scoops.Add((amount,liquid));};editor.ContainerPoured+=(id,moved,spilled,_,liquid)=>{if(id==vessel)pours.Add((moved+spilled,liquid));};
            editor.Liquids.Tick(.05f);double waterAmount=Liquid(vessel);Assert.That(waterAmount,Is.GreaterThan(0));DrainLiquid(vessel);Assert.That(scoops,Is.Empty);
            LiquidPose(vessel,new Vector3(6,2,0),Quaternion.identity);editor.Liquids.Tick(.05f);
            Assert.That(editor.Liquids.Active,Is.False,"Publish the old identity before accepting new contents");Assert.That(Liquid(vessel),Is.Zero);Assert.That(Liquid(milk),Is.EqualTo(32000));
            Assert.That(scoops,Is.EqualTo(new[]{(waterAmount,"Water")}));Assert.That(pours[0].liquid,Is.EqualTo("Water"));Assert.That(pours[0].amount,Is.EqualTo(waterAmount).Within(1e-8));
            editor.Liquids.Tick(.05f);double milkAmount=Liquid(vessel);Assert.That(milkAmount,Is.GreaterThan(0));Assert.That((double)editor.Liquids.ObserveScooping(vessel)["scoopedMl"],Is.EqualTo(milkAmount));physics.PausePhysics();
            Assert.That(scoops,Is.EqualTo(new[]{(waterAmount,"Water"),(milkAmount,"Milk")}));Assert.That(editor.Read(vessel).containers[0].liquid,Is.EqualTo("Milk"));
            editor.Undo();Assert.That(Liquid(vessel),Is.Zero);Assert.That(editor.Read(vessel).containers[0].liquid,Is.EqualTo("Water"));Assert.That(Liquid(milk),Is.EqualTo(32000));Assert.That(Liquid(water),Is.EqualTo(32000-waterAmount));
            editor.Undo();Assert.That(Liquid(water),Is.EqualTo(32000));Assert.That(scoops.Count,Is.EqualTo(2),"Undo must not replay events");yield return null;
        }
        [UnityTest] public IEnumerator FlowIdentityMatchingRefillStaysInOneEpisode(){
            var(_,_,vessel)=IdentityBasins();int events=0;double total=0;editor.ContainerScooped+=(id,amount,_,_)=>{if(id==vessel){events++;total=amount;}};
            editor.Liquids.Tick(.05f);double first=Liquid(vessel);DrainLiquid(vessel);LiquidPose(vessel,new Vector3(4,2,0),Quaternion.identity);editor.Liquids.Tick(.05f);
            double second=Liquid(vessel);Assert.That(second,Is.GreaterThan(0));Assert.That(events,Is.Zero);Assert.That(editor.Liquids.Active,Is.True);Assert.That((double)editor.Liquids.ObserveScooping(vessel)["scoopedMl"],Is.EqualTo(first+second));physics.PausePhysics();Assert.That(events,Is.EqualTo(1));Assert.That(total,Is.EqualTo(first+second));yield return null;
        }
        [UnityTest] public IEnumerator FlowIdentityColourChangeAlsoDelimitsAcceptedScoops(){
            var(_,other,vessel)=IdentityBasins(true);int events=0;editor.ContainerScooped+=(id,_,_,_)=>{if(id==vessel)events++;};editor.Liquids.Tick(.05f);DrainLiquid(vessel);
            LiquidPose(vessel,new Vector3(6,2,0),Quaternion.identity);editor.Liquids.Tick(.05f);Assert.That(events,Is.EqualTo(1));Assert.That(Liquid(vessel),Is.Zero);Assert.That(Liquid(other),Is.EqualTo(32000));
            editor.Liquids.Tick(.05f);physics.PausePhysics();Assert.That(events,Is.EqualTo(2));Assert.That(editor.Read(vessel).containers[0].color,Is.EqualTo(Color.white));yield return null;
        }
        [UnityTest] public IEnumerator FlowIdentityFailedBoundarySaveRestoresOldEpisodeWithoutTakingNewLiquid(){
            var(water,milk,vessel)=IdentityBasins();int events=0;editor.ContainerScooped+=(_,_,_,_)=>events++;editor.Liquids.Tick(.05f);DrainLiquid(vessel);LiquidPose(vessel,new Vector3(6,2,0),Quaternion.identity);
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);try{editor.Liquids.Tick(.05f);}finally{Directory.Delete(pending);}
            Assert.That((string)editor.Liquids.Observe(vessel)["phase"],Is.EqualTo("failed"));Assert.That(events,Is.Zero);Assert.That(Liquid(water),Is.EqualTo(32000));Assert.That(Liquid(milk),Is.EqualTo(32000));Assert.That(Liquid(vessel),Is.Zero);
            editor.Liquids.Tick(.05f);Assert.That(Liquid(vessel),Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);physics.PausePhysics();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(vessel),Is.GreaterThan(0));physics.PausePhysics();Assert.That(events,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator FlowIdentityPourRefillKeepsOutgoingWaterAndMilkEventsSeparate(){
            var(ex,water,vessel)=PouringCups();physics.PausePhysics();var milk=(string)TemplateRun(ex,TemplateCall("cup",new Vector3(6,2,0)))["selected"]["output"]["objectId"];var contents=editor.Read(milk).containers[0];contents.amountMl=400;contents.liquid="Milk";contents.color=Color.white;ContainerRun(ex,ContainerCall(milk,contents));
            var events=new List<(double amount,string liquid)>();editor.ContainerPoured+=(id,moved,spilled,_,liquid)=>{if(id==vessel)events.Add((moved+spilled,liquid));};physics.StartPhysics();editor.Liquids.Tick(.05f);double waterAmount=Liquid(vessel);Assert.That(waterAmount,Is.GreaterThan(0));
            LiquidPose(water,editor.Find(water).transform.position,Quaternion.identity);DrainLiquid(vessel);LiquidPose(milk,new Vector3(6,2,0),Quaternion.Euler(0,0,80));var lip=ContainerFlowGeometry.Lip(contents,editor.Find(milk).transform,Vector3.up,out _);LiquidPose(vessel,new Vector3(lip.x,1.2f,lip.z),Quaternion.identity);
            editor.Liquids.Tick(.05f);Assert.That(Liquid(vessel),Is.Zero);Assert.That(Liquid(milk),Is.EqualTo(400));Assert.That(events.Count,Is.EqualTo(1));Assert.That(events[0].liquid,Is.EqualTo("Water"));Assert.That(events[0].amount,Is.EqualTo(waterAmount).Within(1e-8));
            editor.Liquids.Tick(.05f);double milkAmount=Liquid(vessel);Assert.That(milkAmount,Is.GreaterThan(0));LiquidPose(milk,new Vector3(6,2,0),Quaternion.identity);DrainLiquid(vessel);physics.PausePhysics();Assert.That(events.Count,Is.EqualTo(2));Assert.That(events[1].liquid,Is.EqualTo("Milk"));Assert.That(events[1].amount,Is.EqualTo(milkAmount).Within(1e-8));yield return null;
        }
    }
}

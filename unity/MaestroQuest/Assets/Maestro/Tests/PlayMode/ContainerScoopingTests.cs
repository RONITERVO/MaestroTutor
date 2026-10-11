// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (RoomAgentExecutor executor,string basin,string bucket) ScoopingVessels(){
            editor.Liquids.enabled=false;var executor=new RoomAgentExecutor(editor);
            string basin=(string)TemplateRun(executor,TemplateCall("basin",new Vector3(4,2,0)))["selected"]["output"]["objectId"];
            string bucket=(string)TemplateRun(executor,TemplateCall("bucket",new Vector3(4,2,0)))["selected"]["output"]["objectId"];
            physics.SetSurfaces(true,"Synthetic room ready");physics.StartPhysics();return(executor,basin,bucket);
        }
        double Liquid(string id)=>(double)editor.Liquids.Observe(id)["contents"]["amountMl"];
        [UnityTest] public IEnumerator FullSubmergedTiltedBucketDoesNotDrainTheReservoirByRefillingItsOwnSpill(){
            var(_,basin,bucket)=ScoopingVessels();var item=editor.Find(bucket);double spilled=0;
            editor.ContainerPoured+=(_,_,amount,_,_)=>spilled+=amount;
            item.transform.rotation=Quaternion.Euler(.5f,0,.1f);item.GetComponent<RigidRoomItem>().Teleported();
            for(int i=0;i<100;i++)editor.Liquids.Tick(.05f);
            physics.PausePhysics();
            Assert.That(Liquid(bucket),Is.EqualTo(2000).Within(1e-8));
            Assert.That(Liquid(basin)+Liquid(bucket),Is.EqualTo(32000).Within(1e-8),"A submerged full bucket must not repeatedly spill and refill");
            Assert.That(spilled,Is.Zero);
            // Lifting restores the ordinary gravity-driven pour into the basin.
            item.transform.position+=Vector3.up*.4f;item.GetComponent<RigidRoomItem>().Teleported();physics.StartPhysics();
            for(int i=0;i<20;i++)editor.Liquids.Tick(.05f);physics.PausePhysics();
            Assert.That(Liquid(bucket),Is.LessThan(2000));
            Assert.That(Liquid(basin)+Liquid(bucket),Is.EqualTo(32000).Within(1e-8));
            Assert.That(spilled,Is.Zero);
            // Outside the reservoir, ordinary uncollected spill still applies.
            item.transform.SetPositionAndRotation(new Vector3(6,3,0),Quaternion.Euler(0,0,90));item.GetComponent<RigidRoomItem>().Teleported();
            physics.StartPhysics();editor.Liquids.Tick(.1f);physics.PausePhysics();
            Assert.That(spilled,Is.GreaterThan(0));yield return null;
        }
        [UnityTest] public IEnumerator ScoopingPhysicalBucketConservesBothStoresAndPublishesOneUndo(){
            var(_,basin,bucket)=ScoopingVessels();int scoops=0,pours=0;double reported=0;
            editor.ContainerScooped+=(id,amount,donors,liquid)=>{Assert.That(id,Is.EqualTo(bucket));Assert.That(donors,Is.EqualTo(1));scoops++;reported=amount;};editor.ContainerPoured+=(_,_,_,_,_)=>pours++;
            editor.Liquids.Tick(.05f);double amount=Liquid(bucket);Assert.That(amount,Is.GreaterThan(0));Assert.That(Liquid(basin)+amount,Is.EqualTo(32000).Within(1e-8));
            Assert.That(editor.Read(bucket).containers[0].amountMl,Is.Zero);Assert.That(editor.CanEditObject(basin,true,out _),Is.False);Assert.That(editor.CanEditObject(bucket,true,out _),Is.False);Assert.That(scoops,Is.Zero);
            Assert.That(BehaviourCatalog.TryRead("object.container.scooping",1,new JObject{["target"]=bucket},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That((double)((JObject)fact.Value)["scoopedMl"],Is.EqualTo(amount));
            Assert.That((double)editor.Liquids.ObserveScooping(basin)["drawnMl"],Is.EqualTo(amount));Assert.That((double)editor.Liquids.Observe(basin)["contents"]["transferredMl"],Is.Zero,"Scooping must not masquerade as a pour");
            physics.PausePhysics();Assert.That(scoops,Is.EqualTo(1));Assert.That(pours,Is.Zero);Assert.That(reported,Is.EqualTo(amount));Assert.That(editor.Read(bucket).containers[0].amountMl,Is.EqualTo(amount));
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==bucket).containers[0].amountMl,Is.EqualTo(amount));
            editor.Undo();Assert.That(editor.Read(bucket).containers[0].amountMl,Is.Zero);Assert.That(editor.Read(basin).containers[0].amountMl,Is.EqualTo(32000));editor.Redo();Assert.That(editor.Read(bucket).containers[0].amountMl,Is.EqualTo(amount));Assert.That(scoops,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator ScoopingThenPouringUsesTheSameLiquidAndCanFillAnOrdinaryCup(){
            var(executor,basin,bucket)=ScoopingVessels();for(int i=0;i<20;i++)editor.Liquids.Tick(.05f);physics.PausePhysics();double picked=editor.Read(bucket).containers[0].amountMl;Assert.That(picked,Is.GreaterThan(1000));
            string cup=(string)TemplateRun(executor,TemplateCall("cup",new Vector3(6,1.4f,0)))["selected"]["output"]["objectId"];
            var held=editor.Find(bucket);held.transform.SetPositionAndRotation(new Vector3(6,2.4f,0),Quaternion.Euler(0,0,80));held.GetComponent<RigidRoomItem>().Teleported();var lip=ContainerFlowGeometry.Lip(editor.Read(bucket).containers[0],held.transform,Vector3.up,out _);editor.Find(cup).transform.position=new Vector3(lip.x,1.4f,lip.z);editor.Find(cup).GetComponent<RigidRoomItem>().Teleported();
            physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(cup),Is.GreaterThan(0));Assert.That(Liquid(basin)+Liquid(bucket)+Liquid(cup),Is.EqualTo(32000).Within(1e-8));physics.PausePhysics();editor.Undo();Assert.That(editor.Read(bucket).containers[0].amountMl,Is.EqualTo(picked));Assert.That(editor.Read(cup).containers[0].amountMl,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator ScoopingMultipleReceiversShareTheDonorBudgetAndStopAtCapacity(){
            editor.Liquids.enabled=false;var executor=new RoomAgentExecutor(editor);string basin=(string)TemplateRun(executor,TemplateCall("basin",new Vector3(4,2,0)))["selected"]["output"]["objectId"];physics.SetSurfaces(true,"Synthetic room ready");
            string first=(string)TemplateRun(executor,TemplateCall("cup",new Vector3(4,2,-.16f)))["selected"]["output"]["objectId"],second=(string)TemplateRun(executor,TemplateCall("cup",new Vector3(4,2,.16f)))["selected"]["output"]["objectId"];
            physics.StartPhysics();for(int i=0;i<18;i++)editor.Liquids.Tick(.1f);physics.PausePhysics();
            Assert.That(Liquid(first),Is.EqualTo(500));Assert.That(Liquid(second),Is.EqualTo(500));Assert.That(Liquid(basin),Is.EqualTo(31000));
            editor.Undo();Assert.That(Liquid(first)+Liquid(second),Is.Zero);Assert.That(Liquid(basin),Is.EqualTo(32000));
            Assert.That(BehaviourCatalog.TryRead("object.container",1,new JObject{["target"]=basin},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);var value=(JObject)fact.Value;var definition=(JObject)value["definition"].DeepClone();definition["capacityMl"]=1000;definition["amountMl"]=800;
            var configure=ObjectEditRequest(new JObject{["id"]="object.container.edit",["version"]=1,["arguments"]=new JObject{["operation"]="configure",["target"]=basin,["revision"]=value["revision"].DeepClone(),["definition"]=definition}});Assert.That(executor.Execute(configure,out var error,out _),Is.True,error);
            physics.StartPhysics();editor.Liquids.Tick(.1f);double received=Liquid(first)+Liquid(second);Assert.That(received,Is.EqualTo(75).Within(.001),"Both receivers share the donor's per-tick outflow limit");Assert.That(Liquid(first),Is.GreaterThan(0));Assert.That(Liquid(second),Is.GreaterThan(0));Assert.That(Liquid(basin)+received,Is.EqualTo(800).Within(1e-8));physics.PausePhysics();yield return null;
        }
        [UnityTest] public IEnumerator ScoopingRejectsSolidLidsAndOriginInsideAnObstruction(){
            var(_,basin,bucket)=ScoopingVessels();var lid=GameObject.CreatePrimitive(PrimitiveType.Cube);lid.transform.SetParent(root.transform,false);lid.transform.position=new Vector3(4,2.13f,0);lid.transform.localScale=new Vector3(.3f,.01f,.3f);lid.layer=RoomPhysicsLayers.Scanned;
            editor.Liquids.Tick(.05f);Assert.That(Liquid(bucket),Is.Zero);Assert.That(Liquid(basin),Is.EqualTo(32000));Assert.That(editor.Liquids.Active,Is.False);
            lid.transform.position=new Vector3(4,2.15f,0);lid.transform.localScale=new Vector3(.3f,.02f,.3f);editor.Liquids.Tick(.05f);Assert.That(Liquid(bucket),Is.Zero,"A ray starting in a blocker is not a clear path");
            lid.SetActive(false);editor.Liquids.Tick(.05f);Assert.That(Liquid(bucket),Is.GreaterThan(0));physics.PausePhysics();yield return null;
        }
        [UnityTest] public IEnumerator ScoopingFailedSaveRollsBackAndTemporaryDiscardRestoresTheBase(){
            var(_,basin,bucket)=ScoopingVessels();editor.Liquids.Tick(.05f);string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);int events=0;editor.ContainerScooped+=(_,_,_,_)=>events++;
            try{Assert.That(editor.Liquids.Finish(out _),Is.False);}finally{Directory.Delete(pending);}
            Assert.That(Liquid(bucket),Is.Zero);Assert.That(Liquid(basin),Is.EqualTo(32000));Assert.That(events,Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);editor.Liquids.Tick(.1f);Assert.That(editor.Liquids.Active,Is.False);
            physics.PausePhysics();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;physics.StartPhysics();editor.Liquids.Tick(.05f);physics.PausePhysics();Assert.That(editor.Read(bucket).containers[0].amountMl,Is.GreaterThan(0));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==bucket).containers[0].amountMl,Is.Zero);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(bucket).containers[0].amountMl,Is.Zero);Assert.That(editor.Read(basin).containers[0].amountMl,Is.EqualTo(32000));
        }
        [UnityTest] public IEnumerator ScoopingPublicationResumesAnOrdinaryTypedEventProgram(){
            var(_,basin,bucket)=ScoopingVessels();physics.PausePhysics();var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-container-scoop.json"));var sequence=workshop.Selected;sequence.program=source;sequence.repeat=false;
            Assert.That(workshop.Execute(new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",sequence=sequence}}},out var error,out _),Is.True,error);Assert.That(runtime.Trigger(sequence.id),Is.True);
            for(int i=0;i<40&&!runtime.Scheduler.IsListening("object.container.scooped",bucket);i++)yield return null;Assert.That(runtime.Scheduler.IsListening("object.container.scooped",bucket),Is.True);
            physics.StartPhysics();editor.Liquids.Tick(.05f);double amount=Liquid(bucket);physics.PausePhysics();for(int i=0;i<40&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="hold");i++)yield return null;
            var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.nodeId,Is.EqualTo("hold"));Assert.That(double.Parse(run.state.Single(v=>v.name=="amount").value,System.Globalization.CultureInfo.InvariantCulture),Is.EqualTo(amount).Within(.001));Assert.That(amount,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator ScoopingRunsWithARealGripAndStopsWhenTheUserLiftsTheBucket(){
            var(_,basin,bucket)=ScoopingVessels();var item=editor.Find(bucket);var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);editor.Liquids.enabled=true;
            float deadline=Time.realtimeSinceStartup+3;while(Liquid(bucket)<=0&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(Liquid(bucket),Is.GreaterThan(0));Assert.That(item.Grab.isSelected,Is.True);
            hand.transform.position+=Vector3.up*.5f;deadline=Time.realtimeSinceStartup+3;while(editor.Liquids.Active&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(editor.Liquids.Active,Is.False);double saved=editor.Read(bucket).containers[0].amountMl;Assert.That(saved,Is.GreaterThan(0));Assert.That(editor.Read(basin).containers[0].amountMl+saved,Is.EqualTo(32000).Within(1e-8));
            physics.PausePhysics();manager.SelectExit((IXRSelectInteractor)hand,item.Grab);
        }
    }
}

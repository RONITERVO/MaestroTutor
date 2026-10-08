// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject WeatherCall(WeatherSettings settings,double duration=0)=>new(){["id"]="world.weather.set",["version"]=1,["arguments"]=new JObject{["revision"]=editor.WeatherRevision,["seed"]=17,["settings"]=JObject.Parse(JsonUtility.ToJson(settings)),["transitionSeconds"]=duration}};
        string RainCup(){
            editor.Liquids.enabled=false;var ex=new RoomAgentExecutor(editor);string id=(string)TemplateRun(ex,TemplateCall("cup",new Vector3(3,1,0)))["selected"]["output"]["objectId"];
            editor.Find(id).GetComponent<RigidRoomItem>().Teleported();Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out var e),Is.True,e);physics.SetSurfaces(true,"Test room");physics.StartPhysics();return id;
        }
        GameObject RainRoof(Vector3 position,bool scanned){
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform,false);go.transform.position=position;go.transform.localScale=new Vector3(2,.1f,2);go.layer=scanned?RoomPhysicsLayers.Scanned:RoomPhysicsLayers.Item;
            if(!scanned){var item=go.AddComponent<RoomItem>();item.Configure(new[]{go.GetComponent<Collider>()});}Physics.SyncTransforms();return go;
        }
        [UnityTest]public IEnumerator WeatherNativeReceiptAndClockTransitionShareExactReadbackAndUndo(){
            var settings=new WeatherSettings{rainMmPerHour=60,cloudCover=1,fogDensity=.1f};
            Assert.That(editor.SetWeather(editor.WeatherRevision,1,settings,10,out var e),Is.False);StringAssert.Contains("clock",e);
            Assert.That(editor.ConfigureWorldTime(editor.WorldTimeRevision,new(){running=true,rate=1},out e),Is.True,e);
            var request=TemplateRequest(WeatherCall(settings,10));request.conditions=Array.Empty<RoomObjectCondition>();var ex=new RoomAgentExecutor(editor);Assert.That(ex.Execute(request,out e,out _),Is.True,e);int revision=editor.WeatherRevision;
            Assert.That(ex.Execute(request,out e,out _),Is.True,e);Assert.That(editor.WeatherRevision,Is.EqualTo(revision));Assert.That(editor.CurrentWeather.Rain,Is.Zero);
            Assert.That(editor.SeekWorldTime(editor.WorldTimeRevision,0,43205,out e),Is.True,e);Assert.That(editor.CurrentWeather.Rain,Is.EqualTo(30));Assert.That(WorldWeatherCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,null,out _),Is.True);
            editor.GetComponent<WorldLightingView>().Refresh();Assert.That(Shader.GetGlobalFloat("_MaestroFogDensity"),Is.EqualTo(.05f).Within(.00001));editor.Undo();Assert.That(editor.CurrentWeather.Rain,Is.Zero);editor.Redo();Assert.That(editor.CurrentWeather.Rain,Is.EqualTo(30));yield return null;
        }
        [UnityTest]public IEnumerator WeatherFailedSaveAndTemporaryDiscardPreserveAcceptedWeather(){
            Assert.That(editor.TryFlush(out var e),Is.True,e);int revision=editor.WeatherRevision;
            using(var held=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read)){
                Assert.That(editor.SetWeather(revision,1,new(){rainMmPerHour=50},0,out _),Is.False);Assert.That(editor.CurrentWeather.Rain,Is.Zero);Assert.That(editor.WeatherRevision,Is.EqualTo(revision));
            }
            Assert.That(editor.BeginTemporaryRoom(out e),Is.True,e);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=70},0,out e),Is.True,e);Assert.That(editor.DiscardTemporaryRoom(out e),Is.True,e);Assert.That(editor.CurrentWeather.Rain,Is.Zero);Assert.That(editor.WeatherRevision,Is.GreaterThan(revision));
        }
        [UnityTest]public IEnumerator WeatherRainUsesMeasuredOpeningAreaAndSavesOneReversibleLiquidEpisode(){
            string id=RainCup();var c=editor.Read(id).containers[0];int events=0;double intake=0;editor.ContainerRainCollected+=(target,amount)=>{Assert.That(target,Is.EqualTo(id));events++;intake+=amount;};
            editor.Liquids.Tick(.1f);var observation=editor.Liquids.ObserveRain(id);double expected=60*c.FootprintArea*.1*1000/3600;
            Assert.That((double)observation["collectedMl"],Is.EqualTo(expected).Within(.000001),observation.ToString());Assert.That((double)observation["exposure"],Is.EqualTo(1));Assert.That(editor.Read(id).containers[0].amountMl,Is.Zero);
            Assert.That(WorldWeatherCapability.Rain().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]=id},out _),Is.True);
            physics.PausePhysics();Assert.That(events,Is.EqualTo(1));Assert.That(intake,Is.EqualTo(expected).Within(.000001));Assert.That(editor.Read(id).containers[0].amountMl,Is.EqualTo(intake));editor.Undo();Assert.That(editor.Read(id).containers[0].amountMl,Is.Zero);editor.Redo();Assert.That(editor.Read(id).containers[0].amountMl,Is.EqualTo(intake));yield return null;
        }
        [UnityTest]public IEnumerator WeatherRainRejectsCoveredAndUnknownOpeningsAndUsesEntityRealRoomParticipation(){
            string id=RainCup();var item=editor.Find(id);var point=item.transform.position+Vector3.up*.3f;var cover=new WeatherCover();var roof=RainRoof(new Vector3(3,2,0),true);
            editor.Liquids.Tick(.1f);Assert.That((double)editor.Liquids.Observe(id)["contents"]["amountMl"],Is.Zero);Assert.That(cover.Exposure(editor,point,Vector3.down*6,item),Is.EqualTo(RainExposure.Covered));
            physics.PausePhysics();string profile=NewEnvironment();Assert.That(AssignEnvironment(id,profile,out var e),Is.True,e);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,item),Is.EqualTo(RainExposure.Open));Assert.That(cover.Exposure(editor,point,Vector3.down*6,editor.Find("maestro")),Is.EqualTo(RainExposure.Covered));
            roof.layer=RoomPhysicsLayers.Item;var roofItem=roof.AddComponent<RoomItem>();roofItem.Configure(new[]{roof.GetComponent<Collider>()});Physics.SyncTransforms();Assert.That(cover.Exposure(editor,point,Vector3.down*6,item),Is.EqualTo(RainExposure.Covered));
            UnityEngine.Object.DestroyImmediate(roof);physics.SetSurfaces(false,"Unavailable");Assert.That(cover.Exposure(editor,point,Vector3.down*6,item),Is.EqualTo(RainExposure.Open));Assert.That(cover.Exposure(editor,point,Vector3.down*6,editor.Find("maestro")),Is.EqualTo(RainExposure.Unknown));yield return null;
        }
        [UnityTest]public IEnumerator WeatherRainBlockedSaveRollsBackAndDifferentLiquidsNeverMix(){
            string id=RainCup();editor.Liquids.Tick(.1f);Assert.That((double)editor.Liquids.Observe(id)["contents"]["amountMl"],Is.GreaterThan(0));string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);int events=0;editor.ContainerRainCollected+=(_,_)=>events++;
            try{Assert.That(editor.Liquids.Finish(out _),Is.False);}finally{Directory.Delete(pending);}
            Assert.That((double)editor.Liquids.Observe(id)["contents"]["amountMl"],Is.Zero);Assert.That(events,Is.Zero);editor.Liquids.Tick(.1f);Assert.That(editor.Liquids.Active,Is.False);
            physics.PausePhysics();var c=editor.Read(id).containers[0];c.amountMl=20;c.liquid="Juice";ContainerRun(new RoomAgentExecutor(editor),ContainerCall(id,c));physics.StartPhysics();editor.Liquids.Tick(.1f);Assert.That((double)editor.Liquids.Observe(id)["contents"]["amountMl"],Is.EqualTo(20));Assert.That((string)editor.Liquids.ObserveRain(id)["reason"],Does.Contain("mix"));physics.PausePhysics();yield return null;
        }
        [UnityTest]public IEnumerator WeatherRainCoverDetectsStartingInsideAndQuerySaturation(){
            physics.SetSurfaces(true,"Test room");var cover=new WeatherCover();var point=new Vector3(4,1,0);var roof=RainRoof(point,true);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));UnityEngine.Object.DestroyImmediate(roof);
            for(int i=0;i<64;i++)RainRoof(point+Vector3.up*(1+i*.2f),true);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Unknown));yield return null;
        }
        [UnityTest]public IEnumerator WeatherRainSavedEventResumesAnOrdinaryUserProgram(){
            string id=RainCup();physics.PausePhysics();var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-container-pour.json")));
            var wait=program["functions"][0]["body"][0];wait["event"]="object.container.rainCollected";wait["fields"]=new JObject{["collectedMl"]="ml"};
            var sequence=workshop.Selected;sequence.program=program.ToString();sequence.repeat=false;
            Assert.That(new RoomAgentExecutor(editor).Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new Maestro.Quest.Rules.RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new Maestro.Quest.Rules.RuleEdit{kind="save",sequence=sequence}}}}}},out var e,out _),Is.True,e);
            Assert.That(runtime.Trigger(sequence.id),Is.True);for(int i=0;i<40&&!runtime.Scheduler.IsListening("object.container.rainCollected",id);i++)yield return null;Assert.That(runtime.Scheduler.IsListening("object.container.rainCollected",id),Is.True);
            physics.StartPhysics();editor.Liquids.Tick(.1f);physics.PausePhysics();double intake=editor.Read(id).containers[0].amountMl;
            for(int i=0;i<40&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="hold");i++)yield return null;
            var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.nodeId,Is.EqualTo("hold"));Assert.That(double.Parse(run.state.Single(v=>v.name=="amount").value,System.Globalization.CultureInfo.InvariantCulture),Is.EqualTo(intake).Within(.000001));
        }
        [UnityTest]public IEnumerator WeatherRainRenderingIsBoundedAndStopsUnderCoverWithoutCreatingPhysicalDrops(){
            Assert.That(editor.SetWeather(editor.WeatherRevision,42,new(){rainMmPerHour=120},0,out var e),Is.True,e);physics.SetSurfaces(true,"Test room");
            var view=root.AddComponent<WorldRainView>();view.Initialize(editor,leftAnchor.transform);view.enabled=false;
            for(int i=0;i<12;i++)view.Sample(i*.05f);Assert.That(view.VisibleDrops,Is.InRange(1,WorldRainView.MaximumDrops));
            var mesh=view.GetComponentsInChildren<MeshFilter>().Last(x=>x.sharedMesh?.name=="Bounded rain streaks").sharedMesh;Assert.That(mesh.vertexCount,Is.EqualTo(256));Assert.That(view.GetComponentsInChildren<Collider>().Any(x=>x.gameObject.name=="Local rain"),Is.False);
            var roof=RainRoof(leftAnchor.transform.position+Vector3.up*7,true);roof.transform.localScale=new Vector3(20,.2f,20);Physics.SyncTransforms();for(int i=0;i<12;i++)view.Sample(10+i*.05f);Assert.That(view.VisibleDrops,Is.Zero);yield return null;
        }
    }
}

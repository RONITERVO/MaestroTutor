// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static WorldTimeSettings DayCycle()=>new(){running=false,rate=60,cycleEnabled=true,frames=new[]{new WorldLightKeyframe{second=0,ambientColor="#0000FF",ambientIntensity=.2f,sunIntensity=0,elevation=-60},new WorldLightKeyframe{second=43200,ambientColor="#FFFFFF",ambientIntensity=.6f,sunIntensity=1,elevation=60}}};
        JObject TimeCall(WorldTimeSettings value)=>new(){["id"]="world.time.configure",["version"]=1,["arguments"]=new JObject{["revision"]=editor.WorldTimeRevision,["settings"]=JObject.Parse(JsonUtility.ToJson(value))}};
        [UnityTest]public IEnumerator WorldTimeNativeReceiptIsOnceOnlyAndSeeksProjectLightingWithoutMovingActors(){
            string target=RecipeTarget();Assert.That(runtime.Scheduler.Invoke(PartCall(target,"RightUpperArm",10),Time.unscaledTime,out var animation,out var e),Is.True,e);yield return null;
            var before=block.transform.position;var request=TemplateRequest(TimeCall(DayCycle()));request.conditions=Array.Empty<RoomObjectCondition>();var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(request,out e,out _),Is.True,e);int revision=editor.WorldTimeRevision;Assert.That(executor.Execute(request,out e,out _),Is.True,e);Assert.That(editor.WorldTimeRevision,Is.EqualTo(revision));Assert.That((string)runtime.Scheduler.Invocation(animation)["phase"],Is.EqualTo("running"));
            Assert.That(editor.SeekWorldTime(revision,3,0,out e),Is.True,e);var view=editor.GetComponent<WorldLightingView>();view.Refresh();Assert.That(Shader.GetGlobalVector("_MaestroAmbient").z,Is.EqualTo(.2f).Within(.0001));Assert.That(Shader.GetGlobalVector("_MaestroSun").sqrMagnitude,Is.Zero);Assert.That(block.transform.position,Is.EqualTo(before));
            Assert.That(editor.SetLighting(editor.LightingRevision,new(){enabled=true},out e),Is.False);StringAssert.Contains("cycle",e);Assert.That(editor.SeekWorldTime(revision,0,5,out _),Is.False);
            Assert.That(WorldTimeCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,null,out _),Is.True);Assert.That(WorldTimeCapability.Illumination().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,null,out _),Is.True);
            runtime.Scheduler.CancelInvocation(animation,out _);editor.Undo();Assert.That(editor.WorldTime.day,Is.Zero);Assert.That(editor.WorldSecond,Is.EqualTo(43200));editor.Redo();Assert.That(editor.WorldTime.day,Is.EqualTo(3));
        }
        [UnityTest]public IEnumerator WorldTimeFreezesUnderHoldsAndSkipsResumeGapWithoutInvalidatingConfiguration(){
            var value=DayCycle();value.running=true;Assert.That(editor.ConfigureWorldTime(editor.WorldTimeRevision,value,out var e),Is.True,e);editor.TickWorldTime(.5);int guard=editor.WorldTimeRevision;double start=editor.WorldSecond;editor.TickWorldTime(.5);Assert.That(editor.WorldSecond,Is.EqualTo(start+30));Assert.That(editor.WorldTimeRevision,Is.EqualTo(guard));
            using(editor.RuntimeGate.Hold("Testing a workspace hold")){editor.TickWorldTime(1);Assert.That(editor.WorldSecond,Is.EqualTo(start+30));Assert.That((bool)editor.ObserveWorldTime()["advancing"],Is.False);}
            editor.TickWorldTime(60);Assert.That(editor.WorldSecond,Is.EqualTo(start+30));editor.TickWorldTime(.5);Assert.That(editor.WorldSecond,Is.EqualTo(start+60));
            using(editor.WriteGate.TryFreeze(out e)){editor.TickWorldTime(.5);Assert.That(editor.WorldSecond,Is.EqualTo(start+60));}editor.TickWorldTime(.5);Assert.That(editor.WorldSecond,Is.EqualTo(start+60));editor.TickWorldTime(.5);Assert.That(editor.WorldSecond,Is.EqualTo(start+90));
            Assert.That(editor.ConfigureWorldTime(guard,new WorldTimeSettings{running=false},out e),Is.True,e);Assert.That(editor.WorldSecond,Is.EqualTo(start+90));editor.TickWorldTime(.5);editor.TickWorldTime(.5);Assert.That(editor.WorldSecond,Is.EqualTo(start+90));yield return null;
        }
        [UnityTest]public IEnumerator WorldTimeExplicitSaveReloadAndTemporaryKeepDiscardRetainAcceptedTime(){
            Assert.That(editor.SeekWorldTime(editor.WorldTimeRevision,7,100,out var e),Is.True,e);var settings=new WorldTimeSettings{running=true,rate=10};Assert.That(editor.ConfigureWorldTime(editor.WorldTimeRevision,settings,out e),Is.True,e);editor.TickWorldTime(.5);editor.TickWorldTime(.5);Assert.That(editor.TryFlush(out e),Is.True,e);Assert.That(new RoomStorage(directory).Load(out e).worldTime.second,Is.EqualTo(105),e);
            settings.running=false;Assert.That(editor.ConfigureWorldTime(editor.WorldTimeRevision,settings,out e),Is.True,e);Assert.That(editor.BeginTemporaryRoom(out e),Is.True,e);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.SeekWorldTime(editor.WorldTimeRevision,9,300,out e),Is.True,e);Assert.That(editor.DiscardTemporaryRoom(out e),Is.True,e);Assert.That(editor.WorldTime.day,Is.EqualTo(7));Assert.That(editor.WorldSecond,Is.EqualTo(105));
            Assert.That(editor.BeginTemporaryRoom(out e),Is.True,e);while(editor.TemporarySavePending)yield return null;Assert.That(editor.SeekWorldTime(editor.WorldTimeRevision,10,500,out e),Is.True,e);Assert.That(editor.KeepTemporaryRoom(out e),Is.True,e);while(editor.TemporarySavePending)yield return null;Assert.That(editor.DiscardTemporaryRoom(out e),Is.True,e);Assert.That(editor.WorldTime.day,Is.EqualTo(10));Assert.That(editor.WorldSecond,Is.EqualTo(500));
        }
        [UnityTest]public IEnumerator WorldTimeFailedSaveCannotAdvanceTheAcceptedSettingsOrChangeTheProjection(){
            Assert.That(editor.TryFlush(out var e),Is.True,e);var before=editor.WorldTime;int guard=editor.WorldTimeRevision;
            using(var held=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read)){
                Assert.That(editor.ConfigureWorldTime(guard,DayCycle(),out _),Is.False);Assert.That(editor.WorldTime.Same(before),Is.True);Assert.That(editor.WorldTimeRevision,Is.EqualTo(guard));editor.GetComponent<WorldLightingView>().Refresh();Assert.That(Shader.GetGlobalFloat("_MaestroLightingEnabled"),Is.Zero);
            }yield return null;
        }
    }
}

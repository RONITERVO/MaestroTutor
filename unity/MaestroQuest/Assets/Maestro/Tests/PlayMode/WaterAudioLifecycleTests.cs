// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
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
  JObject WaterAudioCall(string id,bool enabled=true)=>new(){["id"]="object.container.audio.configure",["version"]=1,["arguments"]=new JObject{
   ["target"]=id,["revision"]=editor.ObjectRevision(id),["settings"]=JObject.FromObject(new WaterAudioSettings{enabled=enabled,attenuationDbPerMetre=3,mufflingPerMetre=2})}};
  JObject WaterAudioFact(string id){Assert.IsTrue(BehaviourCatalog.TryRead("object.container.audio",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact));return (JObject)fact.Value;}
  [UnityTest] public IEnumerator WaterAudioEditsShareReceiptsPersistenceUndoAndPreserveOrdinaryContainerEdits(){
   var(ex,id,_)=Containers();Assert.That(RoomControls.Capabilities(editor),Does.Contain(WaterAudioCapability.Feature));var stale=WaterAudioCall(id);var request=ContainerRequest(WaterAudioCall(id));
   Assert.That((bool)WaterAudioFact(id)["settings"]["enabled"],Is.False);
   Assert.IsTrue(ex.Execute(request,out var error,out _),error);yield return SpatialSettingsCompletion.Wait(ex.Executions);
   int revision=editor.ObjectRevision(id);Assert.IsTrue((bool)WaterAudioFact(id)["settings"]["enabled"]);
   Assert.IsTrue(ex.Execute(request,out error,out _),error);yield return SpatialSettingsCompletion.Wait(ex.Executions);
   Assert.AreEqual(revision,editor.ObjectRevision(id),"Replaying a completed receipt cannot apply the edit twice");
   Assert.IsFalse(ex.Execute(ContainerRequest(stale),out _,out _));
   Assert.That(new RoomStorage(directory).Load(out error).objects.Single(o=>o.id==id).containers[0].waterAudio.attenuationDbPerMetre,Is.EqualTo(3),error);
   editor.Undo();Assert.IsFalse((bool)WaterAudioFact(id)["settings"]["enabled"]);editor.Redo();Assert.IsTrue((bool)WaterAudioFact(id)["settings"]["enabled"]);
   yield return ContainerAction(ex,ContainerCall(id,new RoomContainer{amountMl=35}));
   Assert.AreEqual(3,editor.Read(id).containers[0].waterAudio.attenuationDbPerMetre);Assert.AreEqual(2f,editor.Read(id).containers[0].waterAudio.mufflingPerMetre);
   yield return ContainerAction(ex,ContainerCall(id,null));Assert.IsFalse((bool)WaterAudioFact(id)["configured"]);
   Assert.IsFalse(ex.Execute(ContainerRequest(WaterAudioCall(id)),out _,out _));editor.Undo();Assert.IsTrue((bool)WaterAudioFact(id)["settings"]["enabled"]);
  }
  [UnityTest] public IEnumerator WaterAudioFailedPublicationRetainsSavedAndVisibleSettings(){
   var(ex,id,_)=Containers();var before=editor.Read(id).containers[0].waterAudio.attenuationDbPerMetre;var pending=System.IO.Path.Combine(directory,RoomStorage.FileName+".pending");System.IO.Directory.CreateDirectory(pending);
   try{Assert.IsTrue(ex.Execute(ContainerRequest(WaterAudioCall(id)),out var error,out _),error);yield return SpatialSettingsCompletion.Wait(ex.Executions,"failed");
    Assert.IsFalse((bool)WaterAudioFact(id)["settings"]["enabled"]);Assert.AreEqual(before,editor.Read(id).containers[0].waterAudio.attenuationDbPerMetre);
   }finally{System.IO.Directory.Delete(pending);}
  }
  sealed class WaterListener:IDisposable {
   readonly AudioListener[] prior;readonly bool[] enabled;readonly float volume;readonly bool pause;
   internal readonly GameObject View;
   internal WaterListener(GameObject root,Vector3 position){
    prior=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);enabled=prior.Select(x=>x.enabled).ToArray();foreach(var x in prior)x.enabled=false;
    volume=AudioListener.volume;pause=AudioListener.pause;AudioListener.volume=0;AudioListener.pause=false;
    View=new GameObject("Water audio tracked listener",typeof(AudioListener));View.transform.SetParent(root.transform,false);View.transform.position=position;root.GetComponent<RoomInteraction>().Viewer=View.transform;
   }
   public void Dispose(){View.GetComponent<AudioListener>().enabled=false;UnityEngine.Object.Destroy(View);for(int i=0;i<prior.Length;i++)if(prior[i])prior[i].enabled=enabled[i];AudioListener.volume=volume;AudioListener.pause=pause;}
  }
  [UnityTest] public IEnumerator WaterAudioTreatsBoundTutorPcmWithoutChangingGainOrCompletionReceipts(){
   var(pool,_)=MediumFixture(.05f);physics.PausePhysics();yield return ContainerAction(new RoomAgentExecutor(editor),WaterAudioCall(pool));
   using var listener=new WaterListener(root,new Vector3(5,1.4f,0));
   var voice=new GameObject("Water speech");voice.transform.SetParent(root.transform,false);voice.transform.position=new Vector3(3,1.4f,0);
   var output=voice.AddComponent<NativeSpeechOutput>();double clock=AudioSettings.dspTime+600;output.Clock=()=>clock;output.TailOverride=.05;
   var manager=root.GetComponent<RoomWaterAudio>();long generation=output.Begin(24000);
   try {
    Assert.IsTrue(output.TryWrite(generation,1,Enumerable.Repeat((short)16384,4800).ToArray(),out var error),error);Assert.IsTrue(output.TryComplete(generation,1,4800,out error),error);
    var source=voice.GetComponentInChildren<AudioSource>();var filter=source.GetComponent<SpeechPcmFilter>();source.Pause(); // The test owns this callback clock; do not render concurrently.
    manager.Tick(Time.unscaledTime+.06f,clock+.1);Assert.AreEqual("active",(string)manager.Observe()["state"]);Assert.AreEqual(1,(int)manager.Observe()["treatedSources"]);
    Assert.AreEqual(1,source.volume);Assert.IsTrue(source.spatializePostEffects);
    var samples=new float[9600];filter.Render(samples,1,48000,clock+.1);
    Assert.That(samples[8000],Is.EqualTo(.5*Math.Pow(10,-3.0/20)).Within(.0001),"A metre of finite water must reach the bound PCM callback");
    Assert.IsFalse(output.Read().rendererDrained);clock+=.36;Assert.IsTrue(output.Read().rendererDrained);Assert.AreEqual(4800,output.Read().playedSamples);
    output.Stop();Assert.AreEqual(0,(int)manager.Observe()["sources"]);Assert.AreEqual("idle",(string)manager.Observe()["state"]);
   }finally{output.Stop();UnityEngine.Object.Destroy(voice);}
   yield return null;
  }
  [UnityTest] public IEnumerator WaterAudioBypassesUnprovenGeometryTrackingFocusAndExpiredTargets(){
   var(pool,_)=MediumFixture(.05f);physics.PausePhysics();yield return ContainerAction(new RoomAgentExecutor(editor),WaterAudioCall(pool));
   using var listener=new WaterListener(root,new Vector3(5,1.4f,0));
   var go=new GameObject("Water policy source",typeof(AudioSource),typeof(SpeechPcmFilter));go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(3,1.4f,0);
   var filter=go.GetComponent<SpeechPcmFilter>();var manager=root.GetComponent<RoomWaterAudio>();filter.BindWater(manager);float now=Time.unscaledTime;
   try {
    manager.Tick(now+=.06f,10);Assert.IsTrue(filter.WaterAt(10).Applied);Assert.IsFalse(filter.WaterAt(10.21).Applied);
    listener.View.GetComponent<AudioListener>().enabled=false;manager.Tick(now+=.06f,11);Assert.IsFalse(filter.WaterAt(11).Applied);Assert.AreEqual("unavailable",(string)manager.Observe()["state"]);
    listener.View.GetComponent<AudioListener>().enabled=true;
    var tracking=root.AddComponent<WorkspaceViewpoint>();manager.Tick(now+=.06f,12);Assert.IsFalse(filter.WaterAt(12).Applied);UnityEngine.Object.DestroyImmediate(tracking);
    manager.Tick(now+=.06f,13);Assert.IsTrue(filter.WaterAt(13).Applied);
    manager.SendMessage("OnApplicationFocus",false);Assert.IsFalse(filter.WaterAt(13).Applied);Assert.AreEqual("unavailable",(string)manager.Observe()["state"]);manager.SendMessage("OnApplicationFocus",true);
    string missing=MissingMedium(new Vector3(4.8f,1.3f,0));manager.Tick(now+=.06f,14);
    Assert.IsFalse(filter.WaterAt(14).Applied);Assert.AreEqual(1,(int)manager.Observe()["unavailableSources"],"Missing geometry beyond the first water hit must invalidate the entire sound path");
    Assert.IsFalse(editor.Find(missing).gameObject.activeSelf,"A sound query must not load or reveal missing objects");
    manager.enabled=false;Assert.IsFalse(filter.WaterAt(14).Applied);
   }finally{filter.Stop();UnityEngine.Object.Destroy(go);}
   yield return null;
  }
  [UnityTest] public IEnumerator WaterAudioUsesAuthoredSpatialPlaybackAndExcludesNonspatialSounds(){
   var(pool,prop)=MediumFixture(.05f);physics.PausePhysics();yield return ContainerAction(new RoomAgentExecutor(editor),WaterAudioCall(pool));
   using var listener=new WaterListener(root,new Vector3(5,1.4f,0));
   string sound=WorldSound(4);WorldEmitter(prop,sound,spatial:true,gain:.25f);WorldEmitter("book",sound,spatial:false,gain:.4f);
   var audio=WorldAudio.For(editor);var manager=root.GetComponent<RoomWaterAudio>();
   Assert.IsTrue(audio.Begin(prop,"sound",out var spatial,out var error),error);Assert.IsTrue(audio.Begin("book","sound",out var flat,out error),error);
   try {
    double end=Time.realtimeSinceStartupAsDouble+3;while((!spatial.Output||!flat.Output)&&!spatial.Closed&&!flat.Closed&&Time.realtimeSinceStartupAsDouble<end)yield return null;
    Assert.IsNotNull(spatial.Output,spatial.Error);Assert.IsNotNull(flat.Output,flat.Error);
    manager.Tick(Time.unscaledTime+.06f,AudioSettings.dspTime);Assert.AreEqual(1,(int)manager.Observe()["sources"]);
    Assert.IsTrue(spatial.Output.GetComponent<SpeechPcmFilter>().WaterAt(AudioSettings.dspTime).Applied);
    Assert.IsFalse(flat.Output.GetComponent<SpeechPcmFilter>().WaterAt(AudioSettings.dspTime).Applied);Assert.AreEqual(.25f,spatial.Output.volume);Assert.AreEqual(.4f,flat.Output.volume);
    yield return ContainerAction(new RoomAgentExecutor(editor),WaterAudioCall(pool,false));
    double disabledBy=Time.realtimeSinceStartupAsDouble+.5;while((string)manager.Observe()["state"]!="disabled"&&Time.realtimeSinceStartupAsDouble<disabledBy)yield return null;
    Assert.AreEqual("disabled",(string)manager.Observe()["state"]);Assert.IsFalse(spatial.Output.GetComponent<SpeechPcmFilter>().WaterAt(AudioSettings.dspTime).Applied);
    audio.Close(spatial);Assert.AreEqual(0,(int)manager.Observe()["sources"]);Assert.IsFalse(flat.Closed);
   }finally{audio.Close(spatial);audio.Close(flat);}
   yield return null;
  }
  [UnityTest] public IEnumerator WaterAudioCapturedPathsCannotReuseAnotherMediumCapture(){
   MediumFixture(.05f);physics.PausePhysics();var path=new LiquidPath();var a=new Vector3(3,1.4f,0);var b=new Vector3(5,1.4f,0);
   editor.Liquids.CaptureLiquidPaths();Assert.IsTrue(editor.Liquids.MeasureCapturedLiquidPath(a,b,path).Ready);Assert.AreEqual(1,path.WetMetres,.0001);
   MediumAt(new Vector3(4,1.4f,0));Assert.IsFalse(editor.Liquids.MeasureCapturedLiquidPath(a,b,path).Ready);Assert.IsFalse(path.Complete);
   Assert.IsTrue(editor.Liquids.MeasureLiquidPath(a,b,path).Ready);yield return null;
  }
 }
}

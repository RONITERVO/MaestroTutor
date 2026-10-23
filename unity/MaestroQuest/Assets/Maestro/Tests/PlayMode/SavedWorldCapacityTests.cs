// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomEditorTests {
  [UnityTest]public IEnumerator SavedWorldCapacityColdLoadAndEightAreaVisitsKeepNativeObjectsBounded(){
   var saved=editor.Snapshot();var builtins=saved.objects.Where(x=>x.IsBuiltIn).ToArray();
   var values=Enumerable.Range(0,256).Select(i=>{
    float angle=(i/32)*Mathf.PI/4;var centre=new Vector3(Mathf.Cos(angle)*13,1,Mathf.Sin(angle)*13);
    return new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block,name="Town block "+i,
     position=centre+new Vector3(i%4*.25f,0,i%32/4*.25f)};
   }).ToArray();
   saved.objects=builtins.Concat(values).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
   saved.regions=Enumerable.Range(0,8).Select(i=>new RoomRegion{id=Guid.NewGuid().ToString("N"),name="District "+i,loading="independent",members=values.Skip(i*32).Take(32).Select(x=>x.id).ToArray()}).ToArray();
   Assert.That(saved.Validate(out var error),Is.True,error);yield return ReloadForStartup(saved,observe:false);
   Assert.That(values.All(x=>editor.HasSavedObject(x.id)&&editor.NativeEntityDormant(x.id)&&!editor.Find(x.id)),Is.True);
   int peak=0,revision=editor.Revision;string source=JsonUtility.ToJson(editor.Snapshot());
   foreach(var area in saved.regions){
    var task=editor.ActivateNativeArea(area.id);float deadline=Time.realtimeSinceStartup+20;
    while(!task.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
    Assert.That(task.IsCompleted,Is.True);Assert.That(task.Exception,Is.Null);Assert.That(task.Result,Is.Null);
    Assert.That(area.members.All(id=>editor.Find(id)),Is.True);
    int live=root.GetComponentsInChildren<CreatedRoomObject>(true).Length;peak=Math.Max(peak,live);Assert.That(live,Is.EqualTo(32));
    Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(258));Assert.That(editor.ObserveObjectContext(null,null).Length,Is.EqualTo(2));
    Assert.That(editor.RetireNativeArea(area.id,out error),Is.True,error);yield return null;
    Assert.That(area.members.All(id=>editor.NativeEntityDormant(id)&&!editor.Find(id)),Is.True);
   }
   Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(source));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.False);
   Assert.That(NativeObjectResourceFacts.SavedBudget().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,null,out var budget),Is.True);
   Assert.That((int)((JObject)budget.Value)["saved"],Is.EqualTo(256));Assert.That((int)((JObject)budget.Value)["remaining"],Is.Zero);
   string evidence=Environment.GetEnvironmentVariable("MAESTRO_DEFERRED_EVIDENCE");
   if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"capacity.json"),new JObject{
    ["savedCreations"]=256,["areasVisited"]=8,["peakNativeRoots"]=peak,["nativeLimit"]=NativeObjectResources.MaximumInstances,
    ["contextObjects"]=2,["savedBytes"]=new FileInfo(Path.Combine(directory,RoomStorage.FileName)).Length,["savedStateUnchanged"]=true,["headsetMeasured"]=false}.ToString());}
  }
 }

 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator SavedWorldCapacityAreaActionChecksEveryMemberAndNeverLoadsColdObjects(){
   yield return WaitForModuleLibrary();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   int remaining=RoomDocument.MaximumObjects+2-editor.Snapshot().objects.Length;
   while(remaining>0){
    int take=Math.Min(16,remaining);
    var batch=new CreationBatch{position=new Vector3(2,1,1),blueprint=new CreationBlueprint{pieces=Enumerable.Range(0,take).Select(i=>new CreationPiece{
     slot="piece_"+i,name="Area item "+i,source=new CreationSource{kind="prototype",prototype=new CreationPrototype{kind="block",physics=new ObjectPhysicsSettings{mode="fixed",shape="automatic",mass=.5f}}}
    }).ToArray()}};
    Assert.That(editor.CreateBatch(batch,out _,out error,deferCreations:true),Is.True,error);remaining-=take;
   }
   string area=NewRegion();var region=editor.ReadRegion(area);region.loading="independent";
   Assert.That(editor.EditRegion(region,area,editor.RegionRevision(area),Array.Empty<string>(),out error),Is.True,error);
   var ids=editor.Snapshot().objects.Where(x=>!x.IsBuiltIn).Select(x=>x.id).ToArray();
   foreach(string id in ids)Assert.That(AssignRegion(id,area,out error),Is.True,error);
   var cold=ids.Where(editor.NativeEntityDormant).ToArray();Assert.That(cold.Length,Is.GreaterThan(240));
   int native=(int)NativeObjectResources.ObserveBudget()["instances"];int old=editor.RegionRevision(area);
   var call=new JObject{["id"]="world.region.save",["version"]=1,["arguments"]=new JObject{
    ["id"]=area,["revision"]=old,["name"]="Large quiet district",["members"]=new JArray(ids),["loading"]="independent"}};
   var request=TemplateRequest(call);request.conditions=ids.Select(id=>new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}).ToArray();
   var executor=new RoomAgentExecutor(editor);
   request.conditions[255].revision--;Assert.That(executor.Execute(request,out error,out _),Is.False);
   Assert.That(editor.RegionRevision(area),Is.EqualTo(old));
   request.conditions[255].revision=editor.ObjectRevision(ids[255]);
   Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return SpatialSettingsCompletion.Wait(executor.Executions);
   var receipt=executor.Executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"));
   Assert.That(((JArray)receipt["resources"]).Values<string>(),Is.EquivalentTo(ids));
   Assert.That(editor.ReadRegion(area).name,Is.EqualTo("Large quiet district"));
   int revision=editor.RegionRevision(area);Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.RegionRevision(area),Is.EqualTo(revision));
   editor.Undo();Assert.That(editor.ReadRegion(area).name,Is.EqualTo(region.name));editor.Redo();Assert.That(editor.ReadRegion(area).name,Is.EqualTo("Large quiet district"));
   Assert.That(cold.All(id=>editor.NativeEntityDormant(id)&&!editor.Find(id)),Is.True);
   Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(native));
  }
 }
}

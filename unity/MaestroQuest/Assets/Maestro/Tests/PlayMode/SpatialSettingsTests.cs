// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        JObject SettingsFact(string id,string target=null)
        {
            Assert.That(BehaviourCatalog.TryRead(id,1,target==null?null:new JObject {["target"]=target},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True,id);
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        JObject SettingsRequest(string id,JObject args)=>new() {["operation"]="start",["runId"]=modeActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]=id,["version"]=1,["arguments"]=args}};
        JObject PhysicsArgs(string id)=>new() {["target"]=id,["revision"]=editor.ObjectRevision(id),["mode"]="bouncy",["shape"]="sphere",["mass"]=2};
        JObject SpatialArgs()=>new() {["target"]="maestro",["revision"]=editor.ObjectRevision("maestro"),["distance"]=1.8,["speed"]=1};
        IEnumerator SaveSettings(string id,JObject args)
        {
            Assert.That(modeActions.Execute(SettingsRequest(id,args),out var error),Is.True,error);yield return null;
            Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),modeActions.Observe().ToString());
        }
        [UnityTest] public IEnumerator SharedSpatialSettingsSaveBeforeSuccessWithoutPlaybackAndHaveOneUndo()
        {
            SharedModes(out _,out _);string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            var beforePhysics=SettingsFact("object.physics.settings",target);var beforeMovement=SettingsFact("avatar.movement.settings");var beforeWalk=SettingsFact("avatar.walk.settings");
            Assert.That(RoomControls.Capabilities(editor),Does.Contain("spatialSettings.v1"));
            yield return SaveSettings("object.physics.configure",PhysicsArgs(target));var physicsResult=modeActions.Observe().DeepClone();var afterPhysics=SettingsFact("object.physics.settings",target);
            var saved=new RoomStorage(directory).Load(out var error);Assert.That(error,Is.Null);Assert.That(saved.objects.Single(x=>x.id==target).mass,Is.EqualTo(2));Assert.That(editor.Find(target).GetComponent<Rigidbody>().mass,Is.EqualTo(2));
            yield return SaveSettings("avatar.movement.configure",SpatialArgs());var movement=modeActions.Observe().DeepClone();var afterMovement=SettingsFact("avatar.movement.settings");
            Assert.That(motion.Distance,Is.EqualTo(1.8f));Assert.That(motion.Speed,Is.EqualTo(1));Assert.That(motion.Active||authoring.IsPlaying,Is.False);
            var walkBeforeSave=SettingsFact("avatar.walk.settings");
            var request=SettingsRequest("avatar.walk.select",new JObject {["target"]="maestro",["revision"]=editor.ObjectRevision("maestro"),["source"]="included"});Assert.That(modeActions.Execute(request,out error),Is.True,error);yield return null;var walk=modeActions.Observe().DeepClone();var afterWalk=SettingsFact("avatar.walk.settings");
            Assert.That(modeActions.Execute(request,out error),Is.True,error);Assert.That(JToken.DeepEquals(afterWalk,SettingsFact("avatar.walk.settings")),Is.True,"Duplicate receipt cannot resave");
            editor.Undo();Assert.That(editor.Read(target).mass,Is.EqualTo(2));Assert.That(motion.Speed,Is.EqualTo(.65f),"Selecting the already included walk adds no spurious Undo");editor.Undo();Assert.That(editor.Read(target).mass,Is.EqualTo(.5f));
            editor.Redo();Assert.That(editor.Read(target).mass,Is.EqualTo(2));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_SPATIAL_SETTINGS");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"settings.json"),new JObject {["beforePhysics"]=beforePhysics,["beforeMovement"]=beforeMovement,["beforeWalk"]=beforeWalk,["physics"]=physicsResult,["afterPhysics"]=afterPhysics,["movement"]=movement,["afterMovement"]=afterMovement,["walkBeforeSave"]=walkBeforeSave,["walk"]=walk,["afterWalk"]=afterWalk}.ToString());}
        }
        [UnityTest] public IEnumerator BookGeneratedBehaviourUsesFreshGuardsAndPreservesLivePreferencesOnLaterRuns()
        {
            SharedModes(out _,out _);
            var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/current-input-program.json"));
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Book current inputs",program=source};var runs=new JArray();
            modeRules.Scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});
            foreach(float distance in new[]{1.6f,2.2f}){
                Assert.That(editor.ConfigureMovement(editor.ObjectRevision("maestro"),new AvatarMovementSettings {distance=distance,speed=.3f},out var error),Is.True,error);
                var before=SettingsFact("avatar.movement.settings");int revision=editor.ObjectRevision("maestro");Assert.That(motion.Speed,Is.EqualTo(.3f),"Saving a definition must not execute it");
                Assert.That(modeRules.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,modeRules.Scheduler.LastError);
                float until=Time.unscaledTime+2;while(modeRules.Scheduler.RunningCount>0&&Time.unscaledTime<until)yield return null;
                Assert.That(modeRules.Scheduler.RunningCount,Is.Zero);Assert.That(modeRules.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),modeRules.Scheduler.LastError);
                Assert.That(motion.Distance,Is.EqualTo(distance));Assert.That(motion.Speed,Is.EqualTo(.9f));Assert.That(editor.ObjectRevision("maestro"),Is.GreaterThan(revision));Assert.That(motion.Active,Is.False);
                var saved=new RoomStorage(directory).Load(out error);Assert.That(error,Is.Null);Assert.That(saved.objects.Single(x=>x.id=="maestro").followDistance,Is.EqualTo(distance));
                runs.Add(new JObject {["before"]=before,["after"]=SettingsFact("avatar.movement.settings"),["phase"]=modeRules.Scheduler.Outcomes.Last().phase});
            }
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_REUSABLE_INPUTS");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"program.json"),new JObject {["program"]=JObject.Parse(source),["runs"]=runs,["inactive"]=!motion.Active}.ToString());}
        }
        [UnityTest] public IEnumerator SharedSettingsPreserveOtherActorsAndLivePhysicsPositions()
        {
            SharedModes(out _,out _);string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;var item=editor.Find(target);
            Assert.That(motion.Begin("existing follow",AvatarSpatialMode.Follow,out var error),Is.True,error);var stale=SettingsRequest("avatar.movement.configure",SpatialArgs());
            Assert.That(modeActions.Execute(stale,out error),Is.False);Assert.That(motion.OwnedBy("existing follow"),Is.True);Assert.That(motion.Speed,Is.EqualTo(.65f));
            item.transform.position=new Vector3(2,2,1);Physics.SyncTransforms();var position=item.transform.position;
            yield return SaveSettings("object.physics.configure",PhysicsArgs(target));Assert.That(motion.OwnedBy("existing follow"),Is.True,"An object settings save must not globally stop Maestro");
            Assert.That(Vector3.Distance(item.transform.position,position),Is.LessThan(.1f));Assert.That(editor.Read(target).position.y,Is.GreaterThan(1.9f));Assert.That(item.GetComponent<RigidRoomItem>().Simulating,Is.True);motion.Stop();
        }
        [UnityTest] public IEnumerator FailedAndStaleSettingsWritesKeepAcceptedStateForManualAndSharedPaths()
        {
            SharedModes(out _,out _);string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            var stale=SettingsRequest("avatar.movement.configure",SpatialArgs());editor.SetAvatarMovement(2,.4f);Assert.That(modeActions.Execute(stale,out var error),Is.False);StringAssert.Contains("changed",error);
            world.PausePhysics();editor.SaveNow();var before=SettingsFact("object.physics.settings",target);string file=Path.Combine(directory,"room.v14.json"),original=File.ReadAllText(file),pending=file+".pending";Directory.CreateDirectory(pending);
            try{
                Assert.That(modeActions.Execute(SettingsRequest("object.physics.configure",PhysicsArgs(target)),out _),Is.False);yield return null;
                Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("failed"));Assert.That(modeActions.Observe()["selected"]["output"],Is.Null);Assert.That(JToken.DeepEquals(before,SettingsFact("object.physics.settings",target)),Is.True);
                Assert.That(editor.SetItemPhysics(target,new ObjectPhysicsSettings {mode="fixed",shape="box",mass=3}),Is.False);Assert.That(editor.Read(target).mass,Is.EqualTo(.5f));Assert.That(File.ReadAllText(file),Is.EqualTo(original));
            }finally{Directory.Delete(pending);}
            yield return SaveSettings("object.physics.configure",PhysicsArgs(target));Assert.That(editor.Read(target).mass,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator SpatialSettingsRespectTemporaryDiscardPreservationAndReservedTargets()
        {
            SharedModes(out _,out _);string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            var invalid=PhysicsArgs(target);invalid["target"]="book";Assert.That(BehaviourCatalog.TryCall("object.physics.configure",1,invalid,out _,out _),Is.False);
            string error;using(editor.WriteGate.TryFreeze(out error)){Assert.That(error,Is.Null);Assert.That(modeActions.Execute(SettingsRequest("object.physics.configure",PhysicsArgs(target)),out _),Is.False);}
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            yield return SaveSettings("object.physics.configure",PhysicsArgs(target));yield return SaveSettings("avatar.movement.configure",SpatialArgs());Assert.That((bool)modeActions.Observe()["selected"]["output"]["temporary"],Is.True);
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).mass,Is.EqualTo(.5f));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(target).mass,Is.EqualTo(.5f));Assert.That(motion.Speed,Is.EqualTo(.65f));
        }
    }
    public sealed partial class LibraryRuleAndWalkTests
    {
        JObject WalkFact(){Assert.That(BehaviourCatalog.TryRead("avatar.walk.settings",1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);}
        JObject WalkCall(string source)=>new() {["id"]="avatar.walk.select",["version"]=1,["arguments"]=new JObject {["target"]="maestro",["revision"]=editor.ObjectRevision("maestro"),["source"]=source}};
        [UnityTest] public IEnumerator SharedWalkSelectionUsesExactLibraryAndEmbeddedIdentitiesAndDoesNotPlay()
        {
            var executions=new RoomExecutions(editor);var before=WalkFact();var embedded=WalkCall("embedded");embedded["arguments"]["modelHash"]=avatar.ModelHash;embedded["arguments"]["clipIndex"]=0;
            Assert.That(executions.Execute(new JObject {["operation"]="start",["call"]=embedded},out var error),Is.True,error);yield return null;Assert.That(avatar.WalkClip,Is.Zero);var embeddedView=executions.Observe().DeepClone();
            var library=WalkCall("library");library["arguments"]["motionId"]=gait.id;Assert.That(executions.Execute(new JObject {["operation"]="start",["call"]=library},out error),Is.True,error);yield return null;var libraryView=executions.Observe().DeepClone();var after=WalkFact();
            Assert.That(avatar.WalkMotionId,Is.EqualTo(gait.id));Assert.That(avatar.LibraryMotionId,Is.Null,"Choosing a walk cannot start its playback");Assert.That(authoring.IsPlaying,Is.False);
            Assert.That((string)after["selection"]["motionId"],Is.EqualTo(gait.id));Assert.That((bool)after["selection"]["available"],Is.True);
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").walkMotionId,Is.EqualTo(gait.id));editor.Undo();Assert.That(avatar.WalkClip,Is.Zero);editor.Redo();Assert.That(avatar.WalkMotionId,Is.EqualTo(gait.id));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_SPATIAL_SETTINGS");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"walk.json"),new JObject {["before"]=before,["embedded"]=embeddedView,["library"]=libraryView,["after"]=after}.ToString());}
        }
        [UnityTest] public IEnumerator SharedEmbeddedWalkDiscoveryPagesExactIndexesAndRejectsChangedModel()
        {
            var asset=ModelLibrary.Inspect("many-clips.glb",ModelFixture.Mixamo(json=>{
                var source=json["animations"][0].DeepClone();var clips=new JArray();for(int i=0;i<32;i++){var clip=source.DeepClone();clip["name"]=i+new string('\\',100)+"😀";clips.Add(clip);}json["animations"]=clips;
            }));var save=editor.Models.SaveAsync(asset);yield return Until(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);string old=avatar.ModelHash;
            Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);yield return Until(()=>!avatar.ModelBusy);
            var pages=new JArray();var indexes=new System.Collections.Generic.List<int>();
            foreach(int offset in Enumerable.Range(0,11).Select(i=>i*3).Append(32)){
                var args=new JObject {["modelHash"]=asset.Hash,["offset"]=offset};Assert.That(BehaviourCatalog.TryRead("avatar.walk.clips",1,args,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
                Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));var page=JObject.FromObject(value.Value);Assert.That((int)page["total"],Is.EqualTo(32));Assert.That(page["entries"].Count(),Is.EqualTo(System.Math.Min(3,32-offset)));indexes.AddRange(page["entries"].Select(e=>(int)e["index"]));pages.Add(new JObject {["arguments"]=args,["value"]=page});
            }
            Assert.That(indexes,Is.EqualTo(Enumerable.Range(0,32)));Assert.That(BehaviourCatalog.TryRead("avatar.walk.clips",1,new JObject {["modelHash"]=old,["offset"]=0},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_SPATIAL_SETTINGS");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"clips.json"),pages.ToString());}
        }
        [UnityTest] public IEnumerator SharedWalkSelectionRejectsForeignModelsMissingFilesAndStaleManualChoices()
        {
            var executions=new RoomExecutions(editor);var embedded=WalkCall("embedded");embedded["arguments"]["modelHash"]=new string('f',64);embedded["arguments"]["clipIndex"]=0;Assert.That(executions.Execute(new JObject {["operation"]="start",["call"]=embedded},out _),Is.False);Assert.That(avatar.WalkClip,Is.EqualTo(-1));
            var stale=WalkCall("included");Assert.That(editor.SetAvatarWalkMotion(gait.id),Is.True);Assert.That(executions.Execute(new JObject {["operation"]="start",["call"]=stale},out _),Is.False);
            File.Delete(Path.Combine(directory,"motions",gait.hash+".motion.glb"));var unavailable=WalkFact();Assert.That((bool)unavailable["selection"]["available"],Is.False);Assert.That((string)unavailable["selection"]["motionId"],Is.EqualTo(gait.id));
            var library=WalkCall("library");library["arguments"]["motionId"]=gait.id;Assert.That(executions.Execute(new JObject {["operation"]="start",["call"]=library},out _),Is.False);Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(gait.id));
            Assert.That(executions.Execute(new JObject {["operation"]="start",["call"]=WalkCall("included")},out var error),Is.True,error);yield return null;Assert.That((string)WalkFact()["selection"]["source"],Is.EqualTo("included"));
        }
    }
}

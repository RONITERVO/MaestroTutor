// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest]public IEnumerator WaterTraversalUsesFreshCreationBoundsAfterAnImmediateTransformChange(){
            var(_,prop)=MediumFixture(.05f);physics.PausePhysics();
            Assert.That(editor.ConfigureWaterTraversal(prop,editor.ObjectRevision(prop),new(){mode="avoid"},out var e),Is.True,e);
            bool auto=Physics.autoSyncTransforms;Physics.autoSyncTransforms=false;
            try{
                Physics.SyncTransforms();var actor=editor.Find(prop);actor.transform.position=new Vector3(3,3,0);
                Assert.That(editor.WaterMotionStep(actor,new Vector3(5,3,0),out var hit),Is.True,hit.Reason);
                actor.transform.position=new Vector3(3,1.4f,0);
                Assert.That(editor.WaterMotionStep(actor,new Vector3(5,1.4f,0),out hit),Is.False);StringAssert.Contains("water",hit.Reason);
            }finally{Physics.autoSyncTransforms=auto;}
            yield return null;
        }
        [UnityTest]public IEnumerator WaterTraversalAlsoStopsAnOptedInCreationsRecordedMovement(){
            var(_,prop)=MediumFixture(.05f);physics.PausePhysics();
            Assert.That(editor.ConfigureWaterTraversal(prop,editor.ObjectRevision(prop),new(){mode="avoid"},out var e),Is.True,e);
            var start=new Vector3(3,1.4f,0);var end=new Vector3(5,1.4f,0);
            Assert.That(editor.SaveAnimation(prop,new(){frames=new[]{new MotionFrame{position=start,scale=.5f},new MotionFrame{time=.01f,position=end,scale=.5f}}},null,false),Is.True,editor.Status);
            var actor=editor.Find(prop);actor.transform.position=start;actor.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
            var operation=new RecordedMotionOperation(new CapabilityContext(editor,animations),new JObject{["target"]=prop,["seconds"]=1,["loop"]=false});
            try{
                Assert.That(operation.Begin(false,out e),Is.True,e);yield return new WaitForSeconds(.03f);operation.Tick();
                Assert.That(operation.State(out e),Is.EqualTo(Maestro.Quest.Rules.RuleActionState.Failed));StringAssert.Contains("water",e);Assert.That(actor.transform.position.x,Is.EqualTo(3).Within(.001f));
            }finally{operation.Stop(false);}
        }
        [UnityTest]public IEnumerator WaterTraversalUsesLiveDepthAndRemainsInForceWhilePhysicsIsPaused(){
            var(pool,prop)=MediumFixture(.05f);physics.PausePhysics();
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="wade",maxDepthMetres=.15f},out var e),Is.True,e);
            var actor=editor.Find("maestro");var from=new Vector3(3,1.4f,0);var to=new Vector3(5,1.4f,0);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out var hit),Is.False);Assert.That(hit.BodyId,Is.EqualTo(pool));StringAssert.Contains("too deep",hit.Reason);
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="wade",maxDepthMetres=.25f},out e),Is.True,e);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.True,hit.Reason);Assert.That(hit.Depth,Is.EqualTo(.2f).Within(.002));
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,.2f,out hit),Is.False,"Small actors cannot wade over their head");
            physics.SetSurfaces(false,"Scan unavailable");Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.False);StringAssert.Contains("environment",hit.Reason);
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="ignore"},out e),Is.True,e);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.True);
            Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));yield return null;
        }
        [UnityTest]public IEnumerator WaterTraversalSettingsUseSharedReceiptsUndoAndStaleGuards(){
            var(pool,prop)=MediumFixture(.05f);physics.PausePhysics();var ex=new RoomAgentExecutor(editor);
            var call=new JObject{["id"]="object.water.traversal.configure",["version"]=1,["arguments"]=new JObject{["target"]=prop,["revision"]=editor.ObjectRevision(prop),["mode"]="wade",["maxDepthMetres"]=.18}};
            var stale=(JObject)call.DeepClone();ContainerRun(ex,call);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain("waterTraversal.v1"));
            Assert.That(editor.Find(prop).WaterTraversal.mode,Is.EqualTo("wade"));Assert.That(System.Array.Find(new RoomStorage(directory).Load(out var e).objects,x=>x.id==prop).waterTraversal.mode,Is.EqualTo("wade"),e);
            Assert.That(editor.CanConfigureWaterTraversal(prop,(int)stale["arguments"]["revision"],new(){mode="avoid"},out _),Is.False);
            editor.Undo();Assert.That(editor.Find(prop).WaterTraversal.mode,Is.EqualTo("ignore"));editor.Redo();Assert.That(editor.Read(prop).waterTraversal.maxDepthMetres,Is.EqualTo(.18f));
            Assert.That(WaterTraversalCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]=prop},out _),Is.True);
            Assert.That(editor.CanConfigureWaterTraversal("book",editor.ObjectRevision("book"),new(),out _),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator WaterTraversalRefusedSaveAndTemporaryDiscardPreserveAcceptedSettings(){
            var(_,prop)=MediumFixture(.05f);physics.PausePhysics();editor.SaveNow();string file=Path.Combine(directory,RoomStorage.FileName),raw=File.ReadAllText(file),pending=file+".pending";Directory.CreateDirectory(pending);
            try{
                Assert.That(editor.ConfigureWaterTraversal(prop,editor.ObjectRevision(prop),new(){mode="avoid"},out _),Is.False);
                Assert.That(editor.Find(prop).WaterTraversal.mode,Is.EqualTo("ignore"));Assert.That(File.ReadAllText(file),Is.EqualTo(raw));
            }finally{Directory.Delete(pending);}
            Assert.That(editor.BeginTemporaryRoom(out var e),Is.True,e);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.ConfigureWaterTraversal(prop,editor.ObjectRevision(prop),new(){mode="avoid"},out e),Is.True,e);
            Assert.That(editor.Find(prop).WaterTraversal.mode,Is.EqualTo("avoid"));
            Assert.That(editor.DiscardTemporaryRoom(out e),Is.True,e);Assert.That(editor.Find(prop).WaterTraversal.mode,Is.EqualTo("ignore"));
        }
        [UnityTest]public IEnumerator WaterTraversalUsesEachActorsGlassBoxAndDoesNotHideUnreadyWater(){
            var(pool,prop)=MediumFixture(.05f);physics.PausePhysics();physics.Contains=p=>p.y>=0;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(root.transform,false);ground.transform.position=new Vector3(4,-2.1f,0);ground.transform.localScale=new Vector3(4,.2f,4);ground.layer=RoomPhysicsLayers.Environment;ground.AddComponent<RoomWalkableSurface>().Publish(ground.GetComponent<Collider>());
            var profile=NewEnvironment();Assert.That(AssignEnvironment(pool,profile,out var e),Is.True,e);Assert.That(AssignEnvironment(prop,profile,out e),Is.True,e);
            Assert.That(editor.ConfigureWaterTraversal(prop,editor.ObjectRevision(prop),new(){mode="wade",maxDepthMetres=.3f},out e),Is.True,e);
            foreach(string id in new[]{pool,prop}){var item=editor.Find(id);item.transform.position+=Vector3.down*2.5f;item.GetComponent<RigidRoomItem>().Teleported();}
            Physics.SyncTransforms();physics.SetSurfaces(false,"No scan");var from=new Vector3(3,-1.1f,0);var to=new Vector3(5,-1.1f,0);var actor=editor.Find(prop);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out var hit),Is.True,hit.Reason);
            var body=editor.Find(pool).GetComponent<RigidRoomItem>();body.SetGeometryReady(false);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.False);StringAssert.Contains("unavailable",hit.Reason);body.SetGeometryReady(true);
            Assert.That(AssignEnvironment(pool,"",out e),Is.True,e);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.False,"A virtual actor cannot borrow admission for a real-room vessel");yield return null;
        }
        [UnityTest]public IEnumerator WaterTraversalReactsToLiveRainBeforeTheQuantityIsSaved(){
            var(pool,_)=MediumFixture(.05f);editor.Find("maestro").WaterTraversal=new(){mode="wade",maxDepthMetres=.2f};
            var from=new Vector3(3,1.4f,0);var to=new Vector3(5,1.4f,0);var actor=editor.Find("maestro");
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out var hit),Is.True,hit.Reason);
            Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=120},0,out var e),Is.True,e);
            // A deliberately close threshold catches the first visible live change.
            actor.WaterTraversal.maxDepthMetres=.199001f;editor.Liquids.Tick(.1f);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.False);
            Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));physics.PausePhysics();yield return null;
        }
    }
    public sealed partial class AvatarSpatialTests {
        string WaterBasin(){
            Surface(new Vector3(0,-.1f,0),new Vector3(12,.2f,12));Tutor();editor.Liquids.enabled=false;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Walking pool",new Vector3(.8f,-.2f,0),1,Color.white,out var id,out var e),Is.True,e);
            Assert.That(editor.EditContainer(id,editor.ObjectRevision(id),new(){version=2,rectangle=new(){width=1,depth=1},frame=new(){position=new Vector3(0,.2f,0)},height=.3f,capacityMl=300,amountMl=200},out e),Is.True,e);
            Assert.That(editor.SetItemPhysics(id,new ObjectPhysicsSettings{mode="fixed",shape="box",mass=1}),Is.True,editor.Status);Ready();
            Assert.That(navigation.Prepare(.25f,1.7f,out e,editor.Find("maestro")),Is.True,e);return id;
        }
        [UnityTest]public IEnumerator WaterTraversalKeepsManualAndAuthoredWalkingLiteral(){
            WaterBasin();var to=new Vector3(1.6f,0,0);
            Assert.That(navigation.Traverse(Vector3.zero,to,_=>false,out _,out var e),Is.False);StringAssert.Contains("avoids water",e);
            Assert.That(navigation.ClearAuthoredStep(Vector3.zero,to,0,_=>false),Is.False);StringAssert.Contains("avoids water",navigation.WaterBlocker);
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="wade",maxDepthMetres=.25f},out e),Is.True,e);
            Assert.That(navigation.Traverse(Vector3.zero,to,_=>false,out _,out e),Is.True,e);yield return null;
        }
        [UnityTest]public IEnumerator WaterTraversalPreventsSkippedRecordedFramesAndPhysicalPreviewFromCrossingWater(){
            WaterBasin();var saved=new RoomMotion{frames=new[]{new MotionFrame{position=Vector3.zero},new MotionFrame{time=.01f,position=new Vector3(1.6f,0,0)}}};
            Assert.That(editor.SaveAnimation("maestro",saved,null,false),Is.True,editor.Status);
            var args=new JObject{["target"]="maestro",["seconds"]=1,["loop"]=false};
            var operation=new RecordedMotionOperation(new CapabilityContext(editor,authoring),args);
            try{
                Assert.That(operation.Begin(false,out var e),Is.True,e);yield return new WaitForSeconds(.03f);operation.Tick();
                Assert.That(operation.State(out e),Is.EqualTo(Maestro.Quest.Rules.RuleActionState.Failed));StringAssert.Contains("water",e);
                Assert.That(avatar.transform.position.x,Is.LessThan(.05f));
            }finally{operation.Stop(false);}
            editor.Select(editor.Find("maestro"));authoring.Play();yield return new WaitForSeconds(.05f);
            Assert.That(authoring.IsPlaying,Is.False);StringAssert.Contains("water",authoring.Status);Assert.That(avatar.transform.position.x,Is.LessThan(.05f));
        }
    }

}

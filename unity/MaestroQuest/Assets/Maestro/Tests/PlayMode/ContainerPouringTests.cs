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
        (RoomAgentExecutor ex,string from,string to) PouringCups(){
            editor.Liquids.enabled=false;var ex=new RoomAgentExecutor(editor);
            string a=(string)TemplateRun(ex,TemplateCall("cup",new Vector3(3,1.8f,0)))["selected"]["output"]["objectId"],b=(string)TemplateRun(ex,TemplateCall("cup",new Vector3(3,1,0)))["selected"]["output"]["objectId"];
            var c=editor.Read(a).containers[0];c.amountMl=400;ContainerRun(ex,ContainerCall(a,c));var source=editor.Find(a);source.transform.rotation=Quaternion.Euler(0,0,80);var lip=ContainerFlowGeometry.Lip(c,source.transform,Vector3.up,out _);editor.Find(b).transform.position=new Vector3(lip.x,1,lip.z);
            source.GetComponent<RigidRoomItem>().Teleported();editor.Find(b).GetComponent<RigidRoomItem>().Teleported();
            physics.SetSurfaces(true,"Test room");physics.StartPhysics();return(ex,a,b);
        }
        [UnityTest] public IEnumerator PouringTiltsActualCupTransfersThroughOpeningAndPublishesOneUndo(){
            var(_,from,to)=PouringCups();double accepted=0,spilled=0;int events=0;editor.ContainerPoured+=(id,moved,lost,receivers,liquid)=>{if(id==from){events++;accepted=moved;spilled=lost;Assert.That(receivers,Is.EqualTo(1));}};
            editor.Liquids.Tick(.05f);Assert.That(editor.Liquids.Active,Is.True);var live=editor.Liquids.Observe(from);double left=(double)live["contents"]["amountMl"],right=(double)editor.Liquids.Observe(to)["contents"]["amountMl"];
            Assert.That(right,Is.GreaterThan(0));Assert.That(left+right,Is.EqualTo(400).Within(1e-8));Assert.That(editor.Read(from).containers[0].amountMl,Is.EqualTo(400),"Live quantities must not be saved per simulation tick");Assert.That(editor.Read(to).containers[0].amountMl,Is.Zero);
            Assert.That(editor.CanEditObject(from,true,out _),Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);Assert.That(BehaviourCatalog.TryRead("object.container.live",1,new JObject{["target"]=to},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That((double)((JObject)fact.Value)["contents"]["amountMl"],Is.EqualTo(right));
            CapturePourEvidence();
            physics.PausePhysics();Assert.That(editor.Liquids.Active,Is.False);Assert.That(events,Is.EqualTo(1));Assert.That(accepted,Is.EqualTo(right));Assert.That(spilled,Is.Zero);Assert.That(editor.Read(from).containers[0].amountMl,Is.EqualTo(left));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==to).containers[0].amountMl,Is.EqualTo(right));
            editor.Undo();Assert.That(editor.Read(from).containers[0].amountMl,Is.EqualTo(400));Assert.That(editor.Read(to).containers[0].amountMl,Is.Zero);editor.Redo();Assert.That(editor.Read(to).containers[0].amountMl,Is.EqualTo(right));yield return null;
        }
        [UnityTest] public IEnumerator PouringStreamsStopAtSolidGeometryInsteadOfPassingIntoTheCup(){
            var(_,from,to)=PouringCups();var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform);blocker.transform.position=new Vector3(editor.Find(to).transform.position.x,1.4f,0);blocker.transform.localScale=new Vector3(.5f,.02f,.5f);blocker.layer=RoomPhysicsLayers.Scanned;
            editor.Liquids.Tick(.05f);var observation=editor.Liquids.Observe(from);Assert.That((double)observation["contents"]["spilledMl"],Is.GreaterThan(0));Assert.That((double)editor.Liquids.Observe(to)["contents"]["amountMl"],Is.Zero);var stream=editor.Liquids.GetComponentsInChildren<LineRenderer>().Single(s=>s.enabled);Assert.That(stream.GetPosition(stream.positionCount-1).y,Is.EqualTo(1.41f).Within(.002));Assert.That(stream.positionCount,Is.LessThanOrEqualTo(33));Assert.That(stream.GetComponents<Collider>(),Is.Empty);physics.PausePhysics();yield return null;
        }
        [UnityTest] public IEnumerator PouringFailedPublicationRevertsBothQuantitiesAndRequiresPhysicsRestart(){
            var(_,from,to)=PouringCups();editor.Liquids.Tick(.05f);Assert.That((double)editor.Liquids.Observe(to)["contents"]["amountMl"],Is.GreaterThan(0));string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>events++;
            try{Assert.That(editor.Liquids.Finish(out var error),Is.False);StringAssert.Contains("save",error.ToLowerInvariant());}finally{Directory.Delete(pending);}
            Assert.That(editor.Read(from).containers[0].amountMl,Is.EqualTo(400));Assert.That(editor.Read(to).containers[0].amountMl,Is.Zero);Assert.That((double)editor.Liquids.Observe(to)["contents"]["amountMl"],Is.Zero);Assert.That((string)editor.Liquids.Observe(to)["phase"],Is.EqualTo("failed"));Assert.That(events,Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);editor.Liquids.Tick(.1f);Assert.That(editor.Liquids.Active,Is.False);physics.PausePhysics();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(editor.Liquids.Active,Is.True);physics.PausePhysics();yield return null;
        }
        [UnityTest] public IEnumerator PouringWorksWithTwoRealGripSelectionsWithoutTakingAwayEitherHand(){
            var(_,from,to)=PouringCups();var source=editor.Find(from);var destination=editor.Find(to);var a=Hand(1,source.transform.position);var b=Hand(2,destination.transform.position);manager.SelectEnter((IXRSelectInteractor)a,source.Grab);manager.SelectEnter((IXRSelectInteractor)b,destination.Grab);
            editor.Liquids.Tick(.05f);Assert.That((double)editor.Liquids.Observe(to)["contents"]["amountMl"],Is.GreaterThan(0));Assert.That(source.Grab.isSelected&&destination.Grab.isSelected,Is.True);physics.PausePhysics();Assert.That(editor.Read(to).containers[0].amountMl,Is.GreaterThan(0));manager.SelectExit((IXRSelectInteractor)a,source.Grab);manager.SelectExit((IXRSelectInteractor)b,destination.Grab);yield return null;
        }
        [UnityTest] public IEnumerator PouringTemporaryRoomKeepsItsSavedBaseAndDiscardRestoresContents(){
            var(_,from,to)=PouringCups();physics.PausePhysics();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(editor.KeepTemporaryRoom(out _),Is.False);physics.PausePhysics();Assert.That(editor.Read(to).containers[0].amountMl,Is.GreaterThan(0));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==to).containers[0].amountMl,Is.Zero);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(from).containers[0].amountMl,Is.EqualTo(400));Assert.That(editor.Read(to).containers[0].amountMl,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator PouringHonoursPhysicsPauseAndIncompatibleContentsSpillWithoutMixing(){
            var(ex,from,to)=PouringCups();physics.PausePhysics();editor.Liquids.Tick(.05f);Assert.That(editor.Liquids.Active,Is.False);var drink=editor.Read(to).containers[0];drink.amountMl=50;drink.liquid="Juice";ContainerRun(ex,ContainerCall(to,drink));physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That((double)editor.Liquids.Observe(to)["contents"]["amountMl"],Is.EqualTo(50));Assert.That((double)editor.Liquids.Observe(from)["contents"]["spilledMl"],Is.GreaterThan(0));physics.PausePhysics();Assert.That(editor.Read(to).containers[0].liquid,Is.EqualTo("Juice"));yield return null;
        }
        [UnityTest] public IEnumerator PouringOverflowConservesReceivedAndUncollectedQuantities(){
            var(ex,from,to)=PouringCups();physics.PausePhysics();var full=editor.Read(to).containers[0];full.amountMl=499;ContainerRun(ex,ContainerCall(to,full));physics.StartPhysics();editor.Liquids.Tick(.05f);var source=editor.Liquids.Observe(from)["contents"];double transferred=(double)source["transferredMl"],spilled=(double)source["spilledMl"];
            Assert.That(transferred,Is.EqualTo(1));Assert.That(spilled,Is.GreaterThan(0));Assert.That((double)source["amountMl"]+transferred+spilled,Is.EqualTo(400).Within(1e-8));Assert.That((double)editor.Liquids.Observe(to)["contents"]["amountMl"],Is.EqualTo(500));physics.PausePhysics();yield return null;
        }
        [UnityTest] public IEnumerator PouringPublicationResumesAnOrdinaryTypedEventProgram(){
            var(_,from,to)=PouringCups();physics.PausePhysics();
            var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-container-pour.json")));
            var sequence=workshop.Selected;sequence.program=program.ToString();sequence.repeat=false;Assert.That(new RoomAgentExecutor(editor).Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",sequence=sequence}}}}}},out var error,out _),Is.True,error);
            Assert.That(runtime.Trigger(sequence.id),Is.True);for(int i=0;i<40&&!runtime.Scheduler.IsListening("object.container.poured",from);i++)yield return null;Assert.That(runtime.Scheduler.IsListening("object.container.poured",from),Is.True);
            physics.StartPhysics();editor.Liquids.Tick(.05f);physics.PausePhysics();double transferred=editor.Read(to).containers[0].amountMl;Assert.That(transferred,Is.GreaterThan(0));for(int i=0;i<40&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="hold");i++)yield return null;
            var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.nodeId,Is.EqualTo("hold"));Assert.That(double.Parse(run.state.Single(v=>v.name=="amount").value,System.Globalization.CultureInfo.InvariantCulture),Is.EqualTo(transferred).Within(.001));
        }
        [UnityTest] public IEnumerator PouringComponentShutdownPublishesOnceAndReleasesTheWorkspaceWriter(){
            var(_,from,to)=PouringCups();editor.Liquids.Tick(.05f);double received=(double)editor.Liquids.Observe(to)["contents"]["amountMl"];int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>events++;
            UnityEngine.Object.Destroy(editor.Liquids);yield return null;
            Assert.That(received,Is.GreaterThan(0));Assert.That(editor.Read(to).containers[0].amountMl,Is.EqualTo(received));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);Assert.That(events,Is.EqualTo(1));
            physics.PausePhysics();physics.StartPhysics();physics.PausePhysics();yield return null;Assert.That(events,Is.EqualTo(1),"Destroyed pouring must leave no world listener behind");
        }
        void CapturePourEvidence(){
            string directory=Environment.GetEnvironmentVariable("MAESTRO_GROUP_EVIDENCE");if(string.IsNullOrEmpty(directory))return;Directory.CreateDirectory(directory);var go=new GameObject("Pouring evidence",typeof(Camera));var camera=go.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.9f,.92f,.93f);camera.nearClipPlane=.01f;camera.farClipPlane=5;camera.fieldOfView=45;go.transform.position=new Vector3(3.4f,2,-1.4f);go.transform.LookAt(new Vector3(2.96f,1.38f,0));var render=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,"physical-pour.png"),pixels.EncodeToPNG());Assert.That(pixels.GetPixels32().Count(p=>p.b>p.r+40&&p.b>p.g+20),Is.GreaterThan(100),"The native stream and liquid must render visibly");}finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}
        }
        [UnityTest] public IEnumerator PouringRunsFromNativeUpdateAndEndsAfterTheVesselReturnsUpright(){
            var(_,from,to)=PouringCups();var source=editor.Find(from);var destination=editor.Find(to);var hand=Hand(1,source.transform.position);var other=Hand(2,destination.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,source.Grab);manager.SelectEnter((IXRSelectInteractor)other,destination.Grab);editor.Liquids.enabled=true;
            float deadline=Time.realtimeSinceStartup+3;while((double)editor.Liquids.Observe(to)["contents"]["amountMl"]<=0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((double)editor.Liquids.Observe(to)["contents"]["amountMl"],Is.GreaterThan(0),editor.Liquids.Observe(from)+" source pose "+source.transform.position+" / "+source.transform.eulerAngles);
            hand.transform.rotation=Quaternion.Euler(0,0,-80);deadline=Time.realtimeSinceStartup+3;while(editor.Liquids.Active&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.Liquids.Active,Is.False);Assert.That(editor.Read(to).containers[0].amountMl,Is.GreaterThan(0));physics.PausePhysics();manager.SelectExit((IXRSelectInteractor)hand,source.Grab);manager.SelectExit((IXRSelectInteractor)other,destination.Grab);

        }
    }
}

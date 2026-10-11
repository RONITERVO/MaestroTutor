// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        bool recoveryHeadTracked;
        JObject RecoveryFact(){Assert.That(BehaviourCatalog.TryRead("room.tools.recovery",1,null,new BehaviourCatalog.FactContext(editor:host.Current.Editor),out var value),Is.True);return JObject.FromObject(value.Value);}
        JObject RecoveryRequest(RoomExecutions actions)=>new(){["operation"]="start",["runId"]=actions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="room.tools.recall",["version"]=1,["arguments"]=new JObject {["stateId"]=RecoveryFact()["stateId"].DeepClone(),["revision"]=RecoveryFact()["revision"].DeepClone()}}};
        static RoomItem[] RecoveryTrays(RoomEditor editor)=>editor.GetComponentsInChildren<RoomItem>(true).Where(x=>x.GetComponent<RoomToolTray>()||x.GetComponent<AnimationTools>()||x.GetComponent<RuleTools>()||x.GetComponent<Maestro.Quest.Imports.ImportTools>()||x.GetComponent<PhysicsTools>()||x.GetComponent<Maestro.Quest.Avatar.AvatarSpatialTools>()||x.GetComponent<MovementTools>()).ToArray();
        [UnityTest] public IEnumerator SharedRecallKeepsWorldActivityAndSavesOnlyBookPlacement()
        {
            Open();yield return ReadyHost();recoveryHeadTracked=true;
            var editor=host.Current.Editor;var actions=new RoomExecutions(editor,host);var runtime=editor.GetComponent<RoomRules>();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Playing ball",new Vector3(2,1,3),1,Color.red,out var id,out var error),Is.True,error);
            room.transform.SetPositionAndRotation(new Vector3(-4,0,2),Quaternion.Euler(0,37,0));
            room.Viewer.SetPositionAndRotation(new Vector3(3,1.7f,-2),Quaternion.Euler(0,62,0));
            var worldPosition=room.transform.position;var worldRotation=room.transform.rotation;
            var creation=editor.Find(id);var creationPosition=creation.transform.position;var maestroPosition=editor.Find("maestro").transform.position;
            var originals=editor.Snapshot().objects.Where(x=>x.id!="book").Select(JsonUtility.ToJson).ToArray();var beforeBook=JsonUtility.ToJson(editor.Read("book"));
            var trays=RecoveryTrays(editor);Assert.That(trays.Length,Is.EqualTo(7));foreach(var tray in trays)tray.transform.position+=Vector3.right*8;
            var visible=ToolFact();physics.SetSurfaces(true,"Accepted test floor");physics.StartPhysics();Assert.That(physics.Running,Is.True);
            var body=creation.GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=false;body.linearVelocity=new Vector3(.3f,.2f,-.1f);var velocity=body.linearVelocity;
            Assert.That(runtime.Scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}},Time.unscaledTime,out var waiting,out error),Is.True,error);
            Assert.That(editor.Ownership.TryAcquire("npc-test","NPC activity",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},null,out var actor,out error),Is.True,error);
            using(actor){
                Assert.That((bool)RecoveryFact()["ready"],Is.True,(string)RecoveryFact()["reason"]);
                var beforeRecovery=RecoveryFact();var request=RecoveryRequest(actions);Assert.That(actions.Execute(request,out error),Is.True,error);
                Assert.That(room.transform.position,Is.EqualTo(worldPosition));Assert.That(room.transform.rotation,Is.EqualTo(worldRotation));
                Assert.That(creation.transform.position,Is.EqualTo(creationPosition));Assert.That(editor.Find("maestro").transform.position,Is.EqualTo(maestroPosition));
                Assert.That(body.linearVelocity,Is.EqualTo(velocity));Assert.That(physics.Running,Is.True);Assert.That(actor.Held,Is.True);
                Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));Assert.That(JToken.DeepEquals(visible,ToolFact()),Is.True);
                CollectionAssert.AreEqual(originals,editor.Snapshot().objects.Where(x=>x.id!="book").Select(JsonUtility.ToJson).ToArray());
                var expected=room.Viewer.position-Vector3.up*1.55f+Quaternion.Euler(0,62,0)*new Vector3(0,1,1);
                Assert.That(Vector3.Distance(book.transform.position,expected),Is.LessThan(.001f));
                Assert.That(trays.All(x=>!x.gameObject.activeSelf),Is.True);
                var receipt=actions.Observe();Assert.That((string)receipt["selected"]["phase"],Is.EqualTo("completed"));
                string evidence=System.Environment.GetEnvironmentVariable("MAESTRO_TOOL_RECALL");
                if(!string.IsNullOrEmpty(evidence)){System.IO.Directory.CreateDirectory(evidence);System.IO.File.WriteAllText(System.IO.Path.Combine(evidence,"recall.json"),new JObject {["before"]=beforeRecovery,["receipt"]=receipt,["after"]=RecoveryFact()}.ToString());}
                int revision=editor.Revision;room.Viewer.position+=Vector3.right;
                Assert.That(actions.Execute(request,out error),Is.True,error);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(Vector3.Distance(book.transform.position,expected),Is.LessThan(.001f),"Receipt replay must not recall again");
            }
            body.isKinematic=true;
            var recalledTrays=trays.Select(x=>x.transform.position).ToArray();editor.Undo();
            Assert.That(JsonUtility.ToJson(editor.Read("book")),Is.EqualTo(beforeBook));CollectionAssert.AreEqual(recalledTrays,trays.Select(x=>x.transform.position).ToArray());
            Assert.That(room.transform.position,Is.EqualTo(worldPosition));
        }
        [UnityTest] public IEnumerator RecallRefusesHeldToolsStaleObservationsAndWorkspaceBoundariesAtomically()
        {
            Open();yield return ReadyHost();recoveryHeadTracked=true;var editor=host.Current.Editor;var actions=new RoomExecutions(editor,host);
            var request=RecoveryRequest(actions);Assert.That(editor.MoveObject("book",editor.Read("book").position+Vector3.right,out var error),Is.True,error);
            Assert.That(actions.Execute(request,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            var before=JsonUtility.ToJson(editor.Snapshot());var tray=RecoveryTrays(editor).First();tray.gameObject.SetActive(true);yield return null;
            var manager=root.GetComponent<XRInteractionManager>();var handRoot=new GameObject("Recovery grip");handRoot.SetActive(false);handRoot.transform.SetParent(root.transform,false);handRoot.transform.position=tray.transform.position-Vector3.forward*.25f;
            var hand=handRoot.AddComponent<XRRayInteractor>();hand.enableUIInteraction=false;hand.interactionManager=manager;hand.keepSelectedTargetValid=true;hand.manipulateAttachTransform=false;
            hand.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;hand.selectInput=new XRInputButtonReader {inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};handRoot.SetActive(true);manager.SelectEnter((IXRSelectInteractor)hand,tray.Grab);yield return null;
            var positions=RecoveryTrays(editor).Select(x=>x.transform.position).ToArray();Assert.That(tray.Grab.isSelected,Is.True);
            Assert.That(editor.RecallTools(null,0,true,out _,out error),Is.False);Assert.That(error,Does.Contain("Release"));Assert.That(tray.Grab.isSelected,Is.True);CollectionAssert.AreEqual(positions,RecoveryTrays(editor).Select(x=>x.transform.position).ToArray());Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            manager.SelectExit((IXRSelectInteractor)hand,tray.Grab);handRoot.SetActive(false);
            using(editor.WriteGate.TryFreeze(out _)){Assert.That(editor.RecallTools(null,0,true,out _,out _),Is.False);}
            using(editor.RuntimeGate.Hold("Review workspace")){Assert.That(editor.RecallTools(null,0,true,out _,out _),Is.False);}
            recoveryHeadTracked=false;Assert.That(editor.RecallTools(null,0,true,out _,out error),Is.False);Assert.That(error,Does.Contain("tracking"));
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator RecallIntentExpiresAcrossTemporaryRoomBoundaries()
        {
            Open();yield return ReadyHost();recoveryHeadTracked=true;var editor=host.Current.Editor;var before=RecoveryFact();
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            Assert.That(editor.RecallTools((string)before["stateId"],(int)before["revision"],false,out _,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            var temporary=RecoveryFact();Assert.That(editor.RecallTools((string)temporary["stateId"],(int)temporary["revision"],false,out var result,out error),Is.True,error);Assert.That((bool)result["temporary"],Is.True);
            temporary=RecoveryFact();float deadline=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(editor.TemporarySavePending,Is.False);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.RecallTools((string)temporary["stateId"],(int)temporary["revision"],false,out _,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            Assert.That((string)RecoveryFact()["stateId"],Is.Not.EqualTo((string)before["stateId"]));
        }
        [UnityTest] public IEnumerator RecallSaveFailureLeavesEveryPoseAndJournalUnchanged()
        {
            Open();yield return ReadyHost();recoveryHeadTracked=true;var editor=host.Current.Editor;
            Assert.That(editor.MoveObject("book",new Vector3(0,1,2),out var error),Is.True,error);
            var items=RecoveryTrays(editor).Append(book).ToArray();foreach(var item in items)item.transform.position+=Vector3.right*4;
            var positions=items.Select(x=>x.transform.position).ToArray();var before=JsonUtility.ToJson(editor.Snapshot());var state=RecoveryFact();
            var path=System.IO.Path.Combine(editor.SaveDirectory,RoomStorage.FileName);
            using(var locked=new System.IO.FileStream(path,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.None)){
                Assert.That(editor.RecallTools(null,0,true,out _,out error),Is.False,"A denied room write must not acknowledge recovery");Assert.That(error,Is.Not.Null.And.Not.Empty);
                CollectionAssert.AreEqual(positions,items.Select(x=>x.transform.position).ToArray());Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That((string)RecoveryFact()["stateId"],Is.EqualTo((string)state["stateId"]));
            }
        }
        [UnityTest] public IEnumerator RecallHandlesUniformWorldScaleAndRejectsDistortedToolParents()
        {
            Open();yield return ReadyHost();recoveryHeadTracked=true;var editor=host.Current.Editor;
            room.transform.localScale=Vector3.one*1.25f;room.transform.SetPositionAndRotation(new Vector3(5,0,-4),Quaternion.Euler(0,27,0));room.Viewer.position=new Vector3(-2,1.55f,1);
            Assert.That(editor.RecallTools(null,0,true,out _,out var error),Is.True,error);
            Assert.That(Vector3.Distance(book.transform.position,new Vector3(-2,1,2)),Is.LessThan(.001f));Assert.That(book.transform.lossyScale.x,Is.EqualTo(1).Within(.0001f));
            var loaded=new RoomStorage(editor.SaveDirectory).Load(out error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects.Single(x=>x.id=="book").position,Is.EqualTo(editor.Read("book").position));
            room.transform.localScale=Vector3.one*2;
            bool interrupted=false;Assert.That(editor.Ownership.TryAcquire("scaled-book","Book actor",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("book","wholeTarget")},_=>interrupted=true,out var owner,out error),Is.True,error);
            using(owner){var bookPose=book.transform.position;Assert.That(editor.RecallTools(null,0,true,out _,out error),Is.False);Assert.That(interrupted,Is.False,"Invalid saved bounds must be rejected before taking book ownership");Assert.That(book.transform.position,Is.EqualTo(bookPose));}
            room.transform.localScale=Vector3.one*1.25f;
            var tray=RecoveryTrays(editor).First();var parent=new GameObject("Distorted tool parent");parent.transform.SetParent(editor.transform,false);tray.transform.SetParent(parent.transform,true);parent.transform.localScale=new Vector3(1,2,1);
            var positions=RecoveryTrays(editor).Append(book).Select(x=>x.transform.position).ToArray();var before=JsonUtility.ToJson(editor.Snapshot());
            Assert.That(editor.RecallTools(null,0,true,out _,out error),Is.False);Assert.That(error,Does.Contain("coordinate frame"));CollectionAssert.AreEqual(positions,RecoveryTrays(editor).Append(book).Select(x=>x.transform.position).ToArray());Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator PhysicalRecallOnlyInterruptsBookOwnershipAndKeepsOtherHeldCreations()
        {
            Open();yield return ReadyHost();recoveryHeadTracked=true;var editor=host.Current.Editor;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Held creation",new Vector3(0,1,1),1,Color.red,out var id,out var error),Is.True,error);
            bool bookInterrupted=false;Assert.That(editor.Ownership.TryAcquire("book-program","Book program",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("book","wholeTarget")},_=>bookInterrupted=true,out var owner,out error),Is.True,error);
            var held=editor.Find(id);var manager=root.GetComponent<XRInteractionManager>();
            var handRoot=new GameObject("Held creation grip");handRoot.SetActive(false);handRoot.transform.SetParent(root.transform,false);handRoot.transform.position=held.transform.position-Vector3.forward*.25f;
            var hand=handRoot.AddComponent<XRRayInteractor>();hand.enableUIInteraction=false;hand.interactionManager=manager;hand.keepSelectedTargetValid=true;hand.manipulateAttachTransform=false;
            hand.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;hand.selectInput=new XRInputButtonReader {inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};handRoot.SetActive(true);manager.SelectEnter((IXRSelectInteractor)hand,held.Grab);yield return null;
            Assert.That(held.Grab.isSelected,Is.True);
            using(owner){
                var fact=RecoveryFact();Assert.That(editor.RecallTools((string)fact["stateId"],(int)fact["revision"],false,out _,out error),Is.False);Assert.That(error,Does.Contain("Book program"));
                var position=editor.Find(id).transform.position;input.SendMessage("RestoreRoom");
                Assert.That(bookInterrupted,Is.True);Assert.That(owner.Held,Is.False);Assert.That(held.Grab.isSelected,Is.True);Assert.That(editor.Find(id).transform.position,Is.EqualTo(position));
            }
        }
    }
}

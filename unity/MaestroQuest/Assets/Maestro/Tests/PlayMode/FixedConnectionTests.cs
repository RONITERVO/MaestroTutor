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
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        JObject AttachCall(string moving,string mount,float force=0,float torque=0)=>new(){["id"]="object.connection.edit",["version"]=1,["arguments"]=new JObject{["operation"]="attach",["target"]=moving,["revision"]=editor.ObjectRevision(moving),["connected"]=mount,["breakForce"]=force,["breakTorque"]=torque}};
        (string moving,string mount) FixedPieces(){
            var recipe=new RoomRecipe {parts=new[]{new RecipePart {id="Body",size=Vector3.one*.1f}}};
            Assert.That(editor.CreateRecipe("Joint mount",new Vector3(4,2,4),1,recipe,null,new ObjectPhysicsSettings {mode="fixed",shape="box",mass=1},out var mount,out var error),Is.True,error);
            Assert.That(editor.CreateRecipe("Joined piece",new Vector3(4.2f,2,4),1,recipe,null,new ObjectPhysicsSettings {mode="solid",shape="box",mass=1},out var moving,out error),Is.True,error);return(moving,mount);
        }
        bool Connect(JObject call,out string error){var executor=new RoomAgentExecutor(editor);var request=ObjectEditRequest(call);request.conditions=new[]{(string)call["arguments"]["target"],(string)call["arguments"]["connected"]}.Where(id=>id!=null).Distinct().Select(id=>new RoomObjectCondition {id=id,revision=editor.ObjectRevision(id)}).ToArray();return executor.Execute(request,out error,out _);}
        [UnityTest] public IEnumerator FixedJoinPreservesCurrentOffsetAndRotationUnderGravityAndImpulse(){
            var (id,mount)=FixedPieces();var item=editor.Find(id);item.transform.localRotation=Quaternion.Euler(10,20,30);item.GetComponent<RigidRoomItem>().Teleported();var before=item.transform.localPosition;var rotation=item.transform.localRotation;
            Assert.That(Connect(AttachCall(id,mount),out var error),Is.True,error);Assert.That(item.transform.localPosition,Is.EqualTo(before));Assert.That(editor.Read(id).connections.Single().kind,Is.EqualTo("fixed"));
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();var view=item.GetComponent<RoomConnectionView>();Assert.That(view.Active,Is.True,view.Error);Assert.That(item.GetComponent<FixedJoint>(),Is.Not.Null);Assert.That(item.GetComponent<RigidRoomItem>().ApplyImpulse(Vector3.up*2,out error),Is.True,error);
            for(int i=0;i<20;i++)yield return new WaitForFixedUpdate();Assert.That(Vector3.Distance(item.transform.localPosition,before),Is.LessThan(.02f));Assert.That(Quaternion.Angle(item.transform.localRotation,rotation),Is.LessThan(2));Assert.That(view.Broken,Is.False);
        }
        [UnityTest] public IEnumerator RealBreakIsObservableOnceAndDoesNotReconnectAfterPauseOrMisalignedRearm(){
            var (id,mount)=FixedPieces();Assert.That(Connect(AttachCall(id,mount,.1f),out var error),Is.True,error);var item=editor.Find(id);var view=item.GetComponent<RoomConnectionView>();int breaks=0;string other=null;editor.ConnectionBroken+=(source,connected,kind,force,torque)=>{Assert.That(source,Is.EqualTo(id));Assert.That(kind,Is.EqualTo("fixed"));Assert.That(force,Is.EqualTo(.1f));breaks++;other=connected;};
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();for(int i=0;i<30&&!view.Broken;i++)yield return new WaitForFixedUpdate();Assert.That(view.Broken,Is.True);Assert.That(view.Active,Is.False);Assert.That(breaks,Is.EqualTo(1));Assert.That(other,Is.EqualTo(mount));Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.False);
            physics.PausePhysics();yield return null;physics.StartPhysics();for(int i=0;i<6;i++)yield return new WaitForFixedUpdate();Assert.That(view.Phase,Is.EqualTo("broken"));Assert.That(breaks,Is.EqualTo(1));physics.PausePhysics();item.transform.localPosition+=Vector3.right;item.GetComponent<RigidRoomItem>().Teleported();
            Assert.That(editor.EditConnection(id,editor.ObjectRevision(id),"rearm",mount,null,0,out error),Is.False);Assert.That(view.Broken,Is.True);
            Assert.That(editor.EditConnection(id,editor.ObjectRevision(id),"align",mount,null,0,out error),Is.True,error);Assert.That(view.Broken,Is.False);Assert.That(physics.Running,Is.False);Assert.That(BehaviourCatalog.TryRead("object.connection.state",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That((bool)((JObject)fact.Value)["broken"],Is.False);
        }
        [UnityTest] public IEnumerator NativeConnectionBreakResumesTheSharedTypedEventProgram(){
            var (id,mount)=FixedPieces();Assert.That(Connect(AttachCall(id,mount,.1f),out var error),Is.True,error);
            var program=JObject.Parse(@"{'version':3,'entry':'main','resources':[],'state':[{'name':'other','initial':''}],'events':[],'functions':[{'name':'main','returns':'void','parameters':[],'locals':[{'name':'received','initial':false},{'name':'value','initial':''},{'name':'connected','initial':''}],'body':[{'id':'wait','op':'awaitEvent','event':'object.connection.broken','source':'','timeout':{'value':0},'received':'received','value':'value','fields':{'connected':'connected'}},{'id':'remember','op':'setState','variable':'other','value':{'var':'connected'}},{'id':'hold','op':'sleep','seconds':{'value':20}}]}]}");
            var sequence=workshop.Selected;sequence.program=program.ToString();sequence.repeat=false;Assert.That(new RoomAgentExecutor(editor).Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence}}}}}},out error,out _),Is.True,error);
            Assert.That(runtime.Trigger(sequence.id),Is.True);for(int i=0;i<30&&!runtime.Scheduler.IsListening("object.connection.broken",id);i++)yield return null;Assert.That(runtime.Scheduler.IsListening("object.connection.broken",id),Is.True);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();for(int i=0;i<40&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="hold");i++)yield return new WaitForFixedUpdate();var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.nodeId,Is.EqualTo("hold"));Assert.That(run.state.Single(v=>v.name=="other").value,Is.EqualTo(mount));
        }
        [UnityTest] public IEnumerator AlignedRearmChangesOnlyPlayStateAndCanBreakAgain(){
            var (id,mount)=FixedPieces();Assert.That(Connect(AttachCall(id,mount,.1f),out var error),Is.True,error);var item=editor.Find(id);var position=item.transform.localPosition;var rotation=item.transform.localRotation;var view=item.GetComponent<RoomConnectionView>();var saved=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int breaks=0;editor.ConnectionBroken+=(_,__,___,____,_____)=>breaks++;
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();for(int i=0;i<30&&!view.Broken;i++)yield return new WaitForFixedUpdate();Assert.That(view.Broken,Is.True);physics.PausePhysics();yield return null;
            item.transform.localPosition=position;item.transform.localRotation=rotation;item.GetComponent<RigidRoomItem>().Teleported();var call=new JObject {["id"]="object.connection.edit",["version"]=1,["arguments"]=new JObject {["operation"]="rearm",["target"]=id,["connected"]=mount,["revision"]=editor.ObjectRevision(id)}};
            Assert.That(Connect(call,out error),Is.True,error);Assert.That(view.Broken,Is.False);Assert.That(physics.Running,Is.False);Assert.That(File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(saved));Assert.That(breaks,Is.EqualTo(1));
            physics.StartPhysics();for(int i=0;i<30&&!view.Broken;i++)yield return new WaitForFixedUpdate();Assert.That(view.Broken,Is.True);Assert.That(breaks,Is.EqualTo(2));physics.PausePhysics();editor.Undo();Assert.That(editor.Read(id).connections,Is.Empty,"Rearm must not add an authoring Undo entry");yield return null;
        }
        [UnityTest] public IEnumerator FixedJoinFailureStaleSnapshotAndUndoLeaveWholeOriginalState(){
            var (id,mount)=FixedPieces();var call=AttachCall(id,mount);var before=JsonUtility.ToJson(editor.Snapshot());var pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.That(Connect(call,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.Read(id).connections,Is.Empty);}finally{Directory.Delete(pending);}
            Assert.That(Connect(AttachCall(id,mount),out var error),Is.True,error);Assert.That(editor.EditConnection(id,(int)call["arguments"]["revision"],"remove",null,null,0,out _),Is.False);editor.Undo();Assert.That(editor.Read(id).connections,Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator CapturedRigidConnectionRetainsBreakLimitsAndFreshMemberIdentities(){
            var (id,mount)=FixedPieces();Assert.That(Connect(AttachCall(id,mount,45,3),out var error),Is.True,error);var members=new[]{mount,id}.Select((target,index)=>new ConstructionMember {target=target,slot="piece_"+index,revision=editor.ObjectRevision(target)}).ToArray();
            Assert.That(editor.CaptureConstruction(members,out var batch,out error),Is.True,error);Assert.That(batch.blueprint.version,Is.EqualTo(3));var module=ConstructionModule.Definition(batch,"Joined build");ProgramModuleLibrary.Validate(module);
            Assert.That(editor.CreateBatch(batch,out var fresh,out error),Is.True,error);var link=editor.Read(fresh[1]).connections.Single();Assert.That(link.kind,Is.EqualTo("fixed"));Assert.That(link.breakForce,Is.EqualTo(45));Assert.That(link.breakTorque,Is.EqualTo(3));Assert.That(link.connected,Is.EqualTo(fresh[0]));Assert.That(fresh,Does.Not.Contain(id));editor.Undo();Assert.That(editor.Find(fresh[0]),Is.Null);Assert.That(editor.Find(id),Is.Not.Null);yield return null;
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        void SnapPoint(string id,string point,float y,string family="Brick")=>Assert.That(editor.EditSnapPoint(id,editor.ObjectRevision(id),point,new RoomSnapPoint{id=point,name=point,family=family,frame=new ConnectionFrame{position=new Vector3(0,y,0)}},out var error),Is.True,error);
        RoomSnapPlacement SnapTo(string destination,params string[] members)=>new(){members=members.Select(id=>new TransformMember{target=id,revision=editor.ObjectRevision(id)}).ToArray(),point="Bottom",destination=new SnapDestination{target=destination,revision=editor.ObjectRevision(destination),point="Top"}};
        RoomAgentRequest SnapRequest(RoomSnapPlacement value){var args=JObject.Parse(JsonUtility.ToJson(value));if(value.mode=="place"){args.Remove("breakForce");args.Remove("breakTorque");}var request=ObjectEditRequest(new JObject{["id"]="object.layout.snap",["version"]=1,["arguments"]=args});request.conditions=value.members.Select(m=>new RoomObjectCondition{id=m.target,revision=m.revision}).Append(new RoomObjectCondition{id=value.destination.target,revision=value.destination.revision}).ToArray();return request;}
        [UnityTest] public IEnumerator SnapPlaceMovesWholeConstructionAtLiveRotatedScaledDestinationWithOneUndo(){
            var (child,owner)=FixedPieces();Assert.That(Connect(AttachCall(child,owner),out var error),Is.True,error);var destination=LayoutObject(new Vector3(3,2,1));SnapPoint(owner,"Bottom",-.05f);SnapPoint(destination,"Top",.05f);
            root.transform.SetPositionAndRotation(new Vector3(8,0,5),Quaternion.Euler(0,30,0));Assert.That(editor.ResizeObject(destination,1.5f,out error),Is.True,error);
            var target=editor.Find(destination).transform;target.localPosition=new Vector3(3,3,2);target.localRotation=Quaternion.Euler(0,0,90);
            var original=editor.Find(owner).transform.localPosition;var childOffset=editor.Find(child).transform.localPosition-original;var request=SnapTo(destination,owner,child);request.turn=90;
            var executor=new RoomAgentExecutor(editor);var wire=SnapRequest(request);Assert.That(executor.Execute(wire,out error,out _),Is.True,error);
            var moved=editor.Find(owner).transform;var expected=target.localPosition+target.localRotation*(Vector3.up*.075f);
            Assert.That(Vector3.Distance(moved.localPosition+moved.localRotation*(Vector3.down*.05f),expected),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(editor.Find(child).transform.localPosition-moved.localPosition,moved.localRotation*childOffset),Is.LessThan(.0001f));
            Assert.That(editor.Read(child).connections.Single().Aligned(editor.Find(child).transform,moved,out error),Is.True,error);
            Assert.That(editor.Read(owner).connections,Is.Empty);Assert.That(editor.Read(destination).position,Is.EqualTo(target.localPosition));
            editor.Undo();Assert.That(editor.Find(owner).transform.localPosition,Is.EqualTo(original));Assert.That(target.localPosition,Is.EqualTo(new Vector3(3,3,2)));Assert.That(executor.Execute(wire,out error,out _),Is.True,error);Assert.That(editor.Find(owner).transform.localPosition,Is.EqualTo(original));yield return null;
        }
        [UnityTest] public IEnumerator SnapJoinBecomesNativeFixedConstraintAndCaptureKeepsBothPointDefinitions(){
            var (moving,mount)=FixedPieces();SnapPoint(moving,"Bottom",-.05f);SnapPoint(mount,"Top",.05f);var request=SnapTo(mount,moving);request.mode="join";request.breakForce=800;request.breakTorque=100;
            Assert.That(new RoomAgentExecutor(editor).Execute(SnapRequest(request),out var error,out _),Is.True,error);
            var definition=editor.Read(moving).connections.Single();Assert.That(definition.kind,Is.EqualTo("fixed"));Assert.That(definition.breakForce,Is.EqualTo(800));
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();var view=editor.Find(moving).GetComponent<RoomConnectionView>();Assert.That(view.Active,Is.True,view.Error);Assert.That(editor.Find(moving).GetComponent<FixedJoint>(),Is.Not.Null);
            Assert.That(editor.Find(moving).GetComponent<RigidRoomItem>().ApplyImpulse(Vector3.up,out error),Is.True,error);for(int i=0;i<12;i++)yield return new WaitForFixedUpdate();Assert.That(Vector3.Distance(editor.Find(moving).transform.localPosition,editor.Read(moving).position),Is.LessThan(.02f));physics.PausePhysics();
            var members=new[]{mount,moving}.Select((id,i)=>new ConstructionMember{target=id,revision=editor.ObjectRevision(id),slot="piece"+i}).ToArray();Assert.That(editor.CaptureConstruction(members,out var batch,out error),Is.True,error);var module=ConstructionModule.Definition(batch,"Snapped pair");ProgramModuleLibrary.Validate(module);
            var encoded=(JObject)module["program"]["functions"][1]["body"][0]["arguments"];Assert.That(editor.CreateBatch(CreationBatch.Read(encoded),out var clones,out error),Is.True,error);Assert.That(editor.Read(clones[0]).snapPoints.Single().id,Is.EqualTo("Top"));Assert.That(editor.Read(clones[1]).snapPoints.Single().id,Is.EqualTo("Bottom"));Assert.That(editor.Read(clones[1]).connections.Single().connected,Is.EqualTo(clones[0]));
        }
        [UnityTest] public IEnumerator SnapRejectsStaleMissingMismatchedAndIncompleteConnectionsWithoutMoving(){
            var (moving,mount)=FixedPieces();SnapPoint(moving,"Bottom",-.05f);SnapPoint(mount,"Top",.05f);var original=JsonUtility.ToJson(editor.Snapshot());
            var request=SnapTo(mount,moving);request.destination.revision--;Assert.That(editor.SnapConstruction(request,out _),Is.False);request=SnapTo(mount,moving);request.members[0].revision--;Assert.That(editor.SnapConstruction(request,out _),Is.False);
            request=SnapTo(mount,moving);request.point="Missing";Assert.That(editor.SnapConstruction(request,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(original));
            SnapPoint(mount,"Top",.05f,"Other");Assert.That(editor.SnapConstruction(SnapTo(mount,moving),out var error),Is.False);Assert.That(error,Does.Contain("family"));SnapPoint(mount,"Top",.05f);
            Assert.That(Connect(AttachCall(moving,mount),out error),Is.True,error);var third=LayoutObject(new Vector3(3,2,2));SnapPoint(third,"Top",.05f);Assert.That(editor.SnapConstruction(SnapTo(third,moving),out error),Is.False);Assert.That(error,Does.Contain("both ends"));
            request=SnapTo(third,moving,mount);request.mode="join";Assert.That(editor.SnapConstruction(request,out error),Is.False);Assert.That(error,Does.Contain("already owns"));yield return null;
        }
        [UnityTest] public IEnumerator SnapFailedSaveAndOutOfBoundsDoNotChangePosesDefinitionsOrUndo(){
            var (moving,mount)=FixedPieces();SnapPoint(moving,"Bottom",-.05f);SnapPoint(mount,"Top",.05f);var before=JsonUtility.ToJson(editor.Snapshot());var p=editor.Find(moving).transform.localPosition;var request=SnapTo(mount,moving);request.mode="join";
            var obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(editor.SnapConstruction(request,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.Find(moving).transform.localPosition,Is.EqualTo(p));Assert.That(editor.Read(moving).connections,Is.Empty);}finally{Directory.Delete(obstacle);}
            editor.Find(mount).transform.localPosition=new Vector3(0,25,0);Assert.That(editor.SnapConstruction(SnapTo(mount,moving),out _),Is.False);Assert.That(editor.Find(moving).transform.localPosition,Is.EqualTo(p));yield return null;
        }
        [UnityTest] public IEnumerator SnapRequiresPausedPhysicsAndIdleOwnershipAndCannotOverwriteManualGrip(){
            var (moving,mount)=FixedPieces();SnapPoint(moving,"Bottom",-.05f);SnapPoint(mount,"Top",.05f);physics.SetSurfaces(true,"Ready");physics.StartPhysics();Assert.That(editor.SnapConstruction(SnapTo(mount,moving),out _),Is.False);physics.PausePhysics();
            Assert.That(editor.Ownership.TryAcquire("test-grip","Manual grip",RoomActorRole.Grab,new[]{new BehaviourCatalog.Claim(mount,"wholeTarget")},_=>{},out var lease,out var error),Is.True,error);
            using(lease){Assert.That(new RoomAgentExecutor(editor).Execute(SnapRequest(SnapTo(mount,moving)),out error,out _),Is.False);}
            yield return null;
        }
        [UnityTest] public IEnumerator SnapPointEditsAndTemporarySnapsRetainBaselineSavedRoom(){
            var (moving,mount)=FixedPieces();SnapPoint(moving,"Bottom",-.05f);SnapPoint(mount,"Top",.05f);var original=editor.Find(moving).transform.localPosition;
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);float deadline=Time.realtimeSinceStartup+10;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(editor.TemporarySavePending,Is.False);Assert.That(editor.TemporaryRoom,Is.True);
            var saved=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==moving).position,Is.EqualTo(original));
            Assert.That(editor.SnapConstruction(SnapTo(mount,moving),out error),Is.True,error);Assert.That(File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(saved));editor.Undo();Assert.That(editor.Find(moving).transform.localPosition,Is.EqualTo(original));
            Assert.That(editor.EditSnapPoint(moving,editor.ObjectRevision(moving),"Bottom",null,out error),Is.True,error);Assert.That(editor.Read(moving).snapPoints,Is.Empty);editor.Undo();Assert.That(editor.Read(moving).snapPoints.Single().id,Is.EqualTo("Bottom"));Assert.That(File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(saved));
        }
    }
}

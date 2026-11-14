// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        bool BeginConnectionTest(string id,string peer,string kind,RoomConnection definition,float amount,
            out RoomEditor.PendingPersistedEdit pending,out string error,System.Action<string> checkpoint=null)=>
            editor.BeginConnection(id,editor.ObjectRevision(id),kind,peer,peer==null?0:editor.ObjectRevision(peer),definition,amount,out pending,out error,checkpoint);
        [UnityTest] public IEnumerator PendingConnectionQueueCapturesIntentAndAdmittedLiveFrames(){
            var (id,peer)=FixedPieces();var args=(Newtonsoft.Json.Linq.JObject)AttachCall(id,peer)["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.connection.edit").Module;Assert.AreEqual("completion",module.Duration);
                Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"join",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);args.RemoveAll();
                Assert.IsFalse(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(editor.Find(peer).GetComponent<RigidRoomItem>().AnimationOwned);
                editor.Find(peer).transform.localPosition+=Vector3.up;var moved=editor.Find(peer).transform.localPosition;var owner=editor.Find(id).transform.localPosition;
                blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(operation);Assert.IsTrue(operation.Complete(out error),error);
                var saved=new RoomStorage(directory).Load(out error);Assert.AreEqual(moved,saved.objects.Single(x=>x.id==peer).position,error);
                Assert.AreEqual(owner,editor.Find(id).transform.localPosition);Assert.AreEqual(peer,editor.Read(id).connections.Single().connected);
                editor.Undo();Assert.IsEmpty(editor.Read(id).connections);Assert.AreEqual(moved,editor.Find(peer).transform.localPosition);Assert.AreEqual(owner,editor.Find(id).transform.localPosition);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingConnectionStalePeerCannotOverwriteHumanEdit(){
            var (id,peer)=FixedPieces();var args=(Newtonsoft.Json.Linq.JObject)AttachCall(id,peer)["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.connection.edit").Module.Start(new CapabilityContext(editor,animations),"join",args,out operation,out error),error);
                blocker.Dispose();yield return Drained(blocker);SnapPoint(peer,"Top",.08f);
                Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("changed",error);
                Assert.AreEqual(.08f,editor.Read(peer).snapPoints.Single().frame.position.y);Assert.IsEmpty(editor.Read(id).connections);
                Assert.IsFalse(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingConnectionCancelOrChangedFrameLeavesBrokenConnectionUntouched(){
            var (id,peer)=FixedPieces();Assert.IsTrue(AttachFixture(id,peer,out var error),error);
            var view=editor.Find(id).GetComponent<RoomConnectionView>();view.RestoreBrokenState(true);
            foreach(bool move in new[]{false,true}){
                var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));var before=editor.Find(id).transform.localPosition;
                Assert.IsTrue(BeginConnectionTest(id,peer,"align",null,0,out var pending,out error),error);
                try{
                    yield return Prepared(pending);Assert.IsTrue(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsTrue(editor.Find(peer).GetComponent<RigidRoomItem>().AnimationOwned);
                    Assert.IsFalse(block.GetComponent<RigidRoomItem>().AnimationOwned);
                    if(move){editor.Find(peer).transform.localPosition+=Vector3.up;Assert.IsFalse(pending.Poll(out _,out error));StringAssert.Contains("moved during preparation",error);}
                    pending.Dispose();yield return Drained(pending);Assert.IsFalse(pending.Accepted);Assert.IsTrue(view.Broken);
                    Assert.AreEqual(before,editor.Find(id).transform.localPosition);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                    Assert.IsFalse(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(editor.Find(peer).GetComponent<RigidRoomItem>().AnimationOwned);
                }finally{pending.Dispose();}
            }
        }
        [UnityTest] public IEnumerator PendingConnectionConfigureAndRemoveKeepMomentumAndCurrentPoseOnUndo(){
            var (id,peer)=FixedPieces();var definition=new RoomConnection{kind="fixed",connected=peer,enabled=false};
            Assert.IsTrue(editor.EditConnection(id,editor.ObjectRevision(id),"configure",peer,definition,0,out var error),error);
            physics.SetSurfaces(true,"Synthetic connection motion continuity");physics.StartPhysics();var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();
            foreach(string kind in new[]{"configure","remove"}){
                definition.breakForce=45;Assert.IsTrue(BeginConnectionTest(id,kind=="remove"?null:peer,kind,definition,0,out var pending,out error),error);
                try{
                    yield return Prepared(pending);Assert.IsFalse(item.GetComponent<RigidRoomItem>().AnimationOwned);
                    body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;var position=body.position;
                    Assert.IsTrue(pending.Poll(out _,out error),error);Assert.IsTrue(pending.Accepted);
                    Assert.AreEqual(Vector3.right,body.linearVelocity);Assert.AreEqual(Vector3.up,body.angularVelocity);Assert.IsFalse(body.isKinematic);
                    for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();Assert.Greater(Vector3.Distance(position,body.position),.001f);
                    yield return Completed(pending);Assert.IsTrue(pending.Complete(out error),error);
                    var now=body.position;var velocity=body.linearVelocity;var spin=body.angularVelocity;editor.Undo();
                    Assert.AreEqual(now,body.position);Assert.AreEqual(velocity,body.linearVelocity);Assert.AreEqual(spin,body.angularVelocity);
                    Assert.IsFalse(editor.Read(id).connections.Single().enabled);
                }finally{pending.Dispose();}
                yield return Drained(pending);
            }
        }
        [UnityTest] public IEnumerator PendingConnectionStopAfterAcceptanceCannotSkipBreakRepair(){
            var (id,peer)=FixedPieces();Assert.IsTrue(AttachFixture(id,peer,out var error),error);
            var view=editor.Find(id).GetComponent<RoomConnectionView>();view.RestoreBrokenState(true);using var pause=new PublicationPause();
            Assert.IsTrue(BeginConnectionTest(id,peer,"align",null,0,out var pending,out error,pause.Check),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(view.Broken);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);
                Assert.IsTrue(pending.Accepted);Assert.IsFalse(view.Broken);pending.Dispose();
                Assert.IsFalse(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(editor.Find(peer).GetComponent<RigidRoomItem>().AnimationOwned);
                pause.Release();yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.IsFalse(pending.Complete(out _));
                physics.SetSurfaces(true,"Synthetic accepted repair");physics.StartPhysics();yield return new WaitForFixedUpdate();Assert.IsTrue(view.Active,view.Error);
            }finally{pause.Release();pending.Dispose();}
        }
        [UnityTest] public IEnumerator PendingConnectionPublicationFailureKeepsAcceptedRepairUnconfirmed(){
            var (id,peer)=FixedPieces();Assert.IsTrue(AttachFixture(id,peer,out var error),error);
            var view=editor.Find(id).GetComponent<RoomConnectionView>();view.RestoreBrokenState(true);var definition=editor.Read(id).connections.Single().Copy();definition.breakForce=100;
            var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            Assert.IsTrue(BeginConnectionTest(id,peer,"configure",definition,0,out var pending,out error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected connection publication failure");}}),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsFalse(view.Broken);
                pause.Release();yield return Rejected(pending);Assert.IsFalse(pending.Complete(out _));
                Assert.AreEqual(100,editor.Read(id).connections.Single().breakForce);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                Assert.IsFalse(BeginConnectionTest(id,null,"remove",null,0,out _,out _));
            }finally{pause.Release();pending.Dispose();}
            yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingConnectionSlideUsesSavedFramesAndUndoRestoresAdmittedPoses(){
            var (id,peer)=FixedPieces();var definition=new RoomConnection{kind="slider",connected=peer};
            Assert.IsTrue(BeginConnectionTest(id,peer,"configure",definition,0,out var configure,out var error),error);
            try{yield return Completed(configure);Assert.IsTrue(configure.Complete(out error),error);}finally{configure.Dispose();}
            yield return Drained(configure);var before=editor.Find(id).transform.localPosition;var mount=editor.Find(peer).transform.localPosition;
            Assert.IsTrue(BeginConnectionTest(id,peer,"slide",null,.025f,out var pending,out error),error);
            try{
                yield return Completed(pending);Assert.IsTrue(pending.Complete(out error),error);Assert.AreEqual(mount+Vector3.right*.025f,editor.Find(id).transform.localPosition);
                editor.Undo();Assert.AreEqual(before,editor.Find(id).transform.localPosition);Assert.AreEqual(mount,editor.Find(peer).transform.localPosition);
            }finally{pending.Dispose();}
            yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingConnectionTemporaryJoinKeepsBaselineAndOneUndo(){
            var (id,peer)=FixedPieces();Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);float end=Time.realtimeSinceStartup+10;
            while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(editor.TemporaryRoom);Assert.IsFalse(editor.TemporarySavePending);var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            var args=(Newtonsoft.Json.Linq.JObject)AttachCall(id,peer)["arguments"];
            Assert.IsTrue(BehaviourCatalog.Action("object.connection.edit").Module.Start(new CapabilityContext(editor,animations),"join",args,out var operation,out error),error);
            try{
                yield return GeometryReady(operation);Assert.IsTrue(operation.Complete(out error),error);Assert.IsEmpty(operation.Result);Assert.IsTrue(editor.TemporaryRoom);
                Assert.AreEqual(peer,editor.Read(id).connections.Single().connected);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                editor.Undo();Assert.IsEmpty(editor.Read(id).connections);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            }finally{operation.Stop(false);}
        }
    }
}

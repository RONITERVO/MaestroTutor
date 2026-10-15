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
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest] public IEnumerator CanonicalDeletionMissingModelPersistsAndUndoRestoresItsExactSavedSource(){
            string target=null;yield return ResidentModelAt(new(4,1,4),id=>target=id);
            string area=NativeAreaFor(target);var saved=editor.Read(target);string source=JsonUtility.ToJson(saved);
            UnityEngine.Object.DestroyImmediate(editor.Find(target).gameObject);editor.RefreshRegionCollision();
            Assert.That(editor.CanEditObject(target,true,out _),Is.False);
            Assert.That(editor.DeleteObject(target,out var error),Is.True,error);Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(editor.HasSavedObject(target),Is.False);Assert.That(editor.Find(target),Is.Null);
            Assert.That(new RoomStorage(directory).Load(out error).objects.Any(x=>x.id==target),Is.False,error);
            editor.Undo();Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(source));Assert.That(editor.RegionFor(target),Is.EqualTo(area));
            float deadline=Time.realtimeSinceStartup+8;
            while(!editor.Find(target).GetComponent<CreatedRoomObject>().ModelGeometryReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.Find(target).GetComponent<CreatedRoomObject>().ModelGeometryReady,Is.True);
            editor.Redo();Assert.That(editor.HasSavedObject(target),Is.False);Assert.That(editor.Find(target),Is.Null);
        }
        [UnityTest] public IEnumerator CanonicalDeletionAgentUsesSavedIdentityAndReceiptReplayCannotDeleteUndo(){
            string target=editor.Identity(block);string source=JsonUtility.ToJson(editor.Read(target));
            UnityEngine.Object.DestroyImmediate(block.gameObject);
            var executor=new RoomAgentExecutor(editor);var call=ObjectEditCall("object.delete",target);var request=ObjectEditRequest(call);
            Assert.That(executor.Catalog.Execute(new JObject{["operation"]="check",["call"]=call},out var error),Is.True,error);
            Assert.That((bool)editor.Find(target),Is.False);Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            string run=(string)request.commands[0].execution["runId"];yield return NativeActionDone(run);
            Assert.That(editor.HasSavedObject(target),Is.False);Assert.That(editor.NativeActivationPending,Is.False);
            request.conditions=new[]{new RoomObjectCondition{id=target,revision=0}};
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.HasSavedObject(target),Is.False,"Receipt reconciliation cannot need a deleted target");
            editor.Undo();Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(source));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.HasSavedObject(target),Is.True);
            var fresh=ObjectEditRequest(call);fresh.conditions=new[]{new RoomObjectCondition{id=target,revision=0}};
            Assert.That(executor.Execute(fresh,out error,out _),Is.False,"A zero guard can only reconcile an old receipt, never authorize a fresh edit");Assert.That(editor.HasSavedObject(target),Is.True);
            Assert.That(editor.Find(target),Is.Not.Null);
        }
        [UnityTest] public IEnumerator CanonicalDeletionDormantTargetDoesNotAcquireOrLoadItsArea(){
            string target=editor.Identity(block),area=NativeAreaFor(target);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(editor.DeleteObject(target,out error),Is.False,"Retirement must drain before cleanup");yield return null;
            Assert.That(editor.NativePhase(target),Is.EqualTo(NativeEntityPhase.Dormant));
            Assert.That(runtime.Scheduler.Invoke(ObjectEditCall("object.delete",target),Time.unscaledTime,out var run,out error),Is.True,error);
            Assert.That(editor.NativeActivationPending,Is.False);yield return NativeActionDone(run);
            Assert.That(editor.HasSavedObject(target),Is.False);Assert.That(editor.NativeEntityDormant(target),Is.False);
            editor.Undo();Assert.That(editor.RegionFor(target),Is.EqualTo(area));Assert.That(editor.Find(target),Is.Not.Null);
        }
        [UnityTest] public IEnumerator CanonicalDeletionDoesNotRecreateMissingPeersAcrossEditUndoAndRedo(){
            string lost=editor.Identity(block);Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Remove me",new(1,1,1),1,Color.white,out var target,out var error),Is.True,error);
            UnityEngine.Object.DestroyImmediate(block.gameObject);string source=JsonUtility.ToJson(editor.Read(lost));int revision=editor.ObjectRevision(lost);
            Assert.That(editor.DeleteObject(target,out error),Is.True,error);
            Assert.That(editor.Find(lost),Is.Null);Assert.That(JsonUtility.ToJson(editor.Read(lost)),Is.EqualTo(source));Assert.That(editor.ObjectRevision(lost),Is.EqualTo(revision));
            editor.Undo();Assert.That(editor.Find(target),Is.Not.Null);Assert.That(editor.Find(lost),Is.Null);
            editor.Redo();Assert.That(editor.Find(lost),Is.Null);Assert.That(editor.HasSavedObject(target),Is.False);
            yield return null;Assert.That(editor.Find(lost),Is.Null);
        }
        [UnityTest] public IEnumerator CanonicalDeletionUnknownPeerGeometryStillBlocksPhysicsAfterCleanup(){
            string lost=null;yield return ResidentModelAt(new(4,1,4),id=>lost=id);StartIsland();
            string target=editor.Identity(block);UnityEngine.Object.DestroyImmediate(editor.Find(lost).gameObject);editor.RefreshRegionCollision();
            Assert.That(physics.Running,Is.False);Assert.That(editor.DeleteObject(target,out var error),Is.True,error);
            Assert.That(editor.Find(lost),Is.Null);Assert.That(physics.SetRunning(true,out error),Is.False);
            Assert.That(error,Does.Contain(lost));Assert.That(editor.ReadCollisionBounds(lost).Known,Is.False);
            Assert.That(editor.DeleteObject(lost,out error),Is.True,error);Assert.That(physics.Running,Is.False);
            Assert.That(physics.SetRunning(true,out error),Is.True,error);yield return null;
        }
        [UnityTest] public IEnumerator CanonicalDeletionRefusesFrozenHeldAnimatedAndBuiltInTargets(){
            string target=editor.Identity(block);string before=JsonUtility.ToJson(editor.Snapshot());
            using(var freeze=editor.WriteGate.TryFreeze(out var error)){
                Assert.That(freeze,Is.Not.Null,error);Assert.That(editor.DeleteObject(target,out _),Is.False);
            }
            var hand=Hand(1,block.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            Assert.That(editor.DeleteObject(target,out _),Is.False);manager.SelectExit((IXRSelectInteractor)hand,block.Grab);
            var rigid=block.GetComponent<RigidRoomItem>();rigid.SetAnimationOwner(this,true);
            Assert.That(editor.DeleteObject(target,out _),Is.False);rigid.SetAnimationOwner(this,false);
            foreach(string id in new[]{"book","maestro"}){UnityEngine.Object.DestroyImmediate(editor.Find(id).gameObject);Assert.That(editor.DeleteObject(id,out _),Is.False);}
            Assert.That(editor.DeleteObject(Guid.NewGuid().ToString("N"),out _),Is.False);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));yield return null;
        }
        [UnityTest] public IEnumerator CanonicalDeletionSaveFailureKeepsSourceHistoryAndUnavailableTarget(){
            string target=editor.Identity(block);Assert.That(editor.TryFlush(out var error),Is.True,error);
            UnityEngine.Object.DestroyImmediate(block.gameObject);string before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            bool undo=editor.CanUndo,redo=editor.CanRedo;string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{
                Assert.That(editor.DeleteObject(target,out error),Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));
                Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That((bool)editor.Find(target),Is.False);
            }finally{Directory.Delete(obstacle);}
            Assert.That(editor.DeleteObject(target,out error),Is.True,error);yield return null;
        }
        [UnityTest] public IEnumerator CanonicalDeletionKeepsMissingConnectionReferencesAndUndoRepairsTheExactLink(){
            var ids=PhysicsIslandChain();string target=ids[1];UnityEngine.Object.DestroyImmediate(editor.Find(target).gameObject);
            var peers=new[]{ids[0],ids[2]};var before=peers.Select(id=>JsonUtility.ToJson(editor.Read(id).connections.Single())).ToArray();
            Assert.That(editor.DeleteObject(target,out var error),Is.True,error);StartIsland();
            for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            for(int i=0;i<peers.Length;i++){
                Assert.That(JsonUtility.ToJson(editor.Read(peers[i]).connections.Single()),Is.EqualTo(before[i]));
                Assert.That((bool)PhysicsInputFact(peers[i])["ready"],Is.False);
            }
            Assert.That(editor.Find(target),Is.Null);physics.PausePhysics();editor.Undo();StartIsland();yield return IslandActive(ids);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        IEnumerator RecoveryFailureImport(bool damaged){
            var asset=ModelLibrary.Inspect("recovery.glb",ModelFixture.Create());
            var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var placement=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>placement.IsCompleted);Assert.That(placement.Exception,Is.Null);
            string target=placement.Result;var data=editor.Read(target);data.position=new(15,3,15);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},System.Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(target,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            string area=NativeAreaFor(target);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Read(target));int revision=editor.ObjectRevision(target);
            string a=PhysicsInputBall(new(-10,3,-10)),b=PhysicsInputBall(new(-9,3,-10)),free=PhysicsInputBall(new(-15,3,0));
            StartRecoveryFixture();yield return IslandActive(new[]{a,b,free});
            var body=editor.Find(b).GetComponent<Rigidbody>();var rigid=body.GetComponent<RigidRoomItem>();
            Assert.That(rigid.Launch(Vector3.right*3,Vector3.up*2),Is.True);int settled=0;rigid.Settled+=_=>settled++;
            // Exercise the real file loader and XRI ownership handoff with a
            // controlled completed-step contact edge, not a native impact claim.
            string path=Path.Combine(directory,"models",asset.Hash+".glb");var original=File.ReadAllBytes(path);
            if(damaged)File.WriteAllBytes(path,ModelFixture.Create(value=>value["asset"]["generator"]="Damaged copy"));else File.Move(path,path+".test-missing");
            try{
                ContinuityContact(a,b);QueueRecovery(a,target);editor.ProcessDormantContacts();
                var(item,hand)=ContactGrab(a);Assert.That(item.Grab.isSelected,Is.True);editor.ProcessDormantContacts();
                yield return NavigationLoadingDone();editor.ProcessDormantContacts();
                var fact=PhysicsInputFact(b);string issue=damaged?"damaged":"missing";
                Assert.That(editor.NativePhase(target),Is.EqualTo(NativeEntityPhase.Failed));Assert.That(editor.Find(target),Is.Null);
                Assert.That((string)fact["state"],Is.EqualTo("unavailable"));Assert.That((bool)fact["interrupted"],Is.True);Assert.That((bool)fact["momentumHeld"],Is.False);
                Assert.That((string)fact["reason"],Does.Contain(issue));Assert.That(body.isKinematic,Is.True);
                Assert.That((bool)PhysicsInputFact(a)["interrupted"],Is.False);Assert.That(item.Grab.isSelected,Is.True);
                Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Simulating,Is.True);
                Assert.That(editor.Status,Does.Contain(issue),"The visible status must leave 'preparing' when loading fails");
                Assert.That(editor.Status,Does.Contain("from rest"));
                if(damaged)File.WriteAllBytes(path,original);else File.Move(path+".test-missing",path);
                for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
                Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(target),Is.Null,"Repairing a file does not silently start a new recovery attempt");
                Assert.That(body.isKinematic,Is.True);Assert.That(settled,Is.Zero);Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(saved));Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));
                using(var repair=editor.DemandNativeEntities(new[]{target},()=>new[]{target},()=>true)){
                    yield return NativeAreaCompletion(repair.Preparation);Assert.That(repair.Error,Is.Null);
                }
                editor.ProcessDormantContacts();
                Assert.That(editor.Find(target).GetComponent<CreatedRoomObject>().ModelGeometryReady,Is.True);
                Assert.That(body.isKinematic,Is.True,"Independent successful loading is not permission to replay the throw");
                Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.True);Assert.That(settled,Is.Zero);
                hand.gameObject.SetActive(false);physics.PausePhysics();StartIsland();yield return IslandActive(new[]{a,b,free});
                Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.False);Assert.That(Mathf.Abs(body.linearVelocity.x),Is.LessThan(.05f));Assert.That(body.angularVelocity.magnitude,Is.LessThan(.05f));
            }finally{
                if(File.Exists(path+".test-missing"))File.Move(path+".test-missing",path);else File.WriteAllBytes(path,original);
            }
        }
        [UnityTest]public IEnumerator RecoveryFailureMissingImportKeepsFreePeerStoppedUntilExplicitRestart()=>RecoveryFailureImport(false);
        [UnityTest]public IEnumerator RecoveryFailureDamagedImportKeepsFreePeerStoppedUntilExplicitRestart()=>RecoveryFailureImport(true);
    }
}

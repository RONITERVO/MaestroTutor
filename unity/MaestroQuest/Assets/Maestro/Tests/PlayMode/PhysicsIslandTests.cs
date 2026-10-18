// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string[] PhysicsIslandChain(bool spread=false,bool fixedMiddle=false){
            var ids=new string[3];var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="Body",size=Vector3.one*.1f}}};
            for(int i=0;i<3;i++)Assert.That(editor.CreateRecipe("Linked member "+i,new Vector3(spread?(i-1)*8:4+(i-1)*.2f,2,4),1,recipe,null,
                new ObjectPhysicsSettings{mode=i==1&&fixedMiddle?"fixed":"solid",shape="box",mass=1},out ids[i],out var error),Is.True,error);
            Assert.That(AttachFixture(ids[0],ids[1],out var issue),Is.True,issue);
            Assert.That(AttachFixture(ids[2],ids[1],out issue),Is.True,issue);return ids;
        }
        void StartIsland(){physics.SetSurfaces(true,"Aligned physics island fixture");physics.StartPhysics();}
        IEnumerator IslandActive(string[] ids){
            for(int i=0;i<6;i++)yield return new WaitForFixedUpdate();
            foreach(var id in ids){var r=editor.Find(id).GetComponent<RigidRoomItem>();if(r.Dynamic)Assert.That(r.Simulating,Is.True,PhysicsInputFact(id).ToString());}
        }
        (string ground,string[] members) PendingPhysicsIsland(){
            var ids=PhysicsIslandChain();string ground=AvailabilityTerrain();
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);
            StartIsland();
            foreach(var id in ids){
                var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();body.isKinematic=false;body.linearVelocity=Vector3.right*3;body.angularVelocity=Vector3.zero;
                item.GetComponent<RigidRoomItem>().Refresh();
                Assert.That(body.isKinematic,Is.True);Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.True);
            }
            return(ground,ids);
        }
        [UnityTest]public IEnumerator PhysicsIslandMissingDistantMemberFreezesTheWholeChainButNotAnUnrelatedBody(){
            var ids=PhysicsIslandChain(true);string free=PhysicsInputBall(new(-18,2,-10));StartIsland();yield return IslandActive(ids);
            var a=editor.Find(ids[0]);int settled=0;a.GetComponent<RigidRoomItem>().Settled+=_=>settled++;
            var before=a.transform.position;float freeHeight=editor.Find(free).transform.position.y;
            editor.Find(ids[2]).gameObject.SetActive(false);
            for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            foreach(var id in ids.Take(2)){Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);Assert.That((bool)PhysicsInputFact(id)["ready"],Is.False);}
            Assert.That(Vector3.Distance(before,a.transform.position),Is.LessThan(.001f));Assert.That(editor.Find(free).transform.position.y,Is.LessThan(freeHeight));Assert.That(physics.Running,Is.True);
            yield return new WaitForSeconds(.7f);Assert.That(settled,Is.Zero,"Unavailable connected input is not settling");
        }
        [UnityTest]public IEnumerator PhysicsIslandPendingPreparationResumesAllMembersWithoutConstraintDeadlock(){
            var(ground,ids)=PendingPhysicsIsland();
            Assert.That(editor.Find(ids[0]).GetComponent<RigidRoomItem>().ApplyImpulse(Vector3.up,out _),Is.False);
            yield return NavigationLoadingDone();yield return IslandActive(ids);
            Assert.That(editor.Find(ground),Is.Not.Null);
            foreach(var id in ids){Assert.That(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x,Is.EqualTo(3).Within(.15f));Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False);}
            Assert.That(editor.Find(ids[0]).GetComponent<RoomConnectionView>().Active,Is.True);Assert.That(editor.Find(ids[2]).GetComponent<RoomConnectionView>().Active,Is.True);
        }
        [UnityTest]public IEnumerator PhysicsIslandPauseCancelsEveryPendingOwnerAndNeverRestoresAnOldThrow(){
            var(ground,ids)=PendingPhysicsIsland();physics.PausePhysics();yield return NavigationLoadingDone();
            Assert.That(editor.Find(ground),Is.Null);foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False);
            var load=editor.ActivateNativeArea(editor.RegionFor(ground));yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Result,Is.Null);
            StartIsland();yield return IslandActive(ids);foreach(var id in ids)Assert.That(Mathf.Abs(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));
        }
        [UnityTest]public IEnumerator PhysicsIslandPeerTeleportDiscardsHeldMomentumAcrossThePendingGroup(){
            var(_,ids)=PendingPhysicsIsland();var peer=editor.Find(ids[1]);peer.GetComponent<RigidRoomItem>().Teleported();
            foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False);
            yield return NavigationLoadingDone();yield return IslandActive(ids);
            foreach(var id in ids)Assert.That(Mathf.Abs(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));
        }
        [UnityTest]public IEnumerator PhysicsIslandAnimationOwnershipCancelsTheWholePendingGroupButNotAnotherConsumer(){
            var(ground,ids)=PendingPhysicsIsland();using var other=editor.DemandNativeEntities(new[]{ground},()=>new[]{ground},()=>true);
            var rigid=editor.Find(ids[1]).GetComponent<RigidRoomItem>();rigid.SetAnimationOwner(this,true);yield return NavigationLoadingDone();
            Assert.That(editor.Find(ground),Is.Not.Null,"The independent consumer still owns its load");
            foreach(var id in ids){Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False);Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);}
            rigid.SetAnimationOwner(this,false);yield return IslandActive(ids);
            foreach(var id in ids)Assert.That(Mathf.Abs(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));
        }
        [UnityTest]public IEnumerator PhysicsIslandMissingJointCannotLetOtherMembersRunUnconstrained(){
            var ids=PhysicsIslandChain();StartIsland();yield return IslandActive(ids);
            var view=editor.Find(ids[0]).GetComponent<RoomConnectionView>();Object.Destroy(view);yield return null;
            for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            foreach(var id in ids){Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);Assert.That((bool)PhysicsInputFact(id)["ready"],Is.False);}
        }
        [UnityTest]public IEnumerator PhysicsIslandDisabledLinkSeparatesGroupsAndDoesNotMergeAreaNeighbours(){
            var ids=PhysicsIslandChain(true);var link=editor.Read(ids[2]).connections.Single();link.enabled=false;
            Assert.That(editor.EditConnection(ids[2],editor.ObjectRevision(ids[2]),"configure",ids[1],link,0,out var error),Is.True,error);
            editor.Find(ids[2]).gameObject.SetActive(false);StartIsland();yield return IslandActive(ids.Take(2).ToArray());
            Assert.That(editor.PhysicsIslandMembers(editor.Find(ids[0])),Is.EquivalentTo(ids.Take(2)));Assert.That(editor.RegionFor(ids[0]),Is.EqualTo(editor.RegionFor(ids[2])));
        }
        [UnityTest]public IEnumerator PhysicsIslandFixedAnchorDoesNotJoinItsSeparateMovingChildren(){
            var ids=PhysicsIslandChain(true,true);editor.Find(ids[2]).gameObject.SetActive(false);StartIsland();yield return IslandActive(ids.Take(2).ToArray());
            Assert.That(editor.Find(ids[0]).GetComponent<RoomConnectionView>().Active,Is.True);
            Assert.That(editor.Find(ids[0]).GetComponent<RigidRoomItem>().ApplyImpulse(Vector3.up,out var error),Is.True,error);
            Assert.That(editor.PhysicsIslandMembers(editor.Find(ids[0])),Is.EquivalentTo(ids.Take(2)));
        }
        [UnityTest]public IEnumerator PhysicsIslandDeletedMemberRemainsMissingAndPassiveInspectionCannotRecreateIt(){
            var ids=PhysicsIslandChain();Assert.That(editor.DeleteObject(ids[1],out var error),Is.True,error);StartIsland();
            var snapshot=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            for(int i=0;i<3;i++){yield return new WaitForFixedUpdate();foreach(var id in new[]{ids[0],ids[2]})Assert.That((bool)PhysicsInputFact(id)["ready"],Is.False);}
            Assert.That(editor.Find(ids[1]),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(snapshot));
            physics.PausePhysics();editor.Undo();StartIsland();yield return IslandActive(ids);
        }
        [UnityTest]public IEnumerator PhysicsIslandPendingWorldTransferPreservesEveryMembersMomentumAndIdentity(){
            var(_,ids)=PendingPhysicsIsland();var before=new RoomFrame(root.transform);
            var revisions=ids.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>().MotionRevision).ToArray();
            root.transform.rotation=Quaternion.Euler(0,90,0);var after=new RoomFrame(root.transform);
            for(int i=0;i<ids.Length;i++){var rigid=editor.Find(ids[i]).GetComponent<RigidRoomItem>();rigid.MoveFrame(before,after);Assert.That(rigid.MotionRevision,Is.EqualTo(revisions[i]));}
            Physics.SyncTransforms();yield return NavigationLoadingDone();yield return IslandActive(ids);
            foreach(var id in ids)Assert.That(editor.Frame.VectorToRoom(editor.Find(id).GetComponent<Rigidbody>().linearVelocity).x,Is.EqualTo(3).Within(.15f));
        }
        [UnityTest]public IEnumerator PhysicsIslandActualJointBreakSeparatesItsFormerMember(){
            var ids=PhysicsIslandChain(true);var link=editor.Read(ids[0]).connections.Single();link.breakForce=.1f;
            Assert.That(editor.EditConnection(ids[0],editor.ObjectRevision(ids[0]),"configure",ids[1],link,0,out var error),Is.True,error);
            StartIsland();yield return IslandActive(ids);var item=editor.Find(ids[0]);var view=item.GetComponent<RoomConnectionView>();
            Assert.That(item.GetComponent<RigidRoomItem>().ApplyImpulse(Vector3.up*3,out error),Is.True,error);
            for(int i=0;i<30&&!view.Broken;i++)yield return new WaitForFixedUpdate();Assert.That(view.Broken,Is.True);
            Assert.That(editor.PhysicsIslandMembers(item),Is.EquivalentTo(new[]{ids[0]}));item.gameObject.SetActive(false);
            yield return IslandActive(ids.Skip(1).ToArray());
        }
        [UnityTest]public IEnumerator PhysicsIslandGripInterruptsPendingMomentumForEveryConnectedMember(){
            var(_,ids)=PendingPhysicsIsland();var item=editor.Find(ids[0]);var hand=Hand(1,item.transform.position);
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);
            Assert.That(item.Grab.isSelected,Is.True,"The diagnostic must establish an actual grip");
            foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False,"member="+id+" selected="+editor.Find(id).Grab.isSelected+" body="+editor.Find(id).GetComponent<Rigidbody>().linearVelocity+" fact="+PhysicsInputFact(id));
            hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;
            manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);
            yield return NavigationLoadingDone();yield return IslandActive(ids);
            foreach(var id in ids)Assert.That(Mathf.Abs(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));
        }
    }
}

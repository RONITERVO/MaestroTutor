// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        (string area,string[] ids) NativePreparationGroup()
        {
            string area=NewRegion();var ids=new List<string>();
            for(int i=0;i<12;i++){
                Assert.That(editor.CreateDrawing("Staged drawing "+i,new Vector3(i*.2f,1,2),1,Color.blue,.004f,new[]{Vector3.zero,Vector3.right*.1f},out var id,out var error),Is.True,error);
                Assert.That(AssignRegion(id,area,out error),Is.True,error);ids.Add(id);
            }
            return(area,ids.ToArray());
        }
        CreatedRoomObject[] PrivateNativeCandidates()=>root.GetComponentsInChildren<CreatedRoomObject>(true)
            .Where(view=>!view.gameObject.activeInHierarchy&&editor.Identity(view.GetComponent<RoomItem>())==null).ToArray();
        IEnumerator WaitForPrivateNativeCandidates(Task<string> task)
        {
            float deadline=Time.realtimeSinceStartup+10;
            while(!task.IsCompleted&&PrivateNativeCandidates().Length==0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(task.IsCompleted,Is.False,"The multi-object group must yield during private native assembly");
            Assert.That(PrivateNativeCandidates().Length,Is.GreaterThan(0));
        }
        int PhysicsSubscriptionCount()
        {
            var field=typeof(RoomPhysicsWorld).GetField(nameof(RoomPhysicsWorld.Changed),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            return ((Delegate)field.GetValue(physics))?.GetInvocationList().Length??0;
        }
        [UnityTest] public IEnumerator NativePreparationCancellationDuringGeometryReleasesUnadoptedMaterialLeases()
        {
            var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="Body",color=new Color(.271f,.583f,.819f)}}};
            var values=Enumerable.Range(0,12).Select(_=>new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Assembly,recipe=recipe.Copy()}).ToArray();
            var previous=Resources.FindObjectsOfTypeAll<GameObject>().Select(x=>x.GetInstanceID()).ToHashSet();
            using var cancel=new CancellationTokenSource();
            var task=RoomEditPreparation.PrepareNativeAsync(editor,values,new RoomPreparationBudget(()=>cancel.Token.ThrowIfCancellationRequested()));
            Assert.That(task.IsCompleted,Is.False,"Geometry preparation must yield before finishing the group");
            var candidates=Resources.FindObjectsOfTypeAll<GameObject>().Where(x=>x.name=="Recipe geometry"&&!previous.Contains(x.GetInstanceID())).ToArray();
            Assert.That(candidates.Length,Is.InRange(1,values.Length-1));Assert.That(candidates.All(x=>!x.activeInHierarchy),Is.True);
            var materials=candidates.SelectMany(x=>x.GetComponentsInChildren<Renderer>(true)).Select(x=>x.sharedMaterial).Distinct().ToArray();
            Assert.That(materials.Length,Is.GreaterThan(0));cancel.Cancel();float deadline=Time.realtimeSinceStartup+10;
            while(!task.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(task.IsCanceled,Is.True);yield return null;
            Assert.That(candidates.All(x=>!x),Is.True);Assert.That(materials.All(x=>!x),Is.True,"The last private lease must release its native material");
            Assert.That(values.All(x=>!editor.HasSavedObject(x.id)&&editor.Find(x.id)==null),Is.True);
        }
        [UnityTest] public IEnumerator NativePreparationPublishesOneCompleteGroupAfterPrivateFrameBoundaries()
        {
            var(area,ids)=NativePreparationGroup();var poses=ids.Select(id=>editor.Find(id).transform.localPosition+Vector3.up*.2f).ToArray();
            for(int i=0;i<ids.Length;i++)editor.Find(ids[i]).transform.localPosition=poses[i];
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision,registered=root.GetComponent<RoomInteraction>().RegisteredCount;
            var frames=new HashSet<int>();var task=editor.ActivateNativeArea(area);float deadline=Time.realtimeSinceStartup+10;
            while(!task.IsCompleted&&Time.realtimeSinceStartup<deadline){
                Assert.That(ids.All(id=>editor.Find(id)==null),Is.True,"No partial public identity group");
                Assert.That(root.GetComponent<RoomInteraction>().RegisteredCount,Is.EqualTo(registered),"No partial picking group");
                var candidates=PrivateNativeCandidates();
                if(candidates.Length>0){
                    frames.Add(Time.frameCount);Assert.That(candidates.Length,Is.LessThan(ids.Length));
                    Assert.That(candidates.SelectMany(c=>c.GetComponentsInChildren<Renderer>(true)).All(r=>!r.gameObject.activeInHierarchy),Is.True);
                    Assert.That(candidates.SelectMany(c=>c.GetComponentsInChildren<Collider>(true)).All(c=>!c.gameObject.activeInHierarchy),Is.True);
                }
                yield return null;
            }
            yield return NativeAreaCompletion(task);Assert.That(frames.Count,Is.GreaterThanOrEqualTo(2));
            Assert.That(root.GetComponent<RoomInteraction>().RegisteredCount,Is.EqualTo(registered+ids.Length));
            for(int i=0;i<ids.Length;i++){
                var item=editor.Find(ids[i]);Assert.That(item.isActiveAndEnabled,Is.True);Assert.That(editor.Identity(item),Is.EqualTo(ids[i]));
                Assert.That(item.transform.localPosition,Is.EqualTo(poses[i]));
            }
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest] public IEnumerator NativePreparationCancellationReleasesNeverActiveMeshesAndSubscriptionsBeforeRetry()
        {
            var(area,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            int subscriptions=PhysicsSubscriptionCount(),registered=root.GetComponent<RoomInteraction>().RegisteredCount;string saved=JsonUtility.ToJson(editor.Snapshot());
            using var cancel=new CancellationTokenSource();var task=editor.ActivateNativeArea(area,cancel.Token);yield return WaitForPrivateNativeCandidates(task);
            var candidates=PrivateNativeCandidates();var meshes=candidates.Select(c=>c.GetComponent<PencilMarks>().GetComponent<MeshFilter>().sharedMesh).ToArray();
            Assert.That(PhysicsSubscriptionCount(),Is.GreaterThan(subscriptions));cancel.Cancel();yield return NativeAreaCompletion(task,false);yield return null;
            Assert.That(candidates.All(c=>!c),Is.True);Assert.That(meshes.All(m=>!m),Is.True);Assert.That(PhysicsSubscriptionCount(),Is.EqualTo(subscriptions));
            Assert.That(root.GetComponent<RoomInteraction>().RegisteredCount,Is.EqualTo(registered));Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.NativeActivationPending,Is.False);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(ids.All(id=>editor.Find(id)?.isActiveAndEnabled==true),Is.True);
        }
        [UnityTest] public IEnumerator NativePreparationBriefRuntimeHoldCancelsAlreadyConstructedCandidates()
        {
            var(area,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var task=editor.ActivateNativeArea(area);yield return WaitForPrivateNativeCandidates(task);
            using(var hold=editor.RuntimeGate.Hold("Pause while native candidates are private")){}
            yield return NativeAreaCompletion(task,false);Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);Assert.That(PrivateNativeCandidates(),Is.Empty);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
        }
        [UnityTest] public IEnumerator NativePreparationNewerSavedEditCannotPublishAnOldCandidate()
        {
            var(area,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var task=editor.ActivateNativeArea(area);yield return WaitForPrivateNativeCandidates(task);
            var changed=editor.Read(ids[0]);changed.position+=Vector3.right;changed.name="Changed during private construction";
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            yield return NativeAreaCompletion(task,false);Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);Assert.That(editor.Read(ids[0]).name,Is.EqualTo(changed.name));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(ids[0]).transform.localPosition,Is.EqualTo(changed.position));
        }
        [UnityTest] public IEnumerator NativePreparationEditorDisableCancelsPrivateCandidatesWithoutLosingSavedContent()
        {
            var(area,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Snapshot());var task=editor.ActivateNativeArea(area);yield return WaitForPrivateNativeCandidates(task);
            editor.enabled=false;editor.enabled=true;yield return NativeAreaCompletion(task,false);
            Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
        }
        [UnityTest] public IEnumerator NativePreparationConnectedPeersAreReadyBeforeSimulationAdmission()
        {
            var(area,ids)=NativePreparationGroup();var(id,mount)=FixedPieces();Assert.That(Connect(AttachCall(id,mount),out var error),Is.True,error);
            Assert.That(AssignRegion(id,area,out error),Is.True,error);string peerArea=NativeAreaFor(mount);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            var task=editor.ActivateNativeArea(peerArea);yield return WaitForPrivateNativeCandidates(task);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Find(mount),Is.Null);
            yield return NativeAreaCompletion(task);Assert.That(ids.All(target=>editor.Find(target)!=null),Is.True);
            var item=editor.Find(id);var view=item.GetComponent<RoomConnectionView>();Assert.That(view.Broken,Is.False);
            Assert.That(view.Active,Is.False,"Publication must not restart physics");physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();
            Assert.That(view.Active,Is.True,view.Error);Assert.That(item.GetComponent<FixedJoint>().connectedBody,Is.SameAs(editor.Find(mount).GetComponent<Rigidbody>()));
        }
    }
}

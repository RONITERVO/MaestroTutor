// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomEditorTests
    {
        IEnumerator ReloadForStartup(RoomDocument saved=null,RoomRuntimeGate gate=null,bool suspend=false,bool observe=true)
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            if(saved!=null)Assert.That(new RoomStorage(directory).Save(saved,out error),Is.True,error);
            UnityEngine.Object.Destroy(root);yield return null;
            root=new GameObject("Reloaded startup");root.AddComponent<XRInteractionManager>();room=root.AddComponent<RoomInteraction>();
            var book=Included("book");var avatar=Included("maestro");physics=root.AddComponent<RoomPhysicsWorld>();
            editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,avatar,directory,physics,gate);if(observe)StartupCamera(wide:true);if(suspend)editor.SendMessage("OnApplicationFocus",false);
        }
        IEnumerator StartupFinished()
        {
            float deadline=Time.realtimeSinceStartup+12;
            do{yield return null;}while(editor.NativeDisplayPending&&Time.realtimeSinceStartup<deadline);
            Assert.That(editor.NativeDisplayPending,Is.False,"Saved-room admission did not finish");
        }
        RoomDocument StartupMany()
        {
            var saved=editor.Snapshot();var template=saved.objects.First(x=>!x.IsBuiltIn);
            saved.objects=saved.objects.Concat(Enumerable.Range(0,10).Select(i=>{var value=template.Copy();value.id=Guid.NewGuid().ToString("N");value.position=new Vector3(i*.2f,1,2);return value;})).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();return saved;
        }
        // Shared-preparation checks need an explicit atomic area. Independent
        // home jobs may finish before a later coroutine can observe pending work.
        RoomDocument StartupManyInOneArea()
        {
            var saved=StartupMany();saved.regions=new[]{new RoomRegion{
                id=Guid.NewGuid().ToString("N"),name="Shared startup preparation",
                members=saved.objects.Where(x=>!x.IsBuiltIn).Select(x=>x.id).ToArray()}};
            return saved;
        }
        [UnityTest] public IEnumerator NativeStartupPublishesSavedIdentitiesCooperativelyWithoutEditsOrGravity()
        {
            var saved=StartupMany();string json=JsonUtility.ToJson(saved);yield return ReloadForStartup(saved);
            Assert.That(editor.Find("book"),Is.Not.Null);Assert.That(editor.Find("maestro"),Is.Not.Null);
            Assert.That(saved.objects.Where(x=>!x.IsBuiltIn).All(x=>editor.Find(x.id)==null&&editor.NativeEntityDormant(x.id)),Is.True);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(json));int revision=editor.Revision;
            editor.TickNativeDisplay();Assert.That(editor.NativeDisplayPending,Is.True);Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(0),"Prepared members publish as one connected area");
            yield return StartupFinished();Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(physics.Running,Is.False);
            foreach(var data in saved.objects.Where(x=>!x.IsBuiltIn)){var item=editor.Find(data.id);Assert.That(item,Is.Not.Null);Assert.That(item.transform.localPosition,Is.EqualTo(data.position));Assert.That(editor.Identity(item),Is.EqualTo(data.id));}
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(json));Assert.That(editor.CanUndo,Is.False);
        }
        [UnityTest] public IEnumerator NativeStartupFocusLossThenResumesLoadingOnly()
        {
            var saved=StartupMany();yield return ReloadForStartup(saved);
            editor.TickNativeDisplay();editor.SendMessage("OnApplicationFocus",false);
            for(int i=0;i<4;i++)yield return null;Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(0));
            editor.SendMessage("OnApplicationFocus",true);yield return StartupFinished();Assert.That(physics.Running,Is.False);
            Assert.That(saved.objects.Where(x=>!x.IsBuiltIn).All(x=>editor.Find(x.id)),Is.True);
        }
        [UnityTest] public IEnumerator NativeStartupReviewLoadsPreviewButApprovalNeverRestartsAutoplay()
        {
            Assert.That(editor.CreateRecipe("Review robot",new Vector3(2,1,3),.6f,RecipeTemplates.BoxRobot(true),out var id,out var error),Is.True,error);
            var gate=new RoomRuntimeGate();var hold=gate.Hold("Review this imported room");
            try{
                yield return ReloadForStartup(gate:gate);yield return StartupFinished();var value=editor.Find(id).GetComponent<RecipeObject>();
                Assert.That(value.IsPlaying,Is.False);Assert.That(physics.Running,Is.False);
                // Showing the preview does not permit actual physics or autoplay.
                Assert.That(physics.SetRunning(true,out error),Is.False);Assert.That(error,Does.Contain("Review"));Assert.That(editor.Ownership.Suspended,Is.True);
                value.Restart();Assert.That(value.IsPlaying,Is.False);hold.Dispose();hold=null;yield return null;
                Assert.That(value.IsPlaying,Is.False);value.Restart();Assert.That(value.IsPlaying,Is.True);Assert.That(physics.Running,Is.False);
            }finally{hold?.Dispose();}
        }
        [UnityTest] public IEnumerator NativeStartupEarlyApprovalCannotTurnPendingPreviewIntoAutoplay()
        {
            Assert.That(editor.CreateRecipe("Pending robot",new Vector3(2,1,3),.6f,RecipeTemplates.BoxRobot(true),out var id,out var error),Is.True,error);
            var gate=new RoomRuntimeGate();using(var hold=gate.Hold("Review")){yield return ReloadForStartup(gate:gate);}
            yield return StartupFinished();Assert.That(editor.Find(id).GetComponent<RecipeObject>().IsPlaying,Is.False);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator NativeStartupPreviewCannotKeepAnOrdinaryActionAliveAcrossReview()
        {
            var saved=StartupManyInOneArea();yield return ReloadForStartup(saved);editor.TickNativeDisplay();string id=saved.objects.First(x=>!x.IsBuiltIn).id;
            var action=editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None);
            Assert.That(action.IsCompleted,Is.False,"Exercise a pending shared action before review begins");
            using(editor.RuntimeGate.Hold("Review shared load")){
                yield return StartupFinished();Assert.That(action.IsCompleted,Is.True);Assert.That(action.Result,Is.Not.Null,"Only the display owner can survive review");
                Assert.That(editor.Find(id),Is.Not.Null);Assert.That(physics.Running,Is.False);
            }
        }
        [UnityTest] public IEnumerator NativeStartupDisableDrainsWithoutLatePublicationAndEnableRetries()
        {
            var saved=StartupMany();yield return ReloadForStartup(saved);editor.TickNativeDisplay();editor.enabled=false;
            for(int i=0;i<4;i++)yield return null;Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(0));
            editor.enabled=true;yield return StartupFinished();Assert.That(saved.objects.Where(x=>!x.IsBuiltIn).All(x=>editor.Find(x.id)),Is.True);
        }
        [UnityTest] public IEnumerator NativeDisplayTimeoutDrainsAndNeedsExplicitRetry()
        {
            var saved=StartupManyInOneArea();yield return ReloadForStartup(saved);yield return StartupAdmissionStarted();
            editor.TickNativeDisplay(Time.unscaledTimeAsDouble+RoomEditor.NativeDisplayTimeout+1);
            yield return StartupFinished();Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(0));
            Assert.That(editor.Status,Does.Contain("timed out"));for(int i=0;i<3;i++)yield return null;
            Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(0));
            string id=saved.objects.First(x=>!x.IsBuiltIn).id;var retry=editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None);
            float until=Time.realtimeSinceStartup+12;while(!retry.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(retry.IsCompleted,Is.True);Assert.That(retry.Result,Is.Null);Assert.That(editor.Find(id),Is.Not.Null);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator NativeStartupPreservesCanonicalRecallSlotsAcrossSeparateAreas()
        {
            var saved=editor.Snapshot();var creations=saved.objects.Where(x=>!x.IsBuiltIn).ToArray();
            saved.regions=creations.Select((x,i)=>new RoomRegion{id=Guid.NewGuid().ToString("N"),name="Area "+i,members=new[]{x.id}}).ToArray();
            yield return ReloadForStartup(saved);yield return StartupFinished();
            for(int i=0;i<creations.Length;i++){var item=editor.Find(creations[i].id);item.RestoreHome();Assert.That(item.transform.localPosition,Is.EqualTo(new Vector3(-.63f+i*.18f,.7f,1.15f)));}
        }
        [UnityTest] public IEnumerator NativeStartupUsesEditedCanonicalPoseAndDoesNotResurrectRemovedObjects()
        {
            var saved=StartupMany();yield return ReloadForStartup(saved);var creations=saved.objects.Where(x=>!x.IsBuiltIn).ToArray();
            var changed=editor.Read(creations[0].id);changed.position=new Vector3(2,3,4);changed.scale=1.4f;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},new[]{creations[1].id},out var error),Is.True,error);
            yield return StartupFinished();Assert.That(editor.Find(changed.id).transform.localPosition,Is.EqualTo(changed.position));Assert.That(editor.Find(changed.id).transform.localScale.x,Is.EqualTo(1.4f));
            Assert.That(editor.Find(creations[1].id),Is.Null);Assert.That(editor.HasSavedObject(creations[1].id),Is.False);
        }
        [UnityTest] public IEnumerator NativeStartupContinuesInTemporaryForkWithoutSavingItsEdits()
        {
            var saved=StartupMany();yield return ReloadForStartup(saved);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            yield return StartupFinished();Assert.That(editor.TemporaryRoom,Is.True);Assert.That(saved.objects.Where(x=>!x.IsBuiltIn).All(x=>editor.Find(x.id)),Is.True);
            Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator NativeStartupPreservesRecipeAutoplayAndSavedPlacement()
        {
            var recipe=RecipeTemplates.BoxRobot(true);
            Assert.That(editor.CreateRecipe("Startup robot",new Vector3(2,1,3),.6f,recipe,out var id,out var error),Is.True,error);
            yield return ReloadForStartup();yield return StartupFinished();var value=editor.Find(id).GetComponent<RecipeObject>();
            Assert.That(value.IsPlaying,Is.True);string part=editor.Read(id).recipe.tracks[0].part;var before=value.Part(part).localRotation;
            yield return new WaitForSeconds(.4f);Assert.That(Quaternion.Angle(before,value.Part(part).localRotation),Is.GreaterThan(1));Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(new Vector3(2,1,3)));
        }
        [UnityTest] public IEnumerator NativeStartupFailedAreaDoesNotBlockOtherAreasOrRetryWithoutRequest()
        {
            var asset=ModelLibrary.Inspect("startup.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var saved=editor.Snapshot();string id=Guid.NewGuid().ToString("N");saved.objects=saved.objects.Append(new RoomObjectData{id=id,kind=RoomObjectKind.ImportedModel,name="Missing model",modelHash=asset.Hash,position=Vector3.one}).ToArray();
            saved.regions=new[]{new RoomRegion{id=Guid.NewGuid().ToString("N"),name="Import",members=new[]{id}}};
            string path=Path.Combine(directory,"models",asset.Hash+".glb"),parked=path+".pending";File.Move(path,parked);
            try{
                yield return ReloadForStartup(saved);yield return StartupFinished();Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Failed));Assert.That(editor.NativeActivationError(id),Is.Not.Empty);
                Assert.That(saved.objects.Where(x=>!x.IsBuiltIn&&x.id!=id).All(x=>editor.Find(x.id)),Is.True);
                File.Move(parked,path);for(int i=0;i<4;i++)yield return null;Assert.That(editor.Find(id),Is.Null,"Restoring bytes is not an automatic retry");
                var retry=editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None);yield return new WaitUntil(()=>retry.IsCompleted);Assert.That(retry.Result,Is.Null);Assert.That(editor.Find(id),Is.Not.Null);
            }finally{if(File.Exists(parked)&&!File.Exists(path))File.Move(parked,path);}
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomEditorTests
    {
        Camera StartupCamera(bool viewer=true,bool wide=false)
        {
            var go=new GameObject("Saved-world observer",typeof(Camera));go.transform.SetParent(root.transform,false);
            var camera=go.GetComponent<Camera>();camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.fieldOfView=60;camera.aspect=1;camera.stereoTargetEye=StereoTargetEyeMask.None;
            go.transform.localPosition=new Vector3(0,1,0);
            if(wide){go.transform.localPosition=new Vector3(0,2,-5);camera.orthographic=true;camera.orthographicSize=10;camera.farClipPlane=100;}
            camera.enabled=viewer;if(viewer)room.Viewer=camera.transform;return camera;
        }
        IEnumerator StartupAdmissionStarted()
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(!editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeActivationPending,Is.True,"Expected a current view to begin native preparation");
        }
        (RoomDocument saved,string front,string back) StartupSeparateAreas()
        {
            var saved=editor.Snapshot();
            var front=new RoomObjectData{id=Guid.NewGuid().ToString("N"),name="Front",kind=RoomObjectKind.Block,position=new Vector3(0,1,3)};
            var back=new RoomObjectData{id=Guid.NewGuid().ToString("N"),name="Back",kind=RoomObjectKind.Block,position=new Vector3(0,1,-3)};
            saved.objects=saved.objects.Where(x=>x.IsBuiltIn).Concat(new[]{front,back}).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
            saved.regions=new[]{front,back}.Select(x=>new RoomRegion{id=Guid.NewGuid().ToString("N"),name=x.name,members=new[]{x.id}}).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
            return(saved,front.id,back.id);
        }
        IEnumerator ObserveStartupFinished(){editor.TickNativeDisplay();yield return StartupFinished();}
        [UnityTest] public IEnumerator SelectiveStartupWithoutViewerPreparesFactsWithoutAllocatingNativeObjects()
        {
            var(saved,front,back)=StartupSeparateAreas();string json=JsonUtility.ToJson(saved);
            yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();
            Assert.That(editor.Find(front),Is.Null);Assert.That(editor.Find(back),Is.Null);
            Assert.That((bool)editor.ObserveAuthoredBounds(front)["prepared"],Is.True);
            Assert.That((bool)editor.ObserveAuthoredBounds(back)["prepared"],Is.True);
            Assert.That(editor.NativeContentPending(front),Is.False);Assert.That(editor.NativeContentPending(back),Is.False);
            Assert.That(root.GetComponentsInChildren<CreatedRoomObject>(),Is.Empty);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(json));Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator SelectiveStartupObservesIndependentAreasOnDemandWithoutEditingSavedState()
        {
            var(saved,front,back)=StartupSeparateAreas();string json=JsonUtility.ToJson(saved);
            yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();int revision=editor.Revision;
            var camera=StartupCamera();yield return ObserveStartupFinished();
            Assert.That(editor.Find(front),Is.Not.Null);Assert.That(editor.Find(back),Is.Null);Assert.That(editor.NativeDisplayPending,Is.False);
            camera.transform.localRotation=Quaternion.Euler(0,180,0);yield return ObserveStartupFinished();
            Assert.That(editor.Find(back),Is.Not.Null);Assert.That(editor.Revision,Is.EqualTo(revision));
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(json));Assert.That(editor.CanUndo,Is.False);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator SelectiveStartupRequiresTrackedViewerButAcceptsAnOwnedCapture()
        {
            var(saved,front,back)=StartupSeparateAreas();yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();
            bool tracked=false;editor.ConfigureObservationTracking(()=>tracked);StartupCamera();
            for(int i=0;i<4;i++)yield return null;
            Assert.That(editor.Find(front),Is.Null);Assert.That(editor.Find(back),Is.Null);
            var capture=StartupCamera(viewer:false);capture.transform.localRotation=Quaternion.Euler(0,180,0);
            using(editor.ObserveNativeCamera(capture)){yield return ObserveStartupFinished();Assert.That(editor.Find(back),Is.Not.Null);Assert.That(editor.Find(front),Is.Null);}
            tracked=true;yield return ObserveStartupFinished();Assert.That(editor.Find(front),Is.Not.Null);
        }
        [UnityTest] public IEnumerator SelectiveStartupExplicitActionAcquiresOnlyItsRequiredClosureWithoutViewer()
        {
            var(saved,front,back)=StartupSeparateAreas();yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();
            var action=editor.AcquireNativeEntities(new[]{back},()=>true,CancellationToken.None);
            yield return new WaitUntil(()=>action.IsCompleted);Assert.That(action.Result,Is.Null);
            Assert.That(editor.Find(back),Is.Not.Null);Assert.That(editor.Find(front),Is.Null);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator SelectiveStartupVisibleAreaIncludesItsAuthoredOffscreenMembers()
        {
            var(saved,front,back)=StartupSeparateAreas();saved.regions=new[]{new RoomRegion{id=Guid.NewGuid().ToString("N"),name="One connected area",members=new[]{front,back}}};
            yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();StartupCamera();yield return ObserveStartupFinished();
            Assert.That(editor.Find(front),Is.Not.Null);Assert.That(editor.Find(back),Is.Not.Null);
        }
        [UnityTest] public IEnumerator SelectiveStartupLateReviewStillSuppressesOffscreenAutoplayAfterApproval()
        {
            var(saved,front,back)=StartupSeparateAreas();var data=saved.objects.Single(x=>x.id==back);data.kind=RoomObjectKind.Assembly;data.recipe=RecipeTemplates.BoxRobot(true);
            yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();
            using(editor.RuntimeGate.Hold("Review before first display")){yield return null;}
            var camera=StartupCamera();yield return ObserveStartupFinished();Assert.That(editor.Find(back),Is.Null);
            camera.transform.localRotation=Quaternion.Euler(0,180,0);yield return ObserveStartupFinished();
            var recipe=editor.Find(back).GetComponent<RecipeObject>();Assert.That(recipe.IsPlaying,Is.False);
            recipe.Restart();Assert.That(recipe.IsPlaying,Is.True);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator SelectiveStartupUnobservedGroundIsUnavailableUntilAnActualRequest()
        {
            var(saved,front,back)=StartupSeparateAreas();var data=saved.objects.Single(x=>x.id==back);
            data.kind=RoomObjectKind.Assembly;data.recipe=new RoomRecipe{parts=new[]{new RecipePart{id="base",position=Vector3.down*.05f,size=new Vector3(2,.1f,2)}}};data.physics=ItemPhysics.Fixed;data.heightFields=new[]{new RoomHeightField{cells=8,width=2,depth=2,maxHeight=.5f,heights=new float[81]}};
            yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();
            var bounds=new Bounds(data.position,Vector3.one*4);var dependency=editor.GroundDependencies(bounds).Single();
            Assert.That((string)dependency["target"],Is.EqualTo(back));Assert.That((string)dependency["state"],Is.EqualTo("unavailable"));
            Assert.That(editor.NativeContentPending(back),Is.False);Assert.That(editor.Find(back),Is.Null);
            var action=editor.AcquireNativeEntities(new[]{back},()=>true,CancellationToken.None);yield return new WaitUntil(()=>action.IsCompleted);Assert.That(action.Result,Is.Null);
            Assert.That(editor.GroundDependencies(bounds),Is.Empty);Assert.That(editor.Find(front),Is.Null);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator SelectiveStartupOffscreenAcousticsPrepareWithoutNativeDisplay()
        {
            var(saved,front,back)=StartupSeparateAreas();foreach(var data in saved.objects.Where(x=>!x.IsBuiltIn))data.physics=ItemPhysics.Fixed;
            yield return ReloadForStartup(saved,observe:false);yield return StartupFinished();var audio=root.AddComponent<RoomAcoustics>();Assert.That(audio.Ready,Is.True,audio.Issue);
            float deadline=Time.realtimeSinceStartup+12;
            do{editor.TickNativeAcoustics();audio.Synchronize();yield return null;}while((editor.AcousticPreparationPending||audio.OmittedCount>0)&&Time.realtimeSinceStartup<deadline);
            Assert.That(editor.AcousticPreparationPending,Is.False);Assert.That(audio.OmittedCount,Is.Zero);Assert.That(audio.RetainedGeometryCount,Is.GreaterThan(0));
            Assert.That(editor.Find(front),Is.Null);Assert.That(editor.Find(back),Is.Null);Assert.That(editor.NativeDisplayPending,Is.False);
        }
    }
}

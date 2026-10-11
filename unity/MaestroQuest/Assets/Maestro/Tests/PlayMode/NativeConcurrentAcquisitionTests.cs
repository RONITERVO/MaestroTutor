// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        IEnumerator NativeConcurrentCandidates()
        {
            float deadline=Time.realtimeSinceStartup+10;
            while(PrivateNativeCandidates().Length==0&&editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(PrivateNativeCandidates().Length,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator NativeConcurrentResidentEditCompletesWhileAnotherAreaIsPreparing()
        {
            var(area,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(ids[0]),Time.unscaledTime,out var loading,out error),Is.True,error);
            yield return NativeConcurrentCandidates();
            string resident=editor.Identity(block);int revision=editor.ObjectRevision(resident);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(resident),Time.unscaledTime,out var instant,out error),Is.True,error);
            Assert.That((string)runtime.Scheduler.Invocation(instant)["phase"],Is.EqualTo("completed"));
            Assert.That(editor.ObjectRevision(resident),Is.GreaterThan(revision));Assert.That(editor.NativeActivationPending,Is.True);
            yield return NativeActionDone(loading);Assert.That(editor.Read(ids[0]).position,Is.EqualTo(new Vector3(.6f,1.2f,.8f)));
        }
        [UnityTest] public IEnumerator NativeConcurrentDifferentAreasQueueWithoutCancellingEachOthersEdits()
        {
            var(first,firstIds)=NativePreparationGroup();var(second,secondIds)=NativePreparationGroup();
            Assert.That(editor.RetireNativeArea(first,out var error),Is.True,error);Assert.That(editor.RetireNativeArea(second,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(firstIds[0]),Time.unscaledTime,out var a,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(secondIds[0]),Time.unscaledTime,out var b,out error),Is.True,error);
            Assert.That(runtime.Scheduler.PreparingCount,Is.EqualTo(2));
            yield return NativeActionDone(a);yield return NativeActionDone(b);
            Assert.That(firstIds.Concat(secondIds).All(id=>editor.Find(id)?.isActiveAndEnabled==true),Is.True);
            Assert.That(editor.NativeActivationPending,Is.False);
        }
        [UnityTest] public IEnumerator NativeConcurrentSharedLoadSurvivesOneRequesterCancellation()
        {
            var(area,ids)=NativePreparationGroup();var original=editor.Read(ids[0]).position;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(ids[0]),Time.unscaledTime,out var a,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(ids[1]),Time.unscaledTime,out var b,out error),Is.True,error);
            yield return NativeConcurrentCandidates();var candidates=PrivateNativeCandidates().Select(v=>v.gameObject).ToArray();
            Assert.That(runtime.Scheduler.CancelInvocation(a,out error),Is.True,error);
            Assert.That(editor.NativePhase(ids[0]),Is.EqualTo(NativeEntityPhase.Preparing));
            yield return NativeActionDone(b);
            Assert.That(candidates.All(value=>value&&value.activeInHierarchy),Is.True,"Shared prepared roots must be published, not discarded and rebuilt for the surviving requester");
            Assert.That((string)runtime.Scheduler.Invocation(a)["phase"],Is.EqualTo("cancelled"));Assert.That(editor.Read(ids[0]).position,Is.EqualTo(original));
            Assert.That(editor.Read(ids[1]).position,Is.EqualTo(new Vector3(.6f,1.2f,.8f)));
        }
        [UnityTest] public IEnumerator NativeConcurrentSharedLoadAllowsBothIndependentEffects()
        {
            var(area,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(ids[0]),Time.unscaledTime,out var a,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(ids[1]),Time.unscaledTime,out var b,out error),Is.True,error);
            yield return NativeActionDone(a);yield return NativeActionDone(b);
            Assert.That(editor.Read(ids[0]).position,Is.EqualTo(editor.Read(ids[1]).position));
        }
        [UnityTest] public IEnumerator NativeConcurrentCancelledQueuedAreaNeverPublishesOrAppliesItsEffect()
        {
            var(first,firstIds)=NativePreparationGroup();var(second,secondIds)=NativePreparationGroup();var saved=editor.Read(secondIds[0]).position;
            Assert.That(editor.RetireNativeArea(first,out var error),Is.True,error);Assert.That(editor.RetireNativeArea(second,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(firstIds[0]),Time.unscaledTime,out var a,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(secondIds[0]),Time.unscaledTime,out var b,out error),Is.True,error);
            Assert.That(runtime.Scheduler.CancelInvocation(b,out error),Is.True,error);
            yield return NativeActionDone(a);for(int i=0;i<3;i++)yield return null;
            Assert.That(secondIds.All(id=>!editor.Find(id)&&editor.NativeEntityDormant(id)),Is.True);
            Assert.That(editor.Read(secondIds[0]).position,Is.EqualTo(saved));Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(secondIds[0]),Time.unscaledTime,out var retry,out error),Is.True,error);yield return NativeActionDone(retry);
        }
        [UnityTest] public IEnumerator NativeConcurrentChangedQueuedMembershipFailsOnlyItsOwnRequest()
        {
            var(first,firstIds)=NativePreparationGroup();var(second,secondIds)=NativePreparationGroup();
            Assert.That(editor.RetireNativeArea(first,out var error),Is.True,error);Assert.That(editor.RetireNativeArea(second,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(firstIds[0]),Time.unscaledTime,out var a,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(secondIds[0]),Time.unscaledTime,out var b,out error),Is.True,error);
            var destination=NewRegion();Assert.That(AssignRegion(secondIds[0],destination,out error),Is.True,error);
            yield return NativeActionDone(a);yield return NativeActionDone(b,"failed");
            Assert.That(editor.RegionFor(secondIds[0]),Is.EqualTo(destination));Assert.That(editor.Find(secondIds[0]),Is.Null);
            Assert.That(editor.Find(firstIds[0]).isActiveAndEnabled,Is.True);
        }
        [UnityTest] public IEnumerator NativeConcurrentConflictingChannelsStayUnavailableDuringLoading()
        {
            var(area,ids)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(ids[0]),Time.unscaledTime,out var a,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(ids[0]),Time.unscaledTime,out _,out error),Is.False);Assert.That(error,Does.Contain("channel"));
            yield return NativeActionDone(a);
        }
        [UnityTest] public IEnumerator NativeConcurrentRuntimeHoldCancelsActiveAndQueuedLoads()
        {
            var(first,firstIds)=NativePreparationGroup();var(second,secondIds)=NativePreparationGroup();
            Assert.That(editor.RetireNativeArea(first,out var error),Is.True,error);Assert.That(editor.RetireNativeArea(second,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(firstIds[0]),Time.unscaledTime,out var a,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(secondIds[0]),Time.unscaledTime,out var b,out error),Is.True,error);
            using(var hold=editor.RuntimeGate.Hold("Pause concurrent acquisition")){}
            yield return NativeActionDone(a,"cancelled");yield return NativeActionDone(b,"cancelled");
            float deadline=Time.realtimeSinceStartup+10;while(editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(firstIds.Concat(secondIds).All(id=>!editor.Find(id)),Is.True);
        }
        [UnityTest] public IEnumerator NativeConcurrentInputGuardTracksOnlyUsedSharedResources()
        {
            string target=editor.Identity(block),appearance=Guid.NewGuid().ToString("N"),environment=Guid.NewGuid().ToString("N"),layer=Guid.NewGuid().ToString("N");
            var style=new RoomAppearance{id=appearance,name="Used appearance"};
            var profile=new RoomEnvironmentProfile{id=environment,name="Used environment"};
            var visibility=new RoomVisibilityLayer{id=layer,name="Used visual layer"};
            Assert.That(editor.EditAppearance(style,appearance,0,Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.BindAppearance(target,editor.ObjectRevision(target),new AppearanceBinding{appearanceId=appearance},editor.AppearanceRevision(appearance),false,false,out error),Is.True,error);
            Assert.That(editor.EditEnvironment(profile,environment,0,Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.BindEnvironment(target,editor.ObjectRevision(target),environment,editor.EnvironmentRevision(environment),out error),Is.True,error);
            Assert.That(editor.EditVisibility(visibility,layer,0,Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.BindVisibility(target,editor.ObjectRevision(target),layer,editor.VisibilityRevision(layer),out error),Is.True,error);
            var current=editor.CaptureNativeActionInput(new[]{target});
            var other=new RoomAppearance{id=Guid.NewGuid().ToString("N"),name="Other appearance"};
            Assert.That(editor.EditAppearance(other,other.id,0,Array.Empty<string>(),out error),Is.True,error);Assert.That(current(),Is.True);
            style.style.tint="#00FF00";Assert.That(editor.EditAppearance(style,appearance,editor.AppearanceRevision(appearance),new[]{target},out error),Is.True,error);Assert.That(current(),Is.False);
            current=editor.CaptureNativeActionInput(new[]{target});profile.name="Updated environment";
            Assert.That(editor.EditEnvironment(profile,environment,editor.EnvironmentRevision(environment),new[]{target},out error),Is.True,error);Assert.That(current(),Is.False);
            current=editor.CaptureNativeActionInput(new[]{target});visibility.opacity=.5f;
            Assert.That(editor.EditVisibility(visibility,layer,editor.VisibilityRevision(layer),new[]{target},out error),Is.True,error);Assert.That(current(),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator NativeConcurrentSmallLoadsCannotResetThePreparationFrameBudget()
        {
            var ids=new string[3];
            for(int i=0;i<ids.Length;i++){
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Queued block "+i,new Vector3(i*.2f,1,2),1,Color.white,out ids[i],out var error),Is.True,error);
                string area=NativeAreaFor(ids[i]);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            }
            yield return null;int frame=Time.frameCount;
            var tasks=ids.Select(id=>editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None)).ToArray();
            Assert.That(tasks.All(task=>task.IsCompleted),Is.False,"Successive jobs share the lane's step/time budget");
            foreach(var task in tasks)yield return NativeAreaCompletion(task);
            Assert.That(Time.frameCount,Is.GreaterThan(frame));Assert.That(ids.All(id=>editor.Find(id)?.isActiveAndEnabled==true),Is.True);
        }
    }
}

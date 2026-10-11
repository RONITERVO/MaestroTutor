// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
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
        IEnumerator NativeAreaCompletion(Task<string> task,bool success=true)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(!task.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(task.IsCompleted,Is.True,"Native area transition did not complete");Assert.That(task.Exception,Is.Null);
            if(success)Assert.That(task.Result,Is.Null);else Assert.That(task.Result,Is.Not.Null);
        }
        string NativeAreaFor(string target){string area=NewRegion();Assert.That(AssignRegion(target,area,out var error),Is.True,error);return area;}
        [UnityTest] public IEnumerator NativeAreaRetirementPreservesSavedContentAndRuntimePoseWithoutResurrection()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id),saved=JsonUtility.ToJson(editor.Read(id));int revision=editor.ObjectRevision(id);
            block.transform.localPosition+=Vector3.up*.35f;var pose=block.transform.localPosition;var previous=block.gameObject;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Retiring));Assert.That(editor.Find(id),Is.Null);
            Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));
            yield return null;Assert.That((bool)previous,Is.False,"A disabled GameObject is not native retirement");Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Dormant));
            var peer=editor.Read("book");peer.name="Peer edited while area sleeps";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{peer},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.Find(id),Is.Null,"Unrelated reconciliation cannot resurrect an intentionally retired area");
            var activation=editor.ActivateNativeArea(area);yield return NativeAreaCompletion(activation);
            Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(pose));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));Assert.That(editor.Read(id).motion.frames.Length,Is.EqualTo(2));Assert.That(editor.Identity(editor.Find(id)),Is.EqualTo(id));
        }
        [UnityTest] public IEnumerator NativeAreaDormantEditsInvalidateCachedPoseAndUndoUsesCanonicalHistory()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);var old=editor.Read(id);block.transform.localPosition+=Vector3.up;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var changed=editor.Read(id);changed.position+=Vector3.right*.5f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);Assert.That(editor.Find(id),Is.Null);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(changed.position));
            editor.Undo();Assert.That(editor.Read(id).position,Is.EqualTo(old.position));Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(old.position));
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);Assert.That(editor.NativeEntityDormant(id),Is.False);
            editor.Undo();Assert.That(editor.HasSavedObject(id),Is.True);Assert.That((bool)editor.Find(id),Is.True);Assert.That(editor.Read(id).motion.frames.Length,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator NativeAreaImmediateActivationWaitsForDestructionAndCancelledActivationCanRetry()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);var previous=block.gameObject;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);using var cancel=new CancellationTokenSource();var activation=editor.ActivateNativeArea(area,cancel.Token);
            Assert.That(activation.IsCompleted,Is.False);Assert.That(editor.Find(id),Is.Null);cancel.Cancel();yield return NativeAreaCompletion(activation,false);yield return null;
            Assert.That((bool)previous,Is.False);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Resident));Assert.That(editor.Find(id).isActiveAndEnabled,Is.True);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);using var alreadyCancelled=new CancellationTokenSource();alreadyCancelled.Cancel();
            activation=editor.ActivateNativeArea(area,alreadyCancelled.Token);Assert.That(activation.IsCompleted,Is.True);Assert.That(activation.Result,Is.Not.Null);Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Retiring),"Cancellation cannot claim a deferred Unity destruction has already finished");yield return null;
        }
        [UnityTest] public IEnumerator NativeAreaConnectionsRetireAsAClosureAndPreserveBreaks()
        {
            var (id,mount)=FixedPieces();Assert.That(Connect(AttachCall(id,mount,.1f),out var error),Is.True,error);string area=NativeAreaFor(id),other=NativeAreaFor(mount);
            var view=editor.Find(id).GetComponent<RoomConnectionView>();physics.SetSurfaces(true,"Ready");physics.StartPhysics();for(int i=0;i<30&&!view.Broken;i++)yield return new WaitForFixedUpdate();Assert.That(view.Broken,Is.True);physics.PausePhysics();yield return null;
            Assert.That(editor.Ownership.TryAcquire("area-pin","Retain peer",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(mount,"wholeTarget")},null,out var lease,out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.False);Assert.That((bool)editor.Find(id),Is.True);lease.Dispose();
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Find(mount),Is.Null);
            var renamed=editor.Read(id);renamed.name="Renamed broken piece";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{renamed},Array.Empty<string>(),out error),Is.True,error);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(other));Assert.That(editor.Find(id).GetComponent<RoomConnectionView>().Broken,Is.True);Assert.That(editor.Read(id).connections.Single().connected,Is.EqualTo(mount));
        }
        [UnityTest] public IEnumerator NativeAreaPreservesStoppedRecipePoseAndDoesNotRestartAutoplay()
        {
            string id=RecipeTarget(true),area=NativeAreaFor(id);var recipe=editor.Find(id).GetComponent<RecipeObject>();for(int i=0;i<4;i++)yield return null;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.False,"A playing recipe needs its native parts");recipe.Stop();
            string part=editor.Read(id).recipe.tracks[0].part;var rotation=recipe.Part(part).localRotation;string saved=JsonUtility.ToJson(editor.Read(id));
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            var renamed=editor.Read(id);renamed.name="Renamed stopped recipe";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{renamed},Array.Empty<string>(),out error),Is.True,error);saved=JsonUtility.ToJson(editor.Read(id));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            recipe=editor.Find(id).GetComponent<RecipeObject>();Assert.That(recipe.IsPlaying,Is.False);Assert.That(Quaternion.Angle(rotation,recipe.Part(part).localRotation),Is.LessThan(.001f));
            yield return null;Assert.That(recipe.IsPlaying,Is.False);Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));
        }
        [UnityTest] public IEnumerator NativeAreaKeepsDrawingsAndPhysicsRequiresAllAuthoredCollisionUntilRegionalAdmissionExists()
        {
            string id=NewStroke(),area=NativeAreaFor(id),saved=JsonUtility.ToJson(editor.Read(id));physics.SetSurfaces(true,"Ready");physics.StartPhysics();
            Assert.That(editor.RetireNativeArea(area,out var error),Is.False);physics.PausePhysics();Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            Assert.That(physics.SetRunning(true,out error),Is.False);StringAssert.Contains("activation",error);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));Assert.That(physics.Running,Is.False);Assert.That(physics.SetRunning(true,out error),Is.True,error);
        }
        [UnityTest] public IEnumerator NativeAreaActiveProgramsAndIndependentAudioKeepNativeTargets()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(runtime.Trigger(sequenceId),Is.True);Assert.That(editor.RetireNativeArea(area,out var error),Is.False);runtime.StopAll();
            string source=WorldSound(5);WorldEmitter(id,source);var audio=WorldAudio.For(editor);Assert.That(audio.Begin(id,"sound",out var voice,out error,true),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.False);audio.Cancel(voice);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            Assert.That((bool)editor.Find("book"),Is.True);Assert.That((bool)editor.Find("maestro"),Is.True);Assert.That(editor.NativeEntityDormant("book"),Is.False);
        }
        [UnityTest] public IEnumerator NativeAreaFailedModelPreparationPublishesNoPlaceholderAndCanRetryExactSavedHash()
        {
            var asset=ModelLibrary.Inspect("area.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var placement=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>placement.IsCompleted);Assert.That(placement.Exception,Is.Null);string id=placement.Result,area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string modelPath=Path.Combine(directory,"models",asset.Hash+".glb"),parked=modelPath+".test-missing";
            File.Move(modelPath,parked);
            try{yield return NativeAreaCompletion(editor.ActivateNativeArea(area),false);Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Failed));Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Read(id).modelHash,Is.EqualTo(asset.Hash));}
            finally{File.Move(parked,modelPath);}
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            var view=editor.Find(id).GetComponent<CreatedRoomObject>();Assert.That(view.ModelGeometryReady,Is.True,view.ModelStatus);Assert.That(view.Model.AssetHash,Is.EqualTo(asset.Hash));Assert.That(view.Model.IsPlaying,Is.False);
        }
        [UnityTest] public IEnumerator NativeAreaPendingImportsKeepTheWholeAreaUnpublishedAndCancelOnBriefRuntimeHold()
        {
            var asset=ModelLibrary.Inspect("area.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var placement=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>placement.IsCompleted);string id=placement.Result,area=NativeAreaFor(id),peer=editor.Identity(block);Assert.That(AssignRegion(peer,area,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;var barrier=new ModelLoadBarrier();var blocker=ResidencyModel();var blocked=blocker.LoadAsync(asset,barrier);Task<string> activation=null;
            try{
                yield return new WaitUntil(()=>barrier.Entered||blocked.IsCompleted);Assert.That(barrier.Entered,Is.True);activation=editor.ActivateNativeArea(area);for(int i=0;i<5;i++)yield return null;
                Assert.That(activation.IsCompleted,Is.False);Assert.That(editor.Find(peer),Is.Null);Assert.That(editor.Find(id),Is.Null);
                using(var hold=editor.RuntimeGate.Hold("Brief activation hold")){}yield return NativeAreaCompletion(activation,false);Assert.That(editor.Find(peer),Is.Null);Assert.That(editor.Find(id),Is.Null);
            }finally{barrier.Open();blocker.Dispose();}
            yield return new WaitUntil(()=>blocked.IsCompleted);Assert.That(blocked.Exception,Is.Null);yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id).GetComponent<CreatedRoomObject>().ModelGeometryReady,Is.True);Assert.That(editor.Find(peer).isActiveAndEnabled,Is.True);
        }
        [UnityTest] public IEnumerator NativeAreaChangedInputCancelsPendingActivationWithoutLosingTheEdit()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);var activation=editor.ActivateNativeArea(area);
            var changed=editor.Read(id);changed.name="Changed while preparing";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            yield return NativeAreaCompletion(activation,false);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Read(id).name,Is.EqualTo(changed.name));yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
        }
        [UnityTest] public IEnumerator NativeAreaTemporaryDiscardClearsResidencyFromTheReplacedJournal()
        {
            yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.NativeEntityDormant(id),Is.False);Assert.That((bool)editor.Find(id),Is.True);Assert.That(editor.RegionFor(id),Is.EqualTo(area));
        }
        [UnityTest] public IEnumerator NativeAreaNeverActiveCandidatesReleaseDrawingAndImportedResources()
        {
            var asset=ModelLibrary.Inspect("candidate.glb",ModelFixture.Create());var data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.ImportedModel,modelHash=asset.Hash};
            var budget=ImportedModel.LiveBudget;var load=PreparedImportedModel.Load(asset,data,editor.WorldIdentity,CancellationToken.None,()=>true);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);
            var prepare=RoomEditPreparation.PrepareNativeAsync(editor,new[]{data},new RoomPreparationBudget(()=>{}));yield return new WaitUntil(()=>prepare.IsCompleted);Assert.That(prepare.Exception,Is.Null);
            var preparation=prepare.Result;preparation.EnlistModel(load.Result,data);
            var candidate=new GameObject("Never-active native area candidate");candidate.SetActive(false);candidate.transform.SetParent(root.transform,false);
            var view=candidate.AddComponent<CreatedRoomObject>();var item=view.BuildPrepared(data,editor.Models,editor.RuntimeGate,editor.WorldIdentity,preparation);
            var binding=candidate.AddComponent<RoomEnvironmentBinding>();binding.Apply(physics,item,true);var rigid=candidate.GetComponent<RigidRoomItem>();rigid.Configure(physics,data.physics,data.mass);
            var eventField=typeof(RoomPhysicsWorld).GetField(nameof(RoomPhysicsWorld.Changed),System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            bool Subscribed(object target)=>((Delegate)eventField.GetValue(physics))?.GetInvocationList().Any(d=>ReferenceEquals(d.Target,target))==true;
            Assert.That(Subscribed(binding),Is.True);Assert.That(Subscribed(rigid),Is.True);
            var mesh=view.Model.Instance.Renderers[0].GetComponent<MeshFilter>().sharedMesh;var pigment=view.Model.Instance.Renderers[0].sharedMaterial;
            var marks=candidate.GetComponentsInChildren<Maestro.Quest.Art.PencilMarks>(true);Assert.That(marks.Length,Is.GreaterThan(0));var drawingMeshes=marks.Select(m=>m.GetComponent<MeshFilter>().sharedMesh).ToArray();
            Maestro.Quest.Art.NativeResourceLifetime.Release(candidate);Maestro.Quest.Art.NativeResourceLifetime.Release(candidate);preparation.Dispose();
            Assert.That(Subscribed(binding),Is.False);Assert.That(Subscribed(rigid),Is.False);UnityEngine.Object.Destroy(candidate);yield return null;physics.SetSurfaces(true,"Room after abandoned candidate");
            Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));Assert.That((bool)mesh,Is.False);Assert.That((bool)pigment,Is.False);Assert.That(drawingMeshes.All(m=>!m),Is.True);
        }
    }
}

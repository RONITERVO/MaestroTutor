// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        string RetireAuthoredInput(Vector3 position){
            world.PausePhysics();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Authored input fixture",position,.3f,Color.white,out var id,out var error),Is.True,error);
            Physics.SyncTransforms();var item=editor.Find(id);float halfHeight=item.Grab.colliders.Max(c=>c.bounds.extents.y);
            var data=editor.Read(id);data.position=editor.Frame.PointToRoom(new Vector3(position.x,avatar.transform.position.y-.06f-halfHeight,position.z));data.physics=ItemPhysics.Fixed;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            Physics.SyncTransforms();RoomWalkingPreparation.Envelopes(avatar.transform.position,avatar.transform.position,.25f*avatar.transform.lossyScale.y,1.7f*avatar.transform.lossyScale.y,out var envelope,out _);
            Assert.That(item.Grab.colliders.Any(c=>envelope.Intersects(c.bounds)),Is.True,"Fixture must intersect initial body preparation while staying below the floor");
            var region=new RegionSaveCapability();Assert.That(region.Start(new CapabilityContext(editor,authoring),"authored-region",region.Example,out var saved,out error),Is.True,error);string area=(string)saved.Result["id"];
            Assert.That(editor.BindRegion(id,editor.ObjectRevision(id),area,editor.RegionRevision(area),out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            // Isolate per-motion admission; production still has the whole-world gate.
            world.ConfigureCollisionAdmission(editor,()=>null);world.StartPhysics();Assert.That(world.Running,Is.True);
            return id;
        }
        IEnumerator AuthoredInputsLoaded(){float until=Time.realtimeSinceStartup+10;while(editor.NativeActivationPending&&Time.realtimeSinceStartup<until)yield return null;Assert.That(editor.NativeActivationPending,Is.False);yield return null;}
        JObject AuthoredInputFact(){Assert.That(BehaviourCatalog.TryRead("avatar.authored.inputs",1,new JObject(),new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);return JObject.FromObject(value.Value);}
        CapabilityOperation AuthoredInputOperation(){
            var cap=new EmbeddedAnimationCapability();var args=new JObject{["target"]="maestro",["seconds"]=1,["loop"]=false,["modelHash"]=avatar.ModelHash,["clipIndex"]=0,["movement"]="authored"};
            Assert.That(cap.CanRun(new CapabilityContext(editor,authoring),args,out var error),Is.True,error);
            Assert.That(cap.Start(new CapabilityContext(editor,authoring),"authored-input",args,out var op,out error),Is.True,error);return op;
        }
        [UnityTest]public IEnumerator AuthoredInputsStartWaitsWithExactPoseAndDoesNotConsumePlayback(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());string id=RetireAuthoredInput(avatar.transform.position);
            var before=avatar.transform.position;var nodes=avatar.CustomModel.Instance.Nodes.ToArray();var positions=nodes.Select(n=>n.localPosition).ToArray();var rotations=nodes.Select(n=>n.localRotation).ToArray();
            var op=AuthoredInputOperation();try{
                Assert.That(op.State(out var error),Is.EqualTo(RuleActionState.Preparing),error);float remaining=op.RemainingSeconds.Value;
                Assert.That(avatar.transform.position,Is.EqualTo(before));for(int i=0;i<nodes.Length;i++){Assert.That(Vector3.Distance(nodes[i].localPosition,positions[i]),Is.LessThan(.00001f),"Node "+i+" pending pose delta "+Vector3.Distance(nodes[i].localPosition,positions[i]).ToString("R"));Assert.That(Quaternion.Angle(nodes[i].localRotation,rotations[i]),Is.LessThan(.01f));}
                Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("pending"));Assert.That(op.Complete(out _),Is.False);
                yield return AuthoredInputsLoaded();Assert.That(op.RemainingSeconds.Value,Is.EqualTo(remaining));Assert.That(op.State(out error),Is.EqualTo(RuleActionState.Ready),error);Assert.That(editor.Find(id),Is.Not.Null);Assert.That((bool)AuthoredInputFact()["ready"],Is.True);
                var edited=editor.Read(id);edited.position+=Vector3.down*.005f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{edited},Array.Empty<string>(),out error),Is.True,error);
                Assert.That(op.State(out error),Is.EqualTo(RuleActionState.Ready),error);Assert.That((bool)AuthoredInputFact()["ready"],Is.True,"Accepted input edits recheck without retaining a stale native lease");
            }finally{op.Stop(true);}Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("inactive"));
        }
        [UnityTest]public IEnumerator AuthoredInputsStopCancelsPendingActivationAndRetainsPlacement(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());string id=RetireAuthoredInput(avatar.transform.position);var before=avatar.transform.position;var op=AuthoredInputOperation();
            Assert.That(op.State(out _),Is.EqualTo(RuleActionState.Preparing));op.Stop(true);yield return AuthoredInputsLoaded();Assert.That(editor.Find(id),Is.Null);Assert.That(avatar.transform.position,Is.EqualTo(before));Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("inactive"));
        }
        [UnityTest]public IEnumerator AuthoredInputsLaterWaitRestoresAllJointsAndCannotReplayAJumpInTime(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());float now=0;var clock=new RoomPlaybackClock(()=>now);avatar.PauseOwnedPlayback(this,true);
            try{
                Assert.That(avatar.PlayImportedClip(0,true,true,clock),Is.True,avatar.ImportedPlaybackError);Assert.That(avatar.SampleImportedAt(.2f),Is.True,avatar.ImportedPlaybackError);
                var accepted=avatar.transform.position;var nodes=avatar.CustomModel.Instance.Nodes.ToArray();var positions=nodes.Select(n=>n.localPosition).ToArray();
                string id=RetireAuthoredInput(accepted+Vector3.down*.15f);now=.4f;Assert.That(avatar.SampleImportedAt(.4f),Is.False);Assert.That(avatar.ImportedPlaybackError,Is.Null);Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("pending"));
                Assert.That(avatar.transform.position,Is.EqualTo(accepted));for(int i=0;i<nodes.Length;i++)Assert.That(Vector3.Distance(nodes[i].localPosition,positions[i]),Is.LessThan(.00001f));
                now=2.4f;Assert.That(clock.Elapsed,Is.EqualTo(.4f).Within(.00001f));yield return AuthoredInputsLoaded();Assert.That(editor.Find(id),Is.Not.Null);
                Assert.That(avatar.SampleImportedAt(20),Is.True,avatar.ImportedPlaybackError);Assert.That(Vector3.Distance(avatar.transform.position,accepted),Is.InRange(.05f,.6f));Assert.That(clock.Elapsed,Is.EqualTo(.4f).Within(.00001f));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredInputsBudgetFailureCannotFinishPendingGeometryOrChangeThePose(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());string id=RetireAuthoredInput(avatar.transform.position);float now=0;var clock=new RoomPlaybackClock(()=>now);var before=avatar.transform.position;avatar.PauseOwnedPlayback(this,true);
            try{
                Assert.That(avatar.PlayImportedClip(0,true,true,clock),Is.True);Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("pending"));now=31;
                Assert.That(avatar.SampleImportedAt(1),Is.False);Assert.That(avatar.ImportedPlaybackError,Does.Contain("budget"));Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("unavailable"));Assert.That(avatar.transform.position,Is.EqualTo(before));
                yield return AuthoredInputsLoaded();Assert.That(editor.Find(id),Is.Null);
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredInputsHandoffPreservesCompensatedPoseAndResumesAtTheAcceptedPlacement(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());avatar.PauseOwnedPlayback(this,true);
            try{
                Assert.That(avatar.PlayImportedClip(0,true,true),Is.True);Assert.That(avatar.SampleImportedAt(.2f),Is.True);var placement=avatar.transform.position;var nodes=avatar.CustomModel.Instance.Nodes.ToArray();var positions=nodes.Select(n=>n.position).ToArray();
                string id=RetireAuthoredInput(placement);var op=AuthoredInputOperation();try{
                    Assert.That(op.State(out var error),Is.EqualTo(RuleActionState.Preparing),error);
                    for(int i=0;i<nodes.Length;i++)Assert.That(Vector3.Distance(nodes[i].position,positions[i]),Is.LessThan(.00001f),"World-space handoff node "+i);
                    Assert.That(avatar.transform.position,Is.EqualTo(placement));yield return AuthoredInputsLoaded();Assert.That(op.State(out error),Is.EqualTo(RuleActionState.Ready),error);
                    Assert.That(editor.Find(id),Is.Not.Null);Assert.That(Vector3.Distance(avatar.transform.position,placement),Is.LessThan(.00001f));Assert.That(avatar.CustomModel.transform.localPosition,Is.EqualTo(Vector3.zero));
                }finally{op.Stop(true);}
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredInputsExactLibraryMotionSharesPendingClockAndCancellation(){
            TravelStage();var bytes=ModelFixture.HipTravel();yield return LoadTravelAvatar(bytes);
            var imported=editor.Motions.ImportAsync("authored.glb",bytes);yield return new WaitUntil(()=>imported.IsCompleted);Assert.That(imported.Exception,Is.Null);var entry=imported.Result.Single();
            var loaded=editor.Motions.AcquireAsync(entry.id,avatar.CustomModel.MotionRigHash);yield return new WaitUntil(()=>loaded.IsCompleted);Assert.That(loaded.Exception,Is.Null);
            using var warm=loaded.Result;string id=RetireAuthoredInput(avatar.transform.position);var cap=new LibraryAnimationCapability();var args=new JObject{["target"]="maestro",["seconds"]=1,["loop"]=false,["motionId"]=entry.id,["movement"]="authored"};
            Assert.That(cap.Start(new CapabilityContext(editor,authoring),"authored-library",args,out var op,out var error),Is.True,error);
            try{
                Assert.That(op.State(out error),Is.EqualTo(RuleActionState.Preparing),error);Assert.That(avatar.LibraryMotionId,Is.EqualTo(entry.id));Assert.That(op.RemainingSeconds.Value,Is.EqualTo(1).Within(.001f));
                Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("pending"));op.Stop(true);yield return AuthoredInputsLoaded();
                Assert.That(editor.Find(id),Is.Null);Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(warm.Clip,Is.Not.Null,"Stopping one action cannot dispose another exact-motion lease");
            }finally{op.Stop(true);}
        }
        [UnityTest]public IEnumerator AuthoredInputsPassiveEligibilityAndObservationCannotLoadOrStartMotion(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());string id=RetireAuthoredInput(avatar.transform.position);int revision=editor.Revision;
            for(int i=0;i<3;i++){Assert.That(avatar.CanPlayAuthored(out var error),Is.True,error);Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("inactive"));yield return null;}
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(id),Is.Null);Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));
        }
    }
}

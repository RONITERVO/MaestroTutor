// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        JObject PresentationFact(){
            Assert.That(BehaviourCatalog.TryRead("world.presentation",1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            return JObject.FromObject(value.Value);
        }
        JObject PresentationRequest(double opacity,bool depth)=>new(){
            ["operation"]="start",["runId"]=modeActions.Observe()["nextRunId"].DeepClone(),
            ["call"]=new JObject{["id"]="world.presentation.set",["version"]=1,
                ["arguments"]=new JObject{["backdropOpacity"]=opacity,["realDepth"]=depth,["stateId"]=PresentationFact()["stateId"].DeepClone()}}
        };
        IEnumerator Present(float opacity,bool depth){
            Assert.That(modeActions.Execute(PresentationRequest(opacity,depth),out var error),Is.True,error);yield return null;
            Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),modeActions.Observe().ToString());
            Assert.That((float)PresentationFact()["backdropOpacity"],Is.EqualTo(opacity));
            Assert.That((bool)PresentationFact()["realDepth"],Is.EqualTo(depth));
        }
        [UnityTest]public IEnumerator BackdropAndDepthControlsPreservePhysicsWorldAndOtherActors(){
            var controls=SharedModes(out var view,out var origin);var physical=origin.localToWorldMatrix;
            var placed=root.transform.localToWorldMatrix;
            Assert.That(motion.Begin("existing follow",Avatar.AvatarSpatialMode.Follow,out var error),Is.True,error);
            var collision=world.ObserveEnvironment().ToString();
            foreach(var opacity in new[]{.25f,.5f,.75f,1f,0f}){
                yield return Present(opacity,true);
                Assert.That(viewer.GetComponent<Camera>().backgroundColor.a,Is.EqualTo(opacity));
                Assert.That((bool)PresentationFact()["depthEligible"],Is.EqualTo(opacity<1));
                Assert.That(view.WantsRealDepth,Is.EqualTo(opacity<1));
                Assert.That(world.Running,Is.True);Assert.That(world.ObserveEnvironment().ToString(),Is.EqualTo(collision));
                Assert.That(motion.OwnedBy("existing follow"),Is.True);
                Assert.That(origin.localToWorldMatrix,Is.EqualTo(physical));Assert.That(root.transform.localToWorldMatrix,Is.EqualTo(placed));
                // Running rigid-body capture can advance the saved revision.
                // The stationary book probe checks that the view action itself saves nothing.
            }
            yield return Present(0,false);Assert.That(view.WantsRealDepth,Is.False);
            controls.ToggleView();Assert.That(view.Active,Is.True);
            controls.ToggleView();Assert.That(view.Active,Is.False);Assert.That(view.RealDepth,Is.True);
        }
        [UnityTest]public IEnumerator BackdropReceiptReplayAndManualReversalCannotRestoreOldView(){
            var controls=SharedModes(out var view,out _);
            var stale=PresentationRequest(.5,true);var mode=ModeRequest("view.virtual");
            controls.CycleBackdrop();controls.Recover();
            Assert.That(modeActions.Execute(stale,out _),Is.False);Assert.That(modeActions.Execute(mode,out _),Is.False);
            var request=PresentationRequest(.5,false);Assert.That(modeActions.Execute(request,out var error),Is.True,error);yield return null;
            controls.Recover();var recovered=PresentationFact();
            Assert.That(modeActions.Execute(request,out error),Is.True,error);Assert.That(JToken.DeepEquals(recovered,PresentationFact()),Is.True);
            Assert.That(view.BackdropOpacity,Is.Zero);Assert.That(view.RealDepth,Is.True);
            stale=PresentationRequest(.5,true);controls.ToggleRealDepth();controls.ToggleRealDepth();
            Assert.That(modeActions.Execute(stale,out _),Is.False);
        }
        [UnityTest]public IEnumerator BlendAdmissionRefusesBusyUntrackedAndInvalidRequests(){
            var controls=SharedModes(out var view,out _);
            foreach(var opacity in new[]{-.01,1.01}){
                Assert.That(modeActions.Execute(PresentationRequest(opacity,true),out _),Is.False);
                Assert.That(view.BackdropOpacity,Is.Zero);
            }
            frame.busy=true;Assert.That(modeActions.Execute(PresentationRequest(.5,true),out var error),Is.False);StringAssert.Contains("Release",error);frame.busy=false;
            using(editor.RuntimeGate.Hold("Review alignment"))Assert.That(modeActions.Execute(PresentationRequest(.5,true),out _),Is.False);
            tracked=false;Assert.That(modeActions.Execute(PresentationRequest(.5,true),out _),Is.False);tracked=true;
            view.enabled=false;Assert.That(modeActions.Execute(PresentationRequest(.5,true),out _),Is.False);view.enabled=true;
            yield return Present(.5f,false);controls.SendMessage("OnApplicationFocus",false);
            Assert.That(view.BackdropOpacity,Is.Zero);Assert.That(view.RealDepth,Is.True);
            controls.SendMessage("OnApplicationFocus",true);yield return null;Assert.That(view.BackdropOpacity,Is.Zero);
            yield return Present(.75f,false);tracked=false;yield return null;Assert.That(view.BackdropOpacity,Is.Zero);Assert.That(view.RealDepth,Is.True);
        }
        [UnityTest]public IEnumerator IdenticalPresentationIsInertAndMixedBlendRetainsUserOptIn(){
            var controls=SharedModes(out var view,out _);TravelGround();world.PausePhysics();
            yield return Present(1,false);yield return ChangeMode("user.enable");
            var before=PresentationFact();yield return Present(1,false);
            Assert.That(JToken.DeepEquals(before,PresentationFact()),Is.True);Assert.That(controls.UserEnabled,Is.True);
            yield return Present(.5f,false);Assert.That(controls.UserEnabled,Is.True);Assert.That(view.Active,Is.False);
            controls.SendMessage("OnApplicationPause",true);Assert.That(view.BackdropOpacity,Is.Zero);Assert.That(view.RealDepth,Is.True);
        }
    }
}

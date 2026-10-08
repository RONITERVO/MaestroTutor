// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject LightingCall(RoomLighting value)=>new(){["id"]="world.lighting.set",["version"]=1,["arguments"]=new JObject{["revision"]=editor.LightingRevision,["settings"]=JObject.Parse(JsonUtility.ToJson(value))}};
        [UnityTest]public IEnumerator LightingUsesTheSharedSchedulerPersistsAndReplaysOnceWhileAnimationContinues() {
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(WorldLightingCapability.Feature));
            string target=RecipeTarget();Assert.That(runtime.Scheduler.Invoke(PartCall(target,"RightUpperArm",10),Time.unscaledTime,out var animation,out var e),Is.True,e);yield return null;
            var light=new RoomLighting{enabled=true,ambientColor="#224466",sunIntensity=.4f};var request=TemplateRequest(LightingCall(light));request.conditions=Array.Empty<RoomObjectCondition>();var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(request,out e,out _),Is.True,e);int revision=editor.LightingRevision;
            Assert.That(executor.Execute(request,out e,out _),Is.True,e);Assert.That(editor.LightingRevision,Is.EqualTo(revision));Assert.That((string)runtime.Scheduler.Invocation(animation)["phase"],Is.EqualTo("running"));
            Assert.That(new RoomStorage(directory).Load(out e).lighting.Same(light),Is.True,e);Assert.That(WorldLightingCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,null,out _),Is.True);
            Assert.That(editor.SetLighting(revision-1,light,out _),Is.False);runtime.Scheduler.CancelInvocation(animation,out _);editor.Undo();Assert.That(editor.Lighting.enabled,Is.False);editor.Redo();Assert.That(editor.Lighting.Same(light),Is.True);
        }
        [UnityTest]public IEnumerator LightingTemporaryDiscardAndKeepRefreshGlobalsWithoutMovingObjects() {
            var view=editor.GetComponent<WorldLightingView>();var body=block.GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=false;body.linearVelocity=Vector3.right;var pos=block.transform.position;
            Assert.That(editor.SetLighting(editor.LightingRevision,new(){enabled=true,azimuth=0,elevation=0},out var e),Is.True,e);Assert.That(block.transform.position,Is.EqualTo(pos));Assert.That(body.linearVelocity,Is.EqualTo(Vector3.right));body.isKinematic=true;
            Assert.That(editor.BeginTemporaryRoom(out e),Is.True,e);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.SetLighting(editor.LightingRevision,new(){enabled=true,sunIntensity=0},out e),Is.True,e);view.Refresh();Assert.That(Shader.GetGlobalVector("_MaestroSun").x,Is.Zero);
            Assert.That(editor.DiscardTemporaryRoom(out e),Is.True,e);view.Refresh();Assert.That(Shader.GetGlobalVector("_MaestroSun").x,Is.GreaterThan(0));
            Assert.That(editor.BeginTemporaryRoom(out e),Is.True,e);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.SetLighting(editor.LightingRevision,new(){enabled=true,ambientIntensity=.1f,sunIntensity=0},out e),Is.True,e);
            Assert.That(editor.KeepTemporaryRoom(out e),Is.True,e);while(editor.TemporarySavePending)yield return null;Assert.That(new RoomStorage(directory).Load(out e).lighting.ambientIntensity,Is.EqualTo(.1f),e);
        }
        [UnityTest]public IEnumerator LightingFollowsWorldRotationAndDisablingWorkspaceReleasesGlobals() {
            var view=editor.GetComponent<WorldLightingView>();Assert.That(editor.SetLighting(editor.LightingRevision,new(){enabled=true,azimuth=0,elevation=0},out var e),Is.True,e);view.Refresh();
            Assert.That(Vector3.Distance((Vector3)Shader.GetGlobalVector("_MaestroSunDirection"),Vector3.forward),Is.LessThan(.0001f));
            root.transform.rotation=Quaternion.Euler(0,90,0);view.Refresh();Assert.That(Vector3.Distance((Vector3)Shader.GetGlobalVector("_MaestroSunDirection"),Vector3.right),Is.LessThan(.0001f));
            view.enabled=false;Assert.That(Shader.GetGlobalFloat("_MaestroLightingEnabled"),Is.Zero);view.enabled=true;Assert.That(Shader.GetGlobalFloat("_MaestroLightingEnabled"),Is.EqualTo(1));
            root.transform.localScale=new Vector3(1,2,1);view.Refresh();Assert.That(Shader.GetGlobalFloat("_MaestroLightingEnabled"),Is.Zero);yield return null;
        }
        [UnityTest]public IEnumerator FailedLightingSaveDoesNotPublishOrInvalidateAcceptedState() {
            // Lock the current output against replacement; keep its actual bytes for recovery.
            Assert.That(editor.TryFlush(out var flushError),Is.True,flushError);var before=editor.Lighting;int revision=editor.LightingRevision;var view=editor.GetComponent<WorldLightingView>();view.Refresh();
            using(var locked=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read)) {
                Assert.That(editor.SetLighting(revision,new(){enabled=true},out _),Is.False);Assert.That(editor.Lighting.Same(before),Is.True);Assert.That(editor.LightingRevision,Is.EqualTo(revision));view.Refresh();Assert.That(Shader.GetGlobalFloat("_MaestroLightingEnabled"),Is.Zero);
            }
            yield return null;
        }
    }
}

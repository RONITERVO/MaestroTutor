// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject DiagnosticFact(string id)
        {
            Assert.That(BehaviourCatalog.TryRead(id,1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True,id);
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        [UnityTest] public IEnumerator DiagnosticsAreSharedDetachedAndResetAcrossLifecycleBoundaries()
        {
            Assert.That(RoomControls.Capabilities(editor),Does.Not.Contain(RuntimeDiagnosticFacts.Feature));
            foreach(var id in new[]{"runtime.frameIntervals","runtime.modelBudget","runtime.motionCache"})
                Assert.That(BehaviourCatalog.TryRead(id,1,null,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            var sampler=root.AddComponent<RuntimeDiagnostics>();sampler.SendMessage("OnApplicationFocus",true);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(RuntimeDiagnosticFacts.Feature));
            var empty=DiagnosticFact("runtime.frameIntervals");Assert.That((bool)empty["active"],Is.True);Assert.That((bool)empty["hasSamples"],Is.False);
            yield return new WaitForSecondsRealtime(1.1f);
            var frames=DiagnosticFact("runtime.frameIntervals");Assert.That((bool)frames["hasSamples"],Is.True);Assert.That((int)frames["samples"],Is.GreaterThan(0));
            var catalog=new RoomCapabilityCatalog(editor);Assert.That(catalog.Execute(JObject.Parse("{\"operation\":\"inspect\",\"category\":\"facts\",\"capability\":\"runtime.frameIntervals\",\"version\":1}"),out var error),Is.True,error);
            var inspection=catalog.Observe();Assert.That(JToken.DeepEquals(inspection["value"],frames),Is.True);
            inspection["value"]["samples"]=-1;Assert.That((int)DiagnosticFact("runtime.frameIntervals")["samples"],Is.GreaterThan(0));
            foreach(var message in new[]{"OnApplicationPause","OnApplicationFocus"}){
                bool paused=message=="OnApplicationPause";sampler.SendMessage(message,paused);
                var inactive=DiagnosticFact("runtime.frameIntervals");Assert.That((bool)inactive["active"],Is.False);Assert.That((int)inactive["samples"],Is.Zero);
                sampler.SendMessage(message,!paused);Assert.That((int)DiagnosticFact("runtime.frameIntervals")["samples"],Is.Zero);
            }
            sampler.enabled=false;Assert.That((bool)DiagnosticFact("runtime.frameIntervals")["active"],Is.False);
            sampler.enabled=true;sampler.SendMessage("OnApplicationFocus",true);Assert.That((int)DiagnosticFact("runtime.frameIntervals")["samples"],Is.Zero);
            var models=DiagnosticFact("runtime.modelBudget");var motions=DiagnosticFact("runtime.motionCache");
            Assert.That((int)motions["clips"],Is.EqualTo(editor.Motions.ResidentClipCount));Assert.That((int)motions["curveValueLimit"],Is.EqualTo(MotionLibrary.MaximumResidentCurveValues));
            var output=Environment.GetEnvironmentVariable("MAESTRO_RUNTIME_DIAGNOSTICS");if(!string.IsNullOrWhiteSpace(output)){
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"runtimeDiagnostics.json"),new JObject {["frames"]=frames,["empty"]=empty,["models"]=models,["motions"]=motions,["boundary"]="Unity desktop PlayMode observations, not Quest performance evidence"}.ToString());
            }
        }
        [UnityTest] public IEnumerator NeverActiveImportCanBeDisposedTwiceWithoutReloadOrBudgetLeak()
        {
            var before=ImportedModel.LiveBudget;var child=new GameObject("inactive candidate");child.SetActive(false);child.transform.SetParent(root.transform,false);
            var model=child.AddComponent<ImportedModel>();var asset=ModelLibrary.Inspect("triangle.glb",ModelFixture.Create());
            var load=model.LoadAsync(asset);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);Assert.That(model.Ready,Is.True);
            model.Dispose();model.Dispose();Assert.That(model.Ready,Is.False);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
            var reload=model.LoadAsync(asset);yield return new WaitUntil(()=>reload.IsCompleted);Assert.That(reload.Exception?.InnerException,Is.TypeOf<ObjectDisposedException>());
            UnityEngine.Object.Destroy(child);yield return null;Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ModelDiagnosticsTrackActualReservationAndDisposalWithoutLoadingOnRead()
        {
            root.AddComponent<RuntimeDiagnostics>();var before=DiagnosticFact("runtime.modelBudget");
            var asset=ModelLibrary.Inspect("triangle.glb",ModelFixture.Create());var child=new GameObject("diagnostic import");child.transform.SetParent(root.transform,false);
            var model=child.AddComponent<ImportedModel>();var load=model.LoadAsync(asset);float deadline=Time.realtimeSinceStartup+10;
            while(!load.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(load.IsCompleted,Is.True);Assert.That(load.Exception,Is.Null);
            var after=DiagnosticFact("runtime.modelBudget");Assert.That((int)after["reserved"]["models"],Is.EqualTo((int)before["reserved"]["models"]+1));
            Assert.That((int)after["reserved"]["vertices"],Is.EqualTo((int)before["reserved"]["vertices"]+asset.Inspection.Vertices));
            Assert.That(JToken.DeepEquals(DiagnosticFact("runtime.modelBudget"),after),Is.True);Assert.That(model.IsPlaying,Is.False);
            UnityEngine.Object.Destroy(child);yield return null;Assert.That(JToken.DeepEquals(DiagnosticFact("runtime.modelBudget"),before),Is.True);
        }
    }
}

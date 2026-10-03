// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        sealed class SceneSource:IRoomSceneSource
        {
            public bool Supported {get;set;}=true;
            public int Permissions,Scans,Loads;
            public TaskCompletionSource<bool> PermissionResult=new(),ScanResult=new(),LoadResult=new();
            public Task<bool> Permission(){Permissions++;return PermissionResult.Task;}
            public Task<bool> Scan(){Scans++;return ScanResult.Task;}
            public Task<bool> Load(){Loads++;return LoadResult.Task;}
        }
        ScannedRoom SharedEnvironment(out SceneSource source)
        {
            SharedModes(out _,out _);var scan=world.gameObject.AddComponent<ScannedRoom>();scan.Initialize(world);source=new SceneSource();scan.SetSourceForTests(source);return scan;
        }
        JObject EnvironmentFact(){Assert.That(BehaviourCatalog.TryRead("room.environment",1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);}
        JObject EnvironmentRequest(string operation)
        {
            var fact=EnvironmentFact();var args=new JObject {["operation"]=operation,["stateId"]=fact["stateId"].DeepClone()};if(operation=="cancel")args["requestId"]=fact["requestId"].DeepClone();
            return new JObject {["operation"]="start",["runId"]=modeActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="room.environment.set",["version"]=1,["arguments"]=args}};
        }
        static IEnumerator Until(Func<bool> done){float end=Time.realtimeSinceStartup+5;while(!done()&&Time.realtimeSinceStartup<end)yield return null;Assert.That(done(),Is.True,"Room setup did not reach the expected state");}
        [UnityTest] public IEnumerator SharedLoadAcknowledgesBeforePlatformWorkAndNeverScansOrStartsPhysics()
        {
            var scan=SharedEnvironment(out var source);var before=EnvironmentFact();Assert.That(world.Running,Is.True);
            var request=EnvironmentRequest("load");Assert.That(modeActions.Execute(request,out var error),Is.True,error);var receipt=modeActions.Observe().DeepClone();
            Assert.That((string)receipt["selected"]["phase"],Is.EqualTo("completed"));Assert.That((string)receipt["selected"]["output"]["phase"],Is.EqualTo("permission"));Assert.That(scan.Busy,Is.True);Assert.That(world.Running,Is.False);
            yield return Until(()=>source.Permissions==1);source.PermissionResult.SetResult(true);yield return Until(()=>source.Loads==1);Assert.That(source.Scans,Is.Zero);
            var loading=EnvironmentFact();source.LoadResult.SetResult(true);yield return Until(()=>!scan.Busy);
            Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("loaded"));Assert.That(world.Running,Is.False);Assert.That(world.SurfacesReady,Is.False,"Platform completion alone is not tracked collider readiness");
            Assert.That(modeActions.Execute(request,out error),Is.True,error);yield return null;Assert.That(source.Loads,Is.EqualTo(1),"A duplicate receipt cannot reopen loading");
            var loaded=EnvironmentFact();var show=EnvironmentRequest("show");Assert.That(modeActions.Execute(show,out error),Is.True,error);var showReceipt=modeActions.Observe().DeepClone();var showing=EnvironmentFact();
            Assert.That((bool)showing["surfaces"]["showing"],Is.True);Assert.That((bool)showing["surfaces"]["visible"],Is.True);Assert.That(world.Running,Is.False);
            var hide=EnvironmentRequest("hide");Assert.That(modeActions.Execute(hide,out error),Is.True,error);var hideReceipt=modeActions.Observe().DeepClone();var hidden=EnvironmentFact();Assert.That((bool)hidden["surfaces"]["showing"],Is.False);
            var output=Environment.GetEnvironmentVariable("MAESTRO_ROOM_ENVIRONMENT");if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"room-environment.json"),new JObject {["before"]=before,["loadRequest"]=request,["loadReceipt"]=receipt,["loading"]=loading,["loaded"]=loaded,["showRequest"]=show,["showReceipt"]=showReceipt,["showing"]=showing,["hideRequest"]=hide,["hideReceipt"]=hideReceipt,["hidden"]=hidden,["platformBoundary"]="controlled OS/SDK source; no headset scan"}.ToString());}
        }
        [UnityTest] public IEnumerator ExplicitScanWaitsForPermissionAndSystemReturnBeforeLoading()
        {
            var scan=SharedEnvironment(out var source);scan.Scan();yield return Until(()=>source.Permissions==1);
            scan.SendMessage("OnApplicationFocus",false);scan.SendMessage("OnApplicationPause",true);source.PermissionResult.SetResult(true);yield return Until(()=>(string)EnvironmentFact()["phase"]=="returning");Assert.That(source.Scans,Is.Zero);
            scan.SendMessage("OnApplicationPause",false);scan.SendMessage("OnApplicationFocus",true);yield return Until(()=>source.Scans==1);
            scan.SendMessage("OnApplicationFocus",false);source.ScanResult.SetResult(true);yield return Until(()=>(string)EnvironmentFact()["phase"]=="returning");Assert.That(source.Loads,Is.Zero);
            scan.SendMessage("OnApplicationFocus",true);yield return Until(()=>source.Loads==1);source.LoadResult.SetResult(true);yield return Until(()=>!scan.Busy);
            Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("loaded"));Assert.That(world.Running,Is.False);
        }
        [UnityTest] public IEnumerator CancelBindsTheCurrentRequestAndHoldsAdmissionUntilTheRealWorkerDrains()
        {
            var scan=SharedEnvironment(out var source);scan.Scan();yield return Until(()=>source.Permissions==1);var wrong=EnvironmentRequest("cancel");wrong["call"]["arguments"]["requestId"]=Guid.NewGuid().ToString("N");
            Assert.That(modeActions.Execute(wrong,out _),Is.False);Assert.That(scan.CanCancel,Is.True);
            var cancel=EnvironmentRequest("cancel");Assert.That(modeActions.Execute(cancel,out var error),Is.True,error);Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("cancelled"));Assert.That(scan.Busy,Is.True);Assert.That(scan.CanCancel,Is.False);
            Assert.That(modeActions.Execute(EnvironmentRequest("load"),out _),Is.False);source.PermissionResult.SetResult(true);yield return Until(()=>!scan.Busy);
            Assert.That(source.Scans,Is.Zero);Assert.That(source.Loads,Is.Zero);Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("cancelled"));Assert.That(world.SurfacesReady,Is.False);
        }
        [UnityTest] public IEnumerator LoadingFocusLossAndWorkspaceHoldDiscardLateResultsWithoutAutomaticRetry()
        {
            var scan=SharedEnvironment(out var source);source.PermissionResult.SetResult(true);scan.Load();yield return Until(()=>source.Loads==1);
            scan.SendMessage("OnApplicationFocus",false);source.LoadResult.SetResult(true);yield return Until(()=>!scan.Busy);scan.SendMessage("OnApplicationFocus",true);
            Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("cancelled"));Assert.That(world.SurfacesReady,Is.False);Assert.That(source.Loads,Is.EqualTo(1));
            source=new SceneSource();scan.SetSourceForTests(source);scan.Scan();yield return Until(()=>source.Permissions==1);
            using(var hold=editor.RuntimeGate.Hold("Test workspace boundary")){source.PermissionResult.SetResult(true);yield return Until(()=>!scan.Busy);Assert.That((bool)EnvironmentFact()["availability"]["canLoad"],Is.False);}
            Assert.That(source.Scans,Is.Zero);Assert.That(source.Loads,Is.Zero);Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("cancelled"));Assert.That(world.Running,Is.False);
        }
        [UnityTest] public IEnumerator StaleVisibilityAndVirtualViewRequestsFailWithoutChangingStateOrStartingWork()
        {
            var scan=SharedEnvironment(out var source);var stale=EnvironmentRequest("show");scan.ToggleSurfaces();scan.ToggleSurfaces();var before=EnvironmentFact();
            Assert.That(modeActions.Execute(stale,out _),Is.False);Assert.That(JToken.DeepEquals(before,EnvironmentFact()),Is.True,"A readiness check or stale call has no scan side effect");
            scan.SetVirtualView(true);before=EnvironmentFact();Assert.That((bool)before["surfaces"]["visible"],Is.True);Assert.That(modeActions.Execute(EnvironmentRequest("hide"),out _),Is.False);Assert.That(modeActions.Execute(EnvironmentRequest("scan"),out _),Is.False);
            Assert.That(JToken.DeepEquals(before,EnvironmentFact()),Is.True);scan.SetVirtualView(false);Assert.That((bool)EnvironmentFact()["surfaces"]["visible"],Is.False);Assert.That(source.Permissions,Is.Zero);
            var pending=EnvironmentRequest("load");scan.enabled=false;scan.enabled=true;Assert.That(modeActions.Execute(pending,out _),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator DisabledOrDestroyedComponentsDiscardLateWorkAndLoaderFailuresRemainRetryable()
        {
            var scan=SharedEnvironment(out var source);source.PermissionResult.SetResult(true);scan.Load();yield return Until(()=>source.Loads==1);
            scan.enabled=false;source.LoadResult.SetResult(true);yield return Until(()=>!scan.Busy);scan.enabled=true;
            Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("cancelled"));Assert.That(world.SurfacesReady,Is.False);Assert.That(world.Running,Is.False);
            source=new SceneSource();source.PermissionResult.SetResult(true);scan.SetSourceForTests(source);scan.Load();yield return Until(()=>source.Loads==1);
            LogAssert.Expect(LogType.Warning,"Room scene could not load: InvalidOperationException");source.LoadResult.SetException(new InvalidOperationException("test loader failure"));yield return Until(()=>!scan.Busy);
            Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("failed"));Assert.That((bool)EnvironmentFact()["availability"]["canLoad"],Is.True);
            source=new SceneSource();scan.SetSourceForTests(source);scan.Scan();yield return Until(()=>source.Permissions==1);UnityEngine.Object.Destroy(scan);yield return null;
            source.PermissionResult.SetResult(true);yield return null;yield return null;Assert.That(source.Scans,Is.Zero);Assert.That(source.Loads,Is.Zero);Assert.That(world.SurfacesReady,Is.False);
        }
        [UnityTest] public IEnumerator PermissionDenialScanCancellationAndTimeoutHaveTruthfulRetainedState()
        {
            var scan=SharedEnvironment(out var source);scan.Load();yield return Until(()=>source.Permissions==1);source.PermissionResult.SetResult(false);yield return Until(()=>!scan.Busy);
            Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("failed"));Assert.That(source.Loads,Is.Zero);
            source=new SceneSource();source.PermissionResult.SetResult(true);scan.SetSourceForTests(source);scan.Scan();yield return Until(()=>source.Scans==1);source.ScanResult.SetResult(false);yield return Until(()=>!scan.Busy);Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("cancelled"));Assert.That(source.Loads,Is.Zero);
            source=new SceneSource();scan.SetSourceForTests(source,20);scan.Load();yield return Until(()=>(string)EnvironmentFact()["phase"]=="failed");Assert.That(scan.Busy,Is.True);Assert.That((bool)EnvironmentFact()["availability"]["canLoad"],Is.False);
            source.PermissionResult.SetResult(true);yield return Until(()=>!scan.Busy);Assert.That(source.Loads,Is.Zero);Assert.That((string)EnvironmentFact()["phase"],Is.EqualTo("failed"));
        }
    }
}

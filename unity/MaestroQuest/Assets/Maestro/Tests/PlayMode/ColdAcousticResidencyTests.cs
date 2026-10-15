// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        IEnumerator ColdAcousticSettled(RoomAcoustics audio)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            do{editor.TickNativeAcoustics();audio.Synchronize();yield return null;}while((editor.AcousticPreparationPending||audio.OmittedCount>0)&&Time.realtimeSinceStartupAsDouble<deadline);
            Assert.That(editor.AcousticPreparationPending,Is.False);Assert.That(audio.OmittedCount,Is.Zero);
        }
        T ColdAcousticField<T>(string name)=>(T)typeof(RoomEditor).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
        IEnumerator ColdAcousticCancelled(string change)
        {
            yield return GeometryModel(StaticModelFixture.Create());string id=geometryTarget,area=NativeAreaFor(id);var audio=AreaAcoustics();
            string error;
            if(change=="workspace"){yield return WaitForModuleLibrary();Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;}
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;editor.SendMessage("ReleaseNativeAcoustics");audio.Synchronize();
            var slot=(SemaphoreSlim)typeof(ModelLibrary).GetField("boundsPreparation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor.Models);
            Assert.That(slot.Wait(0),Is.True);Task activation=null;
            try{
                editor.TickNativeAcoustics();var pending=ColdAcousticField<Task>("acousticPreparation");Assert.That(pending,Is.Not.Null);Assert.That(pending.IsCompleted,Is.False);
                if(change=="focus")editor.SendMessage("OnApplicationFocus",false);
                else if(change=="disable")editor.enabled=false;
                else if(change=="delete")Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);
                else if(change=="activate")activation=editor.ActivateNativeArea(area);
                else if(change=="workspace")Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
                else if(change=="timeout")typeof(RoomEditor).GetField("acousticDeadline",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(editor,-1d);
                else {var data=editor.Read(id);data.modelGeometry=new RoomModelGeometry{pivot="base"};Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);}
                double deadline=Time.realtimeSinceStartupAsDouble+5;
                while(!pending.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline){editor.TickNativeAcoustics();yield return null;}
                Assert.That(pending.IsCompleted,Is.True,"A cancelled library waiter must finish while the inspection lane is still held");
                Assert.That(pending.Exception,Is.Null);Assert.That(audio.RetainedGeometryCount,Is.Zero,"Stale preparation must never publish");
                if(change=="timeout"){
                    Assert.That(editor.AcousticPreparationIssue(id),Does.Contain("timed out"));
                    for(int i=0;i<8;i++){editor.TickNativeAcoustics();yield return null;}
                    Assert.That(editor.AcousticPreparationPending,Is.False,"An unchanged failure must not retry every frame");Assert.That(audio.OmittedCount,Is.GreaterThan(0));
                    root.AddComponent<Maestro.Quest.Diagnostics.RuntimeDiagnostics>();
                    Assert.That(BehaviourCatalog.TryRead("runtime.acoustics",1,null,new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);
                    Assert.That((string)((JObject)fact.Value)["issue"],Does.Contain("timed out"));
                }
                if(change=="delete")Assert.That(audio.OmittedCount,Is.Zero);
            }finally{slot.Release();editor.SendMessage("OnApplicationFocus",true);editor.enabled=true;}
            if(activation!=null){double deadline=Time.realtimeSinceStartupAsDouble+10;while(!activation.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Assert.That(activation.IsCompleted,Is.True);Assert.That(activation.Exception,Is.Null);}
        }
        [UnityTest] public IEnumerator ColdAcousticWorkspaceReplacementCancelsLibraryWait()=>ColdAcousticCancelled("workspace");
        [UnityTest] public IEnumerator ColdAcousticFocusLossCancelsLibraryWait()=>ColdAcousticCancelled("focus");
        [UnityTest] public IEnumerator ColdAcousticDisableCancelsLibraryWait()=>ColdAcousticCancelled("disable");
        [UnityTest] public IEnumerator ColdAcousticDeletionCancelsLibraryWait()=>ColdAcousticCancelled("delete");
        [UnityTest] public IEnumerator ColdAcousticEditCancelsLibraryWait()=>ColdAcousticCancelled("edit");
        [UnityTest] public IEnumerator ColdAcousticNativeTakeoverCancelsLibraryWait()=>ColdAcousticCancelled("activate");
        [UnityTest] public IEnumerator ColdAcousticTimeoutReportsUnknownWithoutRepeatedRetry()=>ColdAcousticCancelled("timeout");
        [UnityTest] public IEnumerator ColdAcousticEmptyCutoutCacheInvalidatesWhenTerrainAdded()
        {
            yield return GeometryModel(StaticModelFixture.Create(json=>{json["materials"]=new JArray(new JObject{["alphaMode"]="MASK",["alphaCutoff"]=.5});json["meshes"][0]["primitives"][0]["material"]=0;}));
            string id=geometryTarget,area=NativeAreaFor(id);var audio=AreaAcoustics();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            yield return ColdAcousticSettled(audio);Assert.That(audio.RetainedGeometryCount,Is.Zero);
            var field=new RoomHeightField{cells=4,width=1,depth=1,maxHeight=.2f,heights=Enumerable.Repeat(.1f,25).ToArray()};
            var data=editor.Read(id);data.heightFields=new[]{field};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            yield return ColdAcousticSettled(audio);Assert.That(audio.RetainedGeometryCount,Is.GreaterThan(0));Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator ColdAcousticMissingPrivateModelRemainsUnknownUntilAnExplicitRetry()
        {
            yield return GeometryModel(StaticModelFixture.Create());string id=geometryTarget,area=NativeAreaFor(id);var audio=AreaAcoustics();
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;editor.SendMessage("ReleaseNativeAcoustics");audio.Synchronize();
            string folder=(string)typeof(ModelLibrary).GetField("directory",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor.Models);
            string path=Path.Combine(folder,editor.Read(id).modelHash+".glb");File.Move(path,path+".held");
            try{
                double deadline=Time.realtimeSinceStartupAsDouble+5;
                do{editor.TickNativeAcoustics();yield return null;}while(editor.AcousticPreparationIssue(id)==null&&Time.realtimeSinceStartupAsDouble<deadline);
                Assert.That(editor.AcousticPreparationIssue(id),Does.Contain("missing"));Assert.That(audio.RetainedGeometryCount,Is.Zero);Assert.That(audio.OmittedCount,Is.GreaterThan(0));
                for(int i=0;i<8;i++){editor.TickNativeAcoustics();yield return null;}
                Assert.That(editor.AcousticPreparationPending,Is.False);Assert.That(editor.Find(id),Is.Null);
            }finally{File.Move(path+".held",path);}
            var data=editor.Read(id);data.name="Retry saved model";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            yield return ColdAcousticSettled(audio);Assert.That(audio.RetainedGeometryCount,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator ColdAcousticRebuildsSavedBlockWithoutRecreatingTheView()
        {
            string id=editor.Identity(block);var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            string area=NativeAreaFor(id);var audio=AreaAcoustics();int count=audio.GeometryCount,revision=editor.Revision;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            editor.SendMessage("ReleaseNativeAcoustics");audio.Synchronize();Assert.That(audio.GeometryCount,Is.LessThan(count));
            yield return ColdAcousticSettled(audio);
            Assert.That(editor.Find(id),Is.Null);Assert.That(audio.GeometryCount,Is.EqualTo(count));Assert.That(audio.RetainedGeometryCount,Is.GreaterThan(0));Assert.That(editor.Revision,Is.EqualTo(revision));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));audio.Synchronize();
            Assert.That(audio.RetainedGeometryCount,Is.Zero);Assert.That(audio.GeometryCount,Is.EqualTo(count));Assert.That(editor.Find(id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator ColdAcousticDormantRecipeEditAndUndoChangeMeasuredObstruction()
        {
            var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="wall",size=new Vector3(2,2,.05f)}}};
            Assert.That(editor.CreateRecipe("Cold acoustic wall",new Vector3(3,0,1),2,recipe,out var id,out var error),Is.True,error);
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            string area=NativeAreaFor(id);var audio=AreaAcoustics();Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            using var output=new AcousticOutputSample(root,new Vector3(3,0,0));var energy=new double[3];yield return output.Sample(value=>energy[0]=value);
            data=editor.Read(id);data.recipe.parts[0].position=Vector3.right*1.5f;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            yield return ColdAcousticSettled(audio);Assert.That(editor.Find(id),Is.Null);yield return output.Sample(value=>energy[1]=value);
            editor.Undo();yield return ColdAcousticSettled(audio);Assert.That(editor.Find(id),Is.Null);yield return output.Sample(value=>energy[2]=value);
            Assert.That(energy[0],Is.LessThan(energy[1]*.7));Assert.That(energy[2],Is.EqualTo(energy[0]).Within(energy[0]*.05));
        }
    }
    public sealed partial class RoomEditorTests
    {
        [UnityTest] public IEnumerator ColdAcousticStartupDoesNotDuplicateNativeDisplayPreparation()
        {
            yield return ReloadForStartup(StartupManyInOneArea());yield return StartupAdmissionStarted();var audio=root.AddComponent<RoomAcoustics>();Assert.That(audio.Ready,Is.True,audio.Issue);
            editor.TickNativeAcoustics();Assert.That(editor.AcousticPreparationPending,Is.False);Assert.That(audio.OmittedCount,Is.GreaterThan(0));
            yield return StartupFinished();for(int i=0;i<16;i++)audio.Synchronize();editor.TickNativeAcoustics();
            Assert.That(audio.OmittedCount,Is.Zero);Assert.That(audio.GeometryCount,Is.GreaterThan(0));Assert.That(audio.RetainedGeometryCount,Is.Zero);
        }
    }
    public sealed partial class RoomAcousticMapTests
    {
        [UnityTest] public IEnumerator ColdAcousticPendingBoundaryCancelsNativeMapAndBlocksIncompleteResults()
        {
            Box();HoldCompute();Assert.That(room.RequestMap(new[]{Vector3.zero}),Is.True);yield return NativeComputationEntered();
            var owner=new object();room.SetPreparing(owner,true);long revision=room.StructureRevision;
            room.SetPreparing(owner,true);Assert.That(room.StructureRevision,Is.EqualTo(revision),"Stable pending input must not continually invalidate the map");
            gate.Set();yield return Finish();Assert.That(room.MapReady,Is.False);Assert.That(room.OmittedBoundaries,Is.EqualTo(1));
            Assert.That(room.RequestMap(new[]{Vector3.zero}),Is.False);room.SetPreparing(owner,null);room.FirstMapProgress=null;
            Assert.That(room.RequestMap(new[]{Vector3.zero}),Is.True);yield return Finish();Assert.That(room.MapReady,Is.True,room.MapIssue);
            room.SetPreparing(owner,false);Assert.That(room.MapReady,Is.True,"Pending dynamic geometry does not alter fixed reflection boundaries");
            room.SetPreparing(owner,true);Assert.That(room.MapReady,Is.False,"A completed map must also lose readiness for unknown walls");
            room.SetPreparing(owner,null);
        }
    }
    public sealed partial class RoomAcousticsTests
    {
        [UnityTest] public IEnumerator ColdAcousticNeverInstantiatedWallObstructsRealAudio()
        {
            var data=new RoomObjectData{kind=RoomObjectKind.Assembly,recipe=new RoomRecipe{parts=new[]{new RecipePart{id="wall",size=new Vector3(2,2,.05f)}}}};
            var prepare=RoomAcousticGeometry.Prepare(data,null,null,new RoomPreparationBudget(()=>{}),CancellationToken.None);while(!prepare.IsCompleted)yield return null;
            Assert.That(prepare.IsFaulted,Is.False,prepare.Exception?.ToString());Assert.That(root.GetComponentsInChildren<Renderer>().Length,Is.Zero);
            Assert.That(room.TryPrepareInputs(prepare.Result,root.transform,Matrix4x4.TRS(Vector3.forward,Quaternion.identity,Vector3.one*2),true,out var cached,out var error),Is.True,error);
            using(cached)using(var output=new AcousticOutputSample(root,Vector3.zero)){
                double open=0,blocked=0;yield return output.Sample(value=>open=value);
                Assert.That(cached.Commit(),Is.True);room.Synchronize();yield return null;Assert.That(cached.Admitted,Is.True);
                yield return output.Sample(value=>blocked=value);Assert.That(open,Is.GreaterThan(.0001));Assert.That(blocked,Is.LessThan(open*.7));
                Assert.That(root.GetComponentsInChildren<Renderer>().Length,Is.Zero);Assert.That(root.GetComponentsInChildren<AcousticSurface>().Length,Is.Zero);
            }
            yield return null;Assert.That(room.RetainedGeometryCount,Is.Zero);Assert.That(room.GeometryCount,Is.Zero);
        }
        [UnityTest] public IEnumerator ColdAcousticInputReservationsRefuseOverflowAndReleaseForRetry()
        {
            var data=new RoomObjectData{kind=RoomObjectKind.Block};var prepare=RoomAcousticGeometry.Prepare(data,null,null,new RoomPreparationBudget(()=>{}),CancellationToken.None);while(!prepare.IsCompleted)yield return null;
            var maximum=Enumerable.Repeat(prepare.Result.Single(),RoomAcoustics.MaximumVirtualSurfaces).ToArray();
            Assert.That(room.TryPrepareInputs(maximum,root.transform,Matrix4x4.identity,true,out var held,out var error),Is.True,error);
            using(held){Assert.That(room.TryPrepareInputs(prepare.Result,root.transform,Matrix4x4.identity,true,out var refused,out var issue),Is.False);Assert.That(refused,Is.Null);Assert.That(issue,Does.Contain("budget"));Assert.That(room.GeometryCount,Is.Zero);}
            Assert.That(room.TryPrepareInputs(prepare.Result,root.transform,Matrix4x4.identity,true,out var retry,out error),Is.True,error);retry.Dispose();Assert.That(room.RetainedGeometryCount,Is.Zero);yield return null;
        }
    }
}

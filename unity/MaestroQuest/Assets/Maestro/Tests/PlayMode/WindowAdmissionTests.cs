// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        List<WindowResources.Lease> FillWindows(bool bundles=false,int spare=0){
            var held=new List<WindowResources.Lease>();int count=(bundles?WindowResources.MaximumBundles:WindowResources.MaximumMasks)-(int)WindowResources.ObserveBudget()[bundles?"bundles":"masks"]-spare;
            try{for(int i=0;i<count;i++){var owner=new RoomResourceOwner(editor.WorldIdentity,"pending","window");held.Add(bundles?WindowResources.Bundle(owner):WindowResources.Mask(owner,"Pending"));}return held;}catch{foreach(var lease in held)lease.Dispose();throw;}
        }
        static void ReleaseWindows(IEnumerable<WindowResources.Lease> leases){foreach(var lease in leases)lease.Dispose();}
        void CreateWindow(){SurfaceFixture(SurfaceConfigure());var a=(JObject)WindowCall()["arguments"];Assert.IsTrue(editor.EditWindow((string)a["target"],(int)a["revision"],a,out var error),error);}
        Renderer WindowMask()=>block.GetComponentsInChildren<Renderer>(true).Single(RoomWindowView.IsMask);
        [UnityTest]public IEnumerator WindowAdmissionFullBudgetReusesAndRebindsMasksBeforeRemovingTheirOldSurface(){
            CreateWindow();string id=editor.Identity(block);var mask=WindowMask();var mesh=mask.GetComponent<MeshFilter>().sharedMesh;var material=mask.sharedMaterial;var held=FillWindows();held.AddRange(FillWindows(true));
            try{
                var data=editor.Read(id);data.surfaces=new[]{new DrawingSurface{id="Rear",position=Vector3.up*.1f,width=.8f,height=.6f}};data.windows[0].surface="Rear";data.windows[0].shape="ellipse";data.windows[0].reveal=.3f;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);yield return null;
                Assert.That(WindowMask(),Is.SameAs(mask));Assert.That(mask.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));Assert.That(mask.sharedMaterial,Is.SameAs(material));Assert.That(mask.transform.parent,Is.SameAs(block.GetComponent<DrawingSurfaceView>().Surface("Rear")));Assert.That(mask.transform.localScale,Is.EqualTo(new Vector3(.8f,.6f,1)));var properties=new MaterialPropertyBlock();mask.GetPropertyBlock(properties);Assert.That(properties.GetFloat("_Reveal"),Is.EqualTo(.3f));Assert.That(properties.GetFloat("_Ellipse"),Is.EqualTo(1));
                Assert.That(editor.TryFlush(out error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;data=editor.Read(id);data.surfaces=data.surfaces.Append(new DrawingSurface{id="Extra"}).ToArray();data.windows=data.windows.Append(new RoomWindow{id="Extra",surface="Extra"}).ToArray();
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.False);StringAssert.Contains("live window budget",error);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(WindowMask(),Is.SameAs(mask));
                Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),new JObject{["operation"]="remove",["window"]="Window"},out error),Is.True,error);Assert.That((int)WindowResources.ObserveBudget()["masks"],Is.EqualTo(32));Assert.That((int)WindowResources.ObserveBudget()["bundles"],Is.EqualTo(32));Assert.That(block.GetComponent<RoomWindowView>().Requested,Is.False);yield return null;
                Assert.That((int)WindowResources.ObserveBudget()["masks"],Is.EqualTo(31));Assert.That((int)WindowResources.ObserveBudget()["bundles"],Is.EqualTo(31));Assert.That(!mesh&&!material,Is.True);Assert.That(block.GetComponent<RoomWindowView>().ReusableBundle,Is.Null);
            }finally{ReleaseWindows(held);}
        }
        [UnityTest]public IEnumerator WindowAdmissionFailedImmediateSaveNeverPublishesAndRetainsDeferredCharges(){
            yield return SurfaceRun(new RoomAgentExecutor(editor),SurfaceConfigure());string id=editor.Identity(block);Assert.That(editor.TryFlush(out var error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;bool undo=editor.CanUndo,redo=editor.CanRedo;
            using(var locked=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read))Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),(JObject)WindowCall()["arguments"],out error),Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(block.GetComponent<RoomWindowView>(),Is.Null);Assert.That((int)WindowResources.ObserveBudget()["retiring"],Is.EqualTo(2));yield return null;
            Assert.That((int)WindowResources.ObserveBudget()["masks"],Is.Zero);Assert.That((int)WindowResources.ObserveBudget()["bundles"],Is.Zero);Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),(JObject)WindowCall()["arguments"],out error),Is.True,error);Assert.That(block.GetComponent<RoomWindowView>().Requested,Is.True);
            var mask=WindowMask();revision=editor.Revision;bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            using(var locked=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read))Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),(JObject)WindowCall(0)["arguments"],out error),Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(WindowMask(),Is.SameAs(mask));Assert.That(editor.Read(id).windows[0].reveal,Is.EqualTo(1));Assert.That(block.GetComponent<RoomWindowView>().Requested,Is.True);var props=new MaterialPropertyBlock();mask.GetPropertyBlock(props);Assert.That(props.GetFloat("_Reveal"),Is.EqualTo(1));Assert.That((int)WindowResources.ObserveBudget()["retiring"],Is.Zero,"Failed property-only edits cannot retire accepted geometry");
        }
        [UnityTest]public IEnumerator WindowAdmissionLaterBatchRefusalReleasesOnlyPrivateCandidates(){
            var values=editor.Snapshot().objects.Where(x=>!x.IsBuiltIn).Take(2).ToArray();foreach(var data in values){data.surfaces=new[]{new DrawingSurface{id="Front"}};data.windows=new[]{new RoomWindow{surface="Front"}};}
            Assert.That(editor.TryFlush(out var error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;var held=FillWindows(spare:1);
            try{
                Assert.That(editor.ApplyAgentEdit(editor.Revision,values,Array.Empty<string>(),out error),Is.False);StringAssert.Contains("live window budget",error);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(root.GetComponentsInChildren<RoomWindowView>(true),Is.Empty);Assert.That((int)WindowResources.ObserveBudget()["retiring"],Is.EqualTo(3));yield return null;Assert.That((int)WindowResources.ObserveBudget()["masks"],Is.EqualTo(31));Assert.That((int)WindowResources.ObserveBudget()["bundles"],Is.Zero);
            }finally{ReleaseWindows(held);}
        }
        [UnityTest]public IEnumerator WindowAdmissionUndoTemporaryDiscardAndDormantActivationKeepRetry(){
            CreateWindow();string id=editor.Identity(block);Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),new JObject{["operation"]="remove",["window"]="Window"},out var error),Is.True,error);yield return null;int revision=editor.Revision;var held=FillWindows();
            try{editor.Undo();Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.Read(id).windows,Is.Empty);Assert.That(editor.CanUndo,Is.True);}finally{ReleaseWindows(held);}yield return null;
            editor.Undo();Assert.That(editor.Read(id).windows.Length,Is.EqualTo(1));editor.Redo();Assert.That(editor.Read(id).windows,Is.Empty);yield return null;editor.Undo();Assert.That(editor.Read(id).windows.Length,Is.EqualTo(1));
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),new JObject{["operation"]="remove",["window"]="Window"},out error),Is.True,error);yield return null;string session=editor.TemporarySessionId;held=FillWindows();
            try{Assert.That(editor.DiscardTemporaryRoom(out error),Is.False);StringAssert.Contains("live window budget",error);Assert.That(editor.TemporarySessionId,Is.EqualTo(session));Assert.That(editor.Read(id).windows,Is.Empty);}finally{ReleaseWindows(held);}yield return null;
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(block.GetComponent<RoomWindowView>().Requested,Is.True);string area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;held=FillWindows();revision=editor.Revision;
            try{var failed=editor.ActivateNativeArea(area);yield return NativeAreaCompletion(failed,false);StringAssert.Contains("live window budget",failed.Result);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));}finally{ReleaseWindows(held);}yield return null;
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id).GetComponent<RoomWindowView>().Requested,Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest]public IEnumerator WindowAdmissionMissingSurfaceRepairIsPreparedBeforeVisualOnlySave(){
            CreateWindow();string id=editor.Identity(block);var bundle=block.GetComponent<RoomWindowView>().ReusableBundle;UnityEngine.Object.DestroyImmediate(block.GetComponent<DrawingSurfaceView>());yield return null;int revision=editor.Revision;var held=FillWindows();
            try{Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),(JObject)WindowCall(.4f)["arguments"],out var error),Is.False);StringAssert.Contains("live window budget",error);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(block.GetComponent<DrawingSurfaceView>(),Is.Null);Assert.That(editor.Read(id).windows[0].reveal,Is.EqualTo(1));}finally{ReleaseWindows(held);}yield return null;
            Assert.That(editor.EditWindow(id,editor.ObjectRevision(id),(JObject)WindowCall(.4f)["arguments"],out var issue),Is.True,issue);Assert.That(block.GetComponent<DrawingSurfaceView>().Surface("Front"),Is.Not.Null);Assert.That(WindowMask().transform.parent,Is.SameAs(block.GetComponent<DrawingSurfaceView>().Surface("Front")));Assert.That(block.GetComponent<RoomWindowView>().ReusableBundle,Is.SameAs(bundle));Assert.That(block.GetComponent<RoomWindowView>().Requested,Is.True);
        }
        [UnityTest]public IEnumerator WindowAdmissionUnrelatedReconciliationCannotRecreateMissingPeerWindows(){
            CreateWindow();string id=editor.Identity(block);UnityEngine.Object.DestroyImmediate(block.GetComponent<RoomWindowView>());yield return null;var held=FillWindows();held.AddRange(FillWindows(true));
            try{var peer=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id);Assert.That(editor.PaintObject(peer.id,Color.red,out var error),Is.True,error);Assert.That(block.GetComponent<RoomWindowView>(),Is.Null);Assert.That(editor.Read(id).windows.Length,Is.EqualTo(1));Assert.That(editor.PaintObject(id,Color.blue,out error),Is.False);StringAssert.Contains("live window budget",error);}finally{ReleaseWindows(held);}
            Assert.That(editor.PaintObject(id,Color.blue,out var issue),Is.True,issue);Assert.That(block.GetComponent<RoomWindowView>().Requested,Is.True);
        }
        JObject[] WindowReservations(string id){var budget=CollisionFact("runtime.windowBudget");int count=(int)budget["masks"]+(int)budget["bundles"];return Enumerable.Range(0,count).Select(i=>CollisionFact("runtime.windowReservation",new JObject{["index"]=i})).Where(x=>(string)x["target"]==id).OrderBy(x=>(string)x["kind"]).ToArray();}
        [UnityTest]public IEnumerator WindowAdmissionCapturesPassiveFactsAcrossPrivateReadyInactiveAndRetiringLifetime(){
            root.AddComponent<RuntimeDiagnostics>();var data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),surfaces=new[]{new DrawingSurface{id="Front"}},windows=new[]{new RoomWindow{surface="Front"}}};var host=new GameObject("Window lifecycle");host.transform.SetParent(root.transform,false);host.AddComponent<DrawingSurfaceView>().Apply(data.surfaces);var view=host.AddComponent<RoomWindowView>();var held=new List<WindowResources.Lease>();
            try{
                using var candidate=new PreparedRoomWindows(editor.WorldIdentity,data,view);var preparing=new JArray(WindowReservations(data.id));Assert.That(view.Requested,Is.False);Assert.That((int)WindowResources.ObserveBudget()["preparing"],Is.EqualTo(2));view.Apply(data,null,null,candidate);candidate.Dispose();var ready=new JArray(WindowReservations(data.id));Assert.That(view.Requested,Is.True);Assert.That(view.RenderingReady,Is.False);host.SetActive(false);var disabled=new JArray(WindowReservations(data.id));held=FillWindows();held.AddRange(FillWindows(true));var budget=CollisionFact("runtime.windowBudget");var again=CollisionFact("runtime.windowBudget");Assert.That(again.ToString(),Is.EqualTo(budget.ToString()));again["masks"]=0;Assert.That((int)CollisionFact("runtime.windowBudget")["masks"],Is.EqualTo(32));
                NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);var retiring=new JArray(WindowReservations(data.id));Assert.That(retiring.All(x=>(string)x["phase"]=="retiring"),Is.True);Assert.Throws<RoomResourceLimitException>(()=>WindowResources.Mask(new RoomResourceOwner(null,"blocked","window"),"Blocked"));
                string output=Environment.GetEnvironmentVariable("MAESTRO_WINDOW_ADMISSION_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject{["budget"]=budget,["preparing"]=preparing,["ready"]=ready,["disabled"]=disabled,["retiring"]=retiring}.ToString());
                yield return null;Assert.That(WindowReservations(data.id),Is.Empty);using var retry=WindowResources.Mask(new RoomResourceOwner(null,"retry","window"),"Retry");
            }finally{ReleaseWindows(held);if(host){NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);}}
        }
    }
}

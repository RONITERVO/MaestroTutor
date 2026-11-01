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
        List<LightResources.Lease> FillLamps(int spare=0){
            var held=new List<LightResources.Lease>();int count=LightResources.MaximumLights-(int)LightResources.ObserveBudget()["lights"]-spare;
            try{for(int i=0;i<count;i++)held.Add(LightResources.Begin(new RoomLight(),new RoomResourceOwner(editor.WorldIdentity,"pending","lighting")));return held;}
            catch{foreach(var lease in held)lease.Dispose();throw;}
        }
        JObject[] LampReservations(string target){int count=(int)CollisionFact("runtime.lightBudget")["lights"];return Enumerable.Range(0,count).Select(i=>CollisionFact("runtime.lightReservation",new JObject{["index"]=i})).Where(x=>(string)x["target"]==target).ToArray();}
        [UnityTest]public IEnumerator LightAdmissionFullBudgetPreservesSaveAndHistoryButAllowsReuseAndRemoval(){
            string target=editor.Identity(block);Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight()),out var error),Is.True,error);
            Assert.That(editor.TryFlush(out error),Is.True,error);var saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;bool undo=editor.CanUndo,redo=editor.CanRedo;
            var lamp=block.GetComponentInChildren<Light>(true);var held=FillLamps();
            try{
                Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight{color="#FF0000"}),out error),Is.False);StringAssert.Contains("live light or shadow budget",error);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(block.GetComponentInChildren<Light>(true),Is.SameAs(lamp));Assert.That(lamp.isActiveAndEnabled,Is.True);
                var data=editor.Read(target);data.color=Color.blue;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);Assert.That(block.GetComponentInChildren<Light>(true),Is.SameAs(lamp));
                Assert.That(editor.EditLight(target,editor.ObjectRevision(target),new JObject{["operation"]="remove",["light"]="Lamp"},out error),Is.True,error);Assert.That(lamp.isActiveAndEnabled,Is.False);Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(8));
                yield return null;Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(7));
            }finally{foreach(var lease in held)lease.Dispose();}
            editor.Undo();yield return null;Assert.That(block.GetComponent<RoomLightView>().Active("Lamp"),Is.True);
        }
        [UnityTest]public IEnumerator LightAdmissionFailedSaveKeepsAcceptedLampAndRetainsCandidateUntilDestroyed(){
            string target=editor.Identity(block);Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight()),out var error),Is.True,error);Assert.That(editor.TryFlush(out error),Is.True,error);
            var lamp=block.GetComponentInChildren<Light>(true);var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision,count=(int)LightResources.ObserveBudget()["lights"];string saved=JsonUtility.ToJson(editor.Snapshot());bool undo=editor.CanUndo,redo=editor.CanRedo;
            using(var locked=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read)){
                Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight{intensity=2}),out error),Is.False);Assert.That(error,Is.Not.Null.And.Not.Empty);
            }
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(block.GetComponentInChildren<Light>(true),Is.SameAs(lamp));Assert.That(lamp.isActiveAndEnabled,Is.True);
            Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(count+1));Assert.That((int)LightResources.ObserveBudget()["retiring"],Is.EqualTo(1));yield return null;Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(count));
        }
        [UnityTest]public IEnumerator LightAdmissionLaterBatchRefusalNeverPublishesEarlierLamps(){
            var targets=editor.Snapshot().objects.Where(o=>!o.IsBuiltIn).Take(2).ToArray();foreach(var data in targets)data.lights=new[]{new RoomLight()};
            Assert.That(editor.TryFlush(out var error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;var held=FillLamps(1);
            try{
                Assert.That(editor.ApplyAgentEdit(editor.Revision,targets,Array.Empty<string>(),out error),Is.False);StringAssert.Contains("live light or shadow budget",error);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(root.GetComponentsInChildren<Light>(true),Is.Empty);
                Assert.That((int)LightResources.ObserveBudget()["retiring"],Is.EqualTo(1));yield return null;Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(7));
            }finally{foreach(var lease in held)lease.Dispose();}
        }
        [UnityTest]public IEnumerator LightAdmissionUndoAndTemporaryDiscardRefuseWithoutLosingRetry(){
            string target=editor.Identity(block);Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight()),out var error),Is.True,error);
            Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight{intensity=2}),out error),Is.True,error);yield return null;
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());bool undo=editor.CanUndo,redo=editor.CanRedo;var held=FillLamps();
            try{editor.Undo();Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));}
            finally{foreach(var lease in held)lease.Dispose();}
            editor.Undo();Assert.That(editor.Read(target).lights[0].intensity,Is.EqualTo(1));editor.Redo();Assert.That(editor.Read(target).lights[0].intensity,Is.EqualTo(2));yield return null;
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight{intensity=3}),out error),Is.True,error);yield return null;
            string session=editor.TemporarySessionId;saved=JsonUtility.ToJson(editor.Snapshot());revision=editor.Revision;var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));var lamp=block.GetComponentInChildren<Light>(true);held=FillLamps();
            try{
                Assert.That(editor.DiscardTemporaryRoom(out error),Is.False);StringAssert.Contains("live light or shadow budget",error);Assert.That(editor.TemporarySessionId,Is.EqualTo(session));Assert.That(editor.TemporaryRoom,Is.True);Assert.That(workshop.Memory.Temporary,Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(block.GetComponentInChildren<Light>(true),Is.SameAs(lamp));
            }finally{foreach(var lease in held)lease.Dispose();}
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.TemporaryRoom,Is.False);Assert.That(workshop.Memory.Temporary,Is.False);Assert.That(editor.Read(target).lights[0].intensity,Is.EqualTo(2));yield return null;
        }
        [UnityTest]public IEnumerator LightAdmissionRecipeReplacementReanchorsUnchangedLampAtFullBudget(){
            string target=RecipeTarget();var data=editor.Read(target);string part=data.recipe.parts[0].id;
            Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight{part=part,position=Vector3.up*.1f}),out var error),Is.True,error);
            var item=editor.Find(target);var lamp=item.GetComponentInChildren<Light>(true);var oldAnchor=item.GetComponent<RecipeObject>().Part(part);var held=FillLamps();
            try{
                data=editor.Read(target);data.recipe.parts[0].position+=Vector3.up*.2f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
                var anchor=item.GetComponent<RecipeObject>().Part(part);Assert.That(anchor,Is.Not.SameAs(oldAnchor));Assert.That(item.GetComponentInChildren<Light>(true),Is.SameAs(lamp));Assert.That(Vector3.Distance(lamp.transform.position,anchor.TransformPoint(Vector3.up*.1f)),Is.LessThan(.001));yield return null;Assert.That(lamp,Is.Not.Null);Assert.That(lamp.isActiveAndEnabled,Is.True);
            }finally{foreach(var lease in held)lease.Dispose();}
        }
        [UnityTest]public IEnumerator LightAdmissionUnrelatedEditDoesNotRecreateMissingPeerView(){
            string target=editor.Identity(block);Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight()),out var error),Is.True,error);UnityEngine.Object.DestroyImmediate(block.GetComponent<RoomLightView>());yield return null;
            var peer=editor.Snapshot().objects.First(o=>!o.IsBuiltIn&&o.id!=target);peer.color=Color.red;var held=FillLamps();
            try{Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{peer},Array.Empty<string>(),out error),Is.True,error);Assert.That(block.GetComponent<RoomLightView>(),Is.Null);Assert.That(editor.Read(target).lights.Length,Is.EqualTo(1));}
            finally{foreach(var lease in held)lease.Dispose();}
            peer=editor.Read(target);peer.color=Color.blue;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{peer},Array.Empty<string>(),out error),Is.True,error);Assert.That(block.GetComponent<RoomLightView>().Active("Lamp"),Is.True);
        }
        [UnityTest]public IEnumerator LightAdmissionDormantActivationRefusalKeepsIdentityAndRetries(){
            string target=editor.Identity(block);Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight()),out var error),Is.True,error);string area=NativeAreaFor(target);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            var held=FillLamps();int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Read(target));
            try{var failed=editor.ActivateNativeArea(area);yield return NativeAreaCompletion(failed,false);StringAssert.Contains("live light or shadow budget",failed.Result);Assert.That(editor.Find(target),Is.Null);Assert.That(editor.NativeEntityDormant(target),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(saved));}
            finally{foreach(var lease in held)lease.Dispose();}
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(target).GetComponent<RoomLightView>().Active("Lamp"),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator LightAdmissionExactCandidatePassiveFactsDisabledAndDeferredLifetime(){
            root.AddComponent<RuntimeDiagnostics>();string target=editor.Identity(block);var lamp=new RoomLight{kind="spot",shadows="hard"};Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,lamp),out var error),Is.True,error);
            var host=new GameObject("Private lamp view");host.transform.SetParent(root.transform,false);var view=host.AddComponent<RoomLightView>();var held=new List<LightResources.Lease>();
            try{
                using var candidate=new PreparedRoomLights(editor.WorldIdentity,target,new[]{lamp},null);var preparing=LampReservations(target).Single(x=>(string)x["phase"]=="preparing");string id=(string)preparing["reservationId"];
                JObject Read()=>LampReservations(target).Single(x=>(string)x["reservationId"]==id);
                Assert.Throws<InvalidOperationException>(()=>candidate.Validate("different",new[]{lamp}));var wrong=lamp.Copy();wrong.range=4;Assert.Throws<InvalidOperationException>(()=>candidate.Validate(target,new[]{wrong}));
                view.Apply(editor,target,new[]{lamp},candidate);candidate.Dispose();var ready=Read();Assert.That((string)ready["phase"],Is.EqualTo("ready"));Assert.That((bool)ready["active"],Is.True);
                host.SetActive(false);var disabled=Read();Assert.That((bool)disabled["active"],Is.False);held=FillLamps();var budget=CollisionFact("runtime.lightBudget");var again=CollisionFact("runtime.lightBudget");Assert.That(again.ToString(),Is.EqualTo(budget.ToString()));again["lights"]=0;Assert.That((int)CollisionFact("runtime.lightBudget")["lights"],Is.EqualTo(8));
                NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);var retiring=Read();Assert.That((string)retiring["phase"],Is.EqualTo("retiring"));Assert.Throws<RoomResourceLimitException>(()=>LightResources.Begin(new RoomLight(),new RoomResourceOwner(null,"blocked","lighting")));
                string evidence=Environment.GetEnvironmentVariable("MAESTRO_LIGHT_ADMISSION_EVIDENCE");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,new JObject{["budget"]=budget,["preparing"]=preparing,["ready"]=ready,["disabled"]=disabled,["retiring"]=retiring}.ToString());
                yield return null;Assert.That(LampReservations(target).Any(x=>(string)x["reservationId"]==id),Is.False);using var retry=LightResources.Begin(new RoomLight(),new RoomResourceOwner(null,"retry","lighting"));
            }finally{foreach(var lease in held)lease.Dispose();if(host){NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);}}
        }
    }
}

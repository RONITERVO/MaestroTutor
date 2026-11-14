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
        JObject ImageRuntimeFact(string id,JObject args=null)
        {
            if(!BehaviourCatalog.TryRead(id,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value))return null;
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        JObject ImageReservation(int index=0)=>ImageRuntimeFact("runtime.imageReservation",new JObject{["index"]=index});
        JObject ImageOwner(string reservation,int index=0)=>ImageRuntimeFact("runtime.imageOwner",new JObject{["reservationId"]=reservation,["index"]=index});
        [UnityTest] public IEnumerator ImageResidencyRealBindingsShareOneTextureWithDistinctObjectOwners()
        {
            root.AddComponent<RuntimeDiagnostics>();var asset=ImageLibrary.Inspect("Shared tiles",ImageFiles.Png());
            var save=editor.Images.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.IsNull(save.Exception);
            var appearance=new RoomAppearance{id=Guid.NewGuid().ToString("N"),name="Shared tiles",style=new(){patternMode="image",imageHash=asset.Hash}};
            Assert.IsTrue(editor.EditAppearance(appearance,appearance.id,0,Array.Empty<string>(),out var error),error);
            var targets=editor.Snapshot().objects.Where(o=>o.kind==RoomObjectKind.Block||o.kind==RoomObjectKind.Ball).Select(o=>o.id).ToArray();Assert.AreEqual(2,targets.Length);
            var binding=new AppearanceBinding{appearanceId=appearance.id};
            foreach(var target in targets)Assert.IsTrue(editor.BindAppearance(target,editor.ObjectRevision(target),binding,editor.AppearanceRevision(appearance.id),false,false,out error),error);
            var loading=ImageReservation();Assert.AreEqual("loading",(string)loading["state"]);Assert.AreEqual(2,(int)loading["owners"]);Assert.AreEqual(0,(long)loading["textureBytes"]);
            double end=Time.realtimeSinceStartupAsDouble+5;
            while(targets.Any(t=>editor.AppearanceRenderState(t,binding)!="active")&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            foreach(var target in targets)Assert.AreEqual("active",editor.AppearanceRenderState(target,binding),editor.ImageTextures.Error(asset.Hash));
            var ready=ImageReservation();string reservation=(string)ready["reservationId"];
            Assert.AreEqual("ready",(string)ready["state"]);Assert.AreEqual((string)loading["reservationId"],reservation);Assert.AreEqual(28,(long)ready["textureBytes"]);
            var owners=targets.Select((_,i)=>ImageOwner(reservation,i)).ToArray();var identity=editor.WorldIdentity;
            CollectionAssert.AreEquivalent(targets,owners.Select(o=>(string)o["target"]));Assert.AreEqual(2,owners.Select(o=>(string)o["leaseId"]).Distinct().Count());
            foreach(var owner in owners){Assert.AreEqual(identity.worldId,(string)owner["worldId"]);Assert.AreEqual(identity.regionId,(string)owner["regionId"]);Assert.AreEqual("appearance",(string)owner["role"]);Assert.AreEqual(asset.Hash,(string)owner["imageHash"]);}
            var first=editor.Find(targets[0]).GetComponentInChildren<Renderer>().sharedMaterial.mainTexture;
            Assert.AreSame(first,editor.Find(targets[1]).GetComponentInChildren<Renderer>().sharedMaterial.mainTexture);
            int revision=editor.Revision;string snapshot=JsonUtility.ToJson(editor.Snapshot());
            var budget=ImageRuntimeFact("runtime.imageBudget");Assert.AreEqual(1,(int)budget["entries"]);Assert.AreEqual(2,(int)budget["owners"]);Assert.AreEqual(28,(long)budget["textureBytes"]);
            ready["owners"]=-1;owners[0]["target"]="corrupted observation";Assert.AreEqual(2,(int)ImageReservation()["owners"]);CollectionAssert.Contains(targets,(string)ImageOwner(reservation)["target"]);
            Assert.AreEqual(revision,editor.Revision);Assert.AreEqual(snapshot,JsonUtility.ToJson(editor.Snapshot()));
            editor.Find(targets[0]).GetComponent<RoomAppearanceView>().Refresh();Assert.AreEqual(reservation,(string)ImageReservation()["reservationId"]);Assert.AreEqual(2,(int)ImageReservation()["owners"]);
            Assert.AreSame(first,editor.Find(targets[0]).GetComponentInChildren<Renderer>().sharedMaterial.mainTexture);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_IMAGE_RESIDENCY_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(Path.GetDirectoryName(evidence));File.WriteAllText(evidence,new JObject{["loading"]=loading,["ready"]=ImageReservation(),["budget"]=budget,["owners"]=new JArray(ImageOwner(reservation,0),ImageOwner(reservation,1))}.ToString());}
            Assert.IsTrue(editor.BindAppearance(targets[0],editor.ObjectRevision(targets[0]),binding,editor.AppearanceRevision(appearance.id),true,false,out error),error);
            Assert.AreEqual(1,(int)ImageReservation()["owners"]);Assert.AreEqual(targets[1],(string)ImageOwner(reservation)["target"]);Assert.AreEqual(28,editor.ImageTextures.ResidentBytes);
            Assert.AreSame(first,editor.Find(targets[1]).GetComponentInChildren<Renderer>().sharedMaterial.mainTexture);
            Assert.IsTrue(editor.BindAppearance(targets[1],editor.ObjectRevision(targets[1]),binding,editor.AppearanceRevision(appearance.id),true,false,out error),error);
            Assert.AreEqual(0,editor.ImageTextures.ResidentBytes);Assert.IsNull(ImageReservation());Assert.IsNull(ImageOwner(reservation));
            var retained=editor.Images.ReadAsync(asset.Hash);yield return new WaitUntil(()=>retained.IsCompleted);Assert.IsNull(retained.Exception);Assert.AreEqual(asset.Hash,retained.Result.Hash);
        }
        [UnityTest] public IEnumerator ImageResidencyRetainsCopiedScopeOnFailureAndRejectsOldEntryAfterReload()
        {
            root.AddComponent<RuntimeDiagnostics>();var cache=editor.ImageTextures;var asset=ImageLibrary.Inspect("Absent tiles",ImageFiles.Png());
            var identity=editor.WorldIdentity;string world=identity.worldId,region=identity.regionId;
            using var first=cache.Acquire(asset.Hash,new RoomResourceOwner(identity,"book","appearance"));identity.worldId=new string('d',32);identity.regionId=new string('e',32);
            var initial=ImageReservation();string id=(string)initial["reservationId"];
            double end=Time.realtimeSinceStartupAsDouble+5;while(first.State=="loading"&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.AreEqual("failed",first.State);Assert.AreEqual(0,(long)ImageReservation()["textureBytes"]);
            var owner=ImageOwner(id);Assert.AreEqual(world,(string)owner["worldId"]);Assert.AreEqual(region,(string)owner["regionId"]);
            var save=editor.Images.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.IsNull(save.Exception);cache.Retry(asset.Hash);
            end=Time.realtimeSinceStartupAsDouble+5;while(first.State=="loading"&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.AreEqual("ready",first.State);Assert.AreEqual(id,(string)ImageReservation()["reservationId"]);Assert.AreEqual((string)owner["leaseId"],(string)ImageOwner(id)["leaseId"]);
            first.Dispose();first.Dispose();Assert.AreEqual("released",first.State);Assert.IsNull(first.Texture);Assert.IsNull(ImageOwner(id));
            using var replacement=cache.Acquire(asset.Hash,new RoomResourceOwner(editor.WorldIdentity,"book","appearance"));Assert.AreNotEqual(id,(string)ImageReservation()["reservationId"]);Assert.IsNull(ImageOwner(id));
        }
        [UnityTest] public IEnumerator ImageResidencyWorkspaceDisposalCancelsHeldReadAndReleasesOutstandingLeases()
        {
            var asset=ImageLibrary.Inspect("Pending tiles",ImageFiles.Png());var saving=editor.Images.SaveAsync(asset);yield return new WaitUntil(()=>saving.IsCompleted);Assert.IsNull(saving.Exception);
            var cache=editor.ImageTextures;Assert.IsTrue(editor.Images.TryCaptureArchive(out var capture));
            AppearanceImages.Lease lease=null;
            try{
                lease=cache.Acquire(asset.Hash,new RoomResourceOwner(editor.WorldIdentity,"book","appearance"));yield return null;yield return null;
                Assert.AreEqual("loading",lease.State);Assert.IsFalse(editor.WriteGate.CanFreeze(out _));
                UnityEngine.Object.Destroy(cache);yield return null;
                Assert.AreEqual("released",lease.State);Assert.IsNull(lease.Texture);lease.Dispose();lease.Dispose();
                double end=Time.realtimeSinceStartupAsDouble+5;while(!editor.WriteGate.CanFreeze(out _)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                Assert.IsTrue(editor.WriteGate.CanFreeze(out _),"A retired texture read must release workspace preservation ownership");
            }finally{lease?.Dispose();capture.Dispose();}
        }
        [Test] public void ImageResidencyBudgetsRefuseAdditionalOwnersAndEntriesWithoutDroppingExistingLeases()
        {
            var cache=editor.ImageTextures;var leases=new List<AppearanceImages.Lease>();string hash=new string('a',64);
            try{
                for(int i=0;i<AppearanceImages.MaximumOwnersPerImage;i++)leases.Add(cache.Acquire(hash));
                Assert.Throws<InvalidOperationException>(()=>cache.Acquire(hash));Assert.AreEqual(AppearanceImages.MaximumOwnersPerImage,(int)cache.ObserveBudget()["owners"]);
                leases[0].Dispose();leases.Add(cache.Acquire(hash));Assert.AreEqual(AppearanceImages.MaximumOwnersPerImage,(int)cache.ObserveReservation(0)["owners"]);
            }finally{foreach(var lease in leases)lease.Dispose();leases.Clear();}
            Assert.AreEqual(0,cache.Count);
            try{
                for(int i=0;i<AppearanceImages.MaximumEntries;i++)leases.Add(cache.Acquire(i.ToString("x64")));
                Assert.Throws<InvalidOperationException>(()=>cache.Acquire(hash));Assert.AreEqual(AppearanceImages.MaximumEntries,cache.Count);
                leases[0].Dispose();leases.Add(cache.Acquire(hash));Assert.AreEqual(AppearanceImages.MaximumEntries,cache.Count);
            }finally{foreach(var lease in leases)lease.Dispose();}
            Assert.AreEqual(0,cache.Count);Assert.AreEqual(0,cache.ResidentBytes);
        }
        [Test] public void ImageResidencyFactsRejectUnknownExtraAndOutOfRangeArguments()
        {
            root.AddComponent<RuntimeDiagnostics>();var cache=editor.ImageTextures;using var lease=cache.Acquire(new string('a',64));
            string id=(string)ImageReservation()["reservationId"];
            Assert.IsNull(ImageReservation(-1));Assert.IsNull(ImageReservation(AppearanceImages.MaximumEntries));Assert.IsNull(ImageOwner(id,AppearanceImages.MaximumOwnersPerImage));
            Assert.IsNull(ImageRuntimeFact("runtime.imageOwner",new JObject{["reservationId"]=id,["index"]=0,["release"]=true}));
            Assert.IsNull(ImageRuntimeFact("runtime.imageOwner",new JObject{["reservationId"]="not-an-id",["index"]=0}));
            Assert.IsNotNull(ImageOwner(id));Assert.AreEqual(1,cache.Count);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        [UnityTest] public IEnumerator ModelConsumerPreviewCancelAndLibraryAcceptanceReleaseBothOwners()
        {
            ImportRuntime();var bytes=ModelFixture.Create();string hash=ModelLibrary.Hash(bytes);var fence=editor.Models.Assets;
            yield return ChooseModel(bytes);
            Assert.That(fence.Pinned(ImportedAssetKind.Model,hash),Is.EqualTo(2),"The pending selection and ready native preview own independent source pins");
            imports.Cancel();
            yield return PhysicalCreationTestWait.Until(()=>fence.Pinned(ImportedAssetKind.Model,hash)==0,"Cancelled preview retained its source");
            Assert.That(imports.HasPreview,Is.False);
            yield return ChooseModel(bytes);yield return ImportAction(ImportAccept("library"));
            Assert.That((string)ImportFact()["phase"],Is.EqualTo("completed"));
            Assert.That(File.Exists(Path.Combine(directory,"models",hash+".glb")),Is.True);
            yield return PhysicalCreationTestWait.Until(()=>fence.Pinned(ImportedAssetKind.Model,hash)==0,"Library-only acceptance retained a preview or save owner");
            using var retirement=fence.TryRetire(ImportedAssetKind.Model,hash,out var error);Assert.That(retirement,Is.Not.Null,error);
        }
        [UnityTest] public IEnumerator ModelConsumerObjectAcceptanceTransfersToNativeOwnerUntilExplicitDisposal()
        {
            ImportRuntime();var bytes=ModelFixture.Create();string hash=ModelLibrary.Hash(bytes);var fence=editor.Models.Assets;
            yield return ChooseModel(bytes);yield return ImportAction(ImportAccept("object"));
            var model=editor.Find((string)ImportFact()["accepted"]["objectId"]).GetComponent<CreatedRoomObject>().Model;
            Assert.That(model.Ready,Is.True);Assert.That(model.AssetHash,Is.EqualTo(hash));
            yield return PhysicalCreationTestWait.Until(()=>fence.Pinned(ImportedAssetKind.Model,hash)==1,"Acceptance did not release its temporary owners");
            Object.DestroyImmediate(imports);
            using(var refusal=fence.TryRetire(ImportedAssetKind.Model,hash,out var reason)){Assert.That(refusal,Is.Null);Assert.That(reason,Does.Contain("in use"));}
            Assert.That(model.Ready,Is.True,"Ending the import UI must not dispose the accepted object");
            model.Dispose();
            yield return PhysicalCreationTestWait.Until(()=>fence.Pinned(ImportedAssetKind.Model,hash)==0,"Disposed native object retained its source");
        }
        [UnityTest] public IEnumerator ModelConsumerSelectedMotionExtractionReleasesOnlyItsReadOwner()
        {
            ImportRuntime();var bytes=ModelFixture.Mixamo();string hash=ModelLibrary.Hash(bytes);var fence=editor.Models.Assets;
            yield return ChooseModel(bytes);yield return ImportAction(ImportAccept("object"));
            yield return PhysicalCreationTestWait.Until(()=>fence.Pinned(ImportedAssetKind.Model,hash)==1,"Accepted object did not settle to one native source owner");
            int inspectedPins=0;editor.Models.ReadCheckpoint=phase=>{if(phase=="inspected")inspectedPins=fence.Pinned(ImportedAssetKind.Model,hash);};
            try {
                var extract=imports.SaveMotionsAsync();yield return PhysicalCreationTestWait.Until(()=>extract.IsCompleted,"Selected model motion extraction did not finish");
                Assert.That(extract.Exception,Is.Null);Assert.That(extract.Result,Is.True,imports.Status);
            } finally {editor.Models.ReadCheckpoint=null;}
            Assert.That(inspectedPins,Is.GreaterThan(1),"Motion extraction must hold its own source while reading");
            Assert.That(fence.Pinned(ImportedAssetKind.Model,hash),Is.EqualTo(1),"Extraction must release its read while preserving the displayed object's owner");
        }
        [UnityTest] public IEnumerator ModelConsumerBatchRetainsVerifiedSourceUntilCommitOrStop()
        {
            ImportRuntime();using var raw=ModelLibrary.Inspect("batch-source.glb",ModelFixture.Create());var fence=editor.Models.Assets;
            var save=editor.Models.SaveAsync(raw);yield return PhysicalCreationTestWait.Until(()=>save.IsCompleted,"Batch source did not save");Assert.That(save.Exception,Is.Null);
            var args=(JObject)JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/construction-resources-contract.json")))[0]["arguments"];
            args["blueprint"]["pieces"][0]["source"]["prototype"]["geometry"]=new JObject{["kind"]="model",["modelHash"]=raw.Hash};
            int inspected=0;editor.Models.ReadCheckpoint=phase=>{if(phase=="inspected")Interlocked.Exchange(ref inspected,1);};
            CapabilityOperation operation=null;int before=editor.Snapshot().objects.Length;
            try {
                Assert.That(new BatchCreationCapability().Start(new CapabilityContext(editor,workshop),"owned-batch",args,out operation,out var error),Is.True,error);
                yield return PhysicalCreationTestWait.Until(()=>Volatile.Read(ref inspected)==1&&fence.Pinned(ImportedAssetKind.Model,raw.Hash)==1,"Batch preflight did not retain its source after releasing the read");
                using var refusal=fence.TryRetire(ImportedAssetKind.Model,raw.Hash,out error);Assert.That(refusal,Is.Null);Assert.That(error,Does.Contain("in use"));
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before),"Preflight must not commit before the operation is polled");
            } finally {editor.Models.ReadCheckpoint=null;operation?.Stop(true);}
            yield return PhysicalCreationTestWait.Until(()=>fence.Pinned(ImportedAssetKind.Model,raw.Hash)==0,"Stopped batch retained its source after the actual read drained");
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before));
        }
    }
}

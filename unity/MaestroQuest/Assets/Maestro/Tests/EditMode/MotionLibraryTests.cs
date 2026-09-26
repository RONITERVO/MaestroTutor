// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class MotionLibraryTests
    {
        string directory;
        [SetUp] public void Setup() => directory = Path.Combine(Path.GetTempPath(),"MaestroMotions-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        [Test] public void ExtractionKeepsRigAndCurvesWithoutGeometryAndLeavesOriginalBytesUntouched()
        {
            var bytes = ModelFixture.Mixamo(); var original = bytes.ToArray();
            var pack = MotionPack.Extract("source.glb",bytes).Single(); var renamed = MotionPack.Extract("renamed.glb",bytes).Single();
            Assert.That(bytes,Is.EqualTo(original)); Assert.That(pack.Hash,Is.EqualTo(renamed.Hash));
            Assert.That(pack.RigHash,Is.EqualTo(MotionPack.RigIdentity(bytes)));
            var root = JObject.Parse(Encoding.UTF8.GetString(pack.Bytes,20,BitConverter.ToInt32(pack.Bytes,12)));
            Assert.That(((JArray)root["meshes"]).Count,Is.Zero); Assert.That(root["images"],Is.Null);
            Assert.That(((JArray)root["animations"]).Count,Is.EqualTo(1)); Assert.That(pack.Duration,Is.EqualTo(1));
            Assert.Throws<ModelImportException>(() => ModelLibrary.Inspect("not-a-model.glb",pack.Bytes));
            var changed = ModelFixture.Mixamo(json => json["nodes"][5]["translation"][1] = .15);
            Assert.That(MotionPack.RigIdentity(changed),Is.Not.EqualTo(pack.RigHash),"Matching bone names are insufficient when bind proportions differ");
            Assert.Throws<ModelImportException>(() => MotionPack.Extract("ambiguous.glb",ModelFixture.Mixamo(json => json["nodes"][5]["name"] = "bad/path")));
        }
        [Test] public void IdentitySurvivesRenameDuplicateImportRestartAndPayloadRepair()
        {
            var bytes = ModelFixture.TranslationMotion("LINEAR");
            using var library = new MotionLibrary(directory);
            var first = library.ImportAsync("walk.glb",bytes,"walking").GetAwaiter().GetResult().Single();
            library.UpdateAsync(first.id,"My walk",new[] { "walk","floor" },true).GetAwaiter().GetResult();
            var duplicate = library.ImportAsync("renamed.glb",bytes).GetAwaiter().GetResult().Single();
            Assert.That(duplicate.id,Is.EqualTo(first.id)); Assert.That(duplicate.name,Is.EqualTo("My walk")); Assert.That(library.List("FLOOR",favouritesOnly:true).Length,Is.EqualTo(1));
            Assert.That(Directory.GetFiles(directory,"*.motion.glb").Length,Is.EqualTo(1));
            var different = library.ImportAsync("walk-revised.glb",ModelFixture.TranslationMotion("LINEAR",2)).GetAwaiter().GetResult().Single();
            Assert.That(different.id,Is.Not.EqualTo(first.id),"A changed source never silently redirects existing rules");
            using var restored = new MotionLibrary(directory);
            Assert.That(restored.Find(first.id).name,Is.EqualTo("My walk")); Assert.That(restored.Find(first.id).favourite,Is.True);
            string payload = Path.Combine(directory,first.hash+".motion.glb"); File.WriteAllText(payload,"broken");
            var repaired = restored.ImportAsync("walk.glb",bytes).GetAwaiter().GetResult().Single();
            Assert.That(repaired.id,Is.EqualTo(first.id)); Assert.That(ModelLibrary.Hash(File.ReadAllBytes(payload)),Is.EqualTo(first.hash));
        }
        [Test] public void BackupRecoversMetadataAndUnknownVersionsNeverOverwriteTheLibrary()
        {
            using (var library = new MotionLibrary(directory))
            {
                var entry = library.ImportAsync("first.glb",ModelFixture.Create()).GetAwaiter().GetResult().Single();
                library.UpdateAsync(entry.id,"Renamed",new[] { "test" },true).GetAwaiter().GetResult();
            }
            string primary = Path.Combine(directory,"motions.v1.json"); File.WriteAllText(primary,"damaged");
            using (var recovered = new MotionLibrary(directory))
            {
                Assert.That(recovered.ReadOnly,Is.False); Assert.That(recovered.Notice,Does.Contain("backup")); Assert.That(recovered.List().Length,Is.EqualTo(1));
                recovered.ImportAsync("new.glb",ModelFixture.TranslationMotion("STEP")).GetAwaiter().GetResult();
                Assert.That(File.ReadAllText(primary+".unreadable"),Is.EqualTo("damaged"));
            }
            File.WriteAllText(primary,"{\"version\":999}"); // A valid older backup must not permit downgrading this catalogue.
            using var blocked = new MotionLibrary(directory); Assert.That(blocked.ReadOnly,Is.True);
            Assert.Throws<ModelImportException>(() => blocked.ImportAsync("new.glb",ModelFixture.Create()).GetAwaiter().GetResult());
            Assert.That(File.ReadAllText(primary),Is.EqualTo("{\"version\":999}"));
        }
        [Test] public void UnsupportedPackVersionsAndDuplicateChannelsAreRejected()
        {
            var pack = MotionPack.Extract("motion.glb",ModelFixture.Create()).Single(); int jsonLength = BitConverter.ToInt32(pack.Bytes,12);
            var root = JObject.Parse(Encoding.UTF8.GetString(pack.Bytes,20,jsonLength)); byte[] binary = pack.Bytes.Skip(28+jsonLength).ToArray();
            root["extras"]["maestroMotion"]["version"] = 2; Assert.Throws<ModelImportException>(() => MotionPack.Read(ModelFixture.Pack(root,binary)));
            root["extras"]["maestroMotion"]["version"] = 1.1; Assert.Throws<ModelImportException>(() => MotionPack.Read(ModelFixture.Pack(root,binary)));
            root["extras"]["maestroMotion"]["version"] = 1; ((JObject)root["extras"]["maestroMotion"]).Remove("axis"); Assert.Throws<ModelImportException>(() => MotionPack.Read(ModelFixture.Pack(root,binary)));
            root["extras"]["maestroMotion"]["axis"] = "Z"; ((JArray)root["animations"][0]["channels"]).Add(root["animations"][0]["channels"][0].DeepClone());
            Assert.Throws<ModelImportException>(() => MotionPack.Read(ModelFixture.Pack(root,binary)));
        }
    }
}

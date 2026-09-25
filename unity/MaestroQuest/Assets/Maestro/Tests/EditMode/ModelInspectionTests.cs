// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class ModelInspectionTests
    {
        [Test] public void ReadsBoundedAnimatedGlbAndPreservesAvatarMetadata()
        {
            var result = ModelInspection.Inspect(ModelFixture.Create());
            Assert.That(result.Vertices, Is.EqualTo(3)); Assert.That(result.Triangles, Is.EqualTo(1)); Assert.That(result.Clips, Is.EqualTo(1)); Assert.That(result.IsAvatar, Is.False);
            var avatar = ModelInspection.Inspect(ModelFixture.Create(avatar: true)); Assert.That(avatar.IsAvatar, Is.True); Assert.That(avatar.Attribution, Does.Contain("Maestro tests"));
        }
        [Test] public void RejectsExternalResourcesBrokenAccessorsAndRecursiveNodesBeforeLoading()
        {
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["buffers"][0]["uri"] = "https://example.com/data")));
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["accessors"][0]["count"] = 250000)));
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["nodes"][0]["children"] = new JArray(0))));
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["nodes"][0]["mesh"] = -1)));
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["extensionsRequired"] = new JArray("KHR_draco_mesh_compression"))));
            var truncated = ModelFixture.Create(); Array.Resize(ref truncated, truncated.Length-4); Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(truncated));
        }
        [Test] public void RejectsHiddenAllocationsAndInvalidAnimationTime()
        {
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["images"] = new JArray(new JObject { ["uri"] = "data:image/png;base64,xxxx" }))));
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["accessors"][0]["sparse"] = new JObject { ["count"] = 1 })));
            Assert.Throws<ModelImportException>(() => ModelInspection.Inspect(ModelFixture.Create(root => root["animations"][0]["samplers"][0]["input"] = 0)));
        }
        [Test] public void LibraryPersistsExactOwnedBytesRejectsTraversalAndDetectsDamage()
        {
            string directory = Path.Combine(Path.GetTempPath(), "MaestroModelTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var library = new ModelLibrary(directory); var bytes = ModelFixture.Create(); var asset = ModelLibrary.Inspect("triangle.glb", bytes);
                library.SaveAsync(asset).GetAwaiter().GetResult(); var loaded = library.ReadAsync(asset.Hash).GetAwaiter().GetResult();
                Assert.That(loaded.Bytes, Is.EqualTo(bytes)); Assert.That(File.ReadAllText(Path.Combine(directory, asset.Hash + ".txt")), Does.Contain("Apache-2.0"));
                Assert.Throws<ModelImportException>(() => library.ReadAsync("../../secret").GetAwaiter().GetResult());
                bytes[bytes.Length-1] ^= 1; File.WriteAllBytes(Path.Combine(directory, asset.Hash + ".glb"), bytes);
                Assert.Throws<ModelImportException>(() => library.ReadAsync(asset.Hash).GetAwaiter().GetResult());
                library.SaveAsync(ModelLibrary.Inspect("triangle.glb", ModelFixture.Create())).GetAwaiter().GetResult();
                Assert.That(library.ReadAsync(asset.Hash).GetAwaiter().GetResult().Bytes, Is.EqualTo(ModelFixture.Create()));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}

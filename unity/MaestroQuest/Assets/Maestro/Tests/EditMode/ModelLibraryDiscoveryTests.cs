// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ModelLibraryDiscoveryTests
    {
        string directory;ModelLibrary library;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"MaestroModelDiscovery-"+Guid.NewGuid().ToString("N"));library=new ModelLibrary(directory);}
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test]public void ListsExactSortedIdentitiesWithBoundedNamesWithoutLoadingOrAlteringModels()
        {
            Assert.That(library.ListAsync().GetAwaiter().GetResult(),Is.Empty);
            var assets=Enumerable.Range(0,9).Select(i=>ModelLibrary.Inspect("<Chosen>\n"+i+new string('x',110),ModelFixture.Mixamo(json=>json["animations"][0]["name"]="Clip "+i))).ToArray();
            foreach(var asset in assets)library.SaveAsync(asset).GetAwaiter().GetResult();
            var entries=library.ListAsync().GetAwaiter().GetResult();Assert.That(entries.Select(e=>e.Hash),Is.EqualTo(assets.Select(a=>a.Hash).OrderBy(h=>h,StringComparer.Ordinal)));Assert.That(entries.All(e=>e.Name.Length==100&&!e.Name.Contains('<')&&!e.Name.Contains('\n')),Is.True);
            foreach(var entry in entries){Assert.That((library.ReadAsync(entry.Hash).GetAwaiter().GetResult()).Hash,Is.EqualTo(entry.Hash));Assert.That(entry.Bytes,Is.EqualTo(assets.Single(a=>a.Hash==entry.Hash).Bytes.Length));}
        }
        [Test]public void InvalidInventoryFailsInsteadOfInventingAnEmptyLibrary()
        {
            var asset=ModelLibrary.Inspect("Model",ModelFixture.Create());library.SaveAsync(asset).GetAwaiter().GetResult();File.WriteAllText(Path.Combine(directory,asset.Hash+".txt"),new string('x',128*1024+1));
            Assert.Throws<ModelImportException>(()=>library.ListAsync().GetAwaiter().GetResult());File.Delete(Path.Combine(directory,asset.Hash+".txt"));Assert.That((library.ListAsync().GetAwaiter().GetResult()).Single().Name,Is.EqualTo(asset.Hash));
            File.WriteAllBytes(Path.Combine(directory,"invalid.glb"),asset.Bytes);Assert.Throws<ModelImportException>(()=>library.ListAsync().GetAwaiter().GetResult());
        }
    }
}

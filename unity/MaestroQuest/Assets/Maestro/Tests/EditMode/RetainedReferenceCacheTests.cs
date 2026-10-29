// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class RetainedReferenceCacheTests
    {
        [Serializable]public sealed class Document {public int version=1;public string motion="motion",image="image";public bool imageUnknown=true;}
        string directory;VersionedRoomFile<Document> store;
        [SetUp]public void Before(){directory=Path.Combine(Path.GetTempPath(),"MaestroReferenceCache-"+Guid.NewGuid().ToString("N"));store=new(directory,"reference",4096,v=>v.version==1,v=>new Document{version=v.version,motion=v.motion,image=v.image,imageUnknown=v.imageUnknown},null,_=>{},version:1);Assert.That(store.Save(new Document(),out var error),Is.True,error);}
        [TearDown]public void After(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [TestCase(false)][TestCase(true)]public void DomainsKeepDifferentIdentitiesAndUncertaintyInEitherQueryOrder(bool imageFirst)
        {
            void Image(){Assert.That(store.Retains(v=>new[]{v.image},"image",out var uncertain,unknownReferences:v=>v.imageUnknown,cacheDomain:"image"),Is.True);Assert.That(uncertain,Is.True);Assert.That(store.Retains(v=>new[]{v.image},"motion",out _,cacheDomain:"image"),Is.False);}
            void Motion(){Assert.That(store.Retains(v=>new[]{v.motion},"motion",out var uncertain,cacheDomain:"motion"),Is.True);Assert.That(uncertain,Is.False);Assert.That(store.Retains(v=>new[]{v.motion},"image",out _,cacheDomain:"motion"),Is.False);}
            if(imageFirst){Image();Motion();}else{Motion();Image();}Image();Motion();
        }
        [TestCase(false)][TestCase(true)]public void UnnamedSelectorsCannotReuseAnotherSelectorsIdentitySet(bool imageFirst)
        {
            string first=imageFirst?"image":"motion",second=imageFirst?"motion":"image";
            Assert.That(store.Retains(v=>new[]{first},first,out var uncertain),Is.True);Assert.That(uncertain,Is.False);
            Assert.That(store.Retains(v=>new[]{second},second,out uncertain),Is.True);Assert.That(uncertain,Is.False);
        }
        [Test]public void NamedDomainReusesItsInspectionButForceReadsAgain()
        {
            int inspected=0;System.Collections.Generic.IEnumerable<string> Ids(Document value){inspected++;return new[]{value.image};}
            Assert.That(store.Retains(Ids,"image",out _,cacheDomain:"image"),Is.True);Assert.That(store.Retains(Ids,"image",out _,cacheDomain:"image"),Is.True);Assert.That(inspected,Is.EqualTo(1));
            Assert.That(store.Retains(Ids,"image",out _,force:true,cacheDomain:"image"),Is.True);Assert.That(inspected,Is.EqualTo(2));
        }
    }
}

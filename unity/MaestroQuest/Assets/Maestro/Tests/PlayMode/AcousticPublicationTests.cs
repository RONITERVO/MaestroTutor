// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        sealed class HeaderOnlyAcousticInput : IAcousticGeometry
        {
            public bool Active=>true;public bool HasGeometry=>true;public bool Scanned=>false;
            public bool ReflectionBoundary=>true;public int Revision=>0;
            public Matrix4x4 Pose{get;set;}=Matrix4x4.identity;
            public string Issue{get;set;}
            internal int Reads;
            public bool Read(out float[] vertices,out int[] indices,out string issue)
            {Reads++;throw new InvalidOperationException("A passive publication must not read geometry");}
        }
        [UnityTest] public IEnumerator AcousticPublicationReadsHeadersWithoutUploadingOrStartingWork()
        {
            var audio=AreaAcoustics();var input=new HeaderOnlyAcousticInput();audio.Register(input);
            int count=audio.GeometryCount;long revision=audio.StructureRevision;
            var first=audio.DescribeGeometry();Assert.That(first.Available,Is.True);
            Assert.That(audio.DescribeGeometry(),Is.SameAs(first));Assert.That(input.Reads,Is.Zero);
            input.Pose=Matrix4x4.Translate(Vector3.down*5);
            var moved=audio.DescribeGeometry();Assert.That(first.SameDefinitions(moved),Is.True);
            Assert.That(first.SamePlacements(moved),Is.False);Assert.That(audio.GeometryCount,Is.EqualTo(count));
            Assert.That(audio.StructureRevision,Is.EqualTo(revision));Assert.That(input.Reads,Is.Zero);
            var pending=new object();audio.SetPreparing(pending,true);
            Assert.That(audio.DescribeGeometry().PendingBoundaries,Is.EqualTo(1));
            audio.SetPreparing(pending,false);Assert.That(audio.DescribeGeometry().PendingBoundaries,Is.Zero);
            audio.SetPreparing(pending,null);audio.Unregister(input);
            audio.enabled=false;Assert.That(audio.DescribeGeometry(),Is.SameAs(AcousticGeometryCatalog.Unavailable));
            Assert.That(input.Reads,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator AcousticPublicationKeepsIdentityAcrossNativeRetirementAndReturn()
        {
            var audio=AreaAcoustics();string id=editor.Identity(block),area=NativeAreaFor(id);
            var surface=block.GetComponentInChildren<AcousticSurface>();Assert.That(surface,Is.Not.Null);
            var initial=audio.DescribeGeometry();var key=Enumerable.Range(0,initial.Count).Select(initial.At).Single(x=>ReferenceEquals(x.Input,surface)).Key;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var dormant=audio.DescribeGeometry();var retained=Enumerable.Range(0,dormant.Count).Select(dormant.At).Single(x=>ReferenceEquals(x.Key,key));
            Assert.That(retained.Input,Is.Not.SameAs(surface));Assert.That(retained.Active&&retained.HasGeometry,Is.True);
            Assert.That(editor.Find(id),Is.Null,"Describing dormant acoustics cannot load a native view");
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            var restored=audio.DescribeGeometry();var active=Enumerable.Range(0,restored.Count).Select(restored.At).Single(x=>ReferenceEquals(x.Key,key));
            Assert.That(active.Input,Is.InstanceOf<AcousticSurface>());Assert.That(active.Active&&active.HasGeometry,Is.True);
            Assert.That(audio.RetainedGeometryCount,Is.Zero);
        }
    }
}

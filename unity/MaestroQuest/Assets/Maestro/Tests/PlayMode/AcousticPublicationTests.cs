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
        IEnumerator CpuPublicationSettled(RoomAcoustics audio)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            do{editor.TickNativeAcoustics();yield return null;}
            while((editor.AcousticPreparationPending||audio.RetainedGeometryCount==0)&&Time.realtimeSinceStartupAsDouble<deadline);
            editor.TickNativeAcoustics();
            Assert.That(editor.AcousticPreparationPending,Is.False);
            Assert.That(audio.RetainedGeometryCount,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator AcousticPublicationFinishesColdPreparationWithoutMetaContext()
        {
            string id=editor.Identity(block);var data=editor.Read(id);data.physics=Maestro.Quest.Interaction.ItemPhysics.Fixed;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            string area=NativeAreaFor(id);var audio=AreaAcoustics();
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            editor.SendMessage("ReleaseNativeAcoustics");audio.SendMessage("Close");
            var other=new GameObject("Other renderer context owner").AddComponent<RoomAcoustics>();
            try{
                Assert.That(other.ContextOpen,Is.True,other.Issue);
                yield return CpuPublicationSettled(audio);
                Assert.That(audio.ContextOpen,Is.False,"The competing room keeps the native SDK unavailable");
                var catalog=audio.DescribeGeometry();Assert.That(catalog.Available,Is.True);
                Assert.That(editor.Find(id),Is.Null,"CPU geometry preparation must not recreate a view");
                Assert.That(catalog.PendingBoundaries,Is.Zero,"Registered CPU geometry cannot wait for one renderer's upload");
                var retained=Enumerable.Range(0,catalog.Count).Select(catalog.At).Single(x=>x.Input is RoomAcoustics.RetainedInput);
                Assert.That(retained.Input.Read(out var vertices,out var indices,out error),Is.True,error);
                Assert.That(RoomAcoustics.ValidateMeshData(vertices,indices,out error),Is.True,error);
                Assert.That(audio.RequestMap(new[]{Vector3.zero}),Is.False,"CPU readiness does not open a native context");
                var key=retained.Key;
                data=editor.Read(id);data.position+=Vector3.down*3;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
                editor.TickNativeAcoustics();var moved=audio.DescribeGeometry();
                var input=Enumerable.Range(0,moved.Count).Select(moved.At).Single(x=>ReferenceEquals(x.Key,key));
                Assert.That(input.Pose,Is.Not.EqualTo(retained.Pose));Assert.That(moved.PendingBoundaries,Is.Zero);
                Assert.That(editor.Find(id),Is.Null);Assert.That(audio.ContextOpen,Is.False);
            }finally{UnityEngine.Object.Destroy(other.gameObject);}
            yield return null;audio.Synchronize();
            Assert.That(audio.ContextOpen,Is.True,audio.Issue);Assert.That(audio.OmittedCount,Is.Zero);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(audio.RetainedGeometryCount,Is.Zero);Assert.That(editor.Find(id),Is.Not.Null);
        }
        sealed class CapacityAcousticInput : IAcousticGeometry
        {
            static readonly float[] Points={0,0,0,1,0,0,0,1,0};
            static readonly int[] Indices={0,1,2};
            public bool Active=>true;public bool HasGeometry=>true;public bool Scanned=>false;
            public bool ReflectionBoundary=>true;public int Revision=>0;
            public Matrix4x4 Pose{get;set;}=Matrix4x4.identity;
            public string Issue{get;set;}
            public bool Read(out float[] vertices,out int[] indices,out string issue)
            {vertices=Points;indices=Indices;issue=null;return true;}
        }
        [UnityTest] public IEnumerator AcousticPublicationDoesNotHideNativeOmissionsWhenCpuPreparationFinishes()
        {
            string id=editor.Identity(block);var data=editor.Read(id);data.physics=Maestro.Quest.Interaction.ItemPhysics.Fixed;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            string area=NativeAreaFor(id);var audio=AreaAcoustics();
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            editor.SendMessage("ReleaseNativeAcoustics");audio.Synchronize();
            int slots=RoomAcoustics.MaximumVirtualSurfaces-audio.GeometryCount;
            var fillers=Enumerable.Range(0,slots).Select(i=>new CapacityAcousticInput{Pose=Matrix4x4.Translate(Vector3.right*i*.01f)}).ToArray();
            try{
                foreach(var input in fillers)audio.Register(input);
                for(int i=0;i<=slots/4;i++)audio.Synchronize();
                Assert.That(audio.GeometryCount,Is.EqualTo(RoomAcoustics.MaximumVirtualSurfaces));
                yield return CpuPublicationSettled(audio);audio.Synchronize();
                var catalog=audio.DescribeGeometry();Assert.That(catalog.PendingBoundaries,Is.Zero);
                Assert.That(audio.OmittedBoundaries,Is.GreaterThan(0),"Native upload omissions remain independently visible");
                Assert.That(audio.RequestMap(new[]{Vector3.zero}),Is.False,"A complete CPU publication cannot authorize an incomplete native map");
                Assert.That(audio.MapComputing,Is.False);Assert.That(editor.Find(id),Is.Null);
                var retained=Enumerable.Range(0,catalog.Count).Select(catalog.At).Single(x=>x.Input is RoomAcoustics.RetainedInput);
                Assert.That(retained.Input.Read(out _,out _,out error),Is.True,error);
                Assert.That(retained.Input.Issue,Does.Contain("budget"));
            }finally{foreach(var input in fillers)audio.Unregister(input);}
            audio.Synchronize();editor.TickNativeAcoustics();
            Assert.That(audio.OmittedCount,Is.Zero);Assert.That(audio.DescribeGeometry().PendingBoundaries,Is.Zero);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(audio.RetainedGeometryCount,Is.Zero);
        }
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

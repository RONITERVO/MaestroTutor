// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Threading;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AudioSettings = UnityEngine.AudioSettings;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        sealed class AcceptedSurface : IAcousticGeometry
        {
            internal GameObject View;
            internal float[] Points;
            internal int[] Indices;
            public bool Active => View;
            public bool HasGeometry => View;
            public bool Scanned => false;
            public bool ReflectionBoundary => true;
            public int Revision => 1;
            public Matrix4x4 Pose => View.transform.localToWorldMatrix;
            public string Issue { get; set; }
            public bool Read(out float[] points, out int[] indices, out string issue)
            {
                var mesh=View.GetComponent<MeshFilter>().sharedMesh;
                var vertices=mesh.vertices; Points=new float[vertices.Length*3];
                for(int i=0;i<vertices.Length;i++)
                { Points[i*3]=vertices[i].x; Points[i*3+1]=vertices[i].y; Points[i*3+2]=vertices[i].z; }
                points=Points; indices=Indices=mesh.triangles; issue=null; return true;
            }
        }
        static AcousticMeshPose CaptureBox(string id)
        {
            var view=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                view.transform.localScale=Vector3.one*6;
                return AcousticMeshPose.Capture(id,new AcceptedSurface { View=view });
            }
            finally { UnityEngine.Object.DestroyImmediate(view); }
        }
        static AcousticFrame GeometryFrame(AcousticSceneData data, Vector3? listenerPosition=null, Vector3? sourcePosition=null)
        {
            var l=listenerPosition??new Vector3(-1,0,0);
            var s=sourcePosition??new Vector3(1,0,0);
            return new AcousticFrame(data,AcousticFrame.Pose(l.x,l.y,-l.z),AcousticFrame.Pose(s.x,s.y,-s.z),AcousticFrame.Pose(1,0,-.4f));
        }
        [UnityTest]
        public IEnumerator AcousticGeometrySnapshotsValidateBeforeNativeAdmission()
        {
            var pose=CaptureBox("box");
            var data=pose.Mesh;
            var inputs=new[] {pose}; var scene=new AcousticSceneData(inputs); inputs[0]=null;
            Assert.AreSame(pose,scene.At(0));
            Assert.Throws<ArgumentException>(()=>new AcousticSceneData(pose,pose));
            Assert.Throws<ArgumentException>(()=>new AcousticMeshPose("bad",data,Matrix4x4.zero));
            Assert.Throws<ArgumentException>(()=>new AcousticMeshPose("bad",data,Matrix4x4.Scale(new Vector3(-1,1,1))));
            var points=new float[] {0,0,0, 1,0,0, 0,1,0};
            Assert.Throws<ArgumentException>(()=>new AcousticMeshData(points,new[] {0,1,3}));
            Assert.Throws<ArgumentException>(()=>new AcousticMeshData(points,new[] {0,1,1}));
            points[0]=float.NaN;
            Assert.Throws<ArgumentException>(()=>new AcousticMeshData(points,new[] {0,1,2}));
            Assert.Throws<ArgumentException>(()=>new AcousticMeshData(new float[16385*3],new[] {0,1,2}));
            var scans=new AcousticMeshPose[129];
            for(int i=0;i<scans.Length;i++) scans[i]=new AcousticMeshPose("scan-"+i,data,Matrix4x4.identity,true);
            Assert.Throws<ArgumentException>(()=>new AcousticSceneData(scans));
            var virtuals=new AcousticMeshPose[385];
            for(int i=0;i<virtuals.Length;i++) virtuals[i]=new AcousticMeshPose("virtual-"+i,data,Matrix4x4.identity);
            Assert.Throws<ArgumentException>(()=>new AcousticSceneData(virtuals));
            Assert.AreEqual(1,sink.Owner.Active.Simulations,"Invalid inputs cannot mutate native admission");
            yield return null;
        }
        [UnityTest]
        public IEnumerator AcousticGeometryCaptureOutlivesItsViewAndInputArrays()
        {
            var view=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var accepted=new AcceptedSurface {View=view};
            AcousticMeshPose captured;
            var localMesh=UnityEngine.Object.Instantiate(view.GetComponent<MeshFilter>().sharedMesh);
            try
            {
                view.transform.localScale=Vector3.one*6;
                // Deliberately offset local vertices: a centred cube alone cannot
                // detect an incorrect vertex handedness conversion.
                var points=localMesh.vertices;
                for(int i=0;i<points.Length;i++) points[i]+=Vector3.forward;
                localMesh.vertices=points; view.GetComponent<MeshFilter>().sharedMesh=localMesh;
                captured=AcousticMeshPose.Capture("retained-boundary",accepted);
                accepted.Points[0]=float.NaN; accepted.Indices[0]=-1;
                view.transform.position=Vector3.one*100;
            }
            finally { UnityEngine.Object.DestroyImmediate(view); UnityEngine.Object.DestroyImmediate(localMesh); }
            Assert.IsFalse(accepted.Active);
            Assert.Throws<ArgumentException>(()=>AcousticMeshData.Capture(accepted));
            var owner=sink.Owner;
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(new AcousticSceneData(captured),new Vector3(-1,0,6),new Vector3(1,0,6))));
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0],owner.Generation));
            yield return Measure("geometry-without-view"); Tone(440); double inside=MeasuredPower(440);
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(new AcousticSceneData(captured),new Vector3(-1,0,-6),new Vector3(1,0,-6))));
            yield return Measure("geometry-at-incorrect-mirrored-location");
            Assert.Less(MeasuredPower(440),inside*.05,"Local vertices must preserve the accepted Unity world position");
            Debug.Log("GEOMETRY_RETAINED vertices="+captured.Mesh.VertexCount+" triangles="+captured.Mesh.TriangleCount);
        }
        [UnityTest]
        public IEnumerator AcousticGeometryPlacementsReuseMeshesAndFollowWorldTransforms()
        {
            var owner=sink.Owner; var graph=owner.Active; long generation=owner.Generation;
            var box=CaptureBox("building");
            var initial=new AcousticSceneData(box);
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(initial)));
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0],generation));
            yield return Measure("accepted-building-at-origin"); Tone(440); double baseline=MeasuredPower(440);
            int meshCount=PreparedGraph.MeshesOpened, instanceCount=PreparedGraph.InstancesOpened;
            var matrix=Matrix4x4.TRS(new Vector3(30,7,18),Quaternion.Euler(0,63,0),new Vector3(8,4,6));
            var moved=new AcousticSceneData(new AcousticMeshPose(box.Id,box.Mesh,matrix));
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(moved)));
            yield return Measure("accepted-building-moved-away");
            Assert.Less(MeasuredPower(440),baseline*.01);
            var l=matrix.MultiplyPoint3x4(new Vector3(-.15f,0,0));
            var s=matrix.MultiplyPoint3x4(new Vector3(.15f,0,.05f));
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(moved,l,s)));
            yield return Measure("accepted-building-followed"); Tone(440);
            Assert.AreEqual(meshCount,PreparedGraph.MeshesOpened,"Placement must reuse the native payload");
            Assert.AreEqual(instanceCount,PreparedGraph.InstancesOpened,"Placement must reuse the instance");
            Assert.AreSame(graph,owner.Active); Assert.AreEqual(generation,owner.Generation);
            Debug.Log("GEOMETRY_PLACEMENT nativeMeshReused=true instanceReused=true translatedRotatedScaled=true");
        }
        [UnityTest]
        public IEnumerator AcousticGeometryTopologyReplacementKeepsVoiceAndReleasesOldMesh()
        {
            var owner=sink.Owner; var graph=owner.Active; var box=CaptureBox("room-a");
            var second=new AcousticMeshPose("room-b",box.Mesh,Matrix4x4.TRS(new Vector3(30,0,0),Quaternion.identity,Vector3.one*6));
            int meshesBefore=PreparedGraph.MeshesOpened;
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(new AcousticSceneData(box,second))));
            Assert.AreEqual(meshesBefore+1,PreparedGraph.MeshesOpened,"Two placements share one native mesh");
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0],owner.Generation));
            yield return Measure("two-buildings-first"); Tone(440);
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(new AcousticSceneData(second),new Vector3(29,0,0),new Vector3(31,0,0))));
            yield return Measure("two-buildings-remaining"); Tone(440);
            Assert.AreEqual(meshesBefore+1,PreparedGraph.MeshesOpened);
            int released=PreparedGraph.MeshesClosed;
            var replacement=CaptureBox("room-b");
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(new AcousticSceneData(replacement))));
            yield return Measure("same-id-new-topology"); Tone(440);
            Assert.AreEqual(released+1,PreparedGraph.MeshesClosed,"Old topology released after last instance leaves");
            Assert.AreSame(graph,owner.Active); Assert.AreEqual(1,graph.Playing);
        }
        [UnityTest]
        public IEnumerator AcousticGeometryChangeAtSubmissionRejectsOldWetOutput()
        {
            var owner=sink.Owner; var graph=owner.Active;
            var voice=Begin(0,dry:0); Queue(voice,440); yield return PcmReady(0);
            yield return Measure("geometry-before-submit-edit"); Tone(440);
            var box=CaptureBox("moved-building");
            var moved=new AcousticSceneData(new AcousticMeshPose(box.Id,box.Mesh,Matrix4x4.TRS(new Vector3(200,0,0),Quaternion.identity,Vector3.one*6)));
            sink.BypassRendering=true; yield return new WaitForSecondsRealtime(.1f);
            var block=new float[graph.Frame*2];
            graph.BeforeOutput=()=>owner.UpdateScene(GeometryFrame(moved));
            try { Assert.IsTrue(owner.Render(block,2,AudioSettings.dspTime)); }
            finally { graph.BeforeOutput=null; }
            foreach(float sample in block) Assert.AreEqual(0,sample,"Any mesh revision fences an outdated wet block");
            sink.BypassRendering=false; yield return PcmReady(0);
            // The 200 m room is beyond this probe's 0.25 s response horizon.
            yield return Measure("geometry-after-submit-edit"); Silence();
            Assert.Greater(voice.Read(AudioSettings.dspTime).Played,12000);
        }
        [UnityTest]
        public IEnumerator AcousticGeometryResetWhileWorkerOwnsSceneUsesLatestSnapshot()
        {
            var owner=sink.Owner; var old=owner.Active; var box=CaptureBox("held-building");
            preparationGate=new ManualResetEventSlim(); using var entered=new ManualResetEventSlim();
            owner.UpdateScene(GeometryFrame(new AcousticSceneData(box)),()=> {
                entered.Set(); if(!preparationGate.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Geometry worker gate");
            });
            double end=Time.realtimeSinceStartupAsDouble+10;
            while(!entered.IsSet&&Time.realtimeSinceStartupAsDouble<end) { owner.Tick(); yield return null; }
            Assert.IsTrue(entered.IsSet);
            int closedMeshes=PreparedGraph.MeshesClosed;
            var moved=new AcousticSceneData(new AcousticMeshPose(box.Id,box.Mesh,Matrix4x4.TRS(new Vector3(200,0,0),Quaternion.identity,Vector3.one*6)));
            owner.UpdateScene(GeometryFrame(moved));
            var format=AudioSettings.GetConfiguration(); Assert.IsTrue(AudioSettings.Reset(format));
            owner.Tick(); Assert.IsFalse(old.Disposed); Assert.AreEqual(closedMeshes,PreparedGraph.MeshesClosed);
            preparationGate.Set(); yield return Ready(owner);
            Assert.IsTrue(old.Disposed); Assert.Greater(PreparedGraph.MeshesClosed,closedMeshes);
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0],owner.Generation));
            yield return Measure("geometry-reset-keeps-latest-placement"); Silence();
            yield return Updated(owner,owner.UpdateScene(GeometryFrame(moved,new Vector3(199,0,0),new Vector3(201,0,0))));
            yield return Measure("geometry-reset-new-instance-response"); Tone(440);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomObservationPredictionTests
    {
        [Test] public void PredictionNeedsFreshMotionAndReadsDoNotExtendIt()
        {
            var prediction=new RoomObservationPrediction();
            prediction.Sample(Vector3.zero,Quaternion.identity,10);Assert.That(prediction.Read(10,out _,out _),Is.False);
            prediction.Sample(Vector3.zero,Quaternion.identity,10.1);Assert.That(prediction.Read(10.1,out var distance,out var angle),Is.True);
            Assert.That(distance,Is.EqualTo(.15f));Assert.That(angle,Is.EqualTo(12));
            for(int i=0;i<20;i++)Assert.That(prediction.Read(10.2,out _,out _),Is.True);
            Assert.That(prediction.Read(10.4,out _,out _),Is.False);Assert.That(prediction.Read(10,out _,out _),Is.False);
        }
        [Test] public void PredictionIncludesMeasuredSpeedAndRotationAndCapsFullTurn()
        {
            var prediction=new RoomObservationPrediction();prediction.Sample(Vector3.zero,Quaternion.identity,0);
            prediction.Sample(Vector3.right*.2f,Quaternion.Euler(0,10,0),.1);
            Assert.That(prediction.Read(.1,out var distance,out var angle),Is.True);Assert.That(distance,Is.EqualTo(.85f).Within(.001f));Assert.That(angle,Is.EqualTo(47).Within(.01f));
            prediction.Sample(Vector3.right*.2f,Quaternion.Euler(0,100,0),.2);Assert.That(prediction.Read(.2,out _,out angle),Is.True);Assert.That(angle,Is.EqualTo(180));
        }
        [Test] public void PredictionResetsAfterGapJumpAndClockReversal()
        {
            var prediction=new RoomObservationPrediction();prediction.Sample(Vector3.zero,Quaternion.identity,0);prediction.Sample(Vector3.zero,Quaternion.identity,.1);
            prediction.Sample(Vector3.zero,Quaternion.identity,1);Assert.That(prediction.Read(1,out _,out _),Is.False);
            prediction.Sample(Vector3.one*20,Quaternion.identity,1.1);Assert.That(prediction.Read(1.1,out _,out _),Is.False);
            prediction.Sample(Vector3.one*20,Quaternion.identity,1.2);Assert.That(prediction.Read(1.2,out _,out _),Is.True);
            prediction.Sample(Vector3.one*20,Quaternion.Euler(0,180,0),1.21);Assert.That(prediction.Read(1.21,out _,out _),Is.False);
            prediction.Sample(Vector3.one*20,Quaternion.identity,1);Assert.That(prediction.Read(1,out _,out _),Is.False);
        }
        [Test] public void PredictionRejectsNonfiniteAndDegenerateSamplesAndRecovers()
        {
            var prediction=new RoomObservationPrediction();
            foreach(var value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
                prediction.Sample(new Vector3(value,0,0),Quaternion.identity,1);Assert.That(prediction.Read(1,out _,out _),Is.False);
                prediction.Sample(Vector3.zero,new Quaternion(0,value,0,1),1);Assert.That(prediction.Read(1,out _,out _),Is.False);
                prediction.Sample(Vector3.zero,Quaternion.identity,value);Assert.That(prediction.Read(1,out _,out _),Is.False);
            }
            prediction.Sample(Vector3.zero,new Quaternion(0,0,0,0),1);Assert.That(prediction.Read(1,out _,out _),Is.False);
            prediction.Sample(Vector3.zero,Quaternion.identity,1);prediction.Sample(Vector3.zero,Quaternion.identity,1.1);Assert.That(prediction.Read(1.1,out _,out _),Is.True);
            prediction.Reset();Assert.That(prediction.Read(1.1,out _,out _),Is.False);
        }
        [Test] public void DuplicateFrameSamplesPreserveTheRateAndUnnormalisedRotationsDoNotDistortIt()
        {
            var prediction=new RoomObservationPrediction();prediction.Sample(Vector3.zero,new Quaternion(0,0,0,2),1);
            prediction.Sample(Vector3.right*.1f,new Quaternion(0,0,0,2),1.1);Assert.That(prediction.Read(1.1,out var distance,out var angle),Is.True);
            prediction.Sample(Vector3.right*.1f,new Quaternion(0,0,0,2),1.1);Assert.That(prediction.Read(1.1,out var again,out var turn),Is.True);Assert.That(again,Is.EqualTo(distance));Assert.That(turn,Is.EqualTo(angle));
        }
        static RoomSpatialBounds Spatial(Bounds bounds,int layer=0)=>new(){Visual=new(){Known=true,HasBounds=true,Bounds=bounds},VisualLayers=1<<layer};
        [TestCase(false)] [TestCase(true)] public void ExpandedActualProjectionContainsSweptCameraViews(bool orthographic)
        {
            var root=new GameObject("Prediction projection fixture",typeof(Camera));
            try{
                var camera=root.GetComponent<Camera>();camera.stereoTargetEye=StereoTargetEyeMask.None;camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.aspect=1.3f;camera.fieldOfView=65;camera.orthographic=orthographic;camera.orthographicSize=2;
                var baseline=new Vector3(11,-3,7);var rotation=Quaternion.Euler(10,25,15);root.transform.SetPositionAndRotation(baseline,rotation);
                var expanded=new RoomObservationView(camera,translation:.6f,angle:65);
                int covered=0;
                // Evaluate real Unity projections throughout an arc and translation,
                // including intermediate positions; endpoint-only unions miss these.
                for(int step=0;step<=12;step++){
                    float t=step/12f;root.transform.SetPositionAndRotation(baseline+new Vector3(.3f,.4f,.2f)*t,rotation*Quaternion.AngleAxis(65*t,new Vector3(1,2,-1).normalized));
                    var actual=new RoomObservationView(camera);
                    for(int x=-5;x<=5;x++)for(int y=-3;y<=3;y++)for(int z=1;z<=16;z+=3){
                        var bounds=new Bounds(root.transform.TransformPoint(new Vector3(x,y,z)),Vector3.one*.1f);var spatial=Spatial(bounds);
                        if(!actual.Includes(spatial))continue;covered++;Assert.That(expanded.Includes(spatial),Is.True,$"Swept projection lost {bounds.center} at {step}");
                    }
                }
                Assert.That(covered,Is.GreaterThan(100));
            }finally{Object.DestroyImmediate(root);}
        }
        [Test] public void UnknownPredictionRemainsConservativeButHonoursLayersAndKnownEmpty()
        {
            var root=new GameObject("Unknown projection fixture",typeof(Camera));
            try{
                var camera=root.GetComponent<Camera>();camera.cullingMask=1<<7;var unknown=new RoomObservationView(camera,uncertain:true);
                Assert.That(unknown.Includes(Spatial(new Bounds(Vector3.back*100,Vector3.one),7)),Is.True);
                Assert.That(unknown.Includes(Spatial(new Bounds(Vector3.forward,Vector3.one),8)),Is.False);
                Assert.That(unknown.Includes(new RoomSpatialBounds{Visual=RoomSpatialExtent.Empty,VisualLayers=1<<7}),Is.False);
                Assert.That(unknown.Includes(RoomSpatialBounds.Unknown),Is.True);
            }finally{Object.DestroyImmediate(root);}
        }
    }
}

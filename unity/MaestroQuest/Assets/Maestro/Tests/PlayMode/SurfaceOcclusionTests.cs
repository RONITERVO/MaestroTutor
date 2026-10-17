// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        Transform SurfaceOcclusionStage()
        {
            block.transform.position=new Vector3(4,1,2);
            SurfaceRun(new RoomAgentExecutor(editor),SurfaceConfigure());
            return block.GetComponent<DrawingSurfaceView>().Surface("Front");
        }
        BoxCollider SurfaceObstacle(Transform patch,float z=-.06f,float depth=.01f)
        {
            var obj=new GameObject("Surface obstruction");obj.transform.SetParent(root.transform,false);
            obj.transform.SetPositionAndRotation(patch.TransformPoint(new Vector3(0,0,z)),patch.rotation);
            var collider=obj.AddComponent<BoxCollider>();collider.size=new Vector3(.15f,.15f,depth);return collider;
        }
        static Ray SurfaceRay(Transform patch,float x=0,float y=0)=>new(patch.TransformPoint(new Vector3(x,y,-.12f)),patch.forward);
        bool SurfaceHit(Transform patch)=>editor.FindDrawingSurface(SurfaceRay(patch),.25f,out _,out _,out _,out _);
        [UnityTest] public IEnumerator SurfaceOcclusionTracksSolidMotionAndOriginInsideWithoutWaitingForPhysics()
        {
            var patch=SurfaceOcclusionStage();Assert.That(SurfaceHit(patch),Is.True);
            var obstacle=SurfaceObstacle(patch);Assert.That(editor.FindDrawingSurface(SurfaceRay(patch),.25f,out var target,out var surface,out var point,out _),Is.False);
            Assert.That(target,Is.Null);Assert.That(surface,Is.Null);Assert.That(point,Is.EqualTo(Vector3.zero));
            obstacle.transform.position=patch.TransformPoint(new Vector3(0,0,-.12f));Assert.That(SurfaceHit(patch),Is.False,"Origin inside another solid");
            obstacle.transform.position=patch.TransformPoint(new Vector3(.3f,0,-.06f));Assert.That(SurfaceHit(patch),Is.True,"Moved away in this frame");
            obstacle.transform.position=patch.TransformPoint(new Vector3(0,0,.06f));Assert.That(SurfaceHit(patch),Is.True,"Behind the drawing plane");
            obstacle.transform.position=patch.TransformPoint(new Vector3(0,0,-.06f));obstacle.enabled=false;Assert.That(SurfaceHit(patch),Is.True);
            obstacle.enabled=true;obstacle.gameObject.SetActive(false);Assert.That(SurfaceHit(patch),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator SurfaceOcclusionIgnoresTriggersControllerAndOwnApproximateProxy()
        {
            var patch=SurfaceOcclusionStage();var obstacle=SurfaceObstacle(patch);obstacle.isTrigger=true;Assert.That(SurfaceHit(patch),Is.True);
            obstacle.isTrigger=false;obstacle.gameObject.layer=RoomPhysicsLayers.Controller;Assert.That(SurfaceHit(patch),Is.True);
            obstacle.gameObject.layer=RoomPhysicsLayers.Scanned;Assert.That(SurfaceHit(patch),Is.False);
            obstacle.transform.SetParent(block.transform,true);Assert.That(SurfaceHit(patch),Is.True,"Logical patch may sit inside its own proxy");yield return null;
        }
        [UnityTest] public IEnumerator SurfaceOcclusionBlocksBothSidesOfOneSidedScannedWall()
        {
            var patch=SurfaceOcclusionStage();var obj=new GameObject("One sided scanned wall");obj.layer=RoomPhysicsLayers.Scanned;obj.transform.SetParent(root.transform,false);obj.transform.SetPositionAndRotation(patch.TransformPoint(new Vector3(0,0,-.06f)),patch.rotation);
            var mesh=new Mesh {vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(0,1,0)},triangles=new[]{0,1,2}};var collider=obj.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
            bool backfaces=Physics.queriesHitBackfaces;
            Assert.That(SurfaceHit(patch),Is.False);obj.transform.rotation=patch.rotation*Quaternion.Euler(0,180,0);Assert.That(SurfaceHit(patch),Is.False);
            Assert.That(Physics.queriesHitBackfaces,Is.EqualTo(backfaces));Assert.That(RoomPhysicsLayers.InteractionMask&(1<<RoomPhysicsLayers.Scanned),Is.Zero,"Tool recovery must still ignore scanned walls");
            collider.sharedMesh=null;Object.Destroy(mesh);yield return null;
        }
        [UnityTest] public IEnumerator SurfaceOcclusionEndsPhysicalStrokeBlocksEraserButAllowsExplicitSourceEdit()
        {
            var patch=SurfaceOcclusionStage();var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleSurfaceDrawing();
            pencil.Begin(0,SurfaceRay(patch,-.03f));pencil.Move(0,SurfaceRay(patch,0,.03f));Assert.That(pencil.IsDrawing,Is.True);
            var obstacle=SurfaceObstacle(patch);pencil.Move(0,SurfaceRay(patch,.03f));Assert.That(pencil.IsDrawing,Is.False);
            string target=editor.Identity(block);Assert.That(editor.Read(target).surfaces[0].strokes.Single().points.Length,Is.EqualTo(2),"No segment through the obstacle");
            Assert.That(editor.ConfigureDrawing("surfaceErase",Color.white,.003f,out var error),Is.True,error);pencil.Begin(0,SurfaceRay(patch,0,.03f));Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(1));
            SurfaceRun(new RoomAgentExecutor(editor),SurfaceAdd());Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));editor.Undo();Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(1));
            obstacle.enabled=false;pencil.Begin(0,SurfaceRay(patch,0,.03f));Assert.That(editor.Read(target).surfaces[0].strokes,Is.Empty);editor.Undo();Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator SurfaceOcclusionHeldChalkIgnoresItselfAndStopsAtScannedContact()
        {
            block.transform.position=new Vector3(4,1,2);var (id,tip,patch)=DrawingTipStage();
            PlaceTip(id,patch,-.03f);tip.Sample(true,RoomActorRole.Control);var pencil=root.GetComponent<SpatialDrawing>();Assert.That(pencil.IsToolDrawing(id),Is.True);
            PlaceTip(id,patch,0);tip.Sample(true,RoomActorRole.Control);var obstacle=SurfaceObstacle(patch,-.005f,.001f);obstacle.gameObject.layer=RoomPhysicsLayers.Scanned;
            PlaceTip(id,patch,.03f);tip.Sample(true,RoomActorRole.Control);Assert.That(pencil.IsDrawing,Is.False);Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes.Single().points.Length,Is.EqualTo(2));
            tip.Sample(true,RoomActorRole.Control);Assert.That(pencil.IsDrawing,Is.False);obstacle.enabled=false;tip.Sample(true,RoomActorRole.Control);Assert.That(pencil.IsToolDrawing(id),Is.True);
            PlaceTip(id,patch,-.03f);tip.Sample(true,RoomActorRole.Control);tip.Sample(false,RoomActorRole.Control);Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes.Length,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator SurfaceOcclusionSaturatedQueryCannotProveClearPath()
        {
            var patch=SurfaceOcclusionStage();var colliders=new BoxCollider[64];
            for(int i=0;i<colliders.Length;i++){colliders[i]=SurfaceObstacle(patch,-.12f);colliders[i].transform.SetParent(block.transform,true);}
            Assert.That(SurfaceHit(patch),Is.False,"Even ignored hits cannot establish that an omitted blocker is absent");
            foreach(var collider in colliders)collider.enabled=false;Assert.That(SurfaceHit(patch),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator SurfaceOcclusionUsesWorldDistanceAndRejectsInvalidQueries()
        {
            var patch=SurfaceOcclusionStage();block.transform.rotation=Quaternion.Euler(15,35,5);block.transform.localScale=new Vector3(1.2f,.8f,1.5f);
            var ray=SurfaceRay(patch);ray.direction*=7;
            Assert.That(editor.FindDrawingSurface(ray,.25f,out _,out _,out var point,out var distance),Is.True);Assert.That(point.magnitude,Is.LessThan(.00001f));Assert.That(distance,Is.EqualTo(.18f).Within(.00001f));
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleSurfaceDrawing();Assert.That(Vector3.Distance(pencil.PointerPoint(ray),patch.position),Is.LessThan(.00001f));
            Assert.That(editor.FindDrawingSurface(ray,.1f,out _,out _,out _,out _),Is.False);
            foreach(float maximum in new[]{-1,float.NaN,float.PositiveInfinity})Assert.That(editor.FindDrawingSurface(ray,maximum,out _,out _,out _,out _),Is.False);
            Assert.That(editor.FindDrawingSurface(new Ray(ray.origin,Vector3.zero),.25f,out _,out _,out _,out _),Is.False);yield return null;
        }
    }
}

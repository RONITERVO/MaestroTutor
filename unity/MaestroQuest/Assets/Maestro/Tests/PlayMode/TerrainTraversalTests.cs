// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class TerrainTraversalTests
    {
        GameObject physical,content;Transform head;RoomPhysicsWorld world;VirtualRoomView view;
        [UnitySetUp]public IEnumerator Setup(){
            RoomPhysicsLayers.Configure();physical=new GameObject("Physical tracking");content=new GameObject("Authored terrain world");content.transform.SetParent(physical.transform,false);
            head=new GameObject("Tracked head").transform;head.SetParent(physical.transform,false);head.localPosition=Vector3.up*1.6f;
            world=physical.AddComponent<RoomPhysicsWorld>();view=physical.AddComponent<VirtualRoomView>();view.Initialize(content.transform,physical.transform,head.gameObject.AddComponent<Camera>(),null,world);
            Assert.That(view.Enter(),Is.True);yield return null;
        }
        BoxCollider Box(Vector3 center,Vector3 scale,bool ground=true){
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(content.transform,false);go.transform.localPosition=center;go.transform.localScale=scale;go.layer=RoomPhysicsLayers.Environment;
            var collision=go.GetComponent<BoxCollider>();if(ground)go.AddComponent<RoomWalkableSurface>().Publish(collision);return collision;
        }
        HeightFieldView Slope(float gradient,out RoomHeightField data){
            var go=new GameObject("Accepted sloped mesh");go.transform.SetParent(content.transform,false);
            data=new RoomHeightField{cells=8,width=2,depth=2,maxHeight=.5f,heights=new float[81]};
            for(int z=0;z<=8;z++)for(int x=0;x<=8;x++)data.heights[z*9+x]=.25f+gradient*(x*.25f-1);
            var field=go.AddComponent<HeightFieldView>();field.Apply(new[]{data});content.transform.position=Vector3.down*.25f;Physics.SyncTransforms();return field;
        }
        RoomViewpoint Position(){Assert.That(view.ReadViewpoint(out var point),Is.True);return point;}
        void Move(Vector3 direction,int count){for(int i=0;i<count;i++)Assert.That(view.Move(direction*.02f),Is.True,$"step {i}: {view.MovementError}");}
        void Refuses(Vector3 direction){var matrix=content.transform.localToWorldMatrix;Assert.That(view.Move(direction*.04f),Is.False);Assert.That(content.transform.localToWorldMatrix,Is.EqualTo(matrix),"A refused traversal must not partially publish its path");}
        [UnityTest]public IEnumerator ViewerClimbsAndDescendsAcceptedMeshWithoutMovingPhysicalTracking(){
            var field=Slope(.2f,out _);var physicalPose=physical.transform.localToWorldMatrix;var headPose=head.localToWorldMatrix;
            Move(Vector3.right,20);var point=Position();Assert.That(point.position.x,Is.EqualTo(.4f).Within(.001));Assert.That(point.position.y,Is.EqualTo(.33f).Within(.002));
            Assert.That(field.Collision.Raycast(new Ray(head.position,Vector3.down),out var hit,2),Is.True);Assert.That(hit.point.y,Is.EqualTo(0).Within(.002));
            Move(Vector3.left,40);point=Position();Assert.That(point.position.x,Is.EqualTo(-.4f).Within(.001));Assert.That(point.position.y,Is.EqualTo(.17f).Within(.002));
            Assert.That(physical.transform.localToWorldMatrix,Is.EqualTo(physicalPose));Assert.That(head.localToWorldMatrix,Is.EqualTo(headPose));yield return null;
        }
        [UnityTest]public IEnumerator YawedTerrainAndViewpointUseTheSameAcceptedSurface(){
            Slope(.2f,out _);content.transform.rotation=Quaternion.Euler(0,90,0);Physics.SyncTransforms();
            Move(Vector3.back,15);var point=Position();Assert.That(point.position.x,Is.EqualTo(.3f).Within(.001));Assert.That(point.position.y,Is.EqualTo(.31f).Within(.002));
            Assert.That(view.CanPlaceViewpoint(new RoomViewpoint{active=true,position=new Vector3(-.3f,.19f,0)},out var error),Is.True,error);
            Assert.That(view.CanPlaceViewpoint(new RoomViewpoint{active=true,position=new Vector3(-.3f,.39f,0)},out _),Is.False,"Destination support must be checked, not silently projected");yield return null;
        }
        [UnityTest]public IEnumerator EightCentimetreStepsGoUpAndDownButLargeStepsRefuseAtomically(){
            Box(new Vector3(-1,-.1f,0),new Vector3(2,.2f,3));var step=Box(new Vector3(1,-.06f,0),new Vector3(2,.28f,3));
            head.position=new Vector3(-.6f,1.6f,0);Physics.SyncTransforms();Move(Vector3.right,60);Assert.That(Position().position.y,Is.EqualTo(.08f).Within(.002));
            Move(Vector3.left,60);Assert.That(Position().position.y,Is.EqualTo(0).Within(.002));
            step.transform.localPosition+=Vector3.up*.15f;Physics.SyncTransforms();bool refused=false;
            for(int i=0;i<40;i++){var prior=content.transform.localToWorldMatrix;if(!view.Move(Vector3.right*.02f)){Assert.That(content.transform.localToWorldMatrix,Is.EqualTo(prior));refused=true;break;}}
            Assert.That(refused,Is.True,"A 23 cm step must not be climbed");yield return null;
        }
        [UnityTest]public IEnumerator TerrainPreviewsCannotMoveGroundAndRemovedOrChangedGroundStopsWalking(){
            var field=Slope(.2f,out var data);var preview=data.Copy();for(int i=0;i<preview.heights.Length;i++)preview.heights[i]=.49f;field.Preview(preview);
            Move(Vector3.right,5);Assert.That(Position().position.y,Is.EqualTo(.27f).Within(.002));
            field.Apply(new[]{preview});Physics.SyncTransforms();Refuses(Vector3.right);
            field.Collision.enabled=false;Refuses(Vector3.right);yield return null;
        }
        [UnityTest]public IEnumerator LedgesSteepGroundAndForeignGroundNeverInventAPath(){
            var floor=Box(new Vector3(0,-.1f,0),new Vector3(1,.2f,2));head.position=new Vector3(.29f,1.6f,0);Physics.SyncTransforms();Refuses(Vector3.right);
            head.position=Vector3.up*1.6f;floor.transform.localScale=new Vector3(4,.2f,4);floor.transform.rotation=Quaternion.Euler(0,0,30);floor.transform.position=Vector3.down*(.1f/Mathf.Cos(30*Mathf.Deg2Rad));Physics.SyncTransforms();Refuses(Vector3.right);
            floor.transform.rotation=Quaternion.identity;floor.transform.position=Vector3.down*.1f;floor.transform.SetParent(physical.transform,true);Physics.SyncTransforms();Refuses(Vector3.right);yield return null;
        }
        [UnityTest]public IEnumerator ArbitraryPropsAndLowCeilingsDoNotBecomeSteps(){
            Box(Vector3.down*.1f,new Vector3(4,.2f,4));var prop=Box(new Vector3(.18f,.06f,0),new Vector3(.02f,.12f,1),false);Physics.SyncTransforms();Refuses(Vector3.right);
            prop.enabled=false;var field=Slope(.2f,out _);var floor=content.transform.GetChild(0);floor.gameObject.SetActive(false);
            Box(new Vector3(.5f,1.93f,0),new Vector3(1,.1f,2),false);Physics.SyncTransforms();bool refused=false;
            for(int i=0;i<30;i++){var prior=content.transform.localToWorldMatrix;if(!view.Move(Vector3.right*.02f)){Assert.That(content.transform.localToWorldMatrix,Is.EqualTo(prior));refused=true;break;}}
            Assert.That(refused,Is.True,"Climbing must sweep the head as well as the feet");yield return null;
        }
        [UnityTest]public IEnumerator SharedGroundFactPolicyUsesAcceptedSlopeNormalsAndFootprints(){
            var field=Slope(.2f,out _);var query=new RoomGroundQuery();query.Capture(content.transform);var expected=new Vector3(.25f,.05f,0);
            Assert.That(query.Support(expected,.2f,.5f,.5f,out var point,out var normal,out var error),Is.True,error);
            Assert.That(point.y,Is.EqualTo(.05f).Within(.001));Assert.That(Vector3.Angle(normal,new Vector3(-.2f,1,0).normalized),Is.LessThan(.05));
            Assert.That(query.Support(new Vector3(.9f,.18f,0),.2f,.5f,.5f,out _,out _,out _),Is.False);
            field.Collision.enabled=false;query.Capture(content.transform);Assert.That(query.Support(expected,.2f,.5f,.5f,out _,out _,out _),Is.False);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){Object.Destroy(physical);yield return null;yield return null;}
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest] public IEnumerator TemplateStairsUsePhysicalTreadsAndProduceAConnectedNativeRoute(){
            RoomPhysicsLayers.Configure();RoomItem stairs=null;var supports=new System.Collections.Generic.List<RoomItem>();
            var origin=new Vector3(4,0,4);
            IEnumerator Create(string template,Vector3 at,Action<RoomItem> accept=null){
                var entry=CreationTemplates.All.Single(value=>value.Id==template);
                Assert.IsTrue(editor.BeginCreateRecipe(entry.Name,at,4,entry.Recipe,entry.Collision,entry.Physics,out var id,out var pending,out var error,staticSupport:entry.StaticSupport),error);
                using(pending){yield return Completed(pending);}var item=editor.Find(id);Assert.IsNotNull(item);Assert.IsTrue(item.GetComponent<CreatedRoomObject>().StaticSupportReady,item.GetComponent<CreatedRoomObject>().StaticSupportIssue);supports.Add(item);accept?.Invoke(item);
            }
            yield return Create("stairs",origin,value=>stairs=value);
            //Both landings are the shipped platform template too. At scale4,
            //the lower top meets tread1 at8cm, upper top meets tread4 at32cm.
            yield return Create("platform",origin+new Vector3(0,-.24f,-2.8f));
            yield return Create("platform",origin+new Vector3(0,0,2.8f));
            Assert.IsTrue(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out var error),error);
            Assert.IsTrue(physics.SetRunning(true,out error),error);yield return null;Physics.SyncTransforms();
            Assert.That(supports.All(item=>item.GetComponent<CreatedRoomObject>().StaticSupportReady),Is.True);
            var ground=new RoomGroundQuery();ground.Capture(root.transform);var markers=stairs.GetComponentsInChildren<RoomWalkableSurface>();
            Assert.AreEqual(4,markers.Length);Assert.That(markers.Select(m=>m.SourceId),Is.EquivalentTo(new[]{"Tread1","Tread2","Tread3","Tread4"}));
            Assert.That(markers.All(m=>m.Ordinary&&m.Available&&stairs.SolidColliders.Contains(m.Collision)),Is.True);
            var points=new[]{origin+new Vector3(0,.08f,-2),origin+new Vector3(0,.08f,-.9f),origin+new Vector3(0,.16f,-.3f),origin+new Vector3(0,.24f,.3f),origin+new Vector3(0,.32f,.9f),origin+new Vector3(0,.32f,2)};
            for(int i=0;i<points.Length;i++){
                Assert.IsTrue(ground.Sample(points[i],.04f,.04f,out var physical),"Physical support "+i);
                Assert.That(physical.point.y,Is.EqualTo(points[i].y).Within(.001f));
                if(i>0&&i<5)Assert.AreSame(markers.Single(m=>m.SourceId=="Tread"+i).Collision,physical.collider,"Each height belongs to its authored box, not an invisible slope");
            }
            var motor=new RoomGroundMotor();var current=points[0];int mask=1<<RoomPhysicsLayers.Item;
            for(int i=1;i<points.Length;i++){
                Assert.IsTrue(motor.Travel(ground,current,points[i]-current,.2f,1.7f,mask,_=>true,p=>physics.ContainsSimulation(p+Vector3.up*.1f),out var next,out error),"Physical step "+i+": "+error);
                Assert.That(motor.SupportPoint.y,Is.EqualTo(points[i].y).Within(.002f));current=next;
            }
            var navigation=root.AddComponent<RoomNavigation>();navigation.Initialize(physics);Assert.IsTrue(navigation.Prepare(.2f,1.7f,out error),error);yield return NavigationTestWait.Built(navigation);
            var path=new NavMeshPath();Assert.IsTrue(navigation.Path(points[0],points[^1],path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);Assert.GreaterOrEqual(path.corners.Length,2);
            //NavMesh y is approximate. The independently asserted collider samples
            //above remain the foot-placement authority.
            var saved=JsonUtility.ToJson(editor.Snapshot());var middle=markers.Single(m=>m.SourceId=="Tread3").Collision;middle.enabled=false;Physics.SyncTransforms();
            Assert.IsFalse(ground.Sample(points[3],.04f,.04f,out _));Assert.IsFalse(navigation.Path(points[0],points[^1],new NavMeshPath()));yield return NavigationTestWait.Built(navigation);
            Assert.IsFalse(navigation.Path(points[0],points[^1],new NavMeshPath()),"Removed middle support cannot leave the old connected route installed");Assert.AreEqual(saved,JsonUtility.ToJson(editor.Snapshot()));
        }
    }
}

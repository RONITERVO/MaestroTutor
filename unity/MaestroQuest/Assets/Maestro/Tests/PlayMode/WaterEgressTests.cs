// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest]public IEnumerator WaterEgressSharedFactUsesLivePolicyWithoutChangingSavedPlacement(){
            var(pool,_)=MediumFixture(.05f);physics.PausePhysics();var actor=editor.Find("maestro");actor.transform.position=new(4.25f,1.4f,0);Physics.SyncTransforms();
            string saved=JsonUtility.ToJson(editor.Read("maestro"));
            JObject Path(float x)=>editor.ObserveWaterPath("maestro",new Vector3(x,1.4f,0));
            Assert.That((bool)Path(4.3f)["allowed"],Is.True);Assert.That((string)Path(4.3f)["bodyId"],Is.EqualTo(pool));
            Assert.That((bool)Path(4.2f)["allowed"],Is.False);Assert.That((bool)Path(3)["allowed"],Is.False);
            Assert.That(WaterTraversalCapability.Path().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]="maestro",["position"]=new JObject{["x"]=4.3,["y"]=1.4,["z"]=0}},out var observed),Is.True);
            Assert.That((bool)JObject.FromObject(observed.Value)["allowed"],Is.True);
            Assert.That(JsonUtility.ToJson(editor.Read("maestro")),Is.EqualTo(saved));
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="avoid",maxDepthMetres=.1f},out var error),Is.True,error);
            Assert.That((bool)Path(4.3f)["allowed"],Is.False,"Current maximum depth still applies to egress");
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="avoid",maxDepthMetres=.25f},out error),Is.True,error);
            Assert.That((bool)Path(4.3f)["allowed"],Is.True);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(pool),out error),Is.True,error);yield return null;
            var unavailable=Path(4.3f);Assert.That((bool)unavailable["allowed"],Is.False);Assert.That((string)unavailable["availability"],Is.Not.EqualTo("ready"));
            Assert.That(editor.Find(pool),Is.Null,"A passive escape query cannot start loading");
        }
        [UnityTest]public IEnumerator WaterEgressCannotUseOnePoolToEnterAnother(){
            var(_,__)=MediumFixture(.05f);physics.PausePhysics();var actor=editor.Find("maestro");var from=new Vector3(4.25f,1.4f,0);var to=new Vector3(4.9f,1.4f,0);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.25f,1.7f,out var hit),Is.True,hit.Reason);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Next pool",new Vector3(5.1f,1,0),1,Color.white,out var second,out var error),Is.True,error);
            Assert.That(editor.EditContainer(second,editor.ObjectRevision(second),new(){version=2,rectangle=new(){width=1,depth=1},frame=new(){position=new Vector3(0,.2f,0)},height=.6f,capacityMl=600000,amountMl=400000},out error),Is.True,error);
            Physics.SyncTransforms();Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.25f,1.7f,out hit),Is.False);Assert.That(hit.BodyId,Is.EqualTo(second));yield return null;
        }
    }
    public sealed partial class AvatarSpatialTests {
        [UnityTest]public IEnumerator WaterEgressFollowingActuallyLeavesShallowWaterWithoutChangingAvoidPolicy(){
            WaterBasin();var start=new Vector3(1,0,0);avatar.transform.position=start;Physics.SyncTransforms();
            viewer.transform.position=new(3.5f,1.6f,0);editor.SetAvatarMovement(.8f,1.2f);
            Assert.That(motion.Begin("Leave shallow water",AvatarSpatialMode.Follow,out var error),Is.True,error);var previous=start;
            try{
                for(int i=0;i<360&&avatar.transform.position.x<1.7f;i++){
                    yield return null;var current=avatar.transform.position;
                    if((current-previous).sqrMagnitude>1e-10f)Assert.That(editor.Liquids.CheckTraversal(editor.Find("maestro"),previous,current,.25f,1.7f,out var hit),Is.True,hit.Reason);
                    Assert.That(current.x,Is.GreaterThanOrEqualTo(previous.x-.001f));previous=current;
                }
                Assert.That(avatar.transform.position.x,Is.GreaterThan(1.6f),motion.Status);
                Assert.That(editor.Find("maestro").WaterTraversal.mode,Is.EqualTo("avoid"));
            }finally{motion.Stop();}
        }
        [UnityTest]public IEnumerator WaterEgressStillRequiresGroundAndSolidClearance(){
            WaterBasin();var from=new Vector3(1,0,0);var to=new Vector3(1.2f,0,0);
            Assert.That(navigation.Traverse(from,to,_=>false,out _,out var error),Is.True,error);
            var wall=Surface(new Vector3(1.45f,.9f,0),new Vector3(.1f,1.8f,2));var collider=wall.GetComponent<Collider>();Physics.SyncTransforms();
            Assert.That(navigation.Traverse(from,to,c=>c==collider,out _,out error),Is.False,"Escaping water cannot bypass a solid wall");
            wall.SetActive(false);world.SetSurfaces(false,"Accepted ground unavailable");
            Assert.That(navigation.Traverse(from,to,_=>false,out _,out error),Is.False);yield return null;
        }
    }
}

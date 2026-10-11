// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        (string id,string area) NavigationGround(Vector3 position)
        {
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Navigation ground",position,1,Color.white,out var id,out var error),error);
            Assert.IsTrue(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),editor.Status);
            var field=new RoomHeightField{cells=8,width=4,depth=4,maxHeight=.5f,heights=Enumerable.Repeat(.2f,81).ToArray()};
            Assert.IsTrue(editor.EditHeightField(id,editor.ObjectRevision(id),field,out error),error);
            var area=new RoomRegion{id=Guid.NewGuid().ToString("N"),name="Walking ground"};
            Assert.IsTrue(editor.EditRegion(area,area.id,0,Array.Empty<string>(),out error),error);
            Assert.IsTrue(editor.BindRegion(id,editor.ObjectRevision(id),area.id,editor.RegionRevision(area.id),out error),error);
            return(id,area.id);
        }
        bool NavigationRetained(string id,RoomRetentionReason reason=RoomRetentionReason.Navigation)=>(editor.ReadRetention().Reasons(id)&reason)!=0;
        [UnityTest] public IEnumerator NavigationRetentionUsesActualMapSourcesAndReadDoesNotPrepareOrEdit()
        {
            Tutor();var(id,area)=NavigationGround(Vector3.zero);string saved=JsonUtility.ToJson(editor.Snapshot());
            Assert.IsFalse(NavigationRetained(id));Assert.AreEqual(0,navigation.BuildRevision);Ready();uint built=navigation.BuildRevision;
            Assert.IsTrue(NavigationRetained(id));Assert.IsTrue(RegionFacts.Retention().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=area},out var value));
            var fact=(JObject)value.Value;Assert.That(fact["reasons"].Values<string>(),Does.Contain("navigation"));Assert.IsFalse((bool)fact["unloadingSupported"]);
            var unrelated=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id);Assert.IsFalse(NavigationRetained(unrelated.id));
            var view=editor.Find(id).GetComponent<HeightFieldView>();var preview=view.Accepted.Copy();preview.heights[0]+=.01f;view.Preview(preview);
            Assert.IsTrue(NavigationRetained(id));Assert.AreEqual(built,navigation.BuildRevision);Assert.AreEqual(saved,JsonUtility.ToJson(editor.Snapshot()));yield return null;
        }
        [UnityTest] public IEnumerator NavigationRetentionUnchangedMeshWithNewCanonicalOwnerRefreshesItsDependency()
        {
            Tutor();var field=Terrain(Vector3.zero,.2f,out _);Ready();uint built=navigation.BuildRevision;
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"New geometry owner",new Vector3(6,0,0),1,Color.white,out var id,out var error),error);
            Assert.IsTrue(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),editor.Status);
            field.transform.SetParent(editor.Find(id).transform,true);Physics.SyncTransforms();
            Assert.IsFalse(NavigationRetained(id),"An observation alone must not refresh the held map");
            Assert.IsTrue(navigation.Ready);Assert.IsTrue(NavigationRetained(id));Assert.Greater(navigation.BuildRevision,built);yield return null;
        }
        [UnityTest] public IEnumerator NavigationRetentionMultipleConsumersReleaseOnlyTheirOwnMap()
        {
            Tutor();var(id,_)=NavigationGround(Vector3.zero);Ready();var child=new GameObject("Second navigation consumer");child.transform.SetParent(physicalRoot.transform,false);
            var second=child.AddComponent<RoomNavigation>();second.Initialize(world);Assert.IsTrue(second.Prepare(.25f,1.7f,out var error),error);
            navigation.enabled=false;Assert.IsTrue(NavigationRetained(id));UnityEngine.Object.Destroy(child);yield return null;Assert.IsFalse(NavigationRetained(id));
            navigation.enabled=true;Assert.IsFalse(NavigationRetained(id));Assert.IsTrue(navigation.Prepare(.25f,1.7f,out error),error);Assert.IsTrue(NavigationRetained(id));
        }
        [UnityTest] public IEnumerator NavigationRetentionRefreshDropsOnlySourcesRemovedFromTheAcceptedMap()
        {
            Tutor();var(first,_)=NavigationGround(Vector3.zero);var(second,_)=NavigationGround(new Vector3(6,0,0));Ready();
            editor.Find(first).GetComponent<HeightFieldView>().Collision.enabled=false;uint built=navigation.BuildRevision;
            Assert.IsTrue(NavigationRetained(first),"Reading must preserve the old map's ownership until it is rebuilt");Assert.AreEqual(built,navigation.BuildRevision);
            Assert.IsTrue(navigation.Ready);Assert.IsFalse(NavigationRetained(first));Assert.IsTrue(NavigationRetained(second));Assert.Greater(navigation.BuildRevision,built);yield return null;
        }
        [UnityTest] public IEnumerator NavigationRetentionKeepsCapturedIdentityAfterColliderDestructionUntilMapRetires()
        {
            Tutor();var(first,_)=NavigationGround(Vector3.zero);NavigationGround(new Vector3(6,0,0));Ready();
            UnityEngine.Object.Destroy(editor.Find(first).GetComponent<HeightFieldView>().Collision);yield return null;
            Assert.IsTrue(NavigationRetained(first),"A dead component must not erase the stable dependency of an existing map");
            Assert.IsTrue(navigation.Ready);Assert.IsFalse(NavigationRetained(first));Assert.IsTrue(editor.HasSavedObject(first));
        }
        [UnityTest] public IEnumerator NavigationRetentionPendingMixedWorldGroundSurvivesOldMapRetirement()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(12,.2f,12));Tutor();var(id,_)=NavigationGround(new Vector3(8,0,0));navigation.SetVirtualFrame(root.transform);Ready();
            root.transform.position+=Vector3.right*.02f;Assert.IsTrue(navigation.Ready);Assert.IsTrue(navigation.PathsPending);Assert.IsTrue(NavigationRetained(id));
            navigation.enabled=false;Assert.IsFalse(NavigationRetained(id));yield return null;
        }
        [UnityTest] public IEnumerator NavigationRetentionRebindingCannotPinThePreviousWorld()
        {
            Tutor();var(id,_)=NavigationGround(Vector3.zero);Ready();var other=new GameObject("Other physics world");
            try{
                var replacement=other.AddComponent<RoomPhysicsWorld>();navigation.Initialize(replacement);Assert.IsFalse(NavigationRetained(id));
                navigation.Initialize(world);Assert.IsFalse(NavigationRetained(id));Assert.IsTrue(navigation.Prepare(.25f,1.7f,out var error),error);Assert.IsTrue(NavigationRetained(id));
                world.PausePhysics();Assert.IsFalse(NavigationRetained(id));
            }finally{UnityEngine.Object.Destroy(other);}yield return null;
        }
        [UnityTest] public IEnumerator NavigationRetentionPendingWaterDetourUsesTheSharedFactAndReleasesOnCancelOrPause()
        {
            string pool=WaterBasin();var end=new Vector3(3,0,0);bool Obstacle(Collider c)=>!c.transform.IsChildOf(avatar.transform);
            Assert.IsFalse(navigation.FollowRoute(Vector3.zero,end,Obstacle,out _));Assert.IsTrue(navigation.FollowRoutePending);Assert.IsTrue(NavigationRetained(pool,RoomRetentionReason.WaterRoute));
            Assert.IsTrue(RegionFacts.Retention().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=""},out var value));Assert.That(((JObject)value.Value)["reasons"].Values<string>(),Does.Contain("waterRoute"));
            navigation.CancelFollowRoute();Assert.IsFalse(NavigationRetained(pool,RoomRetentionReason.WaterRoute));
            Assert.IsFalse(navigation.FollowRoute(Vector3.zero,end,Obstacle,out _));Assert.IsTrue(NavigationRetained(pool,RoomRetentionReason.WaterRoute));
            world.PausePhysics();Assert.IsFalse(NavigationRetained(pool,RoomRetentionReason.WaterRoute));yield return null;
        }
        [UnityTest] public IEnumerator NavigationRetentionCompletedWaterSearchReleasesItsDependenciesWithoutEditingSources()
        {
            string pool=WaterBasin();string saved=JsonUtility.ToJson(editor.Snapshot());
            yield return WaterRoute(Vector3.zero,new Vector3(3,0,0),points=>Assert.Greater(points.Length,2));
            Assert.IsFalse(NavigationRetained(pool,RoomRetentionReason.WaterRoute));Assert.AreEqual(saved,JsonUtility.ToJson(editor.Snapshot()));
        }
    }
}

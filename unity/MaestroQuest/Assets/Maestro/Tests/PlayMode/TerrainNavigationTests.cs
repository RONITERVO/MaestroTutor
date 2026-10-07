// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        HeightFieldView Terrain(Vector3 position, float height, out RoomHeightField data)
        {
            var owner = new GameObject("Accepted walkable terrain"); owner.transform.SetParent(root.transform, false);
            owner.transform.position = position;
            data = new RoomHeightField { cells=8, width=4, depth=4, maxHeight=.5f, heights=Enumerable.Repeat(height,81).ToArray() };
            var field = owner.AddComponent<HeightFieldView>(); field.Apply(new[]{data}); return field;
        }
        [UnityTest] public IEnumerator TerrainNavigationUsesAcceptedCollisionAndRefreshesAfterEdits()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(4,.2f,4));
            var field=Terrain(new Vector3(8,0,0),.2f,out var data); Ready();
            Assert.That(navigation.Sample(new Vector3(8,.2f,0),.08f,out var floor),Is.True,"Accepted authored terrain must be a walking source");
            Assert.That(floor.y,Is.EqualTo(.2f).Within(.04f));
            var preview=data.Copy(); preview.heights=Enumerable.Repeat(.4f,81).ToArray(); field.Preview(preview);
            Assert.That(navigation.Sample(new Vector3(8,.2f,0),.08f,out _),Is.True,"A sculpt preview must not replace accepted navigation");
            Assert.That(navigation.Sample(new Vector3(8,.4f,0),.08f,out _),Is.False);
            field.Apply(new[]{preview}); Physics.SyncTransforms();
            Assert.That(navigation.Sample(new Vector3(8,.2f,0),.08f,out _),Is.False,"Old paths must not survive accepted geometry changes");
            Assert.That(navigation.Sample(new Vector3(8,.4f,0),.08f,out floor),Is.True);
            Assert.That(field.Collision.Raycast(new Ray(floor+Vector3.up,Vector3.down),out var hit,2),Is.True);
            Assert.That(floor.y,Is.EqualTo(hit.point.y).Within(.04f));
            field.Collision.enabled=false;
            Assert.That(navigation.Sample(new Vector3(8,.4f,0),.08f,out _),Is.False,"Disabled collision cannot remain walkable");
            field.Collision.enabled=true;
            Assert.That(navigation.Sample(new Vector3(8,.4f,0),.08f,out _),Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator TerrainNavigationFollowsItsFrameAndExcludesOtherWorlds()
        {
            var field=Terrain(new Vector3(8,0,0),.2f,out _);
            var unrelated=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try {
                unrelated.name="Another world's floor"; unrelated.layer=RoomPhysicsLayers.Scanned;
                unrelated.transform.position=new Vector3(16,-.1f,0); unrelated.transform.localScale=new Vector3(4,.2f,4);
                Ready(); Assert.That(navigation.Sample(new Vector3(8,.2f,0),.08f,out _),Is.True);
                Assert.That(navigation.Sample(new Vector3(16,0,0),.08f,out _),Is.False,"No global collider discovery across world owners");
                var bake=navigation.BuildRevision;
                physicalRoot.transform.SetPositionAndRotation(new Vector3(0,2,5),Quaternion.Euler(0,45,0));
                Assert.That(navigation.Sample(field.transform.TransformPoint(Vector3.up*.2f),.08f,out _),Is.True);
                Assert.That(navigation.BuildRevision,Is.EqualTo(bake),"Rigid world movement must move the owned navmesh without rebaking it");
                field.transform.SetPositionAndRotation(new Vector3(10,1,3),Quaternion.Euler(0,30,0)); Physics.SyncTransforms();
                Assert.That(navigation.Sample(new Vector3(8,.2f,0),.08f,out _),Is.False);
                Assert.That(navigation.Sample(field.transform.TransformPoint(Vector3.up*.2f),.08f,out _),Is.True);
                field.Apply(null);
                Assert.That(navigation.Sample(new Vector3(10,1.2f,3),.08f,out _),Is.False,"Removed terrain leaves no cached walkable floor");
            } finally { Object.Destroy(unrelated); }
            yield return null;
        }
        [UnityTest] public IEnumerator MaestroWalksTheSameSlopingTerrainAsItsCollider()
        {
            Tutor(); var field=Terrain(Vector3.zero,.2f,out var data);
            for(int z=0;z<=8;z++)for(int x=0;x<=8;x++)data.heights[z*9+x]=.1f+.3f*z/8;
            field.Apply(new[]{data}); avatar.transform.position=new Vector3(0,.25f,0); Ready();
            Assert.That(motion.Begin("terrain walking",AvatarSpatialMode.Manual,out var error),Is.True,error);
            var before=avatar.transform.position;
            for(int i=0;i<30;i++){motion.ManualDirection("terrain walking",Vector3.forward);yield return null;}
            Assert.That(avatar.transform.position.z,Is.GreaterThan(before.z+.12f),motion.Status);
            Assert.That(field.Collision.Raycast(new Ray(avatar.transform.position+Vector3.up,Vector3.down),out var hit,2),Is.True);
            Assert.That(avatar.transform.position.y,Is.EqualTo(hit.point.y).Within(.04f));
            motion.Stop();
        }
    }
}

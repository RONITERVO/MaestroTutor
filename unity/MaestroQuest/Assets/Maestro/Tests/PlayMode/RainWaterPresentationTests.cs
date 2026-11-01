// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator RainWaterPresentationStopsActualStreaksAtMeasuredSurfaceAndRipplesWithPhysicsPaused(){
   physics.PausePhysics();editor.Liquids.enabled=false;
   foreach(var automatic in root.GetComponents<WorldRainView>())automatic.enabled=false;
   Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Rain reservoir",new Vector3(8,0,8),4,Color.white,out var id,out var error),error);
   var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
   data.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="floor",position=new Vector3(0,-.01f,0),size=new Vector3(2,.02f,2)}}};
   data.containers=new[]{new RoomContainer{version=2,rectangle=new ContainerRectangle{width=2,depth=2},height=.5f,capacityMl=1000,amountMl=500}};
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),error);
   Assert.IsTrue(editor.SetWeather(editor.WeatherRevision,42,new(){rainMmPerHour=120},0,out error),error);
   physics.SetSurfaces(true,"Aligned rain presentation fixture");
   var item=editor.Find(id);var water=item.GetComponent<ContainerFillView>();Assert.IsTrue(water.NativeReady);
   var surface=item.GetComponentsInChildren<MeshFilter>().Single(candidate=>candidate.sharedMesh?.name=="Bounded liquid level");
   var surfaceRenderer=surface.GetComponent<MeshRenderer>();Assert.IsTrue(surfaceRenderer.enabled&&surface.gameObject.activeInHierarchy);
   var waterPoints=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray();Assert.That(waterPoints.Length,Is.GreaterThan(3));
   float level=waterPoints.Average(point=>point.y);
   Assert.That(waterPoints.Max(point=>point.y)-waterPoints.Min(point=>point.y),Is.LessThan(.0001f));
   Assert.That(level,Is.EqualTo(1).Within(.0001f));
   var floor=item.SolidColliders.Single();Physics.SyncTransforms();Assert.That(floor.bounds.max.y,Is.LessThan(level-.5f),"The actual solid floor is below the free water surface");
   leftAnchor.transform.position=new Vector3(8,1.5f,8);
   var rainRoot=new GameObject("Rain water presentation fixture");rainRoot.transform.SetParent(root.transform,false);
   var rain=rainRoot.AddComponent<WorldRainView>();rain.Initialize(editor,leftAnchor.transform);rain.enabled=false;
   var filter=rain.GetComponentInChildren<MeshFilter>();Assert.That(filter.sharedMesh.name,Is.EqualTo("Bounded rain streaks"));
   yield return null;
   Assert.IsFalse(physics.Running);Assert.IsFalse(editor.Liquids.enabled);water.ClearRipples();
   int revision=editor.Revision,objectRevision=editor.ObjectRevision(id),contacts=0;
   string snapshot=JsonUtility.ToJson(editor.Snapshot());byte[] saved=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
   var native=editor.Snapshot().objects.Select(value=>(value.id,Item:editor.Find(value.id),Phase:editor.NativePhase(value.id))).ToArray();
   editor.MediumContact+=_=>contacts++;
   int submergedHeads=0,visibleSamples=0,maxRipples=0;float lowest=float.PositiveInfinity;
   var properties=new MaterialPropertyBlock();
   // Fixed sample time drives drop travel only. Ripple age intentionally remains
   // real unscaled time; no yields can expire the impact evidence in this loop.
   for(int step=0;step<64;step++){
    rain.Sample(step*.05f);maxRipples=Math.Max(maxRipples,water.RippleCount);
    var vertices=filter.sharedMesh.vertices;Assert.AreEqual(WorldRainView.MaximumDrops*4,vertices.Length);
    Assert.That(rain.VisibleDrops,Is.InRange(0,WorldRainView.MaximumDrops));
    for(int drop=0;drop<WorldRainView.MaximumDrops;drop++){
     int v=drop*4;if(vertices[v]==Vector3.zero&&vertices[v+1]==Vector3.zero&&vertices[v+2]==Vector3.zero&&vertices[v+3]==Vector3.zero)continue;
     var head=filter.transform.TransformPoint((vertices[v]+vertices[v+1])*.5f);
     // Ignore the tiny edge strip where a streak's width can extend beyond the
     // finite surface; every tested head is strictly inside the rendered water.
     if(head.x<=4.01f||head.x>=11.99f||head.z<=4.01f||head.z>=11.99f)continue;
     visibleSamples++;lowest=Mathf.Min(lowest,head.y);if(head.y<level-.0001f)submergedHeads++;
    }
    surfaceRenderer.GetPropertyBlock(properties);Assert.AreEqual(water.RippleCount,(int)properties.GetFloat("_LiquidRippleCount"));
    if(water.RippleCount>0){
     var ripples=properties.GetVectorArray("_LiquidRipples");Assert.That(ripples.Length,Is.GreaterThanOrEqualTo(water.RippleCount));
     for(int i=0;i<water.RippleCount;i++){
      Assert.That(ripples[i].y,Is.EqualTo(level).Within(.0001f));
      Assert.That(ripples[i].x,Is.InRange(3.9999f,12.0001f));Assert.That(ripples[i].z,Is.InRange(3.9999f,12.0001f));
     }
    }
   }
   Assert.That(visibleSamples,Is.GreaterThan(0),"The real rain mesh must exercise the wet footprint");
   Assert.That(rain.GetComponentsInChildren<Collider>(),Is.Empty,"Visual drops must not create physical colliders");
   Assert.IsFalse(physics.Running);Assert.IsFalse(editor.Liquids.enabled);Assert.IsFalse(editor.Liquids.Active);
   Assert.AreEqual(0,contacts);Assert.AreEqual(revision,editor.Revision);Assert.AreEqual(objectRevision,editor.ObjectRevision(id));
   Assert.AreEqual(500,editor.Read(id).containers[0].amountMl);Assert.AreEqual(snapshot,JsonUtility.ToJson(editor.Snapshot()));
   Assert.That(File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(saved));
   foreach(var before in native){Assert.That(editor.Find(before.id),Is.SameAs(before.Item));Assert.AreEqual(before.Phase,editor.NativePhase(before.id));}
   Assert.IsFalse(editor.NativeActivationPending);
   Assert.That(submergedHeads==0&&maxRipples>=1&&maxRipples<=ContainerFillView.MaximumRipples,Is.True,
    "Actual rain/water contact: submerged heads="+submergedHeads+", lowest world head="+lowest+", ripples="+maxRipples);
  }

  (string id,ContainerFillView water,MeshFilter surface) RainWaterReservoir(Vector3 position,float height=.5f,double amount=500){
   Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Rain boundary reservoir",position,4,Color.white,out var id,out var error),error);
   var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
   data.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="floor",position=new Vector3(0,-.01f,0),size=new Vector3(2,.02f,2)}}};
   data.containers=new[]{new RoomContainer{version=2,rectangle=new ContainerRectangle{width=2,depth=2},height=height,capacityMl=1000,amountMl=amount}};
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),error);
   var item=editor.Find(id);var water=item.GetComponent<ContainerFillView>();Assert.IsTrue(water.NativeReady);
   var surface=item.GetComponentsInChildren<MeshFilter>().Single(value=>value.sharedMesh?.name=="Bounded liquid level");
   Assert.That(surface.sharedMesh.vertexCount,Is.GreaterThanOrEqualTo(4));return(id,water,surface);
  }
  (WorldRainView rain,MeshFilter mesh) RainWaterRenderer(Vector3 observer,float windX=0,float windZ=0){
   physics.PausePhysics();editor.Liquids.enabled=false;
   foreach(var automatic in root.GetComponents<WorldRainView>())automatic.enabled=false;
   Assert.IsTrue(editor.SetWeather(editor.WeatherRevision,42,new(){rainMmPerHour=120,windX=windX,windZ=windZ},0,out var error),error);
   physics.SetSurfaces(true,"Aligned rain boundary fixture");leftAnchor.transform.position=observer;
   var go=new GameObject("Rain boundary renderer");go.transform.SetParent(root.transform,false);
   var rain=go.AddComponent<WorldRainView>();rain.Initialize(editor,leftAnchor.transform);rain.enabled=false;
   Physics.SyncTransforms();return(rain,rain.GetComponentInChildren<MeshFilter>());
  }
  static bool RainRenderedSurfaceContains(Vector3 point,Vector3[] vertices,int[] triangles,float margin=.00001f){
   // Project onto the actual horizontal rendered triangles, not the cavity's
   // enclosing bounds (terrain may have a dry shore inside those bounds).
   for(int i=0;i<triangles.Length;i+=3){
    var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
    float area=(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);if(Mathf.Abs(area)<.00000001f)continue;
    float u=((b.x-point.x)*(c.z-point.z)-(b.z-point.z)*(c.x-point.x))/area;
    float v=((c.x-point.x)*(a.z-point.z)-(c.z-point.z)*(a.x-point.x))/area;
    if(u>margin&&v>margin&&1-u-v>margin)return true;
   }
   return false;
  }
  (int visible,int below,int ripples) RainWaterSamples(WorldRainView rain,MeshFilter rainMesh,ContainerFillView water,MeshFilter surface,float start=0,int count=64){
   var wet=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray();var triangles=surface.sharedMesh.triangles;
   float level=wet.Average(value=>value.y);Assert.That(wet.Max(value=>value.y)-wet.Min(value=>value.y),Is.LessThan(.0001f));
   int visible=0,below=0,ripples=0;var properties=new MaterialPropertyBlock();var renderer=surface.GetComponent<MeshRenderer>();
   for(int step=0;step<count;step++){
    rain.Sample(start+step*.05f);ripples=Math.Max(ripples,water.RippleCount);var vertices=rainMesh.sharedMesh.vertices;
    Assert.That(vertices.Length,Is.EqualTo(WorldRainView.MaximumDrops*4));Assert.That(rain.VisibleDrops,Is.InRange(0,WorldRainView.MaximumDrops));
    for(int d=0;d<WorldRainView.MaximumDrops;d++){
     int v=d*4;if(vertices[v]==Vector3.zero&&vertices[v+1]==Vector3.zero&&vertices[v+2]==Vector3.zero&&vertices[v+3]==Vector3.zero)continue;
     var head=rainMesh.transform.TransformPoint((vertices[v]+vertices[v+1])*.5f);Assert.IsTrue(RoomRecipe.Finite(head));
     if(!RainRenderedSurfaceContains(head,wet,triangles))continue;visible++;if(head.y<level-.0001f)below++;
    }
    renderer.GetPropertyBlock(properties);Assert.AreEqual(water.RippleCount,(int)properties.GetFloat("_LiquidRippleCount"));
    if(water.RippleCount>0){
     var impacts=properties.GetVectorArray("_LiquidRipples");Assert.That(impacts.Length,Is.GreaterThanOrEqualTo(water.RippleCount));
     for(int i=0;i<water.RippleCount;i++)Assert.That(impacts[i].y,Is.EqualTo(level).Within(.0001f));
    }
   }
   return(visible,below,ripples);
  }
  [UnityTest]public IEnumerator RainWaterPresentationSolidLidBlocksTheEarlierImpact(){
   physics.PausePhysics();editor.Liquids.enabled=false;
   var basin=RainWaterReservoir(new Vector3(8,0,8));string roof=SavedWeatherRoof(new Vector3(8,2.5f,8),4);
   var presentation=RainWaterRenderer(new Vector3(8,1.5f,8));yield return null;basin.water.ClearRipples();
   var result=RainWaterSamples(presentation.rain,presentation.mesh,basin.water,basin.surface);
   Assert.That(result.visible,Is.GreaterThan(0),"Drops above the lid must remain visible");
   Assert.That(result.below,Is.Zero);Assert.That(result.ripples,Is.Zero,"A solid lid before the free surface cannot generate a water impact");
   Assert.That(editor.Find(roof).SolidColliders.Single().enabled,Is.True);Assert.AreEqual(500,editor.Read(basin.id).containers[0].amountMl);
   Assert.IsFalse(physics.Running);Assert.IsFalse(editor.Liquids.Active);
  }
  [UnityTest]public IEnumerator RainWaterPresentationMissingGeometryIsLocalAndRecoversWithoutActivatingObjects(){
   physics.PausePhysics();editor.Liquids.enabled=false;
   var basin=RainWaterReservoir(new Vector3(8,0,8));var distant=RainWaterReservoir(new Vector3(-12,0,-12));
   var presentation=RainWaterRenderer(new Vector3(8,1.5f,8));yield return null;
   basin.water.enabled=false;distant.water.enabled=false;basin.water.ClearRipples();distant.water.ClearRipples();
   int revision=editor.Revision;var native=editor.Find(basin.id);var farNative=editor.Find(distant.id);
   var unavailable=RainWaterSamples(presentation.rain,presentation.mesh,basin.water,basin.surface);
   Assert.That(unavailable.visible,Is.GreaterThan(0),"Missing finite water geometry must not globally hide unrelated sky rain");
   Assert.That(unavailable.below,Is.Zero,"A relevant missing body must not become dry space");Assert.That(unavailable.ripples,Is.Zero);
   basin.water.enabled=true;basin.water.Refresh();Assert.IsTrue(basin.water.NativeReady);
   var recovered=RainWaterSamples(presentation.rain,presentation.mesh,basin.water,basin.surface,4);
   Assert.That(recovered.visible,Is.GreaterThan(0));Assert.That(recovered.below,Is.Zero);Assert.That(recovered.ripples,Is.GreaterThan(0),"The distant unavailable body must not hide a ready local water impact");
   Assert.IsFalse(distant.water.enabled);Assert.AreEqual(0,distant.water.RippleCount);Assert.AreEqual(revision,editor.Revision);
   Assert.That(editor.Find(basin.id),Is.SameAs(native));Assert.That(editor.Find(distant.id),Is.SameAs(farNative));Assert.IsFalse(editor.NativeActivationPending);
   Assert.AreEqual(500,editor.Read(basin.id).containers[0].amountMl);Assert.AreEqual(500,editor.Read(distant.id).containers[0].amountMl);
  }
  [UnityTest]public IEnumerator RainWaterPresentationSubmergedSpawnAndPreviouslyAirborneDropsDoNotInventImpacts(){
   physics.PausePhysics();editor.Liquids.enabled=false;
   var basin=RainWaterReservoir(new Vector3(8,0,8),2,100);var presentation=RainWaterRenderer(new Vector3(8,1.5f,8));yield return null;
   for(int i=0;i<4;i++)presentation.rain.Sample(i*.05f);Assert.That(presentation.rain.VisibleDrops,Is.EqualTo(WorldRainView.MaximumDrops));
   var data=editor.Read(basin.id);data.containers[0].amountMl=900;
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),error);basin.water.ClearRipples();
   Assert.That(basin.surface.sharedMesh.vertices.Select(basin.surface.transform.TransformPoint).Average(value=>value.y),Is.EqualTo(7.2f).Within(.0001f));
   int revision=editor.Revision;var result=RainWaterSamples(presentation.rain,presentation.mesh,basin.water,basin.surface,.2f,8);
   // The bounded refresh visits 16 slots each tick: the first four remove the
   // old airborne drops; the next four exercise freshly submerged spawn points.
   Assert.AreEqual(0,presentation.rain.VisibleDrops);Assert.AreEqual(0,result.ripples,"A changed surface or an underwater spawn is not an observed entry");
   Assert.That(presentation.mesh.sharedMesh.vertices.All(point=>point==Vector3.zero),Is.True);
   Assert.AreEqual(revision,editor.Revision);Assert.AreEqual(900,editor.Read(basin.id).containers[0].amountMl);Assert.IsFalse(physics.Running);
  }
  [UnityTest]public IEnumerator RainWaterPresentationUsesWorldSurfaceUnderTiltScaleAndWind(){
   physics.PausePhysics();editor.Liquids.enabled=false;
   var basin=RainWaterReservoir(new Vector3(8,0,8),1,500);
   root.transform.SetPositionAndRotation(new Vector3(3,2,-4),Quaternion.Euler(9,27,-7));root.transform.localScale=Vector3.one*1.25f;
   basin.water.Refresh();var wet=basin.surface.sharedMesh.vertices.Select(basin.surface.transform.TransformPoint).ToArray();
   float level=wet.Average(point=>point.y);Assert.That(wet.Max(point=>point.y)-wet.Min(point=>point.y),Is.LessThan(.0001f));
   var observer=wet.Aggregate(Vector3.zero,(sum,value)=>sum+value)/wet.Length+Vector3.up*.5f;
   var presentation=RainWaterRenderer(observer,.35f,-.2f);yield return null;basin.water.ClearRipples();
   var result=RainWaterSamples(presentation.rain,presentation.mesh,basin.water,basin.surface,count:96);
   Assert.That(result.visible,Is.GreaterThan(0));Assert.That(result.below,Is.Zero,"Tilted room coordinates must not turn the horizontal water plane into dry space");
   Assert.That(result.ripples,Is.GreaterThan(0));Assert.That(level,Is.Not.EqualTo(2));Assert.AreEqual(500,editor.Read(basin.id).containers[0].amountMl);
   Assert.IsFalse(physics.Running);Assert.IsFalse(editor.Liquids.Active);
  }

  [UnityTest]public IEnumerator RainWaterPresentationDoesNotSpawnDownstreamOfSuspendedWaterWithoutSolids(){
   physics.PausePhysics();editor.Liquids.enabled=false;
   var basin=RainWaterReservoir(new Vector3(8,8,8));var item=editor.Find(basin.id);
   // Reuse the accepted complete-empty native fixture: triggers cannot supply
   // solid cover, and an explicitly empty registration is ready, not missing.
   foreach(var collider in item.Grab.colliders)collider.isTrigger=true;
   item.Grab.enabled=false;item.Grab.colliders.Clear();
   Assert.That(item.SolidColliders,Is.Empty);Assert.IsTrue(editor.AuthoredCollisionReady(item));Assert.IsTrue(basin.water.NativeReady);
   var presentation=RainWaterRenderer(new Vector3(8,1.5f,8));yield return null;basin.water.ClearRipples();
   float surfaceY=basin.surface.sharedMesh.vertices.Select(basin.surface.transform.TransformPoint).Average(point=>point.y);
   Assert.That(surfaceY,Is.EqualTo(9).Within(.0001f));int revision=editor.Revision;
   for(int i=0;i<8;i++)presentation.rain.Sample(i*.05f);
   Assert.That(presentation.rain.VisibleDrops,Is.Zero,"All spawn points lie below the suspended free surface, even though no solid collider can act as a roof");
   Assert.That(presentation.mesh.sharedMesh.vertices.All(point=>point==Vector3.zero),Is.True);
   Assert.That(basin.water.RippleCount,Is.Zero,"Testing upstream cover cannot invent a visible free-surface entry");
   Assert.AreEqual(revision,editor.Revision);Assert.AreEqual(500,editor.Read(basin.id).containers[0].amountMl);Assert.IsFalse(physics.Running);
  }
  [UnityTest]public IEnumerator RainWaterPresentationTerrainRipplesOnlyOnRenderedWetShore(){
   physics.PausePhysics();editor.Liquids.enabled=false;
   var(_,id)=TerrainReservoir(true);var data=editor.Read(id);data.scale=4;data.position=new Vector3(8,0,8);
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),error);
   var item=editor.Find(id);var water=item.GetComponent<ContainerFillView>();water.Refresh();Assert.IsTrue(water.NativeReady);
   var surface=item.GetComponentsInChildren<MeshFilter>().Single(value=>value.sharedMesh?.name=="Bounded liquid level");
   var wet=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray();var triangles=surface.sharedMesh.triangles;
   float level=wet.Average(point=>point.y);Assert.That(level,Is.EqualTo(1.6f).Within(.0001f));Assert.That(wet.Max(point=>point.x),Is.LessThanOrEqualTo(8.0001f));
   var presentation=RainWaterRenderer(new Vector3(8,level+.5f,8));yield return null;water.ClearRipples();
   int revision=editor.Revision,wetHeads=0,dryHeads=0,maxRipples=0;string snapshot=JsonUtility.ToJson(editor.Read(id));
   var bed=editor.Read(id).heightFields[0];var inverse=(item.transform.localToWorldMatrix*Matrix4x4.TRS(bed.frame.position,bed.frame.rotation,Vector3.one)).inverse;
   var properties=new MaterialPropertyBlock();var renderer=surface.GetComponent<MeshRenderer>();
   for(int step=0;step<96;step++){
    var result=RainWaterSamples(presentation.rain,presentation.mesh,water,surface,step*.05f,1);
    wetHeads+=result.visible;maxRipples=Math.Max(maxRipples,result.ripples);Assert.AreEqual(0,result.below,"Wet terrain must stop actual streaks at its free surface");
    var vertices=presentation.mesh.sharedMesh.vertices;
    for(int d=0;d<WorldRainView.MaximumDrops;d++){
     int v=d*4;if(vertices[v]==Vector3.zero&&vertices[v+1]==Vector3.zero)continue;
     var head=presentation.mesh.transform.TransformPoint((vertices[v]+vertices[v+1])*.5f);
     if(head.x>8.1f&&head.x<11.9f&&head.z>6.1f&&head.z<9.9f)dryHeads++;
    }
    renderer.GetPropertyBlock(properties);
    if(water.RippleCount>0){
     var impacts=properties.GetVectorArray("_LiquidRipples");
     for(int i=0;i<water.RippleCount;i++){
      Vector3 point=impacts[i];Assert.IsTrue(RainRenderedSurfaceContains(point,wet,triangles,-.0001f),"A dry triangle must not receive a water ripple");
      var local=inverse.MultiplyPoint3x4(point);Assert.That(local.y+.0001f,Is.GreaterThanOrEqualTo(bed.HeightAt(new Vector2(local.x,local.z))));
     }
    }
   }
   Assert.That(wetHeads,Is.GreaterThan(0));Assert.That(dryHeads,Is.GreaterThan(0),"The same rendered rain must exercise both sides of the shore");Assert.That(maxRipples,Is.GreaterThan(0));
   Assert.AreEqual(revision,editor.Revision);Assert.AreEqual(snapshot,JsonUtility.ToJson(editor.Read(id)));Assert.AreEqual(100000,editor.Read(id).containers[0].amountMl);
   Assert.IsFalse(physics.Running);Assert.IsFalse(editor.Liquids.Active);Assert.IsFalse(editor.NativeActivationPending);
  }
 }
}

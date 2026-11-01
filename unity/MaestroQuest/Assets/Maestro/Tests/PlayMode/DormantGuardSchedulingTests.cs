// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string[] GuardGroup(int count,out string area){
            area=NewRegion();var ids=new List<string>();
            for(int i=0;i<count;i++){
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Quiet unloaded guard",new(5+i*.75f,1,5),.2f,Color.white,out var id,out var error),Is.True,error);
                Assert.That(AssignRegion(id,area,out error),Is.True,error);ids.Add(id);
            }
            return ids.ToArray();
        }
        [UnityTest] public IEnumerator DormantGuardSchedulingQuietTicksKeepPhysicalBarriersWithoutReadingEveryObject(){
            var ids=GuardGroup(18,out var area);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var guards=ids.Select(editor.DormantCollisionGuard).ToArray();
            Assert.That(guards.All(g=>g.Known&&g.HasBounds&&g.Shape&&g.Shape.enabled),Is.True,"Barriers must exist before any retirement frame ends");
            yield return null;
            long before=guards.Sum(g=>g.Synchronizations);int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            for(int i=0;i<60;i++)yield return null;
            Assert.That(guards.Sum(g=>g.Synchronizations),Is.EqualTo(before),"Quiet native ticks must not revisit unanchored guards");
            Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);
            editor.RefreshAnchoredDormantCollisions();
            long allocations=ManagedAllocations.Measure(()=>{for(int i=0;i<512;i++)editor.RefreshAnchoredDormantCollisions();});
            Assert.That(allocations,Is.Zero,"An empty anchor update must allocate nothing regardless of the dormant population");
            root.transform.SetPositionAndRotation(new(2,1,-3),Quaternion.Euler(0,37,0));root.transform.localScale=Vector3.one*1.25f;
            Physics.SyncTransforms();yield return null;
            var guard=guards[8];var expected=editor.ReadCollisionBounds(ids[8]).Transform(root.transform.localToWorldMatrix).Bounds;
            Assert.That(guard.Shape.bounds.Contains(expected.min)&&guard.Shape.bounds.Contains(expected.max),Is.True);
            var bounds=guard.Shape.bounds;var ray=new Ray(bounds.center-Vector3.up*(bounds.extents.y+1),Vector3.up);
            Assert.That(Physics.Raycast(ray,out var hit,2+bounds.size.y,1<<RoomPhysicsLayers.Pending,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.collider,Is.SameAs(guard.Shape));Assert.That(guards.Sum(g=>g.Synchronizations),Is.EqualTo(before));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            string folder=Environment.GetEnvironmentVariable("MAESTRO_GUARD_SCHEDULING_EVIDENCE");
            if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"quiet-guards.json"),new JObject{
                ["dormantGuards"]=ids.Length,["frames"]=60,["synchronizationsDuringFrames"]=guards.Sum(g=>g.Synchronizations)-before,
                ["directTicks"]=512,["allocationEvents"]=allocations,["worldMoved"]=true,["rayHitSameGuard"]=hit.collider==guard.Shape,
                ["nativePending"]=editor.NativeActivationPending,["boundary"]="Editor logical work and current-thread allocation events for guard refresh only; other room consumers and Quest frame time are not measured."}.ToString());}
        }
        [UnityTest] public IEnumerator DormantGuardSchedulingAcceptedEditAndUndoOnlyRefreshTheChangedGuard(){
            var ids=GuardGroup(3,out var area);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var guards=ids.Select(editor.DormantCollisionGuard).ToArray();var counts=guards.Select(g=>g.Synchronizations).ToArray();
            var before=guards[1].Shape.center;var data=editor.Read(ids[1]);data.position+=Vector3.right*2;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(guards[1].Shape.center,Is.EqualTo(before+Vector3.right*2));Assert.That(guards[1].Synchronizations,Is.EqualTo(counts[1]+1));
            Assert.That(guards[0].Synchronizations,Is.EqualTo(counts[0]));Assert.That(guards[2].Synchronizations,Is.EqualTo(counts[2]));
            editor.Undo();Assert.That(guards[1].Shape.center,Is.EqualTo(before));Assert.That(guards[1].Synchronizations,Is.EqualTo(counts[1]+2));
            var native=editor.Find("maestro");native.transform.localPosition+=Vector3.forward;editor.RememberPlacement("maestro");
            for(int i=0;i<10;i++)yield return null;
            Assert.That(guards[0].Synchronizations,Is.EqualTo(counts[0]));Assert.That(guards[2].Synchronizations,Is.EqualTo(counts[2]));
            Assert.That(guards[1].Synchronizations,Is.EqualTo(counts[1]+2));
            Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);
        }
        [UnityTest] public IEnumerator DormantGuardSchedulingRetirementAndActivationDoNotRefreshUnrelatedGuards(){
            var ids=GuardGroup(1,out var area);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var first=editor.DormantCollisionGuard(ids[0]);long count=first.Synchronizations;
            string next=editor.Identity(block),nextArea=NativeAreaFor(next);Assert.That(editor.RetireNativeArea(nextArea,out error),Is.True,error);
            var second=editor.DormantCollisionGuard(next);Assert.That(second.Known&&second.Shape&&second.Shape.enabled,Is.True);Assert.That(first.Synchronizations,Is.EqualTo(count));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(nextArea));Assert.That(second.Shape,Is.Null);long ended=second.Synchronizations;
            for(int i=0;i<10;i++)yield return null;
            Assert.That(second.Synchronizations,Is.EqualTo(ended));Assert.That(first.Synchronizations,Is.EqualTo(count));
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),ids,out error),Is.True,error);Assert.That(first.Shape,Is.Null);
            editor.RefreshAnchoredDormantCollisions();Assert.That(editor.DormantCollisionGuard(ids[0]),Is.Null);
        }
    }
    public sealed partial class AvatarSpatialTests {
        [UnityTest] public IEnumerator DormantGuardSchedulingAnchorsStayFreshThroughLossResumeDeletionAndRecreation(){
            var scan=SharedEnvironment(out var platform);var wall=InkWall();var source=new LayoutSource{Entries=new[]{wall}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);yield return InkAdd(id);yield return ScanBoundsPrepared(id);string area=ScanBoundsArea(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var anchored=editor.DormantCollisionGuard(id);Assert.That(anchored.Known&&!anchored.HasBounds,Is.True);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Stable neighbor",new(8,1,8),.2f,Color.white,out var coldId,out error),Is.True,error);
            string coldArea=ScanBoundsArea(coldId);Assert.That(editor.RetireNativeArea(coldArea,out error),Is.True,error);yield return null;
            var cold=editor.DormantCollisionGuard(coldId);long stable=cold.Synchronizations,initial=anchored.Synchronizations;
            source.Entries=Array.Empty<ScannedSurface>();yield return null;yield return null;
            Assert.That(anchored.Known,Is.False,"Lost exact anchors must not retain known-empty collision");
            source.Entries=new[]{wall};yield return null;yield return null;Assert.That(anchored.Known&&!anchored.HasBounds,Is.True);
            editor.enabled=false;source.Entries=Array.Empty<ScannedSurface>();yield return null;
            editor.enabled=true;yield return null;yield return null;Assert.That(anchored.Known,Is.False);
            source.Entries=new[]{wall};yield return null;yield return null;Assert.That(anchored.Known,Is.True);
            Assert.That(anchored.Synchronizations,Is.GreaterThan(initial));Assert.That(cold.Synchronizations,Is.EqualTo(stable));
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);long removed=anchored.Synchronizations;
            for(int i=0;i<5;i++)yield return null;Assert.That(anchored.Synchronizations,Is.EqualTo(removed));Assert.That(editor.DormantCollisionGuard(id),Is.Null);
            // This checks anchor invalidation, independently of the viewer's display
            // grace period which can retain the newly restored unknown envelope.
            var camera=viewer.GetComponent<Camera>();Assert.That(camera,Is.Not.Null);camera.enabled=false;
            editor.Undo();Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);
            var restored=editor.AcquireNativeEntities(new[]{id},()=>true,System.Threading.CancellationToken.None);
            yield return Until(()=>restored.IsCompleted);Assert.That(restored.Exception,Is.Null);Assert.That(restored.Result,Is.Null);yield return null;
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error+"; "+editor.ObserveRegionRetention(area));yield return null;
            var replacement=editor.DormantCollisionGuard(id);Assert.That(replacement,Is.Not.SameAs(anchored));Assert.That(replacement.Known&&!replacement.HasBounds,Is.True);
            source.Entries=Array.Empty<ScannedSurface>();yield return null;yield return null;Assert.That(replacement.Known,Is.False);
            Assert.That(cold.Synchronizations,Is.EqualTo(stable));Assert.That(editor.Find(coldId),Is.Null);
        }
    }
}

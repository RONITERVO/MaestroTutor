// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        static BoxCollider BookRoleCover(RoomItem book) {
            var cover=book.GetComponent<BoxCollider>();Assert.That(cover,Is.Not.Null);
            book.gameObject.layer=RoomPhysicsLayers.Environment;
            cover.center=new Vector3(0,0,.024f);cover.size=new Vector3(.648f,.457f,.012f);
            return cover;
        }
        IEnumerator BuildBookRoleFixture(RoomItem book) {
            BookRoleCover(book);
            book.transform.SetPositionAndRotation(new Vector3(8,2,8),Quaternion.Euler(90,0,0));
            var illustrated=book.gameObject.AddComponent<IllustratedBook>();illustrated.Build();
            book.gameObject.AddComponent<PhysicalBookControls>().Build(null,illustrated);
            // Production controls discard temporary primitive colliders at the
            // end of the frame. Register only the surviving real pointer shapes.
            yield return null;
            book.SendMessage("Start");Physics.SyncTransforms();
            var pages=book.GetComponentsInChildren<BookPageTarget>(true);Assert.That(pages.Length,Is.EqualTo(2));
            foreach(var page in pages)Assert.That(book.Grab.colliders,Does.Contain(page.GetComponent<Collider>()));
        }
        void AssertBookRoleBounds(RoomItem book,BoxCollider cover) {
            var bounds=editor.ReadCollisionBounds("book");Assert.That(bounds.Known&&bounds.HasBounds,Is.True);
            Assert.That(Vector3.Distance(bounds.Bounds.min,cover.bounds.min),Is.LessThan(.0001f),"Pointer registrations must not enlarge the accepted solid envelope");
            Assert.That(Vector3.Distance(bounds.Bounds.max,cover.bounds.max),Is.LessThan(.0001f));
        }
        static Ray BookRoleRay(Collider target,out float length) {
            length=1;return new Ray(target.bounds.center-Vector3.up*.5f,Vector3.up);
        }
        [UnityTest]public IEnumerator BookCollisionRolesHiddenAndVisiblePointersPreserveSolidReadiness() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);
            var notice=book.GetComponentInChildren<BrowserNotice>(true);var illustrated=book.GetComponent<IllustratedBook>();
            Assert.That(notice.gameObject.activeSelf,Is.False);Assert.That(illustrated.BookmarkRibbon.gameObject.activeSelf,Is.False);
            Assert.That(book.Grab.colliders,Does.Contain(notice.GetComponent<Collider>()),"Hidden notice targets need explicit pointer ownership before becoming visible");
            Assert.That(book.Grab.colliders,Does.Contain(illustrated.BookmarkRibbon.GetComponent<Collider>()));
            int revision=editor.Revision;int registered=book.Grab.colliders.Count;
            CollisionAvailabilityIs("book","ready");
            notice.gameObject.SetActive(true);illustrated.SetBookmark(true,PageSide.Right);Physics.SyncTransforms();
            CollisionAvailabilityIs("book","ready");
            notice.gameObject.SetActive(false);illustrated.SetBookmark(false,PageSide.Left);CollisionAvailabilityIs("book","ready");
            Assert.That(book.Grab.colliders.Count,Is.EqualTo(registered));Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest]public IEnumerator BookCollisionRolesPointerTargetsDoNotExpandSolidBounds() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);var cover=BookRoleCover(book);
            AssertBookRoleBounds(book,cover);
            book.GetComponentInChildren<BrowserNotice>(true).gameObject.SetActive(true);
            book.GetComponent<IllustratedBook>().SetBookmark(true,PageSide.Right);Physics.SyncTransforms();
            AssertBookRoleBounds(book,cover);
        }
        [UnityTest]public IEnumerator BookCollisionRolesCurrentSiblingCoverSuppliesExactRainProof() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);var cover=BookRoleCover(book);
            // WorkspaceHost keeps the shared book beside the selected workspace,
            // so current identity ownership cannot depend on transform ancestry.
            book.transform.SetParent(null,true);
            try {
                Assert.That(book.transform.IsChildOf(editor.transform),Is.False);Assert.That(editor.Identity(book),Is.EqualTo("book"));
                Physics.SyncTransforms();var ray=BookRoleRay(cover,out float length);
                Assert.That(cover.Raycast(ray,out _,length),Is.True,"The real configured cover must intersect the query");
                Assert.That(new WeatherCover().Segment(editor,ray.origin,ray.GetPoint(length),false),Is.EqualTo(RainExposure.Covered));
            }finally{book.transform.SetParent(root.transform,true);}
        }
        [UnityTest]public IEnumerator BookCollisionRolesPointerExtensionIsClickableWithoutCoveringRain() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);var cover=BookRoleCover(book);
            var bell=book.GetComponentsInChildren<PhysicalBookAction>(true).Single(x=>x.Action==BookActionKind.ResumeAudio).GetComponent<Collider>();
            var ray=BookRoleRay(bell,out float length);
            Assert.That(RoomPointerHit.Raycast(ray,out var hit,length,RoomPhysicsLayers.InteractionMask,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.collider,Is.EqualTo(bell),"The control remains a real clickable pointer target");
            Assert.That(book.Grab.colliders,Does.Contain(bell));Assert.That(cover.Raycast(ray,out _,length),Is.False);
            var weather=new WeatherCover();
            Assert.That(weather.Segment(editor,ray.origin,ray.GetPoint(length),false),Is.EqualTo(RainExposure.Open));
            int layer=bell.gameObject.layer;
            try {
                physics.SetSurfaces(true,"Aligned test room");bell.gameObject.layer=RoomPhysicsLayers.Scanned;Physics.SyncTransforms();
                Assert.That(bell.Raycast(ray,out _,length),Is.True);Assert.That(cover.Raycast(ray,out _,length),Is.False);
                Assert.That(weather.Segment(editor,ray.origin,ray.GetPoint(length),true),Is.EqualTo(RainExposure.Open),"Explicit pointer role takes precedence over a scanned-layer shortcut");
                Assert.That(book.UnregisterPointerSurface(bell),Is.True);Assert.That(RoomItem.IsPointerOnly(bell),Is.False);
                Assert.That(weather.Segment(editor,ray.origin,ray.GetPoint(length),true),Is.EqualTo(RainExposure.Covered),"The ordinary scanned solid still supplies exact cover after its pointer role is removed");
            }finally{
                bell.gameObject.layer=layer;if(!book.Grab.colliders.Contains(bell))book.RegisterPointerSurface(bell);Physics.SyncTransforms();
            }
        }
        [UnityTest]public IEnumerator BookCollisionRolesForeignWorkspaceCannotSupplyRainProof() {
            var foreignRoot=new GameObject("Foreign workspace");foreignRoot.transform.SetParent(root.transform,false);
            try {
                var interaction=foreignRoot.AddComponent<RoomInteraction>();var foreignPhysics=foreignRoot.AddComponent<RoomPhysicsWorld>();
                RoomItem Included(string name) {
                    var value=new GameObject(name);value.transform.SetParent(foreignRoot.transform,false);
                    var collider=value.AddComponent<BoxCollider>();collider.size=Vector3.one*.1f;
                    var item=value.AddComponent<RoomItem>();item.Configure(new Collider[]{collider});interaction.Register(item);return item;
                }
                var foreignBook=Included("book");var foreignAvatar=Included("maestro");
                var foreign=foreignRoot.AddComponent<RoomEditor>();foreign.Initialize(interaction,foreignBook,foreignAvatar,Path.Combine(directory,"foreign"),foreignPhysics);
                var cover=BookRoleCover(foreignBook);foreignBook.transform.SetPositionAndRotation(new Vector3(8,2,8),Quaternion.Euler(90,0,0));
                Assert.That(editor.Identity(foreignBook),Is.Null);Assert.That(foreign.Identity(foreignBook),Is.EqualTo("book"));
                Assert.That((string)foreign.ObserveCollisionAvailability("book")["state"],Is.EqualTo("ready"));
                // A nested foreign editor is not current-world geometry either.
                Physics.SyncTransforms();var ray=BookRoleRay(cover,out float length);
                Assert.That(cover.Raycast(ray,out _,length),Is.True);
                Assert.That(new WeatherCover().Segment(editor,ray.origin,ray.GetPoint(length),false),Is.EqualTo(RainExposure.Open));
                foreignRoot.transform.SetParent(null,true);Physics.SyncTransforms();
                Assert.That(new WeatherCover().Segment(editor,ray.origin,ray.GetPoint(length),false),Is.EqualTo(RainExposure.Open));
                yield return null;
            }finally{if(foreignRoot)foreignRoot.transform.SetParent(root.transform,true);}
        }
        [UnityTest]public IEnumerator BookCollisionRolesConfiguredDisabledOrWrongLayerSolidStaysUnavailable() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);var cover=BookRoleCover(book);
            // A configured physical handle may also have an action. That marker
            // must never silently reclassify its missing solid as pointer-only.
            book.gameObject.AddComponent<PhysicalBookAction>();book.SendMessage("Start");
            cover.enabled=false;CollisionAvailabilityIs("book","unavailable");cover.enabled=true;
            book.gameObject.layer=0;CollisionAvailabilityIs("book","unavailable");book.gameObject.layer=RoomPhysicsLayers.Environment;
            CollisionAvailabilityIs("book","ready");
        }
        static void ReplaceBookRoleSolids(RoomItem item,params Collider[] handles) {
            Assert.That(item.Grab.isSelected,Is.False);
            bool enabled=item.Grab.enabled;item.Grab.enabled=false;
            try{item.ReplaceSolidColliders(handles);}finally{item.Grab.enabled=enabled;}
        }
        [UnityTest]public IEnumerator BookCollisionRolesDestroyedPointerDoesNotHideNullOrDestroyedSolid() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);var cover=BookRoleCover(book);
            var pointer=book.GetComponentsInChildren<PhysicalBookAction>(true).Single(x=>x.Action==BookActionKind.ResumeAudio).GetComponent<Collider>();
            Assert.That(RoomItem.IsPointerOnly(pointer),Is.True);UnityEngine.Object.Destroy(pointer);yield return null;
            int registrations=book.Grab.colliders.Count;int revision=editor.Revision;
            for(int i=0;i<3;i++){
                CollisionAvailabilityIs("book","ready");Assert.That(book.SolidColliders.ToArray(),Is.EqualTo(new Collider[]{cover}));
            }
            Assert.That(book.Grab.colliders.Count,Is.EqualTo(registrations),"Passive reads must not prune destroyed pointer bookkeeping");
            Assert.That(editor.Revision,Is.EqualTo(revision));
            book.Grab.colliders.Add(null);CollisionAvailabilityIs("book","unavailable");
            Assert.That(book.SolidColliderCount,Is.EqualTo(2));book.Grab.colliders.RemoveAt(book.Grab.colliders.Count-1);
            CollisionAvailabilityIs("book","ready");UnityEngine.Object.Destroy(cover);yield return null;
            Assert.That(book.SolidColliderCount,Is.EqualTo(1),"A destroyed solid is missing geometry, not an accepted empty set");
            CollisionAvailabilityIs("book","unavailable");
        }
        [UnityTest]public IEnumerator BookCollisionRolesNestedAndReparentedSurfacesRequireTheirCurrentOwner() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);
            var child=new GameObject("Nested movable control");child.transform.SetParent(book.transform,false);child.layer=RoomPhysicsLayers.Environment;
            var solid=child.AddComponent<BoxCollider>();solid.size=Vector3.one*.02f;
            var nested=child.AddComponent<RoomItem>();nested.Configure(new Collider[]{solid});child.AddComponent<PhysicalBookAction>();
            var target=new GameObject("Nested pointer");target.transform.SetParent(child.transform,false);var pointer=target.AddComponent<BoxCollider>();pointer.size=Vector3.one*.01f;
            Assert.That(book.RegisterPointerSurface(pointer),Is.False);Assert.That(nested.RegisterPointerSurface(solid),Is.False);
            book.SendMessage("Start");Assert.That(book.Grab.colliders.Contains(solid),Is.False);Assert.That(book.Grab.colliders.Contains(pointer),Is.False);
            Assert.That(nested.RegisterPointerSurface(pointer),Is.True);Assert.That(RoomItem.IsPointerOnly(pointer),Is.True);
            Assert.That(nested.SolidColliders.ToArray(),Is.EqualTo(new Collider[]{solid}));CollisionAvailabilityIs("book","ready");
            Assert.That(nested.UnregisterPointerSurface(pointer),Is.True);Assert.That(RoomItem.IsPointerOnly(pointer),Is.False);
            Assert.That(nested.Grab.colliders.Contains(pointer),Is.False);Assert.That(nested.UnregisterPointerSurface(solid),Is.False);
            var bell=book.GetComponentsInChildren<PhysicalBookAction>(true).Single(x=>x.Action==BookActionKind.ResumeAudio).GetComponent<Collider>();
            bell.transform.SetParent(child.transform,true);
            Assert.That(RoomItem.IsPointerOnly(bell),Is.False,"The old owner cannot exempt a collider in a new owner");
            Assert.That(book.IsPointerSurface(bell),Is.False);Assert.That(book.SolidColliders,Does.Contain(bell));
            CollisionAvailabilityIs("book","unavailable");
            Assert.That(book.UnregisterPointerSurface(bell),Is.True);CollisionAvailabilityIs("book","ready");
            Assert.That(nested.RegisterPointerSurface(bell),Is.True);Assert.That(RoomItem.IsPointerOnly(bell),Is.True);
            Assert.That(editor.AuthoredCollisionReady(nested),Is.True);Assert.That(nested.SolidColliderCount,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator BookCollisionRolesReplacementRetainsPointersAndExplicitlyPromotesNewSolids() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);var cover=BookRoleCover(book);
            var actions=book.GetComponentsInChildren<PhysicalBookAction>(true);
            var expired=actions.Single(x=>x.Action==BookActionKind.Earlier).GetComponent<Collider>();
            var promoted=actions.Single(x=>x.Action==BookActionKind.ResumeAudio).GetComponent<Collider>();
            var hidden=book.GetComponentInChildren<BrowserNotice>(true).GetComponent<Collider>();
            UnityEngine.Object.Destroy(expired);yield return null;
            var retained=book.Grab.colliders.Where(c=>c&&c!=cover).ToArray();
            ReplaceBookRoleSolids(book,cover);
            Assert.That(book.Grab.colliders.All(c=>c),Is.True,"An explicit replacement prunes destroyed pointer handles");
            Assert.That(book.Grab.colliders.Count,Is.EqualTo(retained.Length+1));
            foreach(var pointer in retained){Assert.That(book.Grab.colliders,Does.Contain(pointer));Assert.That(RoomItem.IsPointerOnly(pointer),Is.True);}
            Assert.That(hidden.gameObject.activeSelf,Is.False);CollisionAvailabilityIs("book","ready");
            ReplaceBookRoleSolids(book,cover,promoted);
            Assert.That(book.SolidColliders.ToArray(),Is.EqualTo(new Collider[]{cover,promoted}));Assert.That(book.SolidColliderCount,Is.EqualTo(2));
            Assert.That(RoomItem.IsPointerOnly(promoted),Is.False);Assert.That(book.RegisterPointerSurface(promoted),Is.False);
            Assert.That(book.UnregisterPointerSurface(promoted),Is.False,"Promoted solids cannot be removed through the pointer API");
            CollisionAvailabilityIs("book","unavailable");promoted.gameObject.layer=RoomPhysicsLayers.Environment;
            CollisionAvailabilityIs("book","ready");Assert.That(RoomItem.IsPointerOnly(hidden),Is.True);
        }
        [UnityTest]public IEnumerator BookCollisionRolesBothLayoutTokensRemainClickableOutsideSolidCover() {
            var book=editor.Find("book");yield return BuildBookRoleFixture(book);var cover=BookRoleCover(book);
            var actions=book.GetComponentsInChildren<PhysicalBookAction>(true);
            foreach(var kind in new[]{BookActionKind.ConversationLayout,BookActionKind.PracticeLayout}){
                var token=actions.Single(x=>x.Action==kind).GetComponent<Collider>();var ray=BookRoleRay(token,out float length);
                Assert.That(RoomPointerHit.Raycast(ray,out var hit,length,RoomPhysicsLayers.InteractionMask,QueryTriggerInteraction.Ignore),Is.True);
                Assert.That(hit.collider,Is.EqualTo(token));Assert.That(RoomItem.IsPointerOnly(token),Is.True);
                Assert.That(cover.Raycast(ray,out _,length),Is.False);
                Assert.That(new WeatherCover().Segment(editor,ray.origin,ray.GetPoint(length),false),Is.EqualTo(RainExposure.Open));
            }
            AssertBookRoleBounds(book,cover);CollisionAvailabilityIs("book","ready");
        }
    }
}

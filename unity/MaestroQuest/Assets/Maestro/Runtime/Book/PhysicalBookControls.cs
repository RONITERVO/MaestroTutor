// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Solid illustrated keepsakes beside the book; no Canvas UI.</summary>
    public sealed class PhysicalBookControls : MonoBehaviour
    {
        readonly List<Material> owned = new();
        NativeBookBrowser source;
        Material bellPaint;
        TextMesh bellLabel;
        GameObject notice;
        TextMesh noticeLabel;
        string shownNotice;

        public void Build(NativeBookBrowser browser, IllustratedBook book)
        {
            var wood = Material(IllustratedMaterials.Hex("C89D65"));
            var paper = Material(IllustratedMaterials.Paper);
            var ink = Material(IllustratedMaterials.Ink);
            var teal = Material(IllustratedMaterials.Hex("2B8D88"));
            var gold = Material(IllustratedMaterials.Ribbon);
            source = browser;
            var bell = new GameObject("Resume audio bell"); bell.transform.SetParent(transform,false);
            bell.transform.localPosition = new Vector3(-.41f,.12f,0);
            bellPaint = Material(IllustratedMaterials.Ribbon);
            Part(bell.transform, PrimitiveType.Sphere, Vector3.zero, new Vector3(.055f,.06f,.04f),bellPaint);
            Part(bell.transform, PrimitiveType.Cube, new Vector3(0,-.026f,0),new Vector3(.07f,.012f,.045f),bellPaint);
            Part(bell.transform, PrimitiveType.Sphere, new Vector3(0,-.04f,0),Vector3.one * .012f,ink);
            var bellHit = bell.AddComponent<BoxCollider>(); bellHit.size = new Vector3(.09f,.1f,.065f);
            var bellAction = bell.AddComponent<PhysicalBookAction>(); bellAction.Action = BookActionKind.ResumeAudio;
            bellAction.AccessibleName = "Resume audio"; bellAction.Browser = browser;
            var label = new GameObject("Audio state"); label.transform.SetParent(bell.transform,false);
            label.transform.localPosition = new Vector3(0,-.063f,-.03f);
            bellLabel = label.AddComponent<TextMesh>(); bellLabel.fontSize = 48; bellLabel.characterSize = .004f;
            bellLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = bellLabel.font.material;
            bellLabel.anchor = TextAnchor.MiddleCenter; bellLabel.color = IllustratedMaterials.Ink; bellLabel.text = "Resume audio";
            notice = new GameObject("Browser notice token"); notice.transform.SetParent(transform,false);
            notice.transform.localPosition = new Vector3(-.53f,-.07f,0);
            Part(notice.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.245f,.19f,.016f),wood);
            var noticeHit = notice.AddComponent<BoxCollider>(); noticeHit.size = new Vector3(.245f,.19f,.025f);
            var dismiss = notice.AddComponent<BrowserNotice>(); dismiss.Browser = browser; dismiss.AccessibleName = "Dismiss browser notice";
            var noticeMarking = new GameObject("Browser notice text",typeof(TextMesh)); noticeMarking.transform.SetParent(notice.transform,false); noticeMarking.transform.localPosition = new Vector3(0,0,-.012f);
            noticeLabel = noticeMarking.GetComponent<TextMesh>(); noticeLabel.font = bellLabel.font; noticeLabel.fontSize = 48; noticeLabel.characterSize = .0027f;
            noticeLabel.anchor = TextAnchor.MiddleCenter; noticeLabel.alignment = TextAlignment.Center; noticeLabel.color = IllustratedMaterials.Ink;
            noticeMarking.GetComponent<MeshRenderer>().sharedMaterial = noticeLabel.font.material; notice.SetActive(false);
            // Both miniature books are fully modelled tokens on a wooden holder.
            for (int index = 0; index < 2; index++)
            {
                var root = new GameObject(index == 0 ? "Conversation book token" : "Practice book token");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(.40f, .12f - .11f * index, 0);
                Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0, .016f), new Vector3(.087f, .077f, .02f), wood);
                Part(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(.075f, .065f, .009f), teal);
                Part(root.transform, PrimitiveType.Cube, new Vector3(-.018f, .002f, -.006f), new Vector3(.032f, .057f, .004f), paper);
                Part(root.transform, PrimitiveType.Cube, new Vector3(.018f, .002f, -.006f), new Vector3(.032f, .057f, .004f), paper);
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? -.018f : .018f;
                    if (index == 1 && side == 1)
                        Part(root.transform, PrimitiveType.Sphere, new Vector3(x, .003f, -.013f), new Vector3(.021f, .024f, .008f), gold);
                    else for (int line = 0; line < 4; line++)
                        Part(root.transform, PrimitiveType.Cube, new Vector3(x, .018f - .010f * line, -.01f), new Vector3(.020f, .0015f, .0015f), ink);
                }
                var collider = root.AddComponent<BoxCollider>();
                collider.size = new Vector3(.09f, .085f, .05f);
                var action = root.AddComponent<PhysicalBookAction>();
                action.Action = index == 0 ? BookActionKind.ConversationLayout : BookActionKind.PracticeLayout;
                action.AccessibleName = index == 0 ? "Conversation book" : "Practice spread";
                action.Browser = browser; action.Book = book;
            }
            // Solid paper bundles sit beyond the page edges for earlier/later history.
            for (int side = -1; side <= 1; side += 2)
            {
                var bundle = new GameObject(side < 0 ? "Earlier leaves" : "Later leaves");
                bundle.transform.SetParent(transform, false);
                bundle.transform.localPosition = new Vector3(side * .365f, -.17f, .005f);
                for (int leaf = 0; leaf < 3; leaf++)
                    Part(bundle.transform, PrimitiveType.Cube, new Vector3(leaf * .002f, leaf * .002f, leaf * -.003f), new Vector3(.05f,.065f,.002f), paper);
                Part(bundle.transform, PrimitiveType.Cube, new Vector3(side * .014f,.0f,-.013f), new Vector3(.009f,.038f,.012f), teal);
                var collider = bundle.AddComponent<BoxCollider>(); collider.size = new Vector3(.06f,.075f,.03f);
                var action = bundle.AddComponent<PhysicalBookAction>();
                action.Action = side < 0 ? BookActionKind.Earlier : BookActionKind.Later;
                action.AccessibleName = side < 0 ? "Earlier conversation pages" : "Later conversation pages";
                action.Browser = browser; action.Book = book;
            }
            if (book.BookmarkRibbon)
            {
                var tail = book.BookmarkRibbon.gameObject.AddComponent<BoxCollider>();
                tail.center = new Vector3(0,-.44f,0); tail.size = new Vector3(1.5f,.18f,20);
                var action = book.BookmarkRibbon.gameObject.AddComponent<PhysicalBookAction>();
                action.Action = BookActionKind.Bookmark; action.AccessibleName = "Return to bookmarked message";
                action.Browser = browser; action.Book = book;
            }
        }

        Material Material(Color color)
        {
            var result = IllustratedMaterials.Create(color); owned.Add(result); return result;
        }

        void Update()
        {
            if (!bellLabel) return;
            bool paused = !source || source.Snapshot == null || source.Snapshot.audioPaused;
            bellLabel.text = paused ? "Resume audio" : "Audio on";
            bellPaint.color = paused ? IllustratedMaterials.Ribbon : IllustratedMaterials.Hex("2B8D88");
            string message = source ? source.Error : null;
            if (shownNotice != message) { shownNotice = message; noticeLabel.text = BrowserNotice.Wrap(message); notice.SetActive(!string.IsNullOrEmpty(message)); }
        }

        static void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position; item.transform.localScale = scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            ArtResources.Release(item.GetComponent<Collider>());
        }

        void OnDestroy() { foreach (var material in owned) ArtResources.Release(material); }
    }
}

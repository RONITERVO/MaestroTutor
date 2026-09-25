// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Book
{
    public enum BookActionKind { Earlier, Later, Latest, Bookmark, ConversationLayout, PracticeLayout, LatestArtifact }

    /// <summary>Attached to a solid mesh with a collider, never a flat screen button.</summary>
    public sealed class PhysicalBookAction : MonoBehaviour
    {
        public BookActionKind Action;
        public NativeBookBrowser Browser;
        public IllustratedBook Book;
        public string AccessibleName;
        float lastActivated = -1;

        public void Activate()
        {
            if (Time.unscaledTime - lastActivated < .3f || Browser == null) return;
            lastActivated = Time.unscaledTime;
            switch (Action)
            {
                case BookActionKind.Earlier:
                    Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"history.step\",\"direction\":-1}"); Book?.AnimateTurn(-1); break;
                case BookActionKind.Later:
                    Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"history.step\",\"direction\":1}"); Book?.AnimateTurn(1); break;
                case BookActionKind.Latest: Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"history.latest\"}"); break;
                case BookActionKind.Bookmark: Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"bookmark.jump\"}"); break;
                case BookActionKind.ConversationLayout: Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"layout.set\",\"layout\":\"conversation\"}"); break;
                case BookActionKind.PracticeLayout: Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"layout.set\",\"layout\":\"practice\"}"); break;
                case BookActionKind.LatestArtifact: Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"artifact.latest\"}"); break;
            }
        }
    }
}

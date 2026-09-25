// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using Maestro.Quest.Interaction;

namespace Maestro.Quest.Book
{
    public enum BookActionKind { Earlier, Later, Latest, Bookmark, ConversationLayout, PracticeLayout, LatestArtifact, ResumeAudio }

    /// <summary>Attached to a solid mesh with a collider, never a flat screen button.</summary>
    public sealed class PhysicalBookAction : PhysicalAction
    {
        public BookActionKind Action;
        public NativeBookBrowser Browser;
        public IllustratedBook Book;
        protected override void OnActivate()
        {
            if (Browser == null) return;
            switch (Action)
            {
                case BookActionKind.ResumeAudio: Browser.ExecuteBookCommand("{\"version\":1,\"type\":\"session.resume\"}"); break;
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

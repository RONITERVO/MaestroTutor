// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Book
{
    public static class BookExternalLinks
    {
        // The native host only launches this public, credential-free approval
        // page after Java has verified a main-frame user gesture. No arbitrary
        // artifact URL, intent, query parameter or custom scheme is launched.
        public const string AccountLinkUrl = "https://chatwithmaestro.com/quest-link.html";
        public static bool TryOpen(string url, Action<string> open)
        {
            if (!string.Equals(url, AccountLinkUrl, StringComparison.Ordinal) || open == null) return false;
            open(AccountLinkUrl); return true;
        }
    }
}

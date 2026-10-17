// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Book
{
    public static class BookExternalLinks
    {
        // The native host only launches these public, credential-free approval/policy
        // pages after Java has verified a main-frame user gesture. No arbitrary
        // artifact URL, intent, query parameter or custom scheme is launched.
        public const string AccountLinkUrl = "https://chatwithmaestro.com/quest-link.html";
        public const string PrivacyUrl = "https://chatwithmaestro.com/privacy.html";
        public const string GeminiTermsUrl = "https://ai.google.dev/gemini-api/terms";
        public static bool TryOpen(string url, Action<string> open)
        {
            if (open == null || !(string.Equals(url, AccountLinkUrl, StringComparison.Ordinal)
                || string.Equals(url, PrivacyUrl, StringComparison.Ordinal)
                || string.Equals(url, GeminiTermsUrl, StringComparison.Ordinal))) return false;
            open(url); return true;
        }
    }
}

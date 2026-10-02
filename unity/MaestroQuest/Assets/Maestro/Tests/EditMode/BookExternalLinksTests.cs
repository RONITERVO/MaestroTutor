// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Book;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class BookExternalLinksTests
    {
        [TestCase(BookExternalLinks.AccountLinkUrl)]
        [TestCase(BookExternalLinks.PrivacyUrl)]
        [TestCase(BookExternalLinks.GeminiTermsUrl)]
        public void OpensAnExactPublicPage(string url)
        {
            string opened=null;
            Assert.That(BookExternalLinks.TryOpen(url,value=>opened=value),Is.True);
            Assert.That(opened,Is.EqualTo(url));
        }
        [Test] public void DoesNotClaimToOpenWithoutAHandler()
        {
            Assert.That(BookExternalLinks.TryOpen(BookExternalLinks.PrivacyUrl,null),Is.False);
        }
        [TestCase("https://chatwithmaestro.com/privacy.html?code=secret")]
        [TestCase("https://chatwithmaestro.com/privacy.html#code")]
        [TestCase("http://chatwithmaestro.com/privacy.html")]
        [TestCase("https://chatwithmaestro.com.evil.example/privacy.html")]
        [TestCase("https://ai.google.dev/gemini-api/terms?code=secret")]
        [TestCase("https://ai.google.dev/gemini-api/terms#code")]
        [TestCase("http://ai.google.dev/gemini-api/terms")]
        [TestCase("https://ai.google.dev@evil.example/gemini-api/terms")]
        [TestCase(null)] [TestCase("")] [TestCase("https://example.com/quest-link.html")]
        [TestCase("http://chatwithmaestro.com/quest-link.html")]
        [TestCase("https://chatwithmaestro.com/quest-link.html?token=secret")]
        [TestCase("https://chatwithmaestro.com/quest-link.html#code")]
        [TestCase("https://chatwithmaestro.com@evil.example/quest-link.html")]
        [TestCase("intent://chatwithmaestro.com/quest-link.html")]
        [TestCase("https://chatwithmaestro.com/delete-account.html")]
        public void RefusesOtherUrlsWithoutLaunchingAnything(string url)
        {
            bool opened=false;
            Assert.That(BookExternalLinks.TryOpen(url,_=>opened=true),Is.False);
            Assert.That(opened,Is.False);
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Book;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class BookExternalLinksTests
    {
        [Test] public void OpensOnlyThePublicAccountApprovalPage()
        {
            string opened=null;
            Assert.That(BookExternalLinks.TryOpen(BookExternalLinks.AccountLinkUrl,url=>opened=url),Is.True);
            Assert.That(opened,Is.EqualTo("https://chatwithmaestro.com/quest-link.html"));
        }
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

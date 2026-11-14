// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Reflection;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class BookSnapshotTests
    {
        [Test] public void NavigationAndSuspensionDiscardCachedActivityBeforeAcceptingFreshIdenticalState()
        {
            var root=new GameObject("Book state lifecycle test");
            try
            {
                var browser=root.AddComponent<NativeBookBrowser>(); int updates=0;
                var receive=typeof(NativeBookBrowser).GetMethod("ReadSnapshot",BindingFlags.Instance|BindingFlags.NonPublic);
                void Read(string json) => receive.Invoke(browser,new object[] { json });
                browser.SnapshotChanged+=value => { Assert.That(value,Is.Not.Null); updates++; };
                const string speaking="{\"version\":1,\"activity\":\"speaking\"}";
                Read(speaking); var first=browser.Snapshot;
                Assert.That(first.activity,Is.EqualTo("speaking"));
                Read(speaking); Assert.That(updates,Is.EqualTo(1));
                Read(""); Assert.That(browser.Snapshot,Is.Null);
                Read(speaking); Assert.That(browser.Snapshot,Is.Not.SameAs(first)); Assert.That(updates,Is.EqualTo(2));
                browser.SetSuspended(true); Read(speaking); Assert.That(browser.Snapshot,Is.Null);
                browser.SetSuspended(false); Read(speaking); Assert.That(updates,Is.EqualTo(3));
                Read("{\"version\":7}"); Assert.That(browser.Snapshot,Is.Null);
                Read(speaking); Read(new string('x',4097)); Assert.That(browser.Snapshot,Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

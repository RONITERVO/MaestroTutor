// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator NativeEditReadAllocationsDoNotGrowWithUnchangedSavedObjects()
        {
            var sparse = editor.Snapshot();
            sparse.objects = sparse.objects.Where(x => x.IsBuiltIn).ToArray();
            var populated = sparse.Copy();
            populated.objects = populated.objects.Concat(Enumerable.Range(0, 63).Select(i => new RoomObjectData {
                id = (i + 1).ToString("x32"), kind = RoomObjectKind.Drawing,
                points = Enumerable.Range(0, 512).Select(j => new Vector3(j * .0001f, 0, 0)).ToArray()
            })).ToArray();
            Assert.That(populated.Validate(out var error), Is.True, error);
            var small = new RoomJournal(sparse);
            var large = new RoomJournal(populated);
            var changed = small.Read("book");
            changed.color = Color.red;
            Assert.That(small.Prepare(new[] { changed }, Array.Empty<string>(), out var smallEdit, out error), Is.True, error);
            Assert.That(large.Prepare(new[] { changed }, Array.Empty<string>(), out var largeEdit, out error), Is.True, error);
            using (smallEdit)
            using (largeEdit)
            {
                string state = JsonUtility.ToJson(editor.Snapshot());
                int revision = editor.Revision;
                Action prepareSmall = () => {
                    using var native = RoomEditPreparation.TryCreate(editor, smallEdit, out var issue);
                    if (native == null) throw new InvalidOperationException(issue);
                };
                Action prepareLarge = () => {
                    using var native = RoomEditPreparation.TryCreate(editor, largeEdit, out var issue);
                    if (native == null) throw new InvalidOperationException(issue);
                };
                for (int i = 0; i < 3; i++) { prepareSmall(); prepareLarge(); }
                long smallAllocations = ManagedAllocations.Measure(prepareSmall);
                long largeAllocations = ManagedAllocations.Measure(prepareLarge);
                TestContext.Out.WriteLine($"Native edit read allocation events: small={smallAllocations}, with 63 unchanged drawings={largeAllocations}");
                Assert.That(largeAllocations, Is.LessThanOrEqualTo(smallAllocations + 4),
                    "Native preparation of one edited object must not copy unrelated saved geometry");
                Assert.That(editor.Revision, Is.EqualTo(revision));
                Assert.That(JsonUtility.ToJson(editor.Snapshot()), Is.EqualTo(state));
                Assert.That(populated.objects.Skip(2).All(x => editor.Find(x.id) == null), Is.True);
                Assert.That(smallEdit.Current(small), Is.True);
                Assert.That(largeEdit.Current(large), Is.True);
            }
            yield return null;
        }
    }
}

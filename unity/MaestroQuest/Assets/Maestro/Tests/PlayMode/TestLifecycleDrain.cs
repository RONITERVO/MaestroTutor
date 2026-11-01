// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    internal static class TestLifecycleDrain
    {
        internal static IEnumerator Destroy(GameObject root)
        {
            if(!root)yield break;
            var rooms=root.GetComponentsInChildren<RoomEditor>(true);
            var workshops=root.GetComponentsInChildren<RuleWorkshop>(true);
            Object.Destroy(root);yield return null;
            foreach(var room in rooms)yield return Complete(room);
            foreach(var rules in workshops)yield return Complete(null,rules);
        }
        // Call after Destroy has delivered lifecycle callbacks. CLR references
        // retain their detached tasks; never use Unity APIs on destroyed owners.
        internal static IEnumerator Complete(RoomEditor room,RuleWorkshop rules=null)
        {
            var task=Task.WhenAll(room?.LifecycleCompletion??Task.CompletedTask,
                rules?.LifecycleCompletion??Task.CompletedTask,rules?.Modules?.Retire()??Task.CompletedTask);
            yield return Wait(task);
        }
        internal static IEnumerator Wait(Task task)
        {
            float end=Time.realtimeSinceStartup+10;
            while(!task.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(task.IsCompleted,"Detached lifecycle checkpoints did not drain before test storage cleanup.");
            Assert.IsFalse(task.IsFaulted,task.Exception?.ToString());
        }
    }
}

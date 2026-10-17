// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        [UnityTest]
        public IEnumerator NativePlayerProbeUsesStaticAotCallbackAndDrainsNineStreams()
        {
            var callback=(Delegate)typeof(PreparedGraph).GetField("Log",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Assert.IsTrue(callback.Method.IsStatic,"IL2CPP reverse callback must be static");
            Assert.IsNull(callback.Target,"No instance reverse callback");
            Assert.IsTrue(callback.Method.IsDefined(typeof(AOT.MonoPInvokeCallbackAttribute),false),"Generate an AOT reverse wrapper");
            var run=NativePlayerProbe.Run();
            // The probe owns its worker through its complete cleanup; a runner's
            // outer deadline remains authoritative if the native call stalls.
            while(!run.IsCompleted) yield return null;
            Assert.IsFalse(run.IsFaulted,run.Exception?.ToString());
            var result=run.Result;
            Assert.AreEqual("passed",result.status,result.issue);
            Assert.AreEqual(9,result.completedVoices);
            Assert.AreEqual(43200,result.submitted); Assert.AreEqual(43200,result.played);
            Assert.IsTrue(result.drained); Assert.Greater(result.energy,1e-6);
            Assert.AreEqual(1,result.graphs); Assert.AreEqual(1,result.meshes); Assert.AreEqual(1,result.instances);
            Debug.Log("NATIVE_PLAYER_PROBE "+JsonUtility.ToJson(result));
        }
    }
}

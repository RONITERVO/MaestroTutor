// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace Maestro.Quest.Editor
{
    // Batch logs retain the last started test even when a coroutine never returns.
    [InitializeOnLoad] static class QuestTestProgress
    {
        static readonly TestRunnerApi api;
        static QuestTestProgress()
        {
            if(!Application.isBatchMode)return;
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Progress());
        }
        sealed class Progress:ICallbacks
        {
            public void RunStarted(ITestAdaptor test) {}
            public void RunFinished(ITestResultAdaptor result) {}
            public void TestStarted(ITestAdaptor test) {if(!test.IsSuite)Debug.Log("MAESTRO_TEST_STARTED "+test.FullName);}
            public void TestFinished(ITestResultAdaptor result) {if(!result.Test.IsSuite)Debug.Log("MAESTRO_TEST_FINISHED "+result.Test.FullName+" "+result.TestStatus);}
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace Maestro.Quest.Editor
{
    // Batch-only inventory from the same Test Runner discovery used to execute
    // suites. Verification compares every discovered leaf with finished XML,
    // so partitioning a large suite cannot silently omit a new fixture/case.
    public static class QuestTestInventory
    {
        static TestRunnerApi api;
        public static void Export()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use a dedicated batch verification process.");
            api=ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RetrieveTestList(TestMode.EditMode,edit=>{
                try{
                    var edits=Leaves(edit);
                    api.RetrieveTestList(TestMode.PlayMode,play=>{
                        try{
                            var plays=Leaves(play);
                            if(edits.Count==0||plays.Count==0)throw new InvalidOperationException("Native test inventory is empty.");
                            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/test-inventory.json"));
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            File.WriteAllText(path,new JObject{["editMode"]=edits,["playMode"]=plays}.ToString());
                            Debug.Log("MAESTRO_TEST_INVENTORY "+edits.Count+" EditMode, "+plays.Count+" PlayMode cases");
                            EditorApplication.Exit(0);
                        }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
                    });
                }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
            });
        }
        static JArray Leaves(ITestAdaptor root)
        {
            var result=new JArray();
            void Visit(ITestAdaptor node){
                if(!node.IsSuite){result.Add(node.FullName);return;}
                foreach(var child in node.Children)Visit(child);
            }
            Visit(root);return result;
        }
    }
}

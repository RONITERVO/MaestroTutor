// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static JObject ObjectSearchRequest(string cursor="",string query="",string kind="all")=>new(){["operation"]="inspect",["category"]="facts",["capability"]="world.objects",["version"]=1,["arguments"]=new JObject{["query"]=query,["kind"]=kind,["cursor"]=cursor}};
        [UnityTest] public IEnumerator ObjectSearchCatalogAndProgramSeeDormantObjectsWithoutLoadingOrEditing()
        {
            string dormant=RetireDisplayBlock();yield return null;Assert.That(editor.Find(dormant),Is.Null);string before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(ObjectSearchFact.Feature));var executor=new RoomAgentExecutor(editor);var evidence=new JArray();string cursor="";var ids=new System.Collections.Generic.List<string>();
            do {var request=ObjectSearchRequest(cursor);Assert.That(executor.Catalog.Execute(request,out var status),Is.True,status);var view=executor.Catalog.Observe();Assert.That((bool)view["available"],Is.True,view.ToString());evidence.Add(new JObject{["request"]=request,["view"]=view});
                Assert.That(runtime.TryReadFact("world.objects",1,(JObject)request["arguments"],out var value),Is.True);Assert.That(JToken.DeepEquals(JToken.FromObject(value.Value),view["value"]),Is.True);
                ids.AddRange(view["value"]["entries"].Select(x=>(string)x["id"]));cursor=(string)view["value"]["next"];
            }while(cursor!="");
            Assert.That(ids,Does.Contain(dormant));Assert.That(ids.Count,Is.EqualTo(editor.Snapshot().objects.Length));Assert.That(editor.Find(dormant),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            Assert.That(executor.Catalog.Execute(ObjectSearchRequest(query:dormant),out _),Is.True);var exact=executor.Catalog.Observe();Assert.That((int)exact["value"]["total"],Is.EqualTo(1));
            string path=Environment.GetEnvironmentVariable("MAESTRO_OBJECT_SEARCH_EVIDENCE");if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"pages.json"),evidence.ToString());}
        }
        [UnityTest] public IEnumerator ObjectSearchCursorExpiresAcrossTemporaryBeginAndDiscard()
        {
            string cursor=(string)editor.SearchObjects("","all","")["next"];Assert.That(cursor,Is.Not.Empty);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.SearchObjects("","all",cursor),Is.Null);string temporary=(string)editor.SearchObjects("","all","")["next"];Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.SearchObjects("","all",temporary),Is.Null);Assert.That(editor.SearchObjects("","all",cursor),Is.Null);Assert.That(editor.SearchObjects("","all",""),Is.Not.Null);
        }
    }
}

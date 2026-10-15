// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator BoundedContextStillFindsAndInspectsEverySavedObjectWithoutSelectingOrLoading()
        {
            for(int i=0;i<8;i++)editor.Create(RoomObjectKind.Block);
            var all=editor.Snapshot().objects.Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            string before=JsonUtility.ToJson(editor.Snapshot()),selected=editor.SelectedId;int revision=editor.Revision;
            var send=SavedInspectionClient();var found=new List<string>();string cursor="";RoomAgentState state=null;
            do {
                state=send(new JObject{["action"]="catalog",["catalog"]=new JObject{["operation"]="inspect",["category"]="facts",["capability"]="world.objects",["version"]=1,["arguments"]=new JObject{["query"]="",["kind"]="all",["cursor"]=cursor}}});
                Assert.That(state.ok,Is.True,state.status);Assert.That((bool)state.catalog["available"],Is.True);
                Assert.That(state.capabilities,Does.Contain(RoomObjectOverview.Feature));Assert.That(state.objectOverview,Is.Not.Null);Assert.That(state.objectOverview.exhaustive,Is.False);
                Assert.That(state.objects.Length,Is.LessThanOrEqualTo(RoomObjectOverview.Limit));Assert.That(state.objects.Select(x=>x.id),Does.Contain(selected));
                var page=state.catalog["value"];found.AddRange(page["entries"].Select(x=>(string)x["id"]));cursor=(string)page["next"];
            }while(cursor!="");
            Assert.That(found,Is.EqualTo(all));Assert.That(all.Length,Is.GreaterThan(RoomObjectOverview.Limit));
            string outside=all.First(x=>!state.objects.Any(o=>o.id==x));
            state=send(new JObject{["action"]="catalog",["catalog"]=new JObject{["operation"]="inspect",["category"]="facts",["capability"]="object.observation",["version"]=1,["arguments"]=new JObject{["target"]=outside}}});
            Assert.That((bool)state.catalog["available"],Is.True);Assert.That((string)state.catalog["value"]["id"],Is.EqualTo(outside));Assert.That(state.objects.Any(x=>x.id==outside),Is.False);
            Assert.That(editor.SelectedId,Is.EqualTo(selected));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_OBJECT_OVERVIEW_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)){System.IO.Directory.CreateDirectory(evidence);System.IO.File.WriteAllText(System.IO.Path.Combine(evidence,"context.json"),RoomAgentWire.Serialize(state));}
            state=send(new JObject{["action"]="inspect",["target"]=outside});Assert.That(state.inspection.item.id,Is.EqualTo(outside));Assert.That(state.objects.Any(x=>x.id==outside),Is.True);
            state=send(new JObject{["action"]="inspect",["target"]="maestro"});Assert.That(state.objects.Any(x=>x.id==outside),Is.False);Assert.That(editor.HasSavedObject(outside),Is.True);
            Assert.That(editor.Revision,Is.EqualTo(revision));yield return null;
        }
    }
}

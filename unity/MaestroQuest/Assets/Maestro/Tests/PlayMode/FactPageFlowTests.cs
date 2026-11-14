// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static JObject ReadPageFact(RoomCapabilityCatalog catalog,string id,JObject arguments,int version=1)
        {
            Assert.IsTrue(catalog.Execute(new JObject {["operation"]="inspect",["category"]="facts",["capability"]=id,["version"]=version,["arguments"]=arguments},out var error),error);
            var observed=catalog.Observe();Assert.IsTrue((bool)observed["available"],observed.ToString());
            Assert.IsTrue(JToken.DeepEquals(observed,catalog.Observe()),"Repeated read must not change the selected fact");
            var value=(JObject)observed["value"];ProgramValue.Literal(value,BehaviourCatalog.Fact(id).Type);return value;
        }
        IEnumerator ExactChatChoices(bool audio)
        {
            string prefix=audio?"audio":"image",key=audio?"assetHash":"imageHash";var offers=new JArray();
            for(int i=0;i<8;i++)offers.Add(new JObject {[key]=(i+1).ToString("x64"),["name"]=new string(i%2==0?'\u2028':'\u2029',80),["messageId"]=new string('\u2028',128),["origin"]=new string('\u2029',32),["bytes"]=1024});
            var wire=new JObject {["offerSet"]=new string('a',32),["offers"]=offers};var original=wire.DeepClone();
            var image=editor.GetComponent<ImageImportWorkshop>();var sound=editor.GetComponent<AudioImportWorkshop>();
            if(audio)sound.SyncChat("page-budget",wire);else image.SyncChat("page-budget",wire);
            string saved=JsonUtility.ToJson(editor.Snapshot());var catalog=new RoomCapabilityCatalog(editor);var seen=new List<string>();int offset=0,pages=0;
            do {
                Assert.Less(++pages,10);var page=ReadPageFact(catalog,prefix+".chat.choices",new JObject {["offset"]=offset});var rows=(JArray)page["entries"];
                Assert.AreEqual(8,(int)page["total"]);Assert.IsNotEmpty(rows);Assert.LessOrEqual(rows.Count,2);
                foreach(var row in rows){
                    int index=seen.Count;string hash=(string)row[key];seen.Add(hash);var offered=offers[index];
                    Assert.AreEqual((string)offered[key],hash);Assert.AreEqual(hash,(string)row["id"]);Assert.AreEqual((string)offered["name"],(string)row["name"]);
                    Assert.AreEqual(1,(int)row["revision"]);Assert.AreEqual(1d,(double)row["kibibytes"]);
                    foreach(string field in new[]{"name","messageId","origin"}){
                        var metadata=ReadPageFact(catalog,prefix+".chat.metadata",new JObject {[key]=hash,["field"]=field});
                        Assert.AreEqual(hash,(string)metadata[key]);Assert.AreEqual(field,(string)metadata["field"]);Assert.AreEqual((string)offered[field],(string)metadata["value"]);
                        Assert.AreEqual((string)page["offerSet"],(string)metadata["offerSet"]);
                    }
                    var current=ReadPageFact(catalog,prefix+".chat.item",new JObject {[key]=hash});Assert.AreEqual((string)page["offerSet"],(string)current["offerSet"]);
                    var args=new JObject {["operation"]="selectChat",["offerSet"]=(string)current["offerSet"],[key]=hash};
                    Assert.IsTrue(BehaviourCatalog.Action(prefix+".import").Module.CanRun(new CapabilityContext(editor,animations),args,out var error),error);
                }
                int next=(int)page["next"];Assert.AreEqual(offset+rows.Count<8?offset+rows.Count:-1,next);if(next<0)break;offset=next;
            }while(true);
            CollectionAssert.AreEqual(offers.Select(v=>(string)v[key]),seen);Assert.IsTrue(JToken.DeepEquals(original,wire));
            Assert.AreEqual(saved,JsonUtility.ToJson(editor.Snapshot()));Assert.IsNull(audio?sound.Chat.Request():image.Chat.Request());
            Assert.IsTrue(catalog.Execute(new JObject {["operation"]="inspect",["category"]="facts",["capability"]=prefix+".chat.metadata",["version"]=1,["arguments"]=new JObject {[key]=new string('f',64),["field"]="messageId"}},out _));
            Assert.IsFalse((bool)catalog.Observe()["available"]);Assert.AreEqual(JTokenType.Null,catalog.Observe()["value"].Type);
            // The exact same bytes can acquire new chat metadata; the source-set guard must change with it.
            wire["offerSet"]=new string('b',32);wire["offers"][0]["messageId"]="new message";
            if(audio)sound.SyncChat("page-budget",wire);else image.SyncChat("page-budget",wire);
            var changed=ReadPageFact(catalog,prefix+".chat.metadata",new JObject {[key]=(string)offers[0][key],["field"]="messageId"});
            Assert.AreEqual(new string('b',32),(string)changed["offerSet"]);Assert.AreEqual("new message",(string)changed["value"]);
            var stale=new JObject {["operation"]="selectChat",["offerSet"]=new string('a',32),[key]=(string)offers[0][key]};
            Assert.IsFalse(BehaviourCatalog.Action(prefix+".import").Module.CanRun(new CapabilityContext(editor,animations),stale,out _));yield return null;
        }
        [UnityTest] public IEnumerator ImageChatMaximumMetadataRemainsExactlyDiscoverableAndSelectable()=>ExactChatChoices(false);
        [UnityTest] public IEnumerator AudioChatMaximumMetadataRemainsExactlyDiscoverableAndSelectable()=>ExactChatChoices(true);
        [UnityTest] public IEnumerator SavedAudioNamesPageExactlyWithoutChangingDefinitions()
        {
            var names=new Dictionary<string,string>();
            for(int i=0;i<8;i++){
                string id=(i+1).ToString("x32"),name=i%3==0?new string('\u2028',80):i%3==1?new string('"',80):new string('\\',80);
                var definition=new RoomAudioDefinition {id=id,name=name};Assert.IsTrue(definition.Validate(out var error),error);
                Assert.IsTrue(editor.EditAudio(id,0,definition,out error),error);names.Add(id,name);
            }
            string saved=JsonUtility.ToJson(editor.Snapshot());var catalog=new RoomCapabilityCatalog(editor);var seen=new List<string>();int offset=0,pages=0;
            do {
                Assert.Less(++pages,10);var page=ReadPageFact(catalog,"audio.source.list",new JObject {["offset"]=offset});var entries=(JArray)page["entries"];
                Assert.AreEqual(8,(int)page["total"]);Assert.IsNotEmpty(entries);Assert.LessOrEqual(entries.Count,4);
                foreach(var item in entries){
                    string id=(string)item["id"];seen.Add(id);Assert.AreEqual(names[id],(string)item["name"]);Assert.AreEqual(editor.AudioRevision(id),(int)item["revision"]);Assert.AreEqual("tone",(string)item["kind"]);Assert.AreEqual(names[id],editor.ReadAudio(id).name);
                    var exact=ReadPageFact(catalog,"audio.source.definition",new JObject {["id"]=id},2);
                    Assert.AreEqual(id,(string)exact["id"]);Assert.AreEqual((int)item["revision"],(int)exact["revision"]);Assert.AreEqual(names[id],(string)exact["definition"]["name"]);
                    Assert.IsTrue(JToken.DeepEquals(AudioSchema.ObserveSource(editor.ReadAudio(id)),exact["definition"]));
                }
                int next=(int)page["next"];Assert.AreEqual(offset+entries.Count<8?offset+entries.Count:-1,next);if(next<0)break;offset=next;
            }while(true);
            CollectionAssert.AreEqual(names.Keys.OrderBy(x=>x,StringComparer.Ordinal),seen);Assert.AreEqual(saved,JsonUtility.ToJson(editor.Snapshot()));
            var empty=ReadPageFact(catalog,"audio.source.list",new JObject {["offset"]=8});Assert.IsEmpty((JArray)empty["entries"]);Assert.AreEqual(-1,(int)empty["next"]);yield return null;
        }
    }
}

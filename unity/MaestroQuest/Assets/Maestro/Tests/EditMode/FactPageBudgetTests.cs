// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class FactPageBudgetTests
    {
        internal static string HashKey(bool audio)=>audio?"assetHash":"imageHash";
        internal static string LibraryId(bool audio)=>audio?"audio.chat.library":"image.chat.library";
        internal static JObject Offers(bool audio,int count,int style=0)
        {
            var offers=new JArray();string key=HashKey(audio);
            for(int i=0;i<count;i++)offers.Add(new JObject {
                [key]=(i+1).ToString("x64"),
                ["name"]=style==0?new string('n',80):style==1?string.Concat(Enumerable.Repeat("\"\\",40)):new string('\u2028',80),
                ["messageId"]=style==2?"m":new string(style==1?'\\':'m',128),
                ["origin"]=new string('o',32),["bytes"]=1024
            });
            return new JObject {["offerSet"]=new string('a',32),["offers"]=offers};
        }
        static ChatMediaTransfer Transfer(bool audio)=>audio?new ChatAudioTransfer():new ChatImageTransfer();
        static IEnumerable<TestCaseData> Cases()
        {
            foreach(bool audio in new[]{false,true})foreach(int count in new[]{0,1,2,8})foreach(int style in new[]{0,1,2})
                yield return new TestCaseData(audio,count,style);
        }
        [TestCaseSource(nameof(Cases))]
        public void ChatPagesPreserveEveryAcceptedIdentityAndMetadataInOrder(bool audio,int count,int style)
        {
            var wire=Offers(audio,count,style);var original=wire.DeepClone();var transfer=Transfer(audio);transfer.Receive("room",wire,0);
            var observed=new List<string>();int offset=0,pages=0;string key=HashKey(audio);
            do {
                Assert.Less(++pages,10,"A next cursor must progress");var page=transfer.Library(offset);
                var value=ProgramValue.Literal(page,BehaviourCatalog.Fact(LibraryId(audio)).Type);
                Assert.LessOrEqual(value.Characters,ProgramValue.MaximumCharacters);Assert.AreEqual(count,(int)page["total"]);
                Assert.AreEqual((string)wire["offerSet"],(string)page["offerSet"]);var entries=(JArray)page["entries"];
                Assert.LessOrEqual(entries.Count,2);if(offset<count)Assert.IsNotEmpty(entries);
                for(int i=0;i<entries.Count;i++){
                    var entry=entries[i];var offer=wire["offers"][offset+i];observed.Add((string)entry[key]);
                    Assert.AreEqual((string)offer[key],(string)entry["id"]);Assert.AreEqual(1,(int)entry["revision"]);
                    foreach(string field in new[]{key,"name","messageId","origin"})Assert.AreEqual((string)offer[field],(string)entry[field],field);
                    Assert.AreEqual((int)offer["bytes"]/1024d,(double)entry["kibibytes"]);
                    Assert.IsTrue(transfer.CanSelect((string)page["offerSet"],(string)entry[key],out var error),error);
                }
                int next=(int)page["next"];Assert.AreEqual(offset+entries.Count<count?offset+entries.Count:-1,next);
                if(next<0)break;offset=next;
            }while(true);
            CollectionAssert.AreEqual(((JArray)wire["offers"]).Select(v=>(string)v[key]),observed);
            Assert.IsTrue(JToken.DeepEquals(original,wire));Assert.IsNull(transfer.Request(),"Reading never starts byte transfer");
            if(count>0){var page=transfer.Library(0);page["entries"][0]["name"]="Changed returned value";Assert.AreEqual((string)wire["offers"][0]["name"],(string)transfer.Library(0)["entries"][0]["name"]);}
        }
        [TestCase(false,'\u2028')][TestCase(false,'\u2029')][TestCase(true,'\u2028')][TestCase(true,'\u2029')]
        public void UnrepresentableExactChatEntryFailsAtItsCursorWithoutSkippingOrChangingIt(bool audio,char separator)
        {
            Assert.IsFalse(char.IsControl(separator));var wire=Offers(audio,3);wire["offers"][1]["name"]="x";wire["offers"][1]["origin"]="generated";
            wire["offers"][1]["messageId"]=new string(separator,128);var original=wire.DeepClone();var transfer=Transfer(audio);transfer.Receive("room",wire,0);
            var prefix=transfer.Library(0);Assert.AreEqual(1,((JArray)prefix["entries"]).Count);Assert.AreEqual(1,(int)prefix["next"]);
            ProgramValue.Literal(prefix,BehaviourCatalog.Fact(LibraryId(audio)).Type);
            var failure=Assert.Throws<ProgramFault>(()=>transfer.Library(1));StringAssert.Contains("typed value budget",failure.Message);
            // An explicit later cursor is still readable; the failed cursor did not silently advance the transfer.
            var tail=transfer.Library(2);Assert.AreEqual((string)wire["offers"][2][HashKey(audio)],(string)tail["entries"][0][HashKey(audio)]);
            Assert.AreEqual(-1,(int)tail["next"]);Assert.IsTrue(JToken.DeepEquals(original,wire));Assert.IsNull(transfer.Request());
            Assert.IsTrue(transfer.CanSelect((string)wire["offerSet"],(string)wire["offers"][1][HashKey(audio)],out var error),error);
        }
        [TestCase(false,'\u2028')][TestCase(false,'\u2029')][TestCase(true,'\u2028')][TestCase(true,'\u2029')]
        public void CompactChoicesAndExactMetadataFitEveryMaximumEscapedOffer(bool audio,char separator)
        {
            var wire=Offers(audio,8);foreach(var offer in (JArray)wire["offers"]){offer["name"]=new string(separator,80);offer["messageId"]=new string(separator,128);offer["origin"]=new string(separator,32);}
            var original=wire.DeepClone();var transfer=Transfer(audio);transfer.Receive("room",wire,0);string prefix=audio?"audio":"image",key=HashKey(audio);
            for(int i=0;i<8;i++){
                var page=transfer.Choices(i);var value=ProgramValue.Literal(page,BehaviourCatalog.Fact(prefix+".chat.choices").Type);
                Assert.AreEqual(864,value.Characters,"Worst escaped name and complete numeric envelope");Assert.AreEqual(1,((JArray)page["entries"]).Count);
                Assert.AreEqual(i<7?i+1:-1,(int)page["next"]);Assert.AreEqual((string)wire["offers"][i][key],(string)page["entries"][0][key]);
                Assert.AreEqual((string)wire["offers"][i]["name"],(string)page["entries"][0]["name"]);
                foreach(string field in new[]{"name","messageId","origin"}){
                    var metadata=transfer.Metadata((string)page["entries"][0][key],field);var typed=ProgramValue.Literal(metadata,BehaviourCatalog.Fact(prefix+".chat.metadata").Type);
                    Assert.AreEqual((string)wire["offers"][i][field],(string)metadata["value"]);Assert.AreEqual((string)page["offerSet"],(string)metadata["offerSet"]);
                    if(field=="messageId")Assert.AreEqual(925,typed.Characters,"One complete worst-case128-character identity fits without segmentation");
                    metadata["value"]="Mutated returned copy";Assert.AreEqual((string)wire["offers"][i][field],(string)transfer.Metadata((string)page["entries"][0][key],field)["value"]);
                }
            }
            var end=transfer.Choices(8);Assert.IsEmpty((JArray)end["entries"]);Assert.AreEqual(-1,(int)end["next"]);
            ProgramValue.Literal(end,BehaviourCatalog.Fact(prefix+".chat.choices").Type);
            Assert.IsNull(transfer.Metadata(new string('f',64),"name"));Assert.IsNull(transfer.Metadata((string)wire["offers"][0][key],"bytes"));
            Assert.IsTrue(JToken.DeepEquals(original,wire));Assert.IsNull(transfer.Request());
        }
        [Test]
        public void APageThatFitsRawJsonCanStillExceedTheTypedNumericBudget()
        {
            var wire=Offers(false,2);foreach(var offer in (JArray)wire["offers"])offer["origin"]="generated";
            var page=new JObject {["offerSet"]=(string)wire["offerSet"],["total"]=2,["next"]=-1,["entries"]=new JArray(((JArray)wire["offers"]).Select(v=>new JObject {
                ["id"]=(string)v["imageHash"],["revision"]=1,["imageHash"]=(string)v["imageHash"],["name"]=(string)v["name"],
                ["messageId"]=(string)v["messageId"],["origin"]=(string)v["origin"],["kibibytes"]=1d}))};
            Assert.Less(page.ToString(Newtonsoft.Json.Formatting.None).Length,1024);Assert.Greater(ProgramValue.MeasureCharacters(page),1024);
            Assert.Throws<ProgramFault>(()=>ProgramValue.Literal(page,BehaviourCatalog.Fact("image.chat.library").Type));
            var transfer=new ChatImageTransfer();transfer.Receive("room",wire,0);Assert.AreEqual(1,((JArray)transfer.Library(0)["entries"]).Count);
        }
    }
}

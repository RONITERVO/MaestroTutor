// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomObjectSearchTests
    {
        const string Session="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        static string Id(int i)=>i.ToString("x32");
        static RoomDocument Document(int count=8)=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}.Concat(Enumerable.Range(1,count).Select(i=>new RoomObjectData{id=Id(i),name=i%2==0?"Árbol apple "+i:"Robot "+i,kind=RoomObjectKind.Block})).ToArray()};
        static JObject Page(RoomJournal journal,string cursor="",string query="",string kind="all",string session=Session)=>journal.SearchObjects(session,query,kind,cursor);
        static string Next(RoomJournal journal)=>(string)Page(journal)["next"];
        static void Edit(RoomJournal journal,RoomObjectData data)=>Assert.That(journal.Apply(new[]{data},Array.Empty<string>(),out var error),Is.True,error);
        [Test] public void ObjectSearchPagesEverySavedIdExactlyOnceInOrderWithoutEditing()
        {
            var journal=new RoomJournal(Document(64));string before=JsonUtility.ToJson(journal.Snapshot()),cursor="";var ids=new List<string>();int pages=0;
            do {var page=Page(journal,cursor);Assert.That((int)page["offset"],Is.EqualTo(ids.Count));Assert.That((int)page["total"],Is.EqualTo(66));Assert.That(((JArray)page["entries"]).Count,Is.InRange(1,3));
                Assert.That(ProgramValue.Literal(page,ObjectSearchFact.Fact().Type).Characters,Is.LessThanOrEqualTo(1024));
                ids.AddRange(page["entries"].Select(x=>(string)x["id"]));cursor=(string)page["next"];Assert.That(++pages,Is.LessThan(24));}while(cursor!="");
            Assert.That(ids,Is.EqualTo(journal.Snapshot().objects.Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal)));Assert.That(ids.Distinct().Count(),Is.EqualTo(66));Assert.That(JsonUtility.ToJson(journal.Snapshot()),Is.EqualTo(before));Assert.That(journal.CanUndo,Is.False);
        }
        [Test] public void ObjectSearchUsesAllWordsCaseInsensitiveNamesIdsAndExactKind()
        {
            var journal=new RoomJournal(Document());var page=Page(journal,query:"APPLE árbol");Assert.That((int)page["total"],Is.EqualTo(4));Assert.That(page["entries"].All(x=>((string)x["name"]).Contains("Árbol")),Is.True);
            Assert.That((int)Page(journal,query:Id(5))["total"],Is.EqualTo(1));Assert.That((int)Page(journal,kind:"Book")["total"],Is.EqualTo(1));Assert.That((int)Page(journal,query:"apple",kind:"Book")["total"],Is.Zero);
            Assert.That((string)Page(journal,query:"not found")["next"],Is.Empty);Assert.That(Page(journal,kind:"block"),Is.Null);
            Assert.That(Page(journal,Next(journal),query:"apple"),Is.Null);Assert.That(Page(journal,Next(journal),kind:"Block"),Is.Null);
        }
        [TestCase("rename")][TestCase("remove")][TestCase("add")][TestCase("kind")]
        public void ObjectSearchCursorExpiresForMetadataChangesAndDoesNotReviveAfterUndo(string change)
        {
            var journal=new RoomJournal(Document());string cursor=Next(journal);var item=journal.Read(Id(5));
            if(change=="rename"){item.name="Changed";Edit(journal,item);}else if(change=="kind"){item.kind=RoomObjectKind.Ball;Edit(journal,item);}else if(change=="add")Edit(journal,new RoomObjectData{id=Id(20),kind=RoomObjectKind.Block});else Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[]{item.id},out _),Is.True);
            Assert.That(Page(journal,cursor),Is.Null);string edited=Next(journal);Assert.That(journal.Undo(),Is.True);Assert.That(Page(journal,cursor),Is.Null);Assert.That(Page(journal,edited),Is.Null);string undone=Next(journal);Assert.That(journal.Redo(),Is.True);Assert.That(Page(journal,undone),Is.Null);
        }
        [Test] public void ObjectSearchMovementAndAppearanceEditsKeepPagingButNeverFreezeObjectRevisions()
        {
            var journal=new RoomJournal(Document());string cursor=Next(journal);int revision=journal.ObjectRevision(Id(5));Assert.That(journal.UpdatePlacement(Id(5),Vector3.up,Quaternion.identity),Is.True);
            Assert.That(Page(journal,cursor),Is.Not.Null);Assert.That(journal.ObjectRevision(Id(5)),Is.GreaterThan(revision));var item=journal.Read(Id(5));item.color=Color.blue;Edit(journal,item);Assert.That(Page(journal,cursor),Is.Not.Null);
            var page=Page(journal,cursor);page["entries"][0]["name"]="external change";Assert.That((string)Page(journal,cursor)["entries"][0]["name"],Is.Not.EqualTo("external change"));
        }
        [Test] public void ObjectSearchRejectedEditsPreserveDirectoryAndCursor()
        {
            var journal=new RoomJournal(Document());string cursor=Next(journal);var item=journal.Read(Id(5));item.name=new string('x',81);Assert.That(journal.Apply(new[]{item},Array.Empty<string>(),out _),Is.False);Assert.That(Page(journal,cursor),Is.Not.Null);
        }
        [Test] public void ObjectSearchCursorCannotCrossJournalOrTemporarySessionOrBeForged()
        {
            var journal=new RoomJournal(Document());string cursor=Next(journal);
            Assert.That(Page(new RoomJournal(journal.Snapshot()),cursor),Is.Null);Assert.That(Page(journal.Fork(),cursor),Is.Null);Assert.That(Page(journal,cursor,session:"another"),Is.Null);
            Assert.That(Page(journal,Id(5)+cursor.Substring(cursor.IndexOf('.'))),Is.Null);Assert.That(Page(journal,"bad"),Is.Null);Assert.That(Page(journal,new string('x',66)),Is.Null);Assert.That(Page(journal,query:"bad\nquery"),Is.Null);
        }
        [Test] public void ObjectSearchEscapedUnicodeNamesReducePageSizeWithoutLosingAnyObjects()
        {
            var data=Enumerable.Range(1,7).Select(i=>new RoomObjectData{id=Id(i),kind=RoomObjectKind.ImportedModel,name=new string('\u2028',80)}).ToArray();var index=new RoomObjectSearch(data);string cursor="";int count=0;
            do {var page=index.Read(new string('a',32),Session,"","all",cursor);Assert.That((int)page["offset"],Is.EqualTo(count));Assert.That(ProgramValue.Literal(page,ObjectSearchFact.Fact().Type).Characters,Is.LessThanOrEqualTo(1024));Assert.That(page["entries"].All(x=>(string)x["name"]==data[0].name),Is.True);count+=((JArray)page["entries"]).Count;cursor=(string)page["next"];Assert.That(count,Is.LessThanOrEqualTo(7));}while(cursor!="");
            Assert.That(count,Is.EqualTo(7));
        }
        [Test] public void ObjectSearchWorstCaseNamesFitSharedProgramValueBudgetWithoutTruncation()
        {
            var data=Enumerable.Range(1,5).Select(i=>new RoomObjectData{id=Id(i),kind=RoomObjectKind.ImportedModel,name=new string('"',80)}).ToArray();var index=new RoomObjectSearch(data);var page=index.Read(new string('a',32),Session,"","all","");
            var value=ProgramValue.Literal(page,ObjectSearchFact.Fact().Type);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));Assert.That(((JArray)page["entries"]).Count,Is.EqualTo(3));Assert.That(page["entries"].All(x=>((string)x["name"]).Length==80),Is.True);Assert.That((string)page["next"],Is.Not.Empty);TestContext.WriteLine("Worst-case object search value cost: "+value.Characters);
        }
    }
}

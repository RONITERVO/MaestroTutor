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
    public sealed class RoomResourceMembersTests
    {
        const string Resource="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",Session="saved";
        static string Id(int i)=>i.ToString("x32");
        static RoomDocument Document(string kind="appearance",int count=32) {
            var document=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},
                new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}.Concat(Enumerable.Range(1,count).Select(i=>new RoomObjectData{id=Id(i),kind=RoomObjectKind.Block})).ToArray()};
            if(kind=="appearance") {
                document.appearances=new[]{new RoomAppearance{id=Resource,name="Shared"},new RoomAppearance{id=Other,name="Other"}};
                foreach(var item in document.objects)item.appearanceBindings=new[]{new AppearanceBinding{appearanceId=Resource}};
            } else if(kind=="visibility") {
                document.visibilityLayers=new[]{new RoomVisibilityLayer{id=Resource,name="Shared"},new RoomVisibilityLayer{id=Other,name="Other"}};
                foreach(var item in document.objects)item.visibilityLayer=Resource;
            } else if(kind=="environment") {
                document.environmentProfiles=new[]{new RoomEnvironmentProfile{id=Resource,name="Shared"},new RoomEnvironmentProfile{id=Other,name="Other"}};
                foreach(var item in document.objects)item.environmentProfile=Resource;
            } else document.regions=new[]{new RoomRegion{id=Resource,name="Shared",loading="independent",members=document.objects.Where(x=>x.id!="book"&&x.id!="maestro").Select(x=>x.id).ToArray()},new RoomRegion{id=Other,name="Other",loading="independent"}};
            return document;
        }
        static JObject Page(RoomJournal journal,string kind="appearance",string cursor="",string id=Resource,string session=Session)=>journal.ReadResourceMembers(session,kind,id,cursor);
        static void Edit(RoomJournal journal,RoomObjectData item)=>Assert.That(journal.Apply(new[]{item},Array.Empty<string>(),out var error),Is.True,error);
        [TestCase("appearance",256,258)][TestCase("visibility",256,258)][TestCase("region",256,256)][TestCase("environment",14,16)]
        public void ResourcePagesCoverTheSavedBudgetWithoutChangingTheWorld(string kind,int creations,int count) {
            var journal=new RoomJournal(Document(kind,creations));string before=JsonUtility.ToJson(journal.Snapshot()),cursor="",generation=null;
            var seen=new List<string>();
            do {
                var page=Page(journal,kind,cursor);Assert.That(page,Is.Not.Null);
                Assert.That((int)page["offset"],Is.EqualTo(seen.Count));Assert.That((int)page["total"],Is.EqualTo(count));
                generation??=(string)page["membership"];Assert.That((string)page["membership"],Is.EqualTo(generation));
                Assert.That(ResourceMembersFact.Fact().ValidValue(ProgramValue.Literal(page,ResourceMembersFact.Fact().Type)),Is.True);
                Assert.That(ProgramValue.Literal(page,ResourceMembersFact.Fact().Type).Characters,Is.LessThanOrEqualTo(1024));
                seen.AddRange(((JArray)page["members"]).Values<string>());cursor=(string)page["next"];
                Assert.That(seen.Count,Is.LessThanOrEqualTo(count));
            }while(cursor!="");
            var expected=journal.Snapshot().objects.Where(x=>kind!="region"||x.id!="book"&&x.id!="maestro").Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal);
            Assert.That(seen,Is.EqualTo(expected));Assert.That(seen.Distinct().Count(),Is.EqualTo(count));
            Assert.That(JsonUtility.ToJson(journal.Snapshot()),Is.EqualTo(before));Assert.That(journal.CanUndo,Is.False);
        }
        [TestCase("unbind")][TestCase("remove")][TestCase("add")]
        public void MembershipChangesExpireContinuationWithoutChangingTheDefinitionRevision(string change) {
            var journal=new RoomJournal(Document());var first=Page(journal);string cursor=(string)first["next"];int revision=journal.AppearanceRevision(Resource);
            if(change=="remove")Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[]{Id(25)},out _),Is.True);
            else {
                var item=change=="add"?new RoomObjectData{id=Id(90),kind=RoomObjectKind.Block,appearanceBindings=new[]{new AppearanceBinding{appearanceId=Resource}}}:journal.Read(Id(25));
                if(change=="unbind")item.appearanceBindings=Array.Empty<AppearanceBinding>();Edit(journal,item);
            }
            Assert.That(journal.AppearanceRevision(Resource),Is.EqualTo(revision));Assert.That(Page(journal,cursor:cursor),Is.Null);
            string newer=(string)Page(journal)["next"];Assert.That(journal.Undo(),Is.True);
            Assert.That(Page(journal,cursor:cursor),Is.Null);Assert.That(Page(journal,cursor:newer),Is.Null);
            string undone=(string)Page(journal)["next"];Assert.That(journal.Redo(),Is.True);Assert.That(Page(journal,cursor:undone),Is.Null);
        }
        [TestCase("appearance")][TestCase("visibility")][TestCase("environment")][TestCase("region")]
        public void EveryBindingDomainInvalidatesItsOwnGenerationAndKeepsUnrelatedDefinitions(string kind) {
            var journal=new RoomJournal(Document(kind,8));string generation=(string)Page(journal,kind)["membership"],other=(string)Page(journal,kind,id:Other)["membership"];
            var item=journal.Read(Id(5));
            if(kind=="region") {
                var region=journal.ReadRegion(Resource);region.members=region.members.Where(x=>x!=item.id).ToArray();
                Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,regionEdits:new(){Replacements=new[]{region}}),Is.True,error);
            } else {
                if(kind=="environment")item.environmentProfile="";else if(kind=="visibility")item.visibilityLayer="";else item.appearanceBindings=Array.Empty<AppearanceBinding>();
                Edit(journal,item);
            }
            Assert.That((string)Page(journal,kind)["membership"],Is.Not.EqualTo(generation));
            Assert.That((string)Page(journal,kind,id:Other)["membership"],Is.EqualTo(other));
            Assert.That(journal.Undo(),Is.True);Assert.That((string)Page(journal,kind)["membership"],Is.Not.EqualTo(generation));
        }
        [TestCase("appearance")][TestCase("visibility")][TestCase("environment")][TestCase("region")]
        public void DefinitionEditsInvalidatePagesEvenWhenMembersStayTheSame(string kind) {
            var journal=new RoomJournal(Document(kind,8));string generation=(string)Page(journal,kind)["membership"];
            if(kind=="appearance") {var p=journal.ReadAppearance(Resource);p.name="Changed";Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,appearanceEdits:new(){Replacements=new[]{p}}),Is.True);}
            if(kind=="visibility") {var p=journal.ReadVisibility(Resource);p.name="Changed";Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,visibilityEdits:new(){Replacements=new[]{p}}),Is.True);}
            if(kind=="environment") {var p=journal.ReadEnvironment(Resource);p.name="Changed";Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,environmentEdits:new(){Replacements=new[]{p}}),Is.True);}
            if(kind=="region") {var p=journal.ReadRegion(Resource);p.name="Changed";Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,regionEdits:new(){Replacements=new[]{p}}),Is.True);}
            Assert.That((string)Page(journal,kind)["membership"],Is.Not.EqualTo(generation));Assert.That(journal.Undo(),Is.True);
            Assert.That((string)Page(journal,kind)["membership"],Is.Not.EqualTo(generation));
        }
        [Test]public void NoReadBetweenRemovalAndUndoCannotReviveAnOldCursor() {
            var journal=new RoomJournal(Document());string cursor=(string)Page(journal)["next"];
            var item=journal.Read(Id(25));item.appearanceBindings=Array.Empty<AppearanceBinding>();Edit(journal,item);Assert.That(journal.Undo(),Is.True);
            Assert.That(Page(journal,cursor:cursor),Is.Null);
        }
        [Test]public void MotionLocalPaintAndUnrelatedBindingsKeepPagingButDoNotFreezeEditRevisions() {
            var journal=new RoomJournal(Document());string cursor=(string)Page(journal)["next"];int revision=journal.ObjectRevision(Id(25));
            Assert.That(journal.UpdatePlacement(Id(25),Vector3.up,Quaternion.identity),Is.True);var item=journal.Read(Id(25));item.appearanceBindings[0].tint="#0000FF";Edit(journal,item);
            Assert.That(journal.ObjectRevision(Id(25)),Is.GreaterThan(revision));Assert.That(Page(journal,cursor:cursor),Is.Not.Null);
            Edit(journal,new RoomObjectData{id=Id(90),kind=RoomObjectKind.Ball,appearanceBindings=new[]{new AppearanceBinding{appearanceId=Other}}});
            Assert.That(Page(journal,cursor:cursor),Is.Not.Null);
            var page=Page(journal,cursor:cursor);page["members"][0]="bad";
            Assert.That((string)Page(journal,cursor:cursor)["members"][0],Is.Not.EqualTo("bad"));
        }
        [Test]public void InvalidEditsKeepPagesAndEmptyDefinitionsDifferFromMissingOnes() {
            var journal=new RoomJournal(Document());string cursor=(string)Page(journal)["next"];var item=journal.Read(Id(25));item.appearanceBindings[0].appearanceId=new string('c',32);
            Assert.That(journal.Apply(new[]{item},Array.Empty<string>(),out _),Is.False);Assert.That(Page(journal,cursor:cursor),Is.Not.Null);
            var empty=Page(journal,id:Other);Assert.That((int)empty["total"],Is.Zero);Assert.That((string)empty["next"],Is.Empty);
            Assert.That(Page(journal,id:new string('c',32)),Is.Null);Assert.That(Page(journal,kind:"unknown"),Is.Null);
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,appearanceEdits:new(){Removals=new[]{Other}}),Is.True);
            Assert.That(Page(journal,id:Other),Is.Null);Assert.That(journal.Undo(),Is.True);Assert.That((string)Page(journal,id:Other)["membership"],Is.Not.EqualTo((string)empty["membership"]));
        }
        [Test]public void MultipleAppearanceAddressesCountOnceAndRemovingOnlyOneKeepsMembership() {
            var document=Document();var item=document.objects.Single(x=>x.id==Id(25));item.kind=RoomObjectKind.ImportedModel;item.modelHash=new string('c',64);
            item.appearanceBindings=new[]{new AppearanceBinding{appearanceId=Resource},new AppearanceBinding{appearanceId=Resource,kind="material",modelHash=item.modelHash,materialIndex=0}};
            var journal=new RoomJournal(document);var first=Page(journal);string cursor=(string)first["next"];
            Assert.That((int)first["total"],Is.EqualTo(34));item=journal.Read(item.id);item.appearanceBindings=item.appearanceBindings.Skip(1).ToArray();Edit(journal,item);
            Assert.That(Page(journal,cursor:cursor),Is.Not.Null);Assert.That((int)Page(journal)["total"],Is.EqualTo(34));
        }
        [Test]public void DiscardObservationInvalidationCannotReturnAnOldDefinitionRevision() {
            var journal=new RoomJournal(Document());var first=Page(journal);var fork=journal.Fork();var appearance=fork.ReadAppearance(Resource);appearance.name="Temporary";
            Assert.That(fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,appearanceEdits:new(){Replacements=new[]{appearance}}),Is.True);
            journal.InvalidateChangedObservations(fork);
            Assert.That(Page(journal,cursor:(string)first["next"]),Is.Null);
            Assert.That((int)Page(journal)["revision"],Is.EqualTo(journal.AppearanceRevision(Resource)));
        }
        [Test]public void CursorsCannotCrossDefinitionKindSessionForkReloadOrBeAltered() {
            var document=Document();document.visibilityLayers=new[]{new RoomVisibilityLayer{id=Resource,name="Same ID, other domain"}};
            foreach(var item in document.objects)item.visibilityLayer=Resource;
            var journal=new RoomJournal(document);string cursor=(string)Page(journal)["next"];
            Assert.That(Page(journal,id:Other,cursor:cursor),Is.Null);Assert.That(Page(journal,"visibility",cursor),Is.Null);
            Assert.That(Page(journal,cursor:cursor,session:"temporary"),Is.Null);Assert.That(Page(journal.Fork(),cursor:cursor),Is.Null);
            Assert.That(Page(new RoomJournal(journal.Snapshot()),cursor:cursor),Is.Null);
            Assert.That(Page(journal,cursor:"17"+cursor.Substring(cursor.IndexOf('.'))),Is.Null);
            Assert.That(Page(journal,cursor:"bad"),Is.Null);Assert.That(Page(journal,cursor:new string('x',37)),Is.Null);
        }
    }
}

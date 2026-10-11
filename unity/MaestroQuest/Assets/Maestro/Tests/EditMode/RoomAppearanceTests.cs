// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomAppearanceTests
    {
        const string Style="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Object="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        static RoomAppearance Appearance()=>new(){id=Style,name="Warm glass",style=new(){tint="#DDBBAA",renderMode="blend",opacity=.4f}};
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=Object,kind=RoomObjectKind.Block}}};
        [Test]public void RejectsMalformedStylesAndCoercedWireValues() {
            var a=Appearance();Assert.That(a.Validate(out _),Is.True);
            a.style.opacity=float.NaN;Assert.That(a.Validate(out _),Is.False);
            a=Appearance();a.style.renderMode="opaque";Assert.That(a.Validate(out _),Is.False);
            a=Appearance();a.style.tiling=new Vector2(0,1);Assert.That(a.Validate(out _),Is.False);
            a=Appearance();a.style.pattern.secondary="https://external/image";Assert.That(a.Validate(out _),Is.False);
            var room=Room();room.appearances=new[]{Appearance()};var wire=JObject.Parse(JsonUtility.ToJson(room));Assert.That(RoomAppearance.ValidWire(wire),Is.True);
            wire["appearances"][0]["style"]["opacity"]="0.4";Assert.That(RoomAppearance.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(room));wire["appearances"][0]["style"]["extra"]=1;Assert.That(RoomAppearance.ValidWire(wire),Is.False);
        }
        [Test]public void SharedDefinitionsAndBindingsAreOneAtomicUndoWithFreshRevisions() {
            var journal=new RoomJournal(Room());var obj=journal.Read(Object);obj.appearanceBindings=new[]{new AppearanceBinding{appearanceId=Style}};
            var a=Appearance();Assert.That(journal.Apply(new[]{obj},Array.Empty<string>(),out var error,appearanceEdits:new AppearanceEdits{Replacements=new[]{a}}),Is.True,error);
            int first=journal.AppearanceRevision(Style);a.style.tint="#000000";obj.appearanceBindings[0].appearanceId="changed";
            Assert.That(journal.ReadAppearance(Style).style.tint,Is.EqualTo("#DDBBAA"));Assert.That(journal.Read(Object).appearanceBindings[0].appearanceId,Is.EqualTo(Style));
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.ReadAppearance(Style),Is.Null);Assert.That(journal.Read(Object).appearanceBindings,Is.Empty);
            Assert.That(journal.Redo(),Is.True);Assert.That(journal.AppearanceRevision(Style),Is.GreaterThan(first));
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,appearanceEdits:new AppearanceEdits{Removals=new[]{Style}}),Is.False,"Referenced definitions cannot disappear");
            int oldObjectRevision=journal.ObjectRevision(Object);var changed=journal.ReadAppearance(Style);changed.style.tint="#0000FF";
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,appearanceEdits:new AppearanceEdits{Replacements=new[]{changed}}),Is.True,error);
            Assert.That(journal.ObjectRevision(Object),Is.GreaterThan(oldObjectRevision),"Shared style edits invalidate previously observed object colour");
        }
        [Test]public void TemporaryStylesKeepDiscardAndSnapshotApplyUseOneAuthority() {
            var room=Room();room.appearances=new[]{Appearance()};var saved=new RoomJournal(room);int revision=saved.AppearanceRevision(Style);
            var temporary=saved.Fork();var a=temporary.ReadAppearance(Style);a.style.opacity=.7f;
            Assert.That(temporary.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,appearanceEdits:new AppearanceEdits{Replacements=new[]{a}}),Is.True);
            Assert.That(saved.ReadAppearance(Style).style.opacity,Is.EqualTo(.4f));saved.InvalidateChangedObservations(temporary);Assert.That(saved.AppearanceRevision(Style),Is.GreaterThan(revision));
            Assert.That(saved.ApplySnapshot(temporary.Snapshot(),out var error),Is.True,error);Assert.That(saved.ReadAppearance(Style).style.opacity,Is.EqualTo(.7f));
            Assert.That(saved.Undo(),Is.True);Assert.That(saved.ReadAppearance(Style).style.opacity,Is.EqualTo(.4f));
        }
        [Test]public void DormantModelBindingsKeepTheirExactAssetAndDuplicateAddressesRefuse() {
            var room=Room();room.appearances=new[]{Appearance()};var obj=room.objects[2];obj.kind=RoomObjectKind.ImportedModel;obj.modelHash=new string('c',64);
            obj.appearanceBindings=new[]{new AppearanceBinding{appearanceId=Style,kind="material",modelHash=new string('d',64),materialIndex=2}};
            Assert.That(room.Validate(out var error),Is.True,error);Assert.That(room.Copy().objects[2].appearanceBindings[0].modelHash,Is.EqualTo(new string('d',64)));
            obj.appearanceBindings=obj.appearanceBindings.Concat(obj.appearanceBindings.Select(x=>x.Copy())).ToArray();Assert.That(room.Validate(out _),Is.False);
        }
        [Test]public void SharedAppearanceClaimsAllowTheWholeRoomAndRefuseOverflow() {
            string[] members=Enumerable.Range(0,66).Select(i=>i.ToString("x32")).ToArray();
            var capability=new AppearanceSaveCapability();var args=capability.Example;args["id"]=Style;args["revision"]=1;args["members"]=new JArray(members);
            Assert.That(capability.Claims(args).Select(x=>x.Target),Is.EquivalentTo(members));
            var program=new JObject{["version"]=3,["entry"]="main",["resources"]=new JArray(members),["state"]=new JArray(),["events"]=new JArray(),
                ["functions"]=new JArray(new JObject{["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),["body"]=new JArray()})};
            Assert.That(BehaviourProgram.TryParse(program.ToString(),out _,out var error),Is.True,error);program["resources"]=new JArray(members.Append("book"));
            Assert.That(BehaviourProgram.TryParse(program.ToString(),out _,out _),Is.False);
        }
        [Test]public void SaveReloadPreservesStylesAndRejectsUnknownFutureAppearanceData() {
            string directory=Path.Combine(Path.GetTempPath(),"maestro-appearances-"+Guid.NewGuid().ToString("N"));
            try {
                var room=Room();room.appearances=new[]{Appearance()};room.objects[2].appearanceBindings=new[]{new AppearanceBinding{appearanceId=Style}};
                var storage=new RoomStorage(directory);Assert.That(storage.Save(room,out var error),Is.True,error);
                var reopened=new RoomStorage(directory);var loaded=reopened.Load(out error);Assert.That(loaded,Is.Not.Null,error);
                Assert.That(loaded.appearances[0].style.opacity,Is.EqualTo(.4f));Assert.That(loaded.objects[2].appearanceBindings[0].appearanceId,Is.EqualTo(Style));
                var path=Path.Combine(directory,RoomStorage.FileName);var wire=JObject.Parse(File.ReadAllText(path));wire["appearances"][0]["version"]=99;File.WriteAllText(path,wire.ToString());
                var future=new RoomStorage(directory);Assert.That(future.Load(out _),Is.Null);Assert.That(future.ReadOnly,Is.True);Assert.That((int)JObject.Parse(File.ReadAllText(path))["appearances"][0]["version"],Is.EqualTo(99));
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}

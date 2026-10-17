// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;using System.IO;using System.Linq;
using Maestro.Quest.Creation;using Maestro.Quest.Programs;using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;using NUnit.Framework;using UnityEngine;
namespace Maestro.Quest.Tests {public sealed class RecipePatternTests {
 [Test] public void RecipePatternSharedCasesUseTheNativeCreationContract(){
  var rows=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/pattern-contract.json")));
  foreach(var row in rows){bool valid=(bool)row["valid"];Assert.That(CapabilityArguments.Validate(row["recipe"],CapabilitySchema.RecipeSchema(),out var error),Is.EqualTo(valid),(string)row["name"]+": "+error);if(valid)Assert.That(JsonUtility.FromJson<RoomRecipe>(row["recipe"].ToString()).Validate(out error),Is.True,error);}
 }
 [Test] public void RecipePatternLegacyDefaultsAndCopiesKeepTheirMeaning(){
  var old=RecipeTemplates.BoxRobot(false);var raw=JObject.Parse(JsonUtility.ToJson(old));foreach(var part in (JArray)raw["parts"])((JObject)part).Remove("pattern");
  var loaded=JsonUtility.FromJson<RoomRecipe>(raw.ToString());Assert.That(loaded.Validate(out var error),Is.True,error);Assert.That(loaded.parts.All(p=>p.pattern.kind=="solid"),Is.True);
  loaded.parts[0].pattern=new RecipePattern{kind="checker",plane="xz",columns=8,rows=8,secondary="#123456"};var copy=loaded.Copy();copy.parts[0].pattern.columns=4;Assert.That(loaded.parts[0].pattern.columns,Is.EqualTo(8));Assert.That(copy.Validate(out error),Is.True,error);
 }
 [Test] public void RecipePatternCurrentRoomProtectsPatternsFromOlderReaders(){
  string dir=Path.Combine(Path.GetTempPath(),"MaestroPattern-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try {var recipe=RecipeTemplates.BoxRobot(false);recipe.parts[0].pattern=new RecipePattern{kind="checker",columns=8,rows=8};var room=new RoomDocument{objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Assembly,recipe=recipe}}};
   room.version=13;Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;Assert.That(new RoomStorage(dir).Save(room,out var error),Is.True,error);Assert.That(new RoomStorage(dir).Load(out _).objects[2].recipe.parts[0].pattern.columns,Is.EqualTo(8));
   var old=new VersionedRoomFile<RoomDocument>(dir,"room",4*1024*1024,r=>r.Validate(out _),r=>r.Copy(),RoomStorage.Normalize,r=>r.version=13,version:13);Assert.That(old.Load(out _),Is.Null);Assert.That(old.ReadOnly,Is.True);
  }finally{Directory.Delete(dir,true);}
 }
}}

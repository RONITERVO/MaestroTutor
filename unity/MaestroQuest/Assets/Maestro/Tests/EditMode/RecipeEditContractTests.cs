// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class RecipeEditContractTests
 {
  [Test] public void RecipePatchesShareStructuralValidationAndTheExistingWireBounds(){var entries=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/recipe-edit-contract.json")));foreach(var entry in entries){var call=(JObject)entry["call"];bool valid=BehaviourCatalog.TryCall((string)call["id"],1,(JObject)call["arguments"],out _,out _);Assert.That(valid,Is.EqualTo((bool)entry["valid"]),(string)entry["name"]);if(valid)Assert.That(RoomCapabilityCatalog.ValidCall(call),Is.True);}}
 }
}

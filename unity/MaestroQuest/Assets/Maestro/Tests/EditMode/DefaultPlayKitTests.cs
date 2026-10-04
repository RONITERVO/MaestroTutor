// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class DefaultPlayKitTests
    {
        [TestCase("SmallFort", "small-fort", 16)]
        [TestCase("Spinner", "spinner", 2)]
        public void IncludedConstructionIsPinnedEditableSourceWithFreshMembers(string resource, string fixture, int count)
        {
            var source = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Maestro/Tests/Fixtures/program-" + fixture + ".json")));
            var module = JObject.Parse(Resources.Load<TextAsset>("Programs/Modules/" + resource).text);
            ProgramModuleLibrary.Validate(module);
            Assert.That(JToken.DeepEquals(module, source["imports"][0]["module"]), Is.True);
            Assert.That((string)source["imports"][0]["hash"], Is.EqualTo(ProgramModules.Hash(module)));
            Assert.That(BehaviourProgram.TryParse(source.ToString(Newtonsoft.Json.Formatting.None), out var program, out var error), Is.True, error);
            var machine = new ProgramMachine(program, null);
            Assert.That(machine.Advance(out var call), Is.EqualTo(ProgramYield.Action), machine.Error);
            Assert.That(call.Definition.Id, Is.EqualTo("object.batch.create"));
            Assert.That(call.Resources, Is.Empty);
            var args = module["program"]["functions"][1]["body"][0]["arguments"];
            var batch = JsonUtility.FromJson<CreationBatch>(args.ToString());
            Assert.That(batch.Prepare(out var first, out error), Is.True, error);
            Assert.That(batch.Prepare(out var second, out error), Is.True, error);
            Assert.That(first.Length, Is.EqualTo(count));
            Assert.That(first.Select(x => x.id).Intersect(second.Select(x => x.id)), Is.Empty);
            Assert.That(first.All(x => !x.recipe.playing), Is.True);
            if (resource == "Spinner")
            {
                Assert.That(first[1].connections.Single().connected, Is.EqualTo(first[0].id));
                Assert.That(second[1].connections.Single().connected, Is.EqualTo(second[0].id));
                Assert.That(first[1].connections.Single().drive.mode, Is.EqualTo("passive"));
                Assert.That(first[1].connections.Single().limits.enabled, Is.False);
            }
            else Assert.That(first.All(x => x.connections == null || x.connections.Length == 0), Is.True);
            var ids = new JArray(first.Select(x => x.id));
            Assert.That(machine.CompleteAction(new JObject { ["objectIds"] = ids, ["slots"] = new JArray(batch.blueprint.pieces.Select(x => x.slot)), ["temporary"] = false }, out error), Is.True, error);
            Assert.That(machine.Advance(out _), Is.EqualTo(ProgramYield.Completed), machine.Error);
            Assert.That((JArray)machine.Locals["pieces"].Value, Is.EqualTo(ids));
        }
    }
}

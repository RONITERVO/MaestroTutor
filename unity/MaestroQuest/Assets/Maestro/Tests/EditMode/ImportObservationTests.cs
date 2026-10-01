// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Imports;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ImportObservationTests
    {
        [TestCase("My dancing robot.glb")]
        [TestCase("A quoted \"motion\".glb")]
        [TestCase("Maestro 😀 maailma 世界")]
        public void OrdinaryDisplayNamesRemainUnchanged(string value)=>Assert.That(ImportObservation.Text(value),Is.EqualTo(value));
        [TestCase('\u2028')]
        [TestCase('\u2029')]
        [TestCase('\"')]
        [TestCase('\\')]
        public void EscapedMetadataRespectsSerializedBudget(char value)
        {
            string result=ImportObservation.Text(new string(value,1000));Assert.That(result,Is.Not.Empty);Assert.That(JsonConvert.ToString(result).Length,Is.LessThanOrEqualTo(ImportObservation.TextJsonBudget));Assert.That(result.Length,Is.LessThanOrEqualTo(128));
        }
        [Test] public void CutsAtUnicodePairsAndNeverReturnsControlCharactersOrOrphans()
        {
            string raw=new string('a',125)+"😀";Assert.That(ImportObservation.Text(raw),Is.EqualTo(new string('a',125)));
            string result=ImportObservation.Text(string.Concat(Enumerable.Repeat("😀",100)));Assert.That(result.Length%2,Is.Zero);Assert.That(JsonConvert.ToString(result).Length,Is.LessThanOrEqualTo(128));
            Assert.That(ImportObservation.Text("a\ud800b\udfffc\n\t<>"),Is.EqualTo("abc  "));Assert.That(ImportObservation.Text(null),Is.Empty);Assert.That(ImportObservation.Text(new string('\ud800',1000)+"later"),Is.Empty,"Malformed prefixes do not cause an unbounded metadata scan");
        }
    }
}

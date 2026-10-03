// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class RoomAgentWireTests
    {
        [Test] public void UninspectedRulesKeepAbsentMemoryNullInsteadOfInventingAnInvalidScope()
        {
            var state=new RoomAgentState {objects=Array.Empty<RoomAgentObject>(),rules=new RuleView()};
            var wire=JObject.Parse(RoomAgentWire.Serialize(state));
            Assert.That(wire["rules"]["selected"].Type,Is.EqualTo(JTokenType.Null));Assert.That(wire["rules"]["memory"].Type,Is.EqualTo(JTokenType.Null));
            state.rules.memory=new ProgramMemoryView {sessionId=Guid.NewGuid().ToString("N"),revision="",programId="",error=""};
            wire=JObject.Parse(RoomAgentWire.Serialize(state));Assert.That((string)wire["rules"]["memory"]["sessionId"],Is.EqualTo(state.rules.memory.sessionId));
            Assert.That((bool)wire["rules"]["memory"]["ready"],Is.False,"A real pending memory observation must not disappear");
        }
    }
}

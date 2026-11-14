// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class RoomAgentWireTests
    {
        [Test] public void ContextMetadataSerializesExactlyAndLegacyAbsenceRemainsAbsent()
        {
            var state=new RoomAgentState{objects=Array.Empty<RoomAgentObject>(),objectOverview=new RoomObjectOverview()};
            var wire=JObject.Parse(RoomAgentWire.Serialize(state));
            Assert.That(JToken.DeepEquals(wire["objectOverview"],JObject.Parse("{\"version\":1,\"scope\":\"context\",\"exhaustive\":false,\"limit\":4}")),Is.True);
            state.objectOverview=null;wire=JObject.Parse(RoomAgentWire.Serialize(state));Assert.That(wire.ContainsKey("objectOverview"),Is.False);
        }
        [Test] public void InspectedObjectMetadataKeepsNullsAndMovementContractWithoutAnInventory()
        {
            var item=new RoomAgentObject{id="maestro",objectRevision=3,movement=new AvatarMovementSettings{distance=.3f,speed=.65f}};
            var state=new RoomAgentState{objects=Array.Empty<RoomAgentObject>(),inspection=new RoomInspection{id=item.id,objectRevision=3,item=item}};
            var wire=JObject.Parse(RoomAgentWire.Serialize(state));Assert.That(wire["objects"].Count(),Is.Zero);
            Assert.That((string)wire["inspection"]["item"]["id"],Is.EqualTo("maestro"));Assert.That((double)wire["inspection"]["item"]["movement"]["distance"],Is.EqualTo(.3));
            Assert.That(wire["inspection"]["recipe"].Type,Is.EqualTo(JTokenType.Null));item.movement=null;
            wire=JObject.Parse(RoomAgentWire.Serialize(state));Assert.That(wire["inspection"]["item"]["movement"].Type,Is.EqualTo(JTokenType.Null));
            state.inspection.item=null;wire=JObject.Parse(RoomAgentWire.Serialize(state));Assert.That(((JObject)wire["inspection"]).ContainsKey("item"),Is.False);
        }
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

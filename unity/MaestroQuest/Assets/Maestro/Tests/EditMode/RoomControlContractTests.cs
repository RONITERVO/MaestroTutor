// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomControlContractTests
    {
        [Serializable] sealed class Case { public string name,json; public bool valid; }
        [Serializable] sealed class Fixtures { public Case[] cases; }
        [Test] public void WirePreservesAbsentValuesAndDecimalMovementEndpoints()
        {
            var state=new RoomAgentState {objects=new[] {new RoomAgentObject {id="book"},new RoomAgentObject {id="maestro",movement=new AvatarMovementSettings {distance=2.5f,speed=1.2f}}}};
            string json=RoomAgentWire.Serialize(state);
            Assert.That(json,Does.Contain("\"inspection\":null"));Assert.That(json,Does.Contain("\"rules\":null"));
            Assert.That(json,Does.Contain("\"avatar\":null"));Assert.That(json,Does.Contain("\"movement\":null"));Assert.That(json,Does.Contain("\"speed\":1.2}"));
            state.inspection=new RoomInspection {id="book"};state.rules=new Maestro.Quest.Rules.RuleView();
            json=RoomAgentWire.Serialize(state);Assert.That(json,Does.Contain("\"recipe\":null"));Assert.That(json,Does.Contain("\"selected\":null"));
        }
        [Test] public void NativeControlsMatchTheSharedWireExamples()
        {
            var examples=JsonUtility.FromJson<Fixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/room-controls.json")));
            Assert.That(examples.cases.Length,Is.GreaterThanOrEqualTo(30));
            foreach(var sample in examples.cases) Assert.That(RoomControls.ValidWire(sample.json),Is.EqualTo(sample.valid),sample.name);
            Assert.That(RoomControls.ValidPhysics(new ObjectPhysicsSettings {mode="solid",shape="box",mass=float.NaN}),Is.False);
            Assert.That(RoomControls.ValidMovement(new AvatarMovementSettings {distance=1,speed=float.PositiveInfinity}),Is.False);
        }
    }
}

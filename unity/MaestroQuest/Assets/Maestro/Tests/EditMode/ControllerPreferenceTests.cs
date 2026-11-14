// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class ControllerPreferenceTests
    {
        [Test] public void RejectsSharedSticksInvalidCommandsAndUnboundedValues()
        {
            var prefs=new ControllerPreferences(); Assert.That(prefs.Validate(),Is.True);
            prefs.userStick=MovementStick.Right; Assert.That(prefs.Validate(),Is.False);
            prefs.userStick=MovementStick.None; Assert.That(prefs.Validate(),Is.True);
            prefs.deadZone=float.NaN; Assert.That(prefs.Validate(),Is.False); prefs.deadZone=.2f;
            prefs.buttons[0].command=(ControllerCommand)77; Assert.That(prefs.Validate(),Is.False);
            prefs.buttons[0]=new ControllerBinding { command=ControllerCommand.Sequence,sequenceId="../../file" }; Assert.That(prefs.Validate(),Is.False);
            prefs.buttons[0].sequenceId=Guid.NewGuid().ToString("N"); Assert.That(prefs.Validate(),Is.True);
            var copy=prefs.Copy(); copy.buttons[0].sequenceId=Guid.NewGuid().ToString("N"); Assert.That(copy.buttons[0].sequenceId,Is.Not.EqualTo(prefs.buttons[0].sequenceId));
        }
        [Test] public void RequiresNeutralAfterTrackingLossRebindingAndInvalidAxes()
        {
            var gate=new NeutralMovementGate(); Assert.That(gate.Read(Vector2.up,true,.2f),Is.EqualTo(Vector2.zero));
            gate.Read(Vector2.zero,true,.2f); Assert.That(gate.Read(Vector2.one,true,.2f).magnitude,Is.EqualTo(1).Within(.001f));
            gate.Read(Vector2.up,false,.2f); Assert.That(gate.Read(Vector2.up,true,.2f),Is.EqualTo(Vector2.zero));
            gate.Read(Vector2.zero,true,.2f); gate.Read(new Vector2(float.NaN,0),true,.2f); Assert.That(gate.Read(Vector2.up,true,.2f),Is.EqualTo(Vector2.zero));
            gate.Read(Vector2.zero,true,.2f); Assert.That(gate.Read(Vector2.up*.6f,true,.2f).magnitude,Is.EqualTo(.5f).Within(.001f));
            gate.Reset(); Assert.That(gate.Read(Vector2.up,true,.2f),Is.EqualTo(Vector2.zero));
        }
        [Test] public void SavedBindingsRecoverBackupAndPreserveUnknownVersions()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroControls-"+Guid.NewGuid().ToString("N"));
            try
            {
                var storage=new ControllerPreferenceStorage(directory); var first=storage.Load(out _);
                Assert.That(storage.Save(first,out var error),Is.True,error);
                var second=first.Copy(); second.userSpeed=1; Assert.That(storage.Save(second,out error),Is.True,error);
                var path=Path.Combine(directory,"controls.v2.json"); File.WriteAllText(path,"broken");
                var recovered=new ControllerPreferenceStorage(directory); Assert.That(recovered.Load(out var message).userSpeed,Is.EqualTo(.65f)); Assert.That(message,Does.Contain("backup"));
                File.WriteAllText(path,"{\"version\":3}");
                var newer=new ControllerPreferenceStorage(directory); newer.Load(out message); Assert.That(newer.ReadOnly,Is.True);
                Assert.That(newer.Save(first,out _),Is.False); Assert.That(File.ReadAllText(path),Is.EqualTo("{\"version\":3}"));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
    }
}

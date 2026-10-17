// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    public enum MovementStick { None = -1, Left = 0, Right = 1 }
    public enum ControllerCommand { None, SnapLeft, SnapRight, Sequence }
    [Serializable] public sealed class ControllerBinding
    {
        public ControllerCommand command;
        public string sequenceId;
        public ControllerBinding Copy() => (ControllerBinding)MemberwiseClone();
    }
    [Serializable] public sealed class ControllerPreferences
    {
        public int version = 2;
        public MovementStick avatarStick = MovementStick.Right, userStick = MovementStick.Left;
        public float deadZone = .2f, userSpeed = .65f;
        // X, A, left stick click, right stick click. B/Y and system buttons remain reserved.
        public ControllerBinding[] buttons = { new() { command = ControllerCommand.SnapLeft }, new() { command = ControllerCommand.SnapRight }, new(), new() };
        public ControllerPreferences Copy() => new() { version=version, avatarStick=avatarStick, userStick=userStick, deadZone=deadZone, userSpeed=userSpeed, buttons=buttons.Select(x => x.Copy()).ToArray() };
        public bool Validate()
        {
            return version == 2 && Enum.IsDefined(typeof(MovementStick),avatarStick) && Enum.IsDefined(typeof(MovementStick),userStick) &&
                (avatarStick == MovementStick.None || userStick == MovementStick.None || avatarStick != userStick) &&
                float.IsFinite(deadZone) && deadZone >= .1f && deadZone <= .4f && float.IsFinite(userSpeed) && userSpeed >= .2f && userSpeed <= 1.2f &&
                buttons != null && buttons.Length == 4 && buttons.All(x => x != null && Enum.IsDefined(typeof(ControllerCommand),x.command) &&
                    (x.command == ControllerCommand.Sequence ? Guid.TryParseExact(x.sequenceId,"N",out _) : string.IsNullOrEmpty(x.sequenceId)));
        }
    }
    public sealed class ControllerPreferenceStorage
    {
        readonly VersionedRoomFile<ControllerPreferences> file;
        public bool ReadOnly => file.ReadOnly;
        public ControllerPreferenceStorage(string directory) => file = new(directory,"controls",8192,x => x.Validate(),x => x.Copy(),null,x => x.version=2);
        public ControllerPreferences Load(out string message) => file.Load(out message) ?? new ControllerPreferences();
        public bool Save(ControllerPreferences value,out string error) => file.Save(value,out error);
    }
    public struct ControllerFrame
    {
        public bool leftTracked, rightTracked, busy, manipulating;
        public Vector2 leftStick, rightStick;
        public bool x, a, leftClick, rightClick;
        public bool Tracked(MovementStick stick) => stick == MovementStick.Left ? leftTracked : stick == MovementStick.Right && rightTracked;
        public Vector2 Axis(MovementStick stick) => stick == MovementStick.Left ? leftStick : stick == MovementStick.Right ? rightStick : Vector2.zero;
        public bool Button(int index) => index == 0 ? x : index == 1 ? a : index == 2 ? leftClick : rightClick;
    }
    /// <summary>A control must return to neutral after activation, rebinding or interruption.</summary>
    public sealed class NeutralMovementGate
    {
        bool ready;
        public void Reset() => ready=false;
        public Vector2 Read(Vector2 input,bool available,float deadZone)
        {
            if (!available || !float.IsFinite(input.x) || !float.IsFinite(input.y)) { Reset(); return Vector2.zero; }
            float size = input.magnitude;
            if (size <= deadZone) { ready=true; return Vector2.zero; }
            return ready ? input.normalized * Mathf.Clamp01((size-deadZone)/(1-deadZone)) : Vector2.zero;
        }
    }
}

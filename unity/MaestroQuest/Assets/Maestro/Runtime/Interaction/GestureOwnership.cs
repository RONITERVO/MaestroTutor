// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Interaction
{
    public enum GestureTarget { None, Page, Object }

    /// <summary>A pinch owns its original target until release; tracking recovery requires a fresh gesture.</summary>
    public sealed class GestureOwnership
    {
        bool armed, wasPressed;
        public GestureTarget Target { get; private set; }

        public GestureTarget Update(bool pressed, GestureTarget pointedTarget)
        {
            if (!pressed) { armed = true; wasPressed = false; Target = GestureTarget.None; }
            else if (armed && !wasPressed) { Target = pointedTarget; wasPressed = true; }
            return Target;
        }

        public void Cancel() { armed = false; wasPressed = false; Target = GestureTarget.None; }
    }
}

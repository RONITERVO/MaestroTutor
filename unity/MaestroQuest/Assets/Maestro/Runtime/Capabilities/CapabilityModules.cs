// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    /// <summary>Explicit registrations are retained by IL2CPP; no assembly scanning or downloaded code.</summary>
    internal static class CapabilityModules
    {
        public static readonly IReadOnlyList<CapabilityModule> All=Array.AsReadOnly(new CapabilityModule[] {
            new RecordedAnimationCapability(),
            new GestureCapability(),
            new UpperBodyGestureCapability(),
            new WaitCapability(),
            new CreatePrimitiveCapability(),
            new CreateRecipeCapability(),
            new MoveObjectCapability(),
            new RotateObjectCapability(),
            new ResizeObjectCapability(),
            new PaintObjectCapability(),
            new DeleteObjectCapability(),
            new PhysicsImpulseCapability(),
            new PhysicsStopCapability(),
            new ThrowRecordingCapability(),
            new LookAtUserCapability(),
            new FollowUserCapability(),
            new EmbeddedAnimationCapability(),
            new LibraryAnimationCapability(),
            new RecipeAnimationCapability(),
        });
    }
}

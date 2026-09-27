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
            new AnimationCapability("animation.recording.play",RuleActionKind.RecordedAnimation,"Recorded animation","target.exists target.unheld authoring.inactive recording.available"),
            new AnimationCapability("avatar.gesture.play",RuleActionKind.Gesture,"Gesture","target.exists target.unheld authoring.inactive avatar.available"),
            new AnimationCapability("avatar.gesture.upperBody",RuleActionKind.UpperBodyGesture,"Upper-body gesture","target.exists target.unheld authoring.inactive avatar.available"),
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
            new AnimationCapability("object.recording.throw",RuleActionKind.ThrowRecording,"Throw recording","target.exists target.unheld authoring.inactive recording.twoFrames rigidBody.dynamic physics.running"),
            new AnimationCapability("avatar.look.user",RuleActionKind.LookAtUser,"Look at user","target.exists target.unheld authoring.inactive avatar.spatialReady"),
            new AnimationCapability("avatar.follow.user",RuleActionKind.FollowUser,"Follow user","target.exists target.unheld authoring.inactive avatar.spatialReady physics.running navigation.floorReady"),
            new AnimationCapability("animation.embedded.play",RuleActionKind.ImportedClip,"Imported clip","target.exists target.unheld authoring.inactive model.loaded embeddedClip.available"),
            new AnimationCapability("animation.library.play",RuleActionKind.LibraryMotion,"Library motion","target.exists target.unheld authoring.inactive model.loaded motion.available rig.compatible"),
            new AnimationCapability("animation.recipe.play",RuleActionKind.RecipeAnimation,"Recipe animation","target.exists target.unheld authoring.inactive recipe.tracksAvailable"),
        });
    }
}

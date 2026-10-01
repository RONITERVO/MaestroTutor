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
            new AnimationPlayCapability(),new AnimationAuthoringCapability(),new AnimationRecordingCapability(),new AnimationPosingCapability(),new ModelLibraryCapability(),new ModelImportCapability(),new MotionBatchCapability(),new AvatarModelCapability(),
            new WaitCapability(),
            new RoomSessionCapability(),new ControllerConfigurationCapability(),new ControllerModeCapability(),
            new WorkspaceRetentionCapability("inspect"),new WorkspaceRetentionCapability("export"),new WorkspaceRetentionCapability("reviewRemoval"),new WorkspaceRetentionCapability("remove"),
            new WorkspaceEvidenceCapability("inspect"),new WorkspaceEvidenceCapability("export"),new WorkspaceEvidenceCapability("remove"),
            new WorkspaceHistoryCapability(false),new WorkspaceHistoryCapability(true),
            new WorkspaceExportCapability(),
            new WorkspaceSelectCapability(),
            new WorkspaceSelectPreviousCapability(),
            new WorkspaceCancelSelectionCapability(),
            new WorkspaceActivateCapability(),
            new WorkspaceCancelActivationCapability(),
            new WorkspacePrepareReviewCapability(),
            new WorkspaceCompleteReviewCapability(),
            new WorkspaceCancelReviewCapability(),
            new WorkspaceRecoveryCapability("inspect"),new WorkspaceRecoveryCapability("select"),new WorkspaceRecoveryCapability("commit"),new WorkspaceRecoveryCapability("cancel"),
            new PublishProgramModuleCapability(),
            new ImportProgramModuleCapability(),
            new RemoveProgramModuleCapability(),
            new CreateObjectCapability(),
            new MoveObjectCapability(),
            new RotateObjectCapability(),
            new ResizeObjectCapability(),
            new PaintObjectCapability(),
            new DeleteObjectCapability(),
            new PhysicsSettingsCapability(),new AvatarMovementSettingsCapability(),new AvatarWalkSettingsCapability(),
            new PhysicsImpulseCapability(),
            new PhysicsStopCapability(),
            new ThrowRecordingCapability(),
            new LookAtUserCapability(),
            new FollowUserCapability(),
        });
    }
}

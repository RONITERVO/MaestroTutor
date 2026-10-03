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
            new AnimationPlayCapability(),new AnimationAuthoringCapability(),new AnimationRecordingCapability(),new AnimationPosingCapability(),new ModelLibraryCapability(),new ModelImportCapability(),new MotionBatchCapability(),new IncludedMotionsCapability(),new AvatarModelCapability(),
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
            new ProgramMemoryCapability(),
            new PublishProgramModuleCapability(),new CaptureConstructionCapability(),
            new ImportProgramModuleCapability(),
            new RemoveProgramModuleCapability(),
            new CreateObjectCapability(),new BatchCreationCapability(),new DrawingToolCapability(),new DrawingTipCapability(),new HingeCapability(),new DrawingSurfaceCapability(),new DrawingEditCapability(),new DrawingResolveCapability(),new RecipeEditCapability(),
            new MoveObjectCapability(),new LayoutCapability(),new ConstructionSelectionCapability(),new StructureSaveCapability(),new StructureResetCapability(),new StructureForgetCapability(),
            new RotateObjectCapability(),
            new ResizeObjectCapability(),
            new PaintObjectCapability(),
            new DeleteObjectCapability(),
            new CollisionCapability(),new PhysicsSettingsCapability(),new AvatarMovementSettingsCapability(),new AvatarWalkSettingsCapability(),
            new SurfacePlacementCapability(),new RoomEnvironmentCapability(),new PhysicsSimulationCapability(),new PhysicsImpulseCapability(),
            new PhysicsStopCapability(),new LaunchObjectCapability(),
            new ThrowRecordingCapability(),
            new LookAtUserCapability(),
            new FollowUserCapability(),new HoldObjectCapability(),
        });
    }
}

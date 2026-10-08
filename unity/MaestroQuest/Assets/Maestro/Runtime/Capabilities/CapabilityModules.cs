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
            new AudioSourceCapability(),new AudioEmitterCapability(),new AudioPlayCapability(),new AudioStartCapability(),new AudioControlCapability(),
            new WaitCapability(),new RoomViewCapability(),new RoomToolsCapability(),new RoomToolRecoveryCapability(),new WorldViewpointCapability(),new WorldLightingCapability(),new WorldTimeCapability(),new WorldTimeCapability(true),new WorldWeatherCapability(),
            new RoomSessionCapability(),new ControllerConfigurationCapability(),new ControllerModeCapability(),new WorldPresentationCapability(),new LayerPresentationCapability(),
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
            new CreateObjectCapability(),new BatchCreationCapability(),new DrawingToolCapability(),new DrawingTipCapability(),new ContainerCapability(),new ContainerTransferCapability(),new HeightFieldCapability(),new SculptFieldCapability(),new HeightFieldTransferCapability(),new MaterialStoreCapability(),new MaterialTransferCapability(),new PackMaterialCapability(),new MaterialPackToolCapability(),new SculptTipCapability(),new SculptToolCapability(),new SculptResolveCapability(),new SnapPointCapability(),new SnapConstructionCapability(),new ConnectionCapability(),new DrawingSurfaceCapability(),new DrawingEditCapability(),new DrawingResolveCapability(),new RecipeEditCapability(),
            new MoveObjectCapability(),new LayoutCapability(),new GroupTransformCapability(),new ConstructionSelectionCapability(),new ConstructionManipulationCapability(),new ConstructionSnappingCapability(),new StructureSaveCapability(),new StructureResetCapability(),new StructureForgetCapability(),
            new RotateObjectCapability(),
            new ResizeObjectCapability(),
            new PaintObjectCapability(),new AppearanceSaveCapability(),new AppearanceRemoveCapability(),new AppearanceBindCapability(),new VisibilityLayerSaveCapability(),new VisibilityLayerRemoveCapability(),new VisibilityAssignCapability(),
            new DeleteObjectCapability(),
            new CollisionCapability(),new PhysicsSettingsCapability(),new WaterTraversalCapability(),new AvatarMovementSettingsCapability(),new AvatarWalkSettingsCapability(),
            new ScanDrawingCapability(),new ScanPlacementCapability(),new SurfacePlacementCapability(),new RoomEnvironmentCapability(),new PhysicsSimulationCapability(),new PhysicsEnvironmentCapability(),new EnvironmentProfileSaveCapability(),new EnvironmentProfileRemoveCapability(),new EnvironmentAssignCapability(),new PhysicsImpulseCapability(),
            new PhysicsStopCapability(),new LaunchObjectCapability(),new CatchObjectCapability(),
            new ThrowRecordingCapability(),
            new LookAtUserCapability(),
            new FollowUserCapability(),new HoldObjectCapability(),
        });
    }
}

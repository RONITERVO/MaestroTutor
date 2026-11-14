// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    public sealed class MaestroModelImporter : AssetPostprocessor
    {
        void OnPostprocessMaterial(Material material)
        {
            if (!assetPath.EndsWith("/Avatars/DefaultMaestro.fbx")) return;
            // Our Blender authoring script stores linear pigment values. The FBX
            // importer leaves those unchanged; material color properties expect sRGB.
            if (material.HasProperty("_Color")) material.color = material.color.gamma;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.EndsWith("/Avatars/DefaultMaestro.fbx")) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importLights = false;
            importer.importCameras = false;
            importer.meshCompression = ModelImporterMeshCompression.Low;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.EndsWith("/Avatars/DefaultMaestro.fbx")) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clip.name.Split('|').Last();
                clip.loopTime = clip.name != "Greeting";
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalOrientation = true;
            }
            importer.clipAnimations = clips;
        }
    }
}

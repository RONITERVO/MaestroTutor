// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Editor
{
    internal static class QuestRoomProbeModel
    {
        internal static JObject Seed(GameObject root,string path)
        {
            var editor=root.GetComponent<WorkspaceHost>()?.Current?.Editor;
            if(!editor)throw new InvalidOperationException("Open the probe workspace before seeding its model.");
            var asset=ModelLibrary.Inspect("Original building.glb",ModelLibrary.ReadBounded(path));
            editor.Models.SaveAsync(asset).GetAwaiter().GetResult();
            if(!editor.CreateImportedModel(asset.Hash,out var target,out var error))throw new InvalidOperationException(error);
            return new JObject{["hash"]=asset.Hash,["target"]=target,["boundary"]="Explicit original synthetic GLB imported through the real library and object load path. No user asset, file picker or physical headset evidence."};
        }
    }
}

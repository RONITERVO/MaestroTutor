// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Editor {
    /// <summary>Explicit fresh-session synthetic pixels, never selected/private media.</summary>
    internal static class QuestRoomProbeImage {
        internal static JObject Seed(GameObject root){var editor=root.GetComponent<WorkspaceHost>()?.Current?.Editor;if(!editor)throw new InvalidOperationException("Open the probe workspace before seeding its image.");var texture=new Texture2D(64,64,TextureFormat.RGBA32,false,false);byte[] bytes;
            try{var pixels=new Color32[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=((x/8+y/8)&1)==0?new Color32(30,90,210,255):new Color32(220,235,255,255);texture.SetPixels32(pixels);texture.Apply();bytes=texture.EncodeToPNG();}finally{UnityEngine.Object.DestroyImmediate(texture);}
            var asset=ImageLibrary.Inspect("Blue tiles.png",bytes);editor.Images.SaveAsync(asset).GetAwaiter().GetResult();return new JObject{["hash"]=asset.Hash,["name"]=asset.Name,["width"]=64,["height"]=64,["boundary"]="Synthetic PNG saved through the real private ImageLibrary. No picker interaction or user picture."};
        }
    }
}

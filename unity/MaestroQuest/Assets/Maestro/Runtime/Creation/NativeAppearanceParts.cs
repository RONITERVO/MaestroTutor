// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation {
    // Stable native component addresses. Never use child names or traversal order.
    // Adding a renderer requires a declared address and exact ownership resolver.
    internal static class NativeAppearanceParts {
        internal const string Feature="nativeAppearanceParts.v1";
        internal const string LiquidSurface="liquid_surface",LiquidStream="liquid_stream";
        internal static bool ValidId(string id)=>id is LiquidSurface or LiquidStream;
        internal static IEnumerable<string> Addresses(RoomObjectData data){
            if(data!=null&&!data.IsBuiltIn&&data.containers?.Length==1){
                yield return LiquidSurface;yield return LiquidStream;
            }
        }
        internal static string Address(Renderer renderer)=>renderer
            ?renderer.GetComponentInParent<ContainerFillView>()?.AppearancePart(renderer):null;
        internal static bool Present(RoomItem item,string address){
            var view=item?item.GetComponent<ContainerFillView>():null;
            return view&&view.HasAppearancePart(address);
        }
    }
}

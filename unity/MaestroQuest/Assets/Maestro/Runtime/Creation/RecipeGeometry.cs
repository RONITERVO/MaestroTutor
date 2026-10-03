// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Native recipe evaluators share admission costs and preserve saved shape parameters.</summary>
    internal static class RecipeGeometry {
        internal const int MaximumRoomVertices=262144;
        internal static bool Valid(RecipePart part)=>part.shape=="sweep"?RecipeSweep.Valid(part):(part.path==null||part.path.Length==0)&&(part.shape=="extrude"?RecipeExtrusion.Valid(part):RecipeLathe.Valid(part));
        internal static bool Custom(string shape)=>shape=="lathe"||shape=="extrude"||shape=="sweep";
        internal static Mesh Build(RecipePart part)=>part.shape switch {"lathe"=>RecipeLathe.Build(part),"extrude"=>RecipeExtrusion.Build(part),"sweep"=>RecipeSweep.Build(part),_=>throw new ArgumentException("No custom evaluator for this shape")};
        internal static int VertexCost(RoomRecipe recipe){
            int total=0;if(recipe?.parts==null)return total;
            foreach(var part in recipe.parts)if(part?.profile!=null){if(part.shape=="lathe")total+=part.profile.Length*2*(part.segments+1);else if(part.shape=="extrude")total+=part.profile.Length*6;else if(part.shape=="sweep"&&part.path!=null)total+=part.profile.Length*(2+4*(part.path.Length-1));}
            return total;
        }
    }
}

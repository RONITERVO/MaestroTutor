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
        internal static int VertexCost(RoomRecipe recipe)=>checked((int)VertexCostWide(recipe));
        internal static long VertexCostWide(RoomRecipe recipe){
            long total=0;if(recipe?.parts==null)return total;
            foreach(var part in recipe.parts)if(part?.profile!=null){
                long points=part.profile.Length,cost=0;
                checked {
                    if(part.shape=="lathe")cost=points*2*((long)part.segments+1);
                    else if(part.shape=="extrude")cost=points*6;
                    else if(part.shape=="sweep"&&part.path!=null)cost=points*(2+4L*(part.path.Length-1));
                    if(cost<0)throw new ArgumentException("Invalid recipe geometry cost");
                    total+=cost;
                }
            }
            return total;
        }
    }
}

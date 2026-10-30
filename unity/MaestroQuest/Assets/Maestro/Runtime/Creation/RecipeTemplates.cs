// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public static class RecipeTemplates
    {
        public static RoomRecipe BoxRobot(bool wave)
        {
            var recipe=CreationTemplates.All.Single(entry=>entry.Id=="robot").Recipe;
            recipe.playing=wave;if(!wave)recipe.tracks=Array.Empty<RecipeTrack>();return recipe;
        }
    }
}

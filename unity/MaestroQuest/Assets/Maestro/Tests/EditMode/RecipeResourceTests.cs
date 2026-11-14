// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RecipeResourceTests
    {
        internal static RoomRecipe Parts(int count)=>new(){parts=Enumerable.Range(0,count).Select(i=>new RecipePart{id="part"+i}).ToArray()};
        static RoomResourceOwner Owner()=>new(null,Guid.NewGuid().ToString("N"),"preview");
        [TestCase(1)][TestCase(32)] public void PreparingReservationsEnforceInstanceOrPartLimitsAcrossOwners(int parts)
        {
            var leases=new List<RecipeResources.Lease>();var before=RecipeResources.ObserveBudget();var recipe=Parts(parts);
            try{
                int count=Math.Min(RecipeResources.MaximumInstances-(int)before["instances"],(RecipeResources.MaximumParts-(int)before["parts"])/parts);
                for(int i=0;i<count;i++)leases.Add(RecipeResources.Begin(recipe,Owner()));
                var full=RecipeResources.ObserveBudget();Assert.Throws<RoomResourceLimitException>(()=>RecipeResources.Begin(recipe,Owner()));
                Assert.That(RecipeResources.ObserveBudget().ToString(),Is.EqualTo(full.ToString()));
                leases[0].Retire();Assert.DoesNotThrow(()=>leases.Add(RecipeResources.Begin(recipe,Owner())));
            }finally{foreach(var lease in leases)lease.Retire();}
            Assert.That(RecipeResources.ObserveBudget().ToString(),Is.EqualTo(before.ToString()));
        }
        [Test] public void SeveralValidCandidatesShareTheCustomVertexBudget()
        {
            var recipe=Parts(32);
            foreach(var part in recipe.parts){part.shape="lathe";part.segments=48;part.profile=Enumerable.Range(0,16).Select(i=>new Vector2(.25f+.2f*Mathf.Cos(i*Mathf.PI/8),.3f*Mathf.Sin(i*Mathf.PI/8))).ToArray();}
            Assert.That(recipe.Validate(out var error),Is.True,error);
            var leases=new List<RecipeResources.Lease>();var before=RecipeResources.ObserveBudget();int cost=RecipeGeometry.VertexCost(recipe);
            int count=(RecipeResources.MaximumCustomVertices-(int)before["customVertices"])/cost;
            Assert.That(count*32,Is.LessThan(RecipeResources.MaximumParts));Assert.That(count,Is.LessThan(RecipeResources.MaximumInstances));
            try{
                for(int i=0;i<count;i++)leases.Add(RecipeResources.Begin(recipe,Owner()));
                Assert.Throws<RoomResourceLimitException>(()=>RecipeResources.Begin(recipe,Owner()));
                leases[0].Retire();Assert.DoesNotThrow(()=>leases.Add(RecipeResources.Begin(recipe,Owner())));
            }finally{foreach(var lease in leases)lease.Retire();}
            Assert.That(RecipeResources.ObserveBudget().ToString(),Is.EqualTo(before.ToString()));
        }
        [Test] public void CustomVertexCapacityUsesWideCostsWithoutAllocatingNativeMeshes()
        {
            var recipe=Parts(1);recipe.parts[0].shape="lathe";recipe.parts[0].segments=int.MaxValue;recipe.parts[0].profile=new[]{Vector2.zero,Vector2.one};
            Assert.That(RecipeGeometry.VertexCostWide(recipe),Is.EqualTo(8589934592L));
            var before=RecipeResources.ObserveBudget().ToString();
            Assert.Throws<RoomResourceLimitException>(()=>RecipeResources.Begin(recipe,Owner()));
            Assert.That(RecipeResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test] public void InvalidLaterPartCannotDiscountTheCostOfEarlierGeometry()
        {
            var recipe=Parts(2);foreach(var part in recipe.parts){part.shape="lathe";part.profile=new[]{Vector2.zero,Vector2.one};}
            recipe.parts[0].segments=64;recipe.parts[1].segments=-32;
            Assert.Throws<ArgumentException>(()=>RecipeResources.Begin(recipe,Owner()));
        }
    }
}

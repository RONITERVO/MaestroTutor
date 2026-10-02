// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RecipeLatheTests
    {
        public static RoomRecipe Cup()=>JsonUtility.FromJson<RoomRecipe>(((JArray)JToken.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/lathe-contract.json"))))[0]["recipe"].ToString());
        [Test] public void GeometryAndCatalogShareProfileContractWithWeb()
        {
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/lathe-contract.json")));
            foreach(var c in cases)Assert.That(CapabilityArguments.Validate(c["recipe"],CapabilitySchema.RecipeSchema(),out var error),Is.EqualTo((bool)c["valid"]),(string)c["name"]+": "+error);
        }
        [Test] public void CupHasNondegenerateOutwardTrianglesFiniteNormalsAndAnOpenMouth()
        {
            var part=Cup().parts[0];var mesh=RecipeLathe.Build(part);
            try{
                var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;
                Assert.That(v.Length,Is.LessThanOrEqualTo(RecipeLathe.VertexCost(Cup())));Assert.That(v.Length,Is.GreaterThan(0));
                Assert.That(mesh.uv.Length,Is.EqualTo(v.Length));Assert.That(n.All(x=>RoomRecipe.Finite(x)&&Mathf.Abs(x.magnitude-1)<.001f),Is.True);
                for(int i=0;i<t.Length;i+=3){var normal=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);Assert.That(normal.sqrMagnitude,Is.GreaterThan(1e-12));Assert.That(Vector3.Dot(normal,n[t[i]]+n[t[i+1]]+n[t[i+2]]),Is.GreaterThan(0));
                    if(new[]{v[t[i]].y,v[t[i+1]].y,v[t[i+2]].y}.All(y=>y>.49f))Assert.That(new[]{v[t[i]],v[t[i+1]],v[t[i+2]]}.All(p=>new Vector2(p.x,p.z).magnitude>.39f),Is.True,"No cap may fill the cup mouth");}
                Assert.That(mesh.bounds.size.y,Is.EqualTo(1).Within(.0001));Assert.That(mesh.bounds.size.x,Is.EqualTo(1).Within(.0001));
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void InvalidProfilesDoNotAllocateMeshesAndCopiesKeepIndependentParameters()
        {
            var recipe=Cup();var copy=recipe.Copy();copy.parts[0].profile[1].x=.3f;Assert.That(recipe.parts[0].profile[1].x,Is.EqualTo(.5f));
            copy.parts[0].profile[1].x=-1;Assert.Throws<ArgumentException>(()=>RecipeLathe.Build(copy.parts[0]));
        }
    }
}

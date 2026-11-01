// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Text.RegularExpressions;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Bounded editable pigment pattern; no texture files or generated geometry.</summary>
    [Serializable] public sealed class RecipePattern
    {
        public const string Feature="recipePatterns.v1";
        public string kind="solid",plane="uv",secondary="#FFFFFF";
        public int columns=1,rows=1;
        public bool Enabled=>kind!="solid";
        public bool Valid()=>kind is "solid" or "checker" or "stripes" && plane is "uv" or "xy" or "xz" or "yz" &&
            columns>=1&&columns<=32&&rows>=1&&rows<=32&&secondary!=null&&Regex.IsMatch(secondary,"\\A#[a-fA-F0-9]{6}\\z");
        public Color Color=>ColorUtility.TryParseHtmlString(secondary,out var color)?color:UnityEngine.Color.white;
        internal void Apply(Material material,string shape){
            material.SetFloat("_PatternMode",kind=="checker"?1:kind=="stripes"?2:0);
            material.SetFloat("_PatternPlane",plane=="xy"?1:plane=="xz"?2:plane=="yz"?3:0);
            material.SetVector("_PatternCounts",new Vector4(columns,rows,0,0));
            material.SetVector("_PatternCoordinates",shape=="cylinder"?new Vector4(1,.5f,1,0):new Vector4(1,1,1,0));
            material.SetColor("_PatternColor",Color);
        }
    }
}

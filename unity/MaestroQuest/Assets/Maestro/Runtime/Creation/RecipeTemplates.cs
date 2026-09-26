// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public static class RecipeTemplates
    {
        // Expands to the same editable recipe as a custom creation; no opaque special object.
        public static RoomRecipe BoxRobot(bool wave)
        {
            var parts = new List<RecipePart>();
            void Part(string id,string parent,Vector3 position,Vector3 size) => parts.Add(new RecipePart { id=id,parent=parent,position=position,size=size,color=new Color(.18f,.65f,.63f,1) });
            Part("Hips",null,new Vector3(0,.48f,0),new Vector3(.25f,.12f,.15f));
            Part("Spine","Hips",new Vector3(0,.11f,0),new Vector3(.23f,.12f,.14f));
            Part("Chest","Spine",new Vector3(0,.13f,0),new Vector3(.3f,.17f,.17f));
            Part("Neck","Chest",new Vector3(0,.12f,0),Vector3.one*.07f);
            Part("Head","Neck",new Vector3(0,.14f,0),new Vector3(.25f,.23f,.20f));
            foreach (int side in new[] {-1,1})
            {
                string s=side<0 ? "Left" : "Right";
                Part(s+"UpperArm","Chest",new Vector3(side*.22f,0,0),new Vector3(.12f,.15f,.12f));
                Part(s+"LowerArm",s+"UpperArm",new Vector3(0,-.16f,0),new Vector3(.1f,.15f,.1f));
                Part(s+"Hand",s+"LowerArm",new Vector3(0,-.12f,0),Vector3.one*.1f);
                Part(s+"UpperLeg","Hips",new Vector3(side*.09f,-.14f,0),new Vector3(.11f,.18f,.12f));
                Part(s+"LowerLeg",s+"UpperLeg",new Vector3(0,-.18f,0),new Vector3(.1f,.17f,.1f));
                Part(s+"Foot",s+"LowerLeg",new Vector3(0,-.11f,.04f),new Vector3(.13f,.1f,.2f));
                parts.Add(new RecipePart { id=s+"Eye",parent="Head",position=new Vector3(side*.055f,.025f,.105f),size=new Vector3(.035f,.04f,.014f),color=new Color(.08f,.08f,.12f,1) });
            }
            var recipe=new RoomRecipe {parts=parts.ToArray(),duration=2,loop=true,playing=wave};
            if(wave) recipe.tracks=new[] {
                new RecipeTrack {part="RightUpperArm",keys=new[] {Key(0,0),Key(.4f,135),Key(1.6f,135),Key(2,0)}},
                new RecipeTrack {part="RightLowerArm",keys=new[] {Key(0,0),Key(.4f,0),Key(.7f,35),Key(1,-25),Key(1.3f,35),Key(1.6f,0),Key(2,0)}}
            };
            return recipe;
        }
        static RecipeKey Key(float time,float z) => new() {time=time,rotation=Quaternion.Euler(0,0,z)};
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Saved drawing behaviour on an ordinary object; no separate ink store.</summary>
    [Serializable] public sealed class DrawingTip
    {
        public int version=1;
        public string part="";
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public Color color=Color.white;
        public float radius=.003f;
        public bool enabled=true;
        public string mode="draw";
        public string Mode=>string.IsNullOrEmpty(mode)?"draw":mode;
        public DrawingTip Copy()=>new(){version=version,part=part,position=position,rotation=rotation,color=color,radius=radius,enabled=enabled,mode=mode};
        public bool Validate(RoomObjectData owner,out string error)
        {
            error="Choose a valid drawing tip on an object root or existing recipe part";
            var c=color;
            if((Mode!="draw"&&Mode!="erase")||version!=(Mode=="erase"?2:1)||part==null||part!=""&&(owner.recipe?.parts==null||!owner.recipe.parts.Any(p=>p.id==part))||!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>100||!MotionFrame.ValidRotation(rotation)||!Unit(c.r)||!Unit(c.g)||!Unit(c.b)||c.a!=1||!float.IsFinite(radius)||radius<.001f||radius>.02f)return false;
            error=null;return true;
        }
        static bool Unit(float n)=>float.IsFinite(n)&&n>=0&&n<=1;
        public static bool ValidateCollection(RoomObjectData owner,out string error)
        {
            error="An object supports one drawing tip; the included book and Maestro cannot be drawing tools";
            var tips=owner.drawingTips??Array.Empty<DrawingTip>();
            if(tips.Length>1||owner.IsBuiltIn&&tips.Length>0||tips.Any(t=>t==null))return false;
            if(tips.Length==1)return tips[0].Validate(owner,out error);error=null;return true;
        }
    }
}

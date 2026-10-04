// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    [Serializable] public sealed class SculptTip {
        public int version=1;public string part="",mode="lower";
        public Vector3 position;public Quaternion rotation=Quaternion.identity;
        public float radius=.08f,height=.03f;public bool enabled=true;
        public SculptTip Copy()=>new(){version=version,part=part,mode=mode,position=position,rotation=rotation,radius=radius,height=height,enabled=enabled};
        public bool Validate(RoomObjectData owner,out string error){
            error="Choose a sculpt tip on the root or an existing recipe part, a normalized rotation, radius 0.005–2 m and height 0–0.5 m";
            if(version!=1||part==null||part!=""&&owner.recipe?.parts?.Any(p=>p.id==part)!=true||!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>100||!MotionFrame.ValidRotation(rotation)||!new[]{"raise","lower","level"}.Contains(mode)||!float.IsFinite(radius)||radius<.005f||radius>2||!float.IsFinite(height)||height<0||height>.5f)return false;
            error=null;return true;
        }
        public static bool ValidateCollection(RoomObjectData owner,out string error){
            error="Use one sculpt tip on a created object; disable its drawing tip before enabling sculpting";var tips=owner.sculptTips??Array.Empty<SculptTip>();
            if(tips.Length>1||tips.Any(t=>t==null)||owner.IsBuiltIn&&tips.Length>0||tips.Any(t=>t.enabled)&&owner.drawingTips?.Any(t=>t.enabled)==true)return false;
            if(tips.Length==1)return tips[0].Validate(owner,out error);error=null;return true;
        }
    }
    public sealed partial class RoomEditor {
        internal bool PrepareSculptTip(string target,int revision,SculptTip tip,out RoomObjectData data,out string error){data=null;if(!ComponentSource(target,revision,out data,out error))return false;data.sculptTips=tip==null?Array.Empty<SculptTip>():new[]{tip.Copy()};return ComponentCandidate(new[]{data},out error);}
        internal bool EditSculptTip(string target,int revision,SculptTip tip,out string error){if(!PrepareSculptTip(target,revision,tip,out var data,out error))return false;return CommitPersisted(new[]{data},Array.Empty<string>(),tip==null?"Sculpt tip removed":"Sculpt tip saved",false,out error,ComponentBefore(new[]{data}));}
    }
}

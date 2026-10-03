// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class ObjectPlacement
    {
        public string target;
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public float scale=1;
        public void Apply(RoomObjectData data) {data.position=position;data.rotation=rotation;data.scale=scale;}
        public static ObjectPlacement Capture(string id,Transform value)=>new() {target=id,position=value.localPosition,rotation=value.localRotation.normalized,scale=value.localScale.x};
    }
    /// <summary>Explicit room-local placements, independent of geometry and future assembly membership.</summary>
    [Serializable] public sealed class RoomLayout
    {
        public const int MaximumPlacements=16;
        public ObjectPlacement[] placements;
        public bool Validate(out string error)
        {
            error="Choose 1–16 distinct user-created objects with valid room-local placements";
            if(placements==null||placements.Length<1||placements.Length>MaximumPlacements)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in placements) {
                if(p==null||p.target==null||!Regex.IsMatch(p.target,"^[a-fA-F0-9]{32}$")||!ids.Add(p.target)||
                    !float.IsFinite(p.position.sqrMagnitude)||p.position.sqrMagnitude>625||!MotionFrame.ValidRotation(p.rotation)||
                    !float.IsFinite(p.scale)||p.scale<.1f||p.scale>4)return false;
            }
            error=null;return true;
        }
    }
    public sealed partial class RoomEditor
    {
        public bool CanApplyLayout(RoomLayout layout,out string error)
        {
            error="Layout is missing";if(layout==null||!layout.Validate(out error))return false;
            var workshop=GetComponent<AnimationWorkshop>();
            foreach(var placement in layout.placements) {
                if(!CanEditObject(placement.target,true,out error))return false;
                if(workshop&&workshop.ControlsTarget(placement.target)){error="Stop authoring before arranging these objects";return false;}
                if(!Find(placement.target).isActiveAndEnabled){error="A layout member is unavailable";return false;}
            }
            return true;
        }
        public bool ApplyLayout(RoomLayout layout,out string error)
        {
            if(!CanApplyLayout(layout,out error))return false;
            // Capture the actual before-pose, even between periodic physics saves.
            // It becomes one Undo baseline only AFTER the candidate save succeeds.
            var before=new RoomLayout {placements=layout.placements.Select(p=>ObjectPlacement.Capture(p.target,Find(p.target).transform)).ToArray()};
            var replacements=layout.placements.Select(p=>{var data=Read(p.target);p.Apply(data);return data;}).ToArray();
            return CommitPersisted(replacements,Array.Empty<string>(),"Layout applied — one Undo restores the previous arrangement",true,out error,before);
        }
    }
}

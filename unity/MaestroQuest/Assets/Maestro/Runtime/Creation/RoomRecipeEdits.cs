// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareRecipeEdit(string target,int revision,JObject patch,out RoomObjectData data,out string error)
        {
            data=null;if(!CanEditObject(target,true,out error))return false;
            if(ObjectRevision(target)!=revision){error="The recipe changed; inspect its current revision before editing";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object before editing its recipe";return false;}
            data=Read(target);if(data.kind!=RoomObjectKind.Assembly){error="Choose an existing recipe object";return false;}
            if(!RecipeEdits.Apply(data.recipe,patch,out var recipe,out error))return false;
            data.recipe=recipe;data=Pose(data,Find(target).transform);
            var replacement=data;var candidate=Snapshot();candidate.objects=candidate.objects.Select(x=>x.id==target?replacement:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditRecipe(string target,int revision,JObject patch,out string error)
        {
            if(!PrepareRecipeEdit(target,revision,patch,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Recipe edited; animation stopped",false,out error);
        }
    }
    internal static class RecipeEdits
    {
        internal static bool Apply(RoomRecipe original,JObject patch,out RoomRecipe result,out string error)
        {
            result=null;error="Use unique part/track IDs and remove only existing entries; an ID cannot be removed and set together";
            var updates=JsonUtility.FromJson<RoomRecipe>(patch.ToString());var removeParts=patch["removeParts"].Values<string>().ToArray();var removeTracks=patch["removeTracks"].Values<string>().ToArray();
            if(updates.parts.Select(x=>x.id).Distinct().Count()!=updates.parts.Length||updates.tracks.Select(x=>x.part).Distinct().Count()!=updates.tracks.Length||removeParts.Distinct().Count()!=removeParts.Length||removeTracks.Distinct().Count()!=removeTracks.Length||
                removeParts.Any(id=>!original.parts.Any(p=>p.id==id)||updates.parts.Any(p=>p.id==id))||removeTracks.Any(id=>!original.tracks.Any(t=>t.part==id)||updates.tracks.Any(t=>t.part==id)))return false;
            result=original.Copy();var parts=result.parts.Where(p=>!removeParts.Contains(p.id)).ToList();foreach(var part in updates.parts){int index=parts.FindIndex(p=>p.id==part.id);if(index<0)parts.Add(part);else parts[index]=part;}
            // Keep stable relative order while allowing new parents and explicit reparenting.
            var ordered=new List<RecipePart>();var placed=new HashSet<string>();while(parts.Count>0){int index=parts.FindIndex(p=>string.IsNullOrEmpty(p.parent)||placed.Contains(p.parent));if(index<0){error="Recipe parents must exist and cannot form a cycle";return false;}var part=parts[index];parts.RemoveAt(index);ordered.Add(part);placed.Add(part.id);}
            result.parts=ordered.ToArray();var tracks=result.tracks.Where(t=>!removeTracks.Contains(t.part)).ToList();
            float duration=(float)patch["duration"];if(duration!=result.duration)foreach(var track in tracks)foreach(var key in track.keys)key.time=key.time/result.duration*duration;
            foreach(var track in updates.tracks){int index=tracks.FindIndex(t=>t.part==track.part);if(index<0)tracks.Add(track);else tracks[index]=track;}
            result.tracks=tracks.ToArray();result.duration=duration;result.loop=(bool)patch["loop"];result.playing=false;
            return result.Validate(out error);
        }
    }
}

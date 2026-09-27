// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;

namespace Maestro.Quest.Book
{
    [Serializable] public sealed class MotionUsageView
    {
        public int total,page,pages=1;
        public string[] uses=Array.Empty<string>();
        public bool history,saved,uncertain,playing;
        public string protection;
    }
    public sealed class MotionRetention { public bool Saved,Uncertain; }
    public static class MotionUsage
    {
        public static MotionRetention ReadSaved(RoomEditor editor,RuleWorkshop rules,string id,bool force=false)
        {
            bool saved=editor.SavedMotion(id,out bool a,force); saved |= rules.SavedMotion(id,out bool b,force); saved |= editor.ActivityProfiles.SavedMotion(id,out bool c,force);
            return new MotionRetention { Saved=saved,Uncertain=a || b || c };
        }
        public static MotionUsageView Read(RoomEditor editor,RuleWorkshop rules,string id,int page=0,MotionRetention retained=null)
        {
            var uses=new List<string>();
            if (editor.UsesMotion(id)) uses.Add("Maestro walking");
            foreach (var sequence in rules.Snapshot().sequences)
            {
                if(sequence.UsesMotion(id))uses.Add("Program: "+sequence.name);
            }
            foreach (var profile in editor.ActivityProfiles.Snapshot().avatars)
                foreach (var role in profile.roles)
                    if (role.choices.Any(x => x.motionId == id)) uses.Add("Avatar "+profile.modelHash.Substring(0,8)+": "+role.role);
            bool history=editor.HistoricalMotion(id) || rules.HistoricalMotion(id) || editor.ActivityProfiles.HistoricalMotion(id);
            bool saved=retained?.Saved == true,uncertain=retained == null || retained.Uncertain,playing=editor.Motions.Pinned(id);
            int pages=Math.Max(1,(uses.Count+7)/8); page=Math.Max(0,Math.Min(page,pages-1));
            return new MotionUsageView {
                total=uses.Count,page=page,pages=pages,uses=uses.Skip(page*8).Take(8).ToArray(),history=history,saved=saved,uncertain=uncertain,playing=playing,
                protection=uncertain ? "A retained save cannot be inspected yet. Keep this download until its save is repaired or finishes writing." :
                    uses.Count > 0 ? "Replace or clear its walking, action and tutor-state assignments before removing the download." :
                    history ? "Undo or Redo still needs this motion. Keep its download until those history entries expire." :
                    saved ? "A retained save or recovery backup still needs this motion. Its download is protected." :
                    playing ? "Stop this motion and wait for loading to finish before removing its download." : null
            };
        }
    }
}

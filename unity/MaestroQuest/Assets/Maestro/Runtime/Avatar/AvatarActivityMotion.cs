// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    /// <summary>Presentation only. It consumes the shared tutor activity and never starts a tutor/audio session.</summary>
    sealed class AvatarActivityMotion : IDisposable
    {
        readonly MaestroAvatar avatar;
        readonly Dictionary<string,float> cooldowns=new();
        readonly HashSet<string> failed=new();
        AvatarActivityProfiles profiles;
        MotionLibrary library;
        AvatarActivityProfile profile;
        string model,state,previous;
        TutorMotionChoice chosen;
        MotionLibrary.Lease ready;
        Task loading;
        int generation;
        float nextSelection;
        bool playing;
        public string Status { get; private set; }
        public event Action Changed;
        public AvatarActivityMotion(MaestroAvatar owner) { avatar=owner; }
        public void Configure(AvatarActivityProfiles source,MotionLibrary motions)
        {
            if (profiles == source && library == motions) return;
            if (profiles != null) profiles.Changed-=ProfileChanged;
            Cancel(); profiles=source; library=motions; model=null; profile=null;
            if (profiles != null) profiles.Changed+=ProfileChanged;
        }
        void ProfileChanged() { Cancel(); model=null; profile=null; cooldowns.Clear(); previous=null; }
        public void Cancel(bool blend=false)
        {
            generation++; ready?.Dispose(); ready=null; loading=null; chosen=null; playing=false; state=null; nextSelection=0; failed.Clear();
            if (avatar) avatar.StopActivityMotion(blend); Notice(null);
        }
        public bool Apply(string activity,bool allowed,float now)
        {
            if (!allowed || profiles == null || library == null || !Enum.TryParse<TutorMotionRole>(activity,true,out var role))
            { if (state != null || ready != null || loading != null || playing) Cancel(); return false; }
            if (model != avatar.ModelHash)
            { Cancel(); model=avatar.ModelHash; profile=profiles.Find(model); cooldowns.Clear(); previous=null; }
            if (state != activity) { Finish(now); Cancel(true); state=activity; }
            if (profile == null || !avatar.CustomModel || profile.rigHash != avatar.CustomModel.MotionRigHash) return false;
            if (playing)
            {
                if (avatar.ActivityMotionId == chosen.motionId) return true;
                Finish(now); loading=null; chosen=null; nextSelection=0;
            }
            if (loading != null && loading.IsCompleted && ready == null && !playing) loading=null;
            if (loading == null)
            {
                if (now < nextSelection) return false;
                nextSelection=now+.25f;
                var options=profile.roles.FirstOrDefault(x => x.role == role)?.choices ?? Array.Empty<TutorMotionChoice>();
                var eligible=options.Where(x => !failed.Contains(x.motionId) && (!cooldowns.TryGetValue(activity+x.motionId,out var due) || now >= due))
                    .Where(x => { var entry=library.Find(x.motionId); return entry != null && !entry.Short && entry.rigHash == profile.rigHash; }).ToArray();
                if (eligible.Length == 0) { Notice(options.Length == 0 ? null : "No ready state motion — using the included animation"); return false; }
                if (eligible.Length > 1) eligible=eligible.Where(x => x.motionId != previous).ToArray();
                int ticket=UnityEngine.Random.Range(0,eligible.Sum(x => x.weight));
                chosen=eligible[eligible.Length-1]; foreach (var option in eligible) { if (ticket < option.weight) { chosen=option; break; } ticket-=option.weight; }
                Notice("Loading "+activity+" motion — using the included animation meanwhile");
                loading=Load(++generation,chosen.motionId,model,profile.rigHash);
            }
            if (ready == null) return false;
            var lease=ready; ready=null;
            if (!avatar.PlayActivityMotion(lease,chosen.loop)) { lease.Dispose(); failed.Add(chosen.motionId); loading=null; chosen=null; return false; }
            avatar.SetImportedPlaybackRate(chosen.speed); playing=true; previous=chosen.motionId;
            Notice("Using "+(library.Find(chosen.motionId)?.name ?? "saved motion")+" for "+activity); return true;
        }
        void Finish(float now)
        {
            if (playing && chosen != null && state != null) cooldowns[state+chosen.motionId]=now+chosen.cooldown;
            playing=false;
        }
        async Task Load(int version,string id,string avatarModel,string rig)
        {
            MotionLibrary.Lease lease=null;
            try
            {
                lease=await library.AcquireAsync(id,rig);
                if (!avatar || version != generation || avatar.ModelBusy || avatar.ModelHash != avatarModel) return;
                ready=lease; lease=null;
            }
            catch (Exception error)
            {
                if (avatar && version == generation) { failed.Add(id); loading=null; chosen=null; Notice((error is ModelImportException ? error.Message : "State motion could not load")+" — using the included animation"); }
            }
            finally { lease?.Dispose(); }
        }
        void Notice(string value) { if (Status == value) return; Status=value; Changed?.Invoke(); }
        public void Dispose() { Cancel(); if (profiles != null) profiles.Changed-=ProfileChanged; profiles=null; }
    }
}

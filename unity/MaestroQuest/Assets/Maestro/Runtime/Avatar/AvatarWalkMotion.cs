// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Imports;

namespace Maestro.Quest.Avatar
{
    // Loading only prepares a lease. A subsequent, still-active movement frame
    // starts it; a completed background read never resumes interrupted walking.
    sealed class AvatarWalkMotion
    {
        readonly MaestroAvatar avatar;
        MotionLibrary library;
        MotionLibrary.Lease ready;
        Task request;
        int generation;
        string requestedModel,failed;
        public string Id { get; private set; }
        public string Status { get; private set; }
        public event Action Changed;
        public string Name => string.IsNullOrEmpty(Id) ? null : library?.Find(Id)?.name ?? "Missing saved walk";
        public AvatarWalkMotion(MaestroAvatar avatar) { this.avatar = avatar; }
        public void Configure(MotionLibrary value,string id)
        {
            if (library == value && Id == id) return;
            Stop(); library = value; Id = id;
        }
        public void Stop()
        {
            generation++; ready?.Dispose(); ready = null; request = null; requestedModel = null; failed = null; Status = null;
        }
        public bool Apply(float rate)
        {
            if (string.IsNullOrEmpty(Id) || library == null || !avatar || avatar.ModelBusy || !avatar.CustomModel) return false;
            if (avatar.LibraryMotionId == Id) { avatar.SetImportedPlaybackRate(rate); return true; }
            var entry = library.Find(Id);
            if (entry == null || entry.Short || entry.rigHash != avatar.CustomModel.MotionRigHash)
            { Notice("Saved walk unavailable for this model — using included walk"); return false; }
            if (requestedModel != avatar.ModelHash) { Stop(); requestedModel = avatar.ModelHash; }
            if (request == null && failed == null) { Notice("Loading saved walk — using included walk meanwhile"); request = Load(++generation,Id,requestedModel,entry.rigHash); }
            if (ready == null) return false;
            var lease = ready; ready = null;
            if (!avatar.PlayLibraryMotion(lease,true)) { lease.Dispose(); return false; }
            avatar.SetImportedPlaybackRate(rate); Notice(null); return true;
        }
        async Task Load(int version,string id,string model,string rig)
        {
            MotionLibrary.Lease lease = null;
            try
            {
                lease = await library.AcquireAsync(id,rig);
                if (!avatar || version != generation || avatar.ModelBusy || avatar.ModelHash != model) return;
                ready = lease; lease = null;
            }
            catch (Exception error)
            {
                if (avatar && version == generation) { failed = id; Notice((error is ModelImportException ? error.Message : "Saved walk could not load")+" — using included walk"); }
            }
            finally { lease?.Dispose(); }
        }
        void Notice(string value) { if (Status == value) return; Status = value; Changed?.Invoke(); }
    }
}

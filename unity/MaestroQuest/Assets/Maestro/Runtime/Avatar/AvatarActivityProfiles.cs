// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;

namespace Maestro.Quest.Avatar
{
    public enum TutorMotionRole { Idle, Listening, Thinking, Speaking }
    [Serializable] public sealed class TutorMotionChoice
    {
        public string motionId;
        public int weight=1;
        public float speed=1,cooldown;
        public bool loop;
        public TutorMotionChoice Copy() => (TutorMotionChoice)MemberwiseClone();
        public bool Valid() => Guid.TryParseExact(motionId,"N",out _) && weight >= 1 && weight <= 10 && float.IsFinite(speed) && speed >= .25f && speed <= 2 && float.IsFinite(cooldown) && cooldown >= 0 && cooldown <= 60;
    }
    [Serializable] public sealed class TutorRoleChoices
    {
        public TutorMotionRole role;
        public TutorMotionChoice[] choices=Array.Empty<TutorMotionChoice>();
        public TutorRoleChoices Copy() => new() { role=role,choices=choices.Select(x => x.Copy()).ToArray() };
    }
    [Serializable] public sealed class AvatarActivityProfile
    {
        public string modelHash,rigHash;
        public TutorRoleChoices[] roles=Array.Empty<TutorRoleChoices>();
        public AvatarActivityProfile Copy() => new() { modelHash=modelHash,rigHash=rigHash,roles=roles.Select(x => x.Copy()).ToArray() };
    }
    [Serializable] public sealed class AvatarActivityDocument
    {
        public int version=2;
        public AvatarActivityProfile[] avatars=Array.Empty<AvatarActivityProfile>();
        public AvatarActivityDocument Copy() => new() { version=version,avatars=avatars.Select(x => x.Copy()).ToArray() };
        public bool Valid() => version == 2 && avatars != null && avatars.Length <= 64 && avatars.All(x => x != null && ModelLibrary.ValidHash(x.modelHash) && ModelLibrary.ValidHash(x.rigHash) &&
            x.roles != null && x.roles.Length <= 4 && x.roles.All(r => r != null && Enum.IsDefined(typeof(TutorMotionRole),r.role) && r.choices != null && r.choices.Length <= 4 && r.choices.All(c => c != null && c.Valid()) && r.choices.Select(c => c.motionId).Distinct().Count() == r.choices.Length) &&
            x.roles.Select(r => r.role).Distinct().Count() == x.roles.Length) && avatars.Select(x => x.modelHash).Distinct().Count() == avatars.Length;
    }
    /// <summary>Bounded per-avatar preferences. Assignment changes are independent of room placement Undo.</summary>
    public sealed class AvatarActivityProfiles
    {
        readonly VersionedRoomFile<AvatarActivityDocument> file;
        sealed class Change { public string Model; public AvatarActivityProfile Before,After; }
        readonly List<Change> undo=new(),redo=new();
        AvatarActivityDocument document;
        public int Revision { get; private set; }=1;
        public bool ReadOnly => file.ReadOnly;
        public bool CanUndo(string model) => !ReadOnly && undo.Any(x => x.Model == model);
        public bool CanRedo(string model) => !ReadOnly && redo.Any(x => x.Model == model);
        public string Notice { get; private set; }
        public event Action Changed;
        public AvatarActivityProfiles(string directory)
        {
            file=new(directory,"avatar-activities",256*1024,x => x.Valid(),x => x.Copy(),null,x => x.version=2);
            document=file.Load(out var message) ?? new AvatarActivityDocument(); Notice=message;
        }
        public AvatarActivityProfile Find(string model) => document.avatars.FirstOrDefault(x => x.modelHash == model)?.Copy();
        public AvatarActivityDocument Snapshot() => document.Copy();
        public bool HistoricalMotion(string id) => undo.Concat(redo).SelectMany(x => new[] { x.Before,x.After }).Where(x => x != null).Any(x => x.roles.Any(role => role.choices.Any(choice => choice.motionId == id)));
        public bool SavedMotion(string id,out bool uncertain,bool force=false) => file.Retains(x => x.avatars.SelectMany(avatar => avatar.roles).SelectMany(role => role.choices).Select(choice => choice.motionId),id,out uncertain,force);
        // Keep current and undo/redo references available to library maintenance.
        public string[] ReferencedMotionIds => undo.Concat(redo).SelectMany(x => new[] { x.Before,x.After }).Where(x => x != null).Concat(document.avatars).SelectMany(x => x.roles).SelectMany(x => x.choices).Select(x => x.motionId).Distinct().ToArray();
        public bool Assign(string model,string rig,TutorMotionRole role,TutorMotionChoice choice,MotionLibrary library,out string error) =>
            Apply(new AvatarActivityRequest { modelHash=model,revision=Revision,operation="edit",edits=new[] { new AvatarActivityEdit { operation="assign",role=(int)role,choice=choice } } },rig,library,out error);
        public bool Remove(string model,TutorMotionRole role,string id,out string error) =>
            Apply(new AvatarActivityRequest { modelHash=model,revision=Revision,operation="edit",edits=new[] { new AvatarActivityEdit { operation=id == null ? "clear" : "remove",role=(int)role,motionId=id } } },null,null,out error);
        public bool Apply(AvatarActivityRequest request,string rig,MotionLibrary library,out string error)
        {
            error="This tutor-state request is invalid";
            if (request == null || !request.Valid()) return false;
            if (request.revision != Revision) { error="Tutor-state assignments changed. Review the latest choices before editing."; return false; }
            if (ReadOnly || Revision == int.MaxValue) { error=Notice ?? "Tutor-state assignments are read-only"; return false; }
            if (request.operation == "undo") return Undo(request.modelHash,out error);
            if (request.operation == "redo") return Redo(request.modelHash,out error);
            var next=document.Copy();
            foreach (var change in request.edits)
            {
                var profile=next.avatars.FirstOrDefault(x => x.modelHash == request.modelHash);
                if (change.operation != "assign")
                {
                    if (profile == null) continue;
                    foreach (var group in profile.roles.Where(x => (int)x.role == change.role)) group.choices=change.operation == "clear" ? Array.Empty<TutorMotionChoice>() : group.choices.Where(x => x.motionId != change.motionId).ToArray();
                    profile.roles=profile.roles.Where(x => x.choices.Length > 0).ToArray(); next.avatars=next.avatars.Where(x => x.roles.Length > 0).ToArray();
                    continue;
                }
                var choice=change.choice; var entry=library?.Find(choice.motionId);
                if (!ModelLibrary.ValidHash(rig) || entry == null || !library.Downloaded(entry.id) || entry.Short || entry.rigHash != rig) { error="Choose a downloaded compatible motion at least 0.1 seconds long"; return false; }
                if (profile == null)
                {
                    if (next.avatars.Length >= 64) { error="The avatar profile limit is reached"; return false; }
                    profile=new AvatarActivityProfile { modelHash=request.modelHash,rigHash=rig }; next.avatars=next.avatars.Append(profile).ToArray();
                }
                if (profile.rigHash != rig) { error="The avatar rig changed; its existing assignments were preserved"; return false; }
                var choices=profile.roles.FirstOrDefault(x => (int)x.role == change.role);
                if (choices == null) { choices=new TutorRoleChoices { role=(TutorMotionRole)change.role }; profile.roles=profile.roles.Append(choices).ToArray(); }
                int existing=Array.FindIndex(choices.choices,x => x.motionId == choice.motionId);
                if (existing >= 0) choices.choices[existing]=choice.Copy();
                else if (choices.choices.Length < 4) choices.choices=choices.choices.Append(choice.Copy()).ToArray();
                else { error="Keep up to four choices per tutor state; remove one before adding another"; return false; }
            }
            return Commit(next,request.modelHash,out error);
        }
        bool Commit(AvatarActivityDocument next,string model,out string error)
        {
            error=Notice; if (ReadOnly || Revision == int.MaxValue) return false;
            error=null; var before=document.avatars.FirstOrDefault(x => x.modelHash == model); var after=next.avatars.FirstOrDefault(x => x.modelHash == model);
            if (UnityEngine.JsonUtility.ToJson(before) == UnityEngine.JsonUtility.ToJson(after)) return true;
            if (!file.Save(next,out error)) return false;
            undo.Add(new Change { Model=model,Before=before?.Copy(),After=after?.Copy() }); if (undo.Count > 32) undo.RemoveAt(0);
            redo.RemoveAll(x => x.Model == model); document=next; Revision++; Notice=null; Changed?.Invoke(); return true;
        }
        public bool Undo(string model,out string error) => Move(model,undo,redo,false,out error);
        public bool Redo(string model,out string error) => Move(model,redo,undo,true,out error);
        bool Move(string model,List<Change> from,List<Change> to,bool forward,out string error)
        {
            error=Notice; if (ReadOnly || Revision == int.MaxValue) return false;
            error="No assignment change to undo or redo for this avatar"; int index=from.FindLastIndex(x => x.Model == model); if (index < 0) return false;
            var change=from[index]; var replacement=forward ? change.After : change.Before; var next=document.Copy();
            next.avatars=next.avatars.Where(x => x.modelHash != model).Concat(replacement == null ? Array.Empty<AvatarActivityProfile>() : new[] { replacement.Copy() }).ToArray();
            if (!file.Save(next,out error)) return false;
            from.RemoveAt(index); to.Add(change); if (to.Count > 32) to.RemoveAt(0); document=next; Revision++; Notice=null; Changed?.Invoke(); return true;
        }
    }
}

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
        // Future library deletion must retain current and undo/redo references.
        public string[] ReferencedMotionIds => undo.Concat(redo).SelectMany(x => new[] { x.Before,x.After }).Where(x => x != null).Concat(document.avatars).SelectMany(x => x.roles).SelectMany(x => x.choices).Select(x => x.motionId).Distinct().ToArray();
        public bool Assign(string model,string rig,TutorMotionRole role,TutorMotionChoice choice,MotionLibrary library,out string error)
        {
            error="This tutor-state assignment is invalid";
            if (!ModelLibrary.ValidHash(model) || !ModelLibrary.ValidHash(rig) || !Enum.IsDefined(typeof(TutorMotionRole),role) || choice == null || !choice.Valid()) return false;
            var entry=library.Find(choice.motionId);
            if (entry == null || entry.Short || entry.rigHash != rig) { error="Choose a compatible motion at least 0.1 seconds long"; return false; }
            var next=document.Copy(); var profile=next.avatars.FirstOrDefault(x => x.modelHash == model);
            if (profile == null)
            {
                if (next.avatars.Length >= 64) { error="The avatar profile limit is reached"; return false; }
                profile=new AvatarActivityProfile { modelHash=model,rigHash=rig }; next.avatars=next.avatars.Append(profile).ToArray();
            }
            if (profile.rigHash != rig) { error="The avatar rig changed; its existing assignments were preserved"; return false; }
            var group=profile.roles.FirstOrDefault(x => x.role == role);
            if (group == null) { group=new TutorRoleChoices { role=role }; profile.roles=profile.roles.Append(group).ToArray(); }
            int existing=Array.FindIndex(group.choices,x => x.motionId == choice.motionId);
            if (existing >= 0) group.choices[existing]=choice.Copy();
            else if (group.choices.Length < 4) group.choices=group.choices.Append(choice.Copy()).ToArray();
            else { error="Keep up to four choices per tutor state; remove one before adding another"; return false; }
            return Commit(next,model,out error);
        }
        public bool Remove(string model,TutorMotionRole role,string id,out string error)
        {
            error="This tutor-state assignment is invalid";
            if (!ModelLibrary.ValidHash(model) || !Enum.IsDefined(typeof(TutorMotionRole),role) || id != null && !Guid.TryParseExact(id,"N",out _)) return false;
            var next=document.Copy(); var profile=next.avatars.FirstOrDefault(x => x.modelHash == model);
            if (profile == null) { error=null; return true; }
            foreach (var group in profile.roles.Where(x => x.role == role)) group.choices=id == null ? Array.Empty<TutorMotionChoice>() : group.choices.Where(x => x.motionId != id).ToArray();
            profile.roles=profile.roles.Where(x => x.choices.Length > 0).ToArray(); next.avatars=next.avatars.Where(x => x.roles.Length > 0).ToArray();
            return Commit(next,model,out error);
        }
        bool Commit(AvatarActivityDocument next,string model,out string error)
        {
            error=null; var before=document.avatars.FirstOrDefault(x => x.modelHash == model); var after=next.avatars.FirstOrDefault(x => x.modelHash == model);
            if (UnityEngine.JsonUtility.ToJson(before) == UnityEngine.JsonUtility.ToJson(after)) return true;
            if (!file.Save(next,out error)) return false;
            undo.Add(new Change { Model=model,Before=before?.Copy(),After=after?.Copy() }); if (undo.Count > 32) undo.RemoveAt(0);
            redo.RemoveAll(x => x.Model == model); document=next; Notice=null; Changed?.Invoke(); return true;
        }
        public bool Undo(string model,out string error) => Move(model,undo,redo,false,out error);
        public bool Redo(string model,out string error) => Move(model,redo,undo,true,out error);
        bool Move(string model,List<Change> from,List<Change> to,bool forward,out string error)
        {
            error="No assignment change to undo or redo for this avatar"; int index=from.FindLastIndex(x => x.Model == model); if (index < 0) return false;
            var change=from[index]; var replacement=forward ? change.After : change.Before; var next=document.Copy();
            next.avatars=next.avatars.Where(x => x.modelHash != model).Concat(replacement == null ? Array.Empty<AvatarActivityProfile>() : new[] { replacement.Copy() }).ToArray();
            if (!file.Save(next,out error)) return false;
            from.RemoveAt(index); to.Add(change); if (to.Count > 32) to.RemoveAt(0); document=next; Notice=null; Changed?.Invoke(); return true;
        }
    }
}

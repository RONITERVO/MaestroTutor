// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>Canonical existence and native presence are independent. This
    /// observation never loads, deletes, repairs or transfers an entity.</summary>
    internal readonly struct RoomObjectPresence
    {
        internal readonly bool Saved,NativeInstance,Active;
        internal readonly string State;
        internal RoomObjectPresence(bool saved,RoomItem item)
        {
            Saved=saved;NativeInstance=item;Active=item&&item.isActiveAndEnabled;
            State=!saved?"missing":!item?"unavailable":!Active?"inactive":"active";
        }
    }
    public sealed partial class RoomJournal
    {
        internal bool ContainsObject(string target)=>target!=null&&items.ContainsKey(target);
    }
    public sealed partial class RoomEditor
    {
        public bool HasSavedObject(string target)=>journal?.ContainsObject(target)==true;
        internal RoomObjectPresence ObjectPresence(string target)=>new(HasSavedObject(target),Find(target));
        internal bool TryGetNativeObject(string target,out RoomItem item,out string error)
        {
            item=null;error="This object is not in the saved room; inspect the room first";
            if(!HasSavedObject(target))return false;
            var candidate=Find(target);error="This object's saved content is present, but it is currently unavailable in the room";
            if(!candidate)return false;
            item=candidate;error=null;return true;
        }
        internal bool TryGetLiveObject(string target,out RoomItem item,out string error)
        {
            if(!TryGetNativeObject(target,out item,out error))return false;
            if(item.isActiveAndEnabled)return true;
            item=null;error="This object's saved content is present, but it is currently unavailable in the room";return false;
        }
        internal JObject ObserveObjectPresence(string target)
        {
            if(journal==null)return null;var presence=ObjectPresence(target);
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["saved"]=presence.Saved,
                ["nativeInstance"]=presence.NativeInstance,["active"]=presence.Active,["state"]=presence.State};
        }
    }
}

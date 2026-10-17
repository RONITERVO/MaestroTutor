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
    }
    /// <summary>Explicit room-local placements, independent of geometry and future assembly membership.</summary>
    [Serializable] public sealed class RoomLayout
    {
        public const int MaximumPlacements=16;
        public ObjectPlacement[] placements;
        internal RoomLayout Copy()=>new(){placements=placements?.Select(p=>p==null?null:new ObjectPlacement{target=p.target,position=p.position,rotation=p.rotation,scale=p.scale}).ToArray()};
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
        public bool CanApplyLayout(RoomLayout layout,out string error)=>CanApplyLayout(layout,null,out error);
        bool CanApplyLayout(RoomLayout layout,object physicsOwner,out string error)
        {
            error="Layout is missing";if(layout==null||!layout.Validate(out error))return false;
            var workshop=GetComponent<AnimationWorkshop>();
            foreach(var placement in layout.placements) {
                if(!CanChangeObject(placement.target,true,true,false,out error,physicsOwner))return false;
                if(!Frame.Read(Find(placement.target).transform,out _,out _,out _)){error="A layout member needs a valid uniform room frame";return false;}
                if(workshop&&workshop.ControlsTarget(placement.target)){error="Stop authoring before arranging these objects";return false;}
                if(!Find(placement.target).isActiveAndEnabled){error="A layout member is unavailable";return false;}
            }
            return true;
        }
        internal bool CanApplyCapturedLayout(RoomLayout layout,IReadOnlyDictionary<string,int> revisions,object physicsOwner,Func<object,string> guard,out string error) {
            error="Provide a valid captured layout";if(layout==null||!layout.Validate(out error))return false;
            error=guard?.Invoke(physicsOwner);if(error!=null)return false;
            error="An arrangement member changed while waiting; read the current objects before retrying";
            if(revisions==null||!layout.placements.Select(p=>p.target).ToHashSet().SetEquals(revisions.Keys)||revisions.Any(p=>ObjectRevision(p.Key)!=p.Value))return false;
            return CanApplyLayout(layout,physicsOwner,out error);
        }
        sealed class LayoutPoseHolds:IDisposable {
            readonly List<IDisposable> holds=new();
            internal void Add(IDisposable value){if(value!=null)holds.Add(value);}
            public void Dispose(){for(int i=holds.Count-1;i>=0;i--)holds[i].Dispose();holds.Clear();}
        }
        internal bool BeginLayout(RoomLayout layout,IReadOnlyDictionary<string,int> revisions,Func<object,string> guard,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null) {
            pending=null;var captured=layout?.Copy();var expected=revisions?.ToDictionary(x=>x.Key,x=>x.Value);
            if(PendingEditSave){error="Wait for the pending room edit to finish or cancel it first";return false;}
            if(!CanApplyCapturedLayout(captured,expected,null,guard,out error))return false;
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            RoomJournal.PreparedEdit edit=null;LayoutPoseHolds poses=new();var owner=new object();
            try {
                var before=new RoomLayout{placements=captured.placements.Select(p=>Frame.Placement(p.target,Find(p.target).transform)).ToArray()};
                var replacements=captured.placements.Select(p=>{var data=Read(p.target);p.Apply(data);return data;}).ToArray();
                if(!journal.Prepare(replacements,Array.Empty<string>(),out edit,out error,observedBefore:before))return false;
                foreach(var placement in captured.placements)poses.Add(Find(placement.target).GetComponent<RigidRoomItem>()?.HoldForEdit(owner));
                pending=pendingPersistedEdit=new PendingPersistedEdit(this,edit,write,poses,
                    ()=>CanApplyCapturedLayout(captured,expected,owner,guard,out var issue)?null:issue,
                    "Layout applied — one Undo restores the previous arrangement",true,checkpoint);
                edit=null;write=null;poses=null;return true;
            } finally {poses?.Dispose();edit?.Dispose();write?.Dispose();}
        }
        public bool ApplyLayout(RoomLayout layout,out string error)
        {
            if(!CanApplyLayout(layout,out error))return false;
            // Capture the actual before-pose, even between periodic physics saves.
            // It becomes one Undo baseline only AFTER the candidate save succeeds.
            var before=new RoomLayout {placements=layout.placements.Select(p=>Frame.Placement(p.target,Find(p.target).transform)).ToArray()};
            var replacements=layout.placements.Select(p=>{var data=Read(p.target);p.Apply(data);return data;}).ToArray();
            return CommitPersisted(replacements,Array.Empty<string>(),"Layout applied — one Undo restores the previous arrangement",true,out error,before);
        }
    }
}

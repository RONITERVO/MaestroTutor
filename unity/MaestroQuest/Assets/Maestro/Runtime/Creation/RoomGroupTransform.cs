// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class TransformMember {public string target;public int revision;}
    [Serializable] public sealed class RoomGroupTransform {
        public TransformMember[] members;public Vector3 position;public Quaternion rotation=Quaternion.identity;public float scale=1;
        public bool Validate(out string error){
            error="Choose 1–16 distinct creations with current revisions and a bounded group transform";
            if(members==null||members.Length<1||members.Length>16||members.Any(m=>m==null||m.target==null||!Regex.IsMatch(m.target,"^[a-f0-9]{32}$")||m.revision<1)||members.Select(m=>m.target).Distinct().Count()!=members.Length||!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>625||!MotionFrame.ValidRotation(rotation)||!float.IsFinite(scale)||scale<.1f||scale>4)return false;
            error=null;return true;
        }
        // Reused by the native action and the physical preview. No allocation in the drag loop.
        public bool Project(RoomLayout before,RoomLayout result,out string error){
            error="The complete construction must stay within 25 metres and each piece's scale within 0.1–4";
            if(before?.placements==null||result?.placements==null||before.placements.Length!=members.Length||result.placements.Length!=members.Length)return false;
            if(!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>625||!MotionFrame.ValidRotation(rotation)||!float.IsFinite(scale)||scale<.1f||scale>4)return false;
            var origin=before.placements[0];var turn=rotation*Quaternion.Inverse(origin.rotation);
            for(int i=0;i<members.Length;i++){
                var source=before.placements[i];var next=result.placements[i];if(source.target!=members[i].target||next.target!=source.target)return false;
                next.position=position+turn*(source.position-origin.position)*scale;next.rotation=(turn*source.rotation).normalized;next.scale=source.scale*scale;
                if(!float.IsFinite(next.position.sqrMagnitude)||next.position.sqrMagnitude>625||!MotionFrame.ValidRotation(next.rotation)||!float.IsFinite(next.scale)||next.scale<.1f||next.scale>4)return false;
            }
            error=null;return true;
        }
    }
    public sealed partial class RoomEditor {
        internal bool GroupSources(TransformMember[] members,out RoomLayout before,out string error){
            before=null;var request=new RoomGroupTransform {members=members};if(!request.Validate(out error))return false;
            if(RuntimeGate.Held||Ownership.Suspended||WriteGate.Frozen||PhysicsWorld&&PhysicsWorld.Running){error="Pause room physics before arranging a construction";return false;}
            if(DrawingMode||DrawingInProgress){error="Put drawing tools away before arranging a construction";return false;}
            var selected=members.Select(m=>m.target).ToHashSet();var poses=new ObjectPlacement[members.Length];
            for(int i=0;i<members.Length;i++){
                var m=members[i];if(!CanEditObject(m.target,true,out error))return false;
                if(ObjectRevision(m.target)!=m.revision){error="A construction member changed; read its current revision";return false;}
                var item=Find(m.target);
                if(!item.isActiveAndEnabled||!item.Grab||!item.Grab.enabled||item.GetComponent<RigidRoomItem>()?.GeometryReady==false||Read(m.target).recipe?.playing==true||GetComponent<AnimationWorkshop>()?.ControlsTarget(m.target)==true){error="Release members and stop their animation authoring/playback first";return false;}
                poses[i]=ObjectPlacement.Capture(m.target,item.transform);
            }
            if(Snapshot().objects.Any(o=>(o.hinges??Array.Empty<RoomHinge>()).Any(h=>selected.Contains(o.id)!=selected.Contains(h.connected)))){error="Include both ends of every connected hinge before moving a construction";return false;}
            before=new RoomLayout {placements=poses};return before.Validate(out error);
        }
        internal bool PrepareGroupTransform(RoomGroupTransform request,out RoomLayout layout,out string error){
            layout=null;error="Group transform is missing";if(request==null||!request.Validate(out error)||!GroupSources(request.members,out var before,out error))return false;
            layout=new RoomLayout {placements=before.placements.Select(p=>new ObjectPlacement {target=p.target}).ToArray()};
            return request.Project(before,layout,out error)&&CanApplyLayout(layout,out error);
        }
        internal bool TransformGroup(RoomGroupTransform request,out string error)=>PrepareGroupTransform(request,out var layout,out error)&&ApplyLayout(layout,out error);
    }
}

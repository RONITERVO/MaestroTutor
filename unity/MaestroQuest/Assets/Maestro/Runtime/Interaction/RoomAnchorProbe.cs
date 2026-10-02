// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    // Captures exact object/socket identities. A later replacement cannot silently retarget a watch.
    internal sealed class RoomAnchorProbe:IProgramAnchorProbe
    {
        readonly RoomEditor editor;readonly string target;readonly RoomPropAnchor anchor;readonly RoomItem item,holder;readonly Transform socket;readonly Vector3 offset;readonly bool physics;
        bool disposed;
        internal RoomAnchorProbe(RoomEditor editor,string target,RoomPropAnchor anchor,Vector3 offset,bool physics){
            this.editor=editor;this.target=target;this.anchor=anchor;this.offset=offset;this.physics=physics;
            if(!editor||target==anchor.HolderId)throw new ProgramFault("Choose different target and anchor objects");
            item=editor.Find(target);if(!item||!item.isActiveAndEnabled)throw new ProgramFault("The observed object is missing or disabled");
            if(!anchor.Resolve(editor,out holder,out socket,out var error))throw new ProgramFault(error);
        }
        public bool Read(out AnchorProximitySample sample,out string error){
            sample=default;error="The observed object or anchor was removed, disabled or replaced";
            if(disposed||!editor||!item||!holder||!socket||!item.isActiveAndEnabled||!holder.isActiveAndEnabled||editor.Find(target)!=item||editor.Find(anchor.HolderId)!=holder)return false;
            bool available=!editor.RuntimeGate.Held&&!holder.Grab.isSelected;
            if(available&&!anchor.Matches(editor,holder,socket,out error))return false;
            var rigid=item.GetComponent<RigidRoomItem>();var holderRigid=holder.GetComponent<RigidRoomItem>();
            if(physics){if(!rigid||!rigid.TryReadMotion(out var free,out _,out _)){error="Anchor physics observation needs a solid or bouncy object";return false;}available&=free;}
            var centre=socket.position+socket.rotation*(offset*holder.transform.lossyScale.y);
            // Saved placement revisions also change during ordinary walking. Only
            // physical discontinuities reset a watch; exact instances guard replacement.
            sample=new AnchorProximitySample(item.transform.position,centre,available,rigid?rigid.MotionRevision:0,holderRigid?holderRigid.MotionRevision:0);error=null;return true;
        }
        public void Dispose(){disposed=true;}
    }
}

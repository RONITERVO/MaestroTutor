// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal delegate bool PrepareSculptGesture(out RoomObjectData[] data,out string error);
        // All physical variants use the ordinary journal candidate and ordered save.
        // Packing includes the new ball; scooping includes both conserved balances.
        internal bool BeginSculptGesture(Func<string> validate,PrepareSculptGesture prepare,string message,bool preservePlacement,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)=>
            BeginPreparedEdit(validate,(out RoomJournal.PreparedEdit edit,out string issue)=>{
                edit=null;if(!prepare(out var data,out issue))return false;
                var existing=data.Where(d=>Read(d.id)!=null).ToArray();
                return journal.Prepare(data,Array.Empty<string>(),out edit,out issue,
                    observedBefore:preservePlacement?null:ComponentBefore(existing),
                    scope:preservePlacement?RoomEditScope.Content:RoomEditScope.Full);
            },message,out pending,out error,checkpoint);
    }
}

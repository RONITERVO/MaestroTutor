// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Creation
{
    // An internal transaction intent, not a stored format or public capability ID.
    // Each scope declares exactly what history can restore and native views can update.
    internal enum RoomEditScope { Full, Visual, LiquidContents, Content }
    internal static class RoomEditFields
    {
        internal static bool CanApply(RoomObjectData current,RoomObjectData requested,RoomEditScope scope)=>
            scope==RoomEditScope.Content?current.id==requested.id&&current.kind==requested.kind:
            scope!=RoomEditScope.LiquidContents||
            current.containers?.Length==1&&current.containers[0]!=null&&requested.containers?.Length==1&&requested.containers[0]!=null;
        internal static RoomObjectData Apply(RoomObjectData current,RoomObjectData requested,RoomEditScope scope) {
            var value=current.Copy();
            if(scope==RoomEditScope.Content) {
                value=requested.Copy();value.position=current.position;value.rotation=current.rotation;value.scale=current.scale;
            } else if(scope==RoomEditScope.Visual) {
                var source=requested.Copy();value.visibilityLayer=source.visibilityLayer;value.lights=source.lights;value.windows=source.windows;
            } else if(scope==RoomEditScope.LiquidContents) {
                var source=requested.containers[0];var target=value.containers[0];
                target.amountMl=source.amountMl;target.liquid=source.liquid;target.color=source.color;
                target.fluid=RoomFluid.Effective(source.fluid).Copy();
            }
            return value;
        }
    }
    public sealed partial class RoomEditor
    {
        void ReconcileEdit(RoomJournal.PreparedEdit edit,RoomEditPreparation preparation,bool applyPose,bool history=false) {
            InvalidateLiquidReports(edit);
            if(edit.VisualOnly){ReconcileVisibility(preparation);return;}
            if(edit.LiquidContentsOnly) {
                // Preserve root/part animation, velocity, contacts, connection state
                // and native ownership. Missing views recover through residency.
                foreach(var id in edit.ChangedObjects) {
                    var item=Find(id);if(item)item.GetComponent<ContainerFillView>()?.ApplyLive(Read(id).containers);
                }
                Liquids?.Synchronize(journal.Snapshot());UpdateSelection();return;
            }
            if(history&&!edit.PreservesPlacement)Reconcile(poseChanges:edit.ChangedObjects,preparation:preparation);
            else Reconcile(edit.ChangedObjects,applyPose&&!edit.PreservesPlacement,preparation:preparation);
        }
    }
}

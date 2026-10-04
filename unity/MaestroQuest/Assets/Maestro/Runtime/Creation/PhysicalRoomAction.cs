// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public enum RoomTool { Block, Ball, Cylinder, Pencil, Paint, Duplicate, Erase, Undo, Redo, Save, Recall, BeginTemporary, KeepTemporary, DiscardTemporary, SurfacePencil, CollectPieces, MovePieces, SculptLower, SculptRaise, SculptLevel }
    public sealed class PhysicalRoomAction : PhysicalAction
    {
        public RoomEditor Editor;
        public RoomInteraction Room;
        public RoomTool Tool;
        public Color Paint;
        void RunSession(string operation) {Maestro.Quest.Programs.RoomSessionCapability.RunManual(Editor,operation,out var message);Editor.ReportStatus(message);}
        protected override void OnActivate()
        {
            if (!Editor) return;
            var sculpt=Editor.GetComponent<SpatialSculpting>();
            if(sculpt&&sculpt.Retained){if(Tool==RoomTool.Pencil||Tool==RoomTool.SurfacePencil||Tool==RoomTool.Save){sculpt.ResolveManual(false);return;}if(Tool==RoomTool.Erase){sculpt.ResolveManual(true);return;}}
            switch (Tool)
            {
                case RoomTool.SculptLower: case RoomTool.SculptRaise: case RoomTool.SculptLevel:
                    string mode=Tool==RoomTool.SculptLower?"lower":Tool==RoomTool.SculptRaise?"raise":"level";var tool=Editor.Sculpting;
                    if(!tool.Configure(tool.Mode==mode?"off":mode,tool.Radius,tool.Height,out var sculptError))Editor.ReportStatus(sculptError);break;
                case RoomTool.Block: Editor.Create(RoomObjectKind.Block); break;
                case RoomTool.Ball: Editor.Create(RoomObjectKind.Ball); break;
                case RoomTool.Cylinder: Editor.Create(RoomObjectKind.Cylinder); break;
                case RoomTool.Pencil: if(Editor.GetComponent<SpatialDrawing>() is SpatialDrawing pencil&&pencil.HasUnsavedStroke)pencil.ResolveManual(false);else Editor.ToggleDrawing(); break;
                case RoomTool.SurfacePencil: if(Editor.GetComponent<SpatialDrawing>() is SpatialDrawing surface&&surface.HasUnsavedStroke)surface.ResolveManual(false);else Editor.ToggleSurfaceDrawing(); break;
                case RoomTool.Paint: Editor.ChoosePaint(Paint); break;
                case RoomTool.Duplicate: Editor.Duplicate(); break;
                case RoomTool.Erase: if(Editor.GetComponent<SpatialDrawing>() is SpatialDrawing stroke&&stroke.HasUnsavedStroke)stroke.ResolveManual(true);else if(Editor.DrawingOnSurfaces){if(!Editor.ConfigureDrawing("surfaceErase",Editor.Paint,Editor.DrawingRadius,out var error))Editor.ReportStatus(error);}else Editor.Erase(); break;
                case RoomTool.Undo: Editor.Undo(); break;
                case RoomTool.Redo: Editor.Redo(); break;
                case RoomTool.Save: if(Editor.GetComponent<SpatialDrawing>() is SpatialDrawing pendingStroke&&pendingStroke.HasUnsavedStroke)pendingStroke.ResolveManual(false);else if(Editor.TemporaryRoom) {Maestro.Quest.Programs.RoomSessionCapability.RunManual(Editor,"keep",out var saveStatus);Editor.ReportStatus(saveStatus);} else Editor.SaveNow(); break;
                case RoomTool.BeginTemporary: RunSession("begin");break;
                case RoomTool.KeepTemporary: RunSession("keep");break;
                case RoomTool.DiscardTemporary: RunSession("discard");break;
                case RoomTool.CollectPieces: var selection=Editor.ObserveConstructionSelection();Maestro.Quest.Programs.ConstructionSelectionCapability.RunManual(Editor,selection.members,!selection.collecting,out var selectionStatus);Editor.ReportStatus(selectionStatus);break;
                case RoomTool.MovePieces: Maestro.Quest.Programs.ConstructionManipulationCapability.RunManual(Editor,!Editor.ObserveConstructionManipulation().visible,out var moveStatus);Editor.ReportStatus(moveStatus);break;
                case RoomTool.Recall: Room.RestoreInFrontOfViewer(); break;
            }
        }
    }
}

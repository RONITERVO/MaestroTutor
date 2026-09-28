// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public enum RoomTool { Block, Ball, Cylinder, Pencil, Paint, Duplicate, Erase, Undo, Redo, Save, Recall, BeginTemporary, KeepTemporary, DiscardTemporary }
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
            switch (Tool)
            {
                case RoomTool.Block: Editor.Create(RoomObjectKind.Block); break;
                case RoomTool.Ball: Editor.Create(RoomObjectKind.Ball); break;
                case RoomTool.Cylinder: Editor.Create(RoomObjectKind.Cylinder); break;
                case RoomTool.Pencil: Editor.ToggleDrawing(); break;
                case RoomTool.Paint: Editor.ChoosePaint(Paint); break;
                case RoomTool.Duplicate: Editor.Duplicate(); break;
                case RoomTool.Erase: Editor.Erase(); break;
                case RoomTool.Undo: Editor.Undo(); break;
                case RoomTool.Redo: Editor.Redo(); break;
                case RoomTool.Save: if(Editor.TemporaryRoom) {Maestro.Quest.Programs.RoomSessionCapability.RunManual(Editor,"keep",out var saveStatus);Editor.ReportStatus(saveStatus);} else Editor.SaveNow(); break;
                case RoomTool.BeginTemporary: RunSession("begin");break;
                case RoomTool.KeepTemporary: RunSession("keep");break;
                case RoomTool.DiscardTemporary: RunSession("discard");break;
                case RoomTool.Recall: Room.RestoreInFrontOfViewer(); break;
            }
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public enum RoomTool { Block, Ball, Cylinder, Pencil, Paint, Duplicate, Erase, Undo, Redo, Save, Recall }
    public sealed class PhysicalRoomAction : PhysicalAction
    {
        public RoomEditor Editor;
        public RoomInteraction Room;
        public RoomTool Tool;
        public Color Paint;
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
                case RoomTool.Save: Editor.SaveNow(); break;
                case RoomTool.Recall: Room.RestoreInFrontOfViewer(); break;
            }
        }
    }
}

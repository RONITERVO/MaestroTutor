// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>Solid, movable tools beside the book. Labels are noninteractive surface markings.</summary>
    public sealed class RoomToolTray : MonoBehaviour
    {
        readonly List<Material> materials = new();
        RoomEditor editor;
        RoomInteraction room;
        TextMesh status;
        Material pencilPaint;
        public void Build(RoomEditor editor, RoomInteraction room)
        {
            this.editor = editor; this.room = room;
            var wood = Material(IllustratedMaterials.Hex("C89D65")); var paper = Material(IllustratedMaterials.Paper);
            var teal = Material(IllustratedMaterials.Hex("2B8D88")); var ink = Material(IllustratedMaterials.Ink);
            Part(transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.62f,.42f,.025f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.center = new Vector3(0,0,.007f); handle.size = new Vector3(.62f,.42f,.025f);
            var movable = gameObject.AddComponent<RoomItem>(); movable.Configure(new Collider[] { handle },1,1); room.Register(movable);
            var kinds = new[] { RoomTool.Block, RoomTool.Ball, RoomTool.Cylinder };
            var primitives = new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder };
            for (int i = 0; i < 3; i++)
            {
                var tool = Tool(kinds[i],new Vector3(-.24f + i*.12f,.12f,-.04f),kinds[i].ToString());
                Part(tool,primitives[i],Vector3.zero,primitives[i] == PrimitiveType.Cylinder ? new Vector3(.046f,.025f,.046f) : Vector3.one*.046f,teal);
            }
            var pencil = Tool(RoomTool.Pencil,new Vector3(.12f,.12f,-.04f),"Draw");
            pencilPaint = Material(IllustratedMaterials.Ribbon);
            Part(pencil,PrimitiveType.Cylinder,new Vector3(0,.012f,0),new Vector3(.016f,.031f,.016f),pencilPaint);
            Part(pencil,PrimitiveType.Sphere,new Vector3(0,-.025f,0),Vector3.one*.012f,ink);
            var copy = Tool(RoomTool.Duplicate,new Vector3(.24f,.12f,-.04f),"Copy");
            Part(copy,PrimitiveType.Cube,new Vector3(-.011f,.007f,.005f),Vector3.one*.038f,paper);
            Part(copy,PrimitiveType.Cube,new Vector3(.011f,-.007f,-.014f),Vector3.one*.038f,teal);
            var colors = new[] { IllustratedMaterials.Cover, IllustratedMaterials.Hex("2B8D88"), IllustratedMaterials.Ribbon, IllustratedMaterials.Hex("B8644E"), IllustratedMaterials.Ink };
            for (int i = 0; i < colors.Length; i++)
            {
                var well = Tool(RoomTool.Paint,new Vector3(-.24f+i*.12f,0,-.032f),"Paint"); well.GetComponent<PhysicalRoomAction>().Paint = colors[i];
                Part(well,PrimitiveType.Sphere,Vector3.zero,new Vector3(.052f,.052f,.025f),Material(colors[i]));
            }
            var bottom = new[] { RoomTool.Erase, RoomTool.Undo, RoomTool.Redo, RoomTool.Save, RoomTool.Recall };
            var labels = new[] { "Erase", "Undo", "Redo", "Save", "Bring back" };
            for (int i = 0; i < bottom.Length; i++)
            {
                var tool = Tool(bottom[i],new Vector3(-.24f+i*.12f,-.11f,-.035f),labels[i]);
                if (bottom[i] == RoomTool.Undo || bottom[i] == RoomTool.Redo) Arrow(tool,bottom[i] == RoomTool.Undo ? -1 : 1,teal);
                else
                {
                    Part(tool,PrimitiveType.Cube,Vector3.zero,new Vector3(.05f,.037f,.027f),bottom[i] == RoomTool.Erase ? Material(IllustratedMaterials.Hex("C98087")) : teal);
                    if (bottom[i] == RoomTool.Save || bottom[i] == RoomTool.Recall) Part(tool,PrimitiveType.Cube,new Vector3(0,0,-.016f),new Vector3(.027f,.024f,.006f),paper);
                }
            }
            status = Label(transform,new Vector3(0,-.192f,-.020f),"",.0048f);
            editor.Changed += Refresh; Refresh();
        }

        Transform Tool(RoomTool kind, Vector3 position, string label)
        {
            var root = new GameObject(label); root.transform.SetParent(transform,false); root.transform.localPosition = position;
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(.085f,.082f,.08f);
            var action = root.AddComponent<PhysicalRoomAction>(); action.Tool = kind; action.Editor = editor; action.Room = room; action.AccessibleName = label;
            Label(root.transform,new Vector3(0,-.05f,-.023f),label,.006f);
            return root.transform;
        }

        void Refresh()
        {
            status.text = editor.Status;
            if (status.text.Length > 70) status.text = status.text.Substring(0,70) + "…";
            pencilPaint.color = editor.DrawingMode ? IllustratedMaterials.Hex("2B8D88") : IllustratedMaterials.Ribbon;
        }
        static TextMesh Label(Transform parent, Vector3 position, string text, float size)
        {
            var label = new GameObject("Tool marking",typeof(TextMesh)); label.transform.SetParent(parent,false); label.transform.localPosition = position;
            var mesh = label.GetComponent<TextMesh>(); mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.text = text; mesh.fontSize = 48; mesh.characterSize = size; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.color = IllustratedMaterials.Ink; label.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }
        static void Arrow(Transform parent, float sign, Material material)
        {
            Part(parent,PrimitiveType.Cube,Vector3.zero,new Vector3(.044f,.01f,.012f),material);
            foreach (float y in new[] { -1f,1f })
            {
                var stroke = Part(parent,PrimitiveType.Cube,new Vector3(sign*.015f,y*.008f,0),new Vector3(.025f,.009f,.012f),material);
                stroke.transform.localRotation = Quaternion.Euler(0,0,sign*y*-45);
            }
        }
        static GameObject Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var shape = GameObject.CreatePrimitive(type); shape.transform.SetParent(parent,false); shape.transform.localPosition = position; shape.transform.localScale = size;
            shape.GetComponent<Collider>().enabled = false; ArtResources.Release(shape.GetComponent<Collider>());
            shape.GetComponent<Renderer>().sharedMaterial = material; return shape;
        }
        Material Material(Color color) { var material = IllustratedMaterials.Create(color); materials.Add(material); return material; }
        void OnDestroy() { if (editor) editor.Changed -= Refresh; foreach (var material in materials) ArtResources.Release(material); }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    public sealed class ImportTools : MonoBehaviour
    {
        readonly List<Material> materials = new();
        ImportWorkshop workshop;
        TextMesh details, status;
        public void Build(ImportWorkshop owner, RoomInteraction room)
        {
            workshop = owner;
            var wood = Paint("C89D65"); var teal = Paint("2B8D88"); var purple = Paint("73534E");
            Part(transform, Vector3.zero, new Vector3(.72f, .94f, .04f), wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.72f, .94f, .04f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle }, 1, 1); room.Register(item);
            var labels = new[] { "Import", "Add model", "Cancel", "More info", "Next clip", "Play", "Stop", "Loop", "Use Maestro", "Default" };
            Action[] commands = { owner.Pick, owner.Accept, owner.Cancel, owner.NextDetails, owner.NextClip, owner.Play, owner.Stop, owner.ToggleLoop, owner.UseMaestro, owner.DefaultMaestro };
            for (int i = 0; i < labels.Length; i++)
            {
                var tool = new GameObject(labels[i]); tool.transform.SetParent(transform, false); tool.transform.localPosition = new Vector3(-.255f + i % 4 * .17f, -.02f - i / 4 * .12f, -.05f);
                var collider = tool.AddComponent<BoxCollider>(); collider.size = new Vector3(.10f, .076f, .065f);
                var action = tool.AddComponent<RuleToolAction>(); action.Command = commands[i]; action.AccessibleName = labels[i];
                Part(tool.transform, Vector3.zero, new Vector3(.06f, .036f, .04f), i == 1 ? purple : teal);
                Label(tool.transform, new Vector3(0, -.048f, -.024f), labels[i], .0055f);
            }
            details = Label(transform, new Vector3(0, .25f, -.023f), "", .0045f);
            status = Label(transform, new Vector3(0, -.365f, -.023f), "", .0046f);
            Label(transform, new Vector3(0, -.435f, -.023f), "Add / Use Maestro confirms you may use this model", .0043f);
            owner.Changed += Refresh; Refresh();
        }
        void Refresh() { details.text = workshop.Details; status.text = string.Join("\n", ModelText.Wrap(workshop.Status, 65).Take(3)); }
        Material Paint(string color) { var value = IllustratedMaterials.Create(IllustratedMaterials.Hex(color)); materials.Add(value); return value; }
        static void Part(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Collider>().enabled = false; ArtResources.Release(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = material;
        }
        static TextMesh Label(Transform parent, Vector3 position, string text, float size)
        {
            var root = new GameObject("Import tool marking", typeof(TextMesh)); root.transform.SetParent(parent, false); root.transform.localPosition = position;
            var mesh = root.GetComponent<TextMesh>(); mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); mesh.fontSize = 48; mesh.characterSize = size;
            mesh.text = text; mesh.richText = false; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = IllustratedMaterials.TextColor(IllustratedMaterials.Ink);
            root.GetComponent<MeshRenderer>().sharedMaterial = IllustratedMaterials.TextMaterial(mesh.font); return mesh;
        }
        void OnDestroy() { if (workshop) workshop.Changed -= Refresh; foreach (var material in materials) ArtResources.Release(material); }
    }
}

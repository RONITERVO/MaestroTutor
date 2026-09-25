// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    public sealed class RuleToolAction : PhysicalAction
    {
        public Action Command;
        protected override void OnActivate() => Command?.Invoke();
    }
    public sealed class RuleTools : MonoBehaviour
    {
        readonly List<Material> materials = new();
        RuleWorkshop workshop;
        TextMesh summary, status;
        public void Build(RuleWorkshop owner, RoomInteraction room)
        {
            workshop = owner;
            var wood = Paint("C89D65"); var teal = Paint("2B8D88"); var plum = Paint("73534E");
            Part(transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.94f,.87f,.045f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.94f,.87f,.045f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item);
            var labels = new[] { "New action","Prev action","Next action","Delete action","Try action","Stop actions",
                "Step type","Use target","Prev step","Next step","Add step","Delete step",
                "Duration","Gesture","Clip loop","Repeat","On interrupt","While state",
                "Event","Event source","Condition","Add trigger","Next trigger","Remove trigger",
                "Left button","Right button","Room button","Remove button","Undo rules","Redo rules" };
            Action[] commands = { owner.NewSequence,() => owner.SelectSequence(-1),() => owner.SelectSequence(1),owner.DeleteSequence,() => owner.Runtime.TrySelected(),() => { owner.Runtime.StopAll(); owner.Say("All rule actions stopped"); },
                owner.CycleAction,owner.UseTarget,() => owner.Step(-1),() => owner.Step(1),owner.AddStep,owner.DeleteStep,
                owner.CycleTime,owner.CycleGesture,owner.ToggleClipLoop,owner.ToggleRepeat,owner.CyclePolicy,owner.ToggleWhileState,
                owner.CycleEvent,owner.UseSource,owner.CycleCondition,owner.AddBinding,owner.NextBinding,owner.RemoveBinding,
                () => owner.AddButton(ButtonMount.LeftController),() => owner.AddButton(ButtonMount.RightController),() => owner.AddButton(ButtonMount.Room),owner.RemoveButton,owner.Undo,owner.Redo };
            for (int i = 0; i < labels.Length; i++)
            {
                var tool = new GameObject(labels[i]); tool.transform.SetParent(transform,false); tool.transform.localPosition = new Vector3(-.375f + (i%6)*.15f,.19f-(i/6)*.12f,-.049f);
                var collider = tool.AddComponent<BoxCollider>(); collider.size = new Vector3(.10f,.076f,.08f);
                var action = tool.AddComponent<RuleToolAction>(); action.AccessibleName = labels[i]; action.Command = commands[i];
                Part(tool.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.045f,.015f,.045f),i >= 24 ? plum : teal).transform.localRotation = Quaternion.Euler(90,0,0);
                Label(tool.transform,new Vector3(0,-.052f,-.02f),labels[i].Replace(" ","\n"),.0053f);
            }
            summary = Label(transform,new Vector3(0,.34f,-.027f),"",.0046f);
            status = Label(transform,new Vector3(0,-.397f,-.027f),"",.0047f);
            workshop.Changed += Refresh; Refresh();
        }
        void Refresh() { summary.text = workshop.Summary; status.text = workshop.Status.Length > 92 ? workshop.Status.Substring(0,92)+"…" : workshop.Status; }
        Material Paint(string value) { var paint = IllustratedMaterials.Create(IllustratedMaterials.Hex(value)); materials.Add(paint); return paint; }
        static GameObject Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Material paint)
        {
            var part = GameObject.CreatePrimitive(type); part.transform.SetParent(parent,false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Collider>().enabled = false; ArtResources.Release(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = paint; return part;
        }
        static TextMesh Label(Transform parent, Vector3 position, string text, float size)
        {
            var root = new GameObject("Rule tool marking",typeof(TextMesh)); root.transform.SetParent(parent,false); root.transform.localPosition = position;
            var label = root.GetComponent<TextMesh>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 48; label.characterSize = size;
            label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = IllustratedMaterials.TextColor(IllustratedMaterials.Ink);
            root.GetComponent<MeshRenderer>().sharedMaterial = IllustratedMaterials.TextMaterial(label.font); return label;
        }
        void OnDestroy() { if (workshop) workshop.Changed -= Refresh; foreach (var material in materials) ArtResources.Release(material); }
    }
}

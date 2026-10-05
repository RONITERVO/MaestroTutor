// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public enum AnimationTool { Pose, Record, AddFrame, Play, Stop, Previous, Next, Replace, Delete, Loop, Gesture, Automatic, Faster, Slower, DiscardTake, SavePose, DiscardPose }
    public sealed class AnimationToolAction : PhysicalAction
    {
        public AnimationWorkshop Workshop;
        public AnimationTool Tool;
        protected override void OnActivate()
        {
            switch (Tool)
            {
                case AnimationTool.Pose: Workshop.TogglePose(); break;
                case AnimationTool.Record: Workshop.ToggleRecord(); break;
                case AnimationTool.DiscardTake: Workshop.DiscardTake(); break;
                case AnimationTool.SavePose: Workshop.SaveRetainedPose(); break;
                case AnimationTool.DiscardPose: Workshop.DiscardRetainedPose(); break;
                case AnimationTool.AddFrame: Workshop.AddFrame(); break;
                case AnimationTool.Play: Workshop.Play(); break;
                case AnimationTool.Stop: Workshop.Stop(); break;
                case AnimationTool.Previous: Workshop.StepFrame(-1); break;
                case AnimationTool.Next: Workshop.StepFrame(1); break;
                case AnimationTool.Replace: Workshop.ReplaceFrame(); break;
                case AnimationTool.Delete: Workshop.DeleteFrame(); break;
                case AnimationTool.Loop: Workshop.ToggleLoop(); break;
                case AnimationTool.Gesture: Workshop.Gesture(); break;
                case AnimationTool.Automatic: Workshop.ResetPose(); break;
                case AnimationTool.Faster: Workshop.ChangeSpeed(.8f); break;
                case AnimationTool.Slower: Workshop.ChangeSpeed(1.25f); break;
            }
        }
    }

    /// <summary>A movable, solid animation box. Surface markings are not separate panels.</summary>
    public sealed class AnimationTools : MonoBehaviour
    {
        readonly List<Material> materials = new();
        AnimationWorkshop workshop;
        TextMesh status;
        Material recordPaint, posePaint;
        public void Build(AnimationWorkshop owner, RoomInteraction room)
        {
            workshop = owner;
            var wood = Paint("C89D65"); var teal = Paint("2B8D88"); var paper = Paint("FFF0D2");
            recordPaint = Paint("B8644E"); posePaint = Paint("73534E");
            Part(transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.76f,.50f,.055f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.76f,.50f,.055f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item);
            var kinds = new[] { AnimationTool.Pose,AnimationTool.Record,AnimationTool.AddFrame,AnimationTool.Play,AnimationTool.Stop,AnimationTool.Loop,
                AnimationTool.Previous,AnimationTool.Next,AnimationTool.Replace,AnimationTool.Delete,AnimationTool.Gesture,AnimationTool.Automatic,AnimationTool.Faster,AnimationTool.Slower,AnimationTool.DiscardTake,AnimationTool.SavePose,AnimationTool.DiscardPose };
            var labels = new[] { "Pose Maestro","Record","Save frame","Play","Stop","Loop","Earlier","Later","Replace","Remove","Gesture","Auto gestures","Faster","Slower","Discard take","Save pose","Discard pose" };
            for (int i = 0; i < kinds.Length; i++)
            {
                var tool = new GameObject(labels[i]); tool.transform.SetParent(transform,false);
                tool.transform.localPosition = new Vector3(-.31f+(i%6)*.124f,.17f-(i/6)*.135f,-.058f);
                var collider = tool.AddComponent<BoxCollider>(); collider.size = new Vector3(.09f,.08f,.075f);
                var action = tool.AddComponent<AnimationToolAction>(); action.Workshop = workshop; action.Tool = kinds[i]; action.AccessibleName = labels[i];
                var paint = kinds[i] == AnimationTool.Record ? recordPaint : kinds[i] == AnimationTool.Pose ? posePaint : teal;
                Part(tool.transform,kinds[i] == AnimationTool.Record || kinds[i] == AnimationTool.Pose ? PrimitiveType.Sphere : PrimitiveType.Cube,Vector3.zero,new Vector3(.046f,.041f,.035f),paint);
                if (kinds[i] == AnimationTool.AddFrame || kinds[i] == AnimationTool.Replace) Part(tool.transform,PrimitiveType.Cube,new Vector3(0,0,-.02f),new Vector3(.027f,.025f,.006f),paper);
                Label(tool.transform,new Vector3(0,-.059f,-.02f),labels[i].Replace(" ","\n"),.0044f);
            }
            status = Label(transform,new Vector3(0,-.215f,-.029f),"",.0048f);
            workshop.Changed += Refresh; Refresh();
        }
        void OnEnable()=>Refresh();
        void Refresh()
        {
            if(!isActiveAndEnabled||!workshop||!status||!recordPaint||!posePaint)return;
            status.text = workshop.Status.Length > 80 ? workshop.Status.Substring(0,80) + "…" : workshop.Status;
            recordPaint.color = IllustratedMaterials.Hex(workshop.IsRecording ? "F04C42" : workshop.HasUnsavedRecording ? "E5A42A" : "B8644E");
            posePaint.color = IllustratedMaterials.Hex(workshop.HasUnsavedPose ? "E5A42A" : workshop.IsPosing ? "2B8D88" : "73534E");
        }
        Material Paint(string hex) { var value = IllustratedMaterials.Create(IllustratedMaterials.Hex(hex)); materials.Add(value); return value; }
        static void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Material paint)
        {
            var part = GameObject.CreatePrimitive(type); part.transform.SetParent(parent,false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Collider>().enabled = false; ArtResources.Release(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = paint;
        }
        static TextMesh Label(Transform parent, Vector3 position, string text, float size)
        {
            var root = new GameObject("Animation tool marking",typeof(TextMesh)); root.transform.SetParent(parent,false); root.transform.localPosition = position;
            var label = root.GetComponent<TextMesh>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 48; label.characterSize = size;
            label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = IllustratedMaterials.TextColor(IllustratedMaterials.Ink);
            root.GetComponent<MeshRenderer>().sharedMaterial = IllustratedMaterials.TextMaterial(label.font); return label;
        }
        void OnDestroy() { if (workshop) workshop.Changed -= Refresh; foreach (var material in materials) ArtResources.Release(material); }
    }
}

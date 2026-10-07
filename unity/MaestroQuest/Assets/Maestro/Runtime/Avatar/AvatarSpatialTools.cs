// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    public sealed class AvatarSpatialTools : MonoBehaviour
    {
        readonly List<Material> materials = new();
        AvatarSpatialMotion motion;
        RoomEditor editor;
        AnimationWorkshop animations;
        RoomRules rules;
        MaestroAvatar avatar;
        TextMesh status, preferences;
        public void Build(AvatarSpatialMotion movement,RoomEditor source,AnimationWorkshop authoring,RoomRules behaviour,RoomInteraction room)
        {
            motion = movement; editor = source; animations = authoring; rules = behaviour; avatar = editor ? editor.Find("maestro").GetComponent<MaestroAvatar>() : null;
            var wood = Paint("C89D65"); var teal = Paint("2B8D88");
            Part(transform,Vector3.zero,new Vector3(.72f,.59f,.04f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.72f,.59f,.04f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item,true);
            string[] labels = { "Look at me","Follow me","Stop","Distance","Walk speed","Size","Walk clip","Preview walk" };
            Action[] commands = { () => Begin(AvatarSpatialMode.Look),() => Begin(AvatarSpatialMode.Follow),Stop,Distance,Speed,Size,WalkClip,PreviewWalk };
            for (int i=0;i<labels.Length;i++)
            {
                var tool = new GameObject(labels[i]); tool.transform.SetParent(transform,false); tool.transform.localPosition = new Vector3(-.23f+i%3*.23f,.14f-i/3*.12f,-.05f);
                var collider = tool.AddComponent<BoxCollider>(); collider.size = new Vector3(.10f,.065f,.065f);
                var action = tool.AddComponent<RuleToolAction>(); action.Command = commands[i]; action.AccessibleName = labels[i];
                Part(tool.transform,Vector3.zero,new Vector3(.06f,.03f,.04f),teal);
                Label(tool.transform,new Vector3(0,-.04f,-.024f),labels[i],.0048f);
            }
            preferences = Label(transform,new Vector3(0,.245f,-.023f),"",.0047f);
            status = Label(transform,new Vector3(0,-.225f,-.023f),"",.0044f);
            motion.Changed += Refresh; if (editor) editor.Changed += Refresh; if (avatar) { avatar.ModelChanged += Refresh; avatar.WalkMotionChanged += Refresh; } if (animations) animations.Changed += Refresh; Refresh();
        }
        void Begin(AvatarSpatialMode mode) { RoomControls.AvatarMotion(editor,mode == AvatarSpatialMode.Look ? "look" : "follow",out _); }
        void Stop() { RoomControls.AvatarMotion(editor,"stop",out _); }
        void Distance() { float[] values = { .8f,1.3f,1.8f,2.5f }; float next = values.FirstOrDefault(x => x > motion.Distance+.01f); editor.SetAvatarMovement(next == 0 ? values[0] : next,motion.Speed); }
        void Speed() { float[] values = { .35f,.65f,1f }; float next = values.FirstOrDefault(x => x > motion.Speed+.01f); editor.SetAvatarMovement(motion.Distance,next == 0 ? values[0] : next); }
        void Size()
        {
            float[] values = { .35f,.5f,.75f,1f }; float next = values.FirstOrDefault(x => x > editor.Read("maestro").scale+.01f);
            editor.SetAvatarSize(next == 0 ? values[0] : next);
        }
        void WalkClip()
        {
            var choices = new List<int> { -1 }; var model = avatar.CustomModel;
            if (model) for (int i=0;i<model.ClipCount;i++) if (model.ClipDuration(i) >= .1f) choices.Add(i);
            var saved = editor.Motions.List(rigHash:model ? model.MotionRigHash ?? "" : "");
            int at = string.IsNullOrEmpty(avatar.WalkMotionId) ? choices.IndexOf(avatar.WalkClip) : choices.Count+Array.FindIndex(saved,x => x.id == avatar.WalkMotionId);
            int next = (at+1)%(choices.Count+saved.Length);
            if (next < choices.Count) editor.SetAvatarWalkClip(choices[next]); else editor.SetAvatarWalkMotion(saved[next-choices.Count].id);
        }
        void PreviewWalk() { motion.Stop(); rules?.Scheduler.StopTarget("maestro",true); animations.PreviewWalk(); Refresh(); }
        void OnEnable()=>Refresh();
        void Refresh()
        {
            if(!isActiveAndEnabled||!motion||!preferences||!status)return;
            preferences.text = "Maestro · Distance " + motion.Distance.ToString("0.0") + " m · Walk " + motion.Speed.ToString("0.00") + " m/s · Size " + (editor ? editor.Read("maestro").scale : 1f).ToString("0.00") + "×";
            status.text = string.Join("\n",ModelText.Wrap((animations && animations.ControlsTarget("maestro") ? animations.Status : motion.Status) + " · " + (avatar ? avatar.WalkClipName : "Included walk") + (avatar && avatar.WalkMotionStatus != null ? " · "+avatar.WalkMotionStatus : ""),62).Take(3));
        }
        Material Paint(string color) { var value = IllustratedMaterials.Create(IllustratedMaterials.Hex(color)); materials.Add(value); return value; }
        static void Part(Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(parent,false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false; ArtResources.Release(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = material;
        }
        static TextMesh Label(Transform parent,Vector3 position,string text,float size)
        {
            var root = new GameObject("Maestro tool marking",typeof(TextMesh)); root.transform.SetParent(parent,false); root.transform.localPosition = position;
            var value = root.GetComponent<TextMesh>(); value.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); value.fontSize = 48; value.characterSize = size;
            value.text = text; value.richText = false; value.anchor = TextAnchor.MiddleCenter; value.alignment = TextAlignment.Center; value.color = IllustratedMaterials.TextColor(IllustratedMaterials.Ink);
            root.GetComponent<MeshRenderer>().sharedMaterial = IllustratedMaterials.TextMaterial(value.font); return value;
        }
        void OnDestroy() { if (motion) motion.Changed -= Refresh; if (editor) editor.Changed -= Refresh; if (avatar) { avatar.ModelChanged -= Refresh; avatar.WalkMotionChanged -= Refresh; } if (animations) animations.Changed -= Refresh; foreach (var material in materials) ArtResources.Release(material); }
    }
}

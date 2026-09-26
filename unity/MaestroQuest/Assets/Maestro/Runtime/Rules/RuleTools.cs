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
        TextMesh summary, status,tabLabel;
        GameObject actionPage,propPage;
        RuleToolAction tab;
        public bool PropsVisible { get; private set; }
        public void Build(RuleWorkshop owner, RoomInteraction room)
        {
            workshop = owner;
            var wood = Paint("C89D65"); var teal = Paint("2B8D88"); var plum = Paint("73534E");
            Part(transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.94f,.87f,.045f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.94f,.87f,.045f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item);
            var labels = new[] { "New action","Prev action","Next action","Delete action","Try action","Stop actions",
                "Step type","Use target","Prev step","Next step","Add step","Delete step",
                "Duration","Motion","Clip loop","Repeat","On interrupt","While state",
                "Event","Event source","Condition","Add trigger","Next trigger","Remove trigger",
                "Left button","Right button","Room button","Remove button","Undo rules","Redo rules" };
            Action[] commands = { owner.NewSequence,() => owner.SelectSequence(-1),() => owner.SelectSequence(1),owner.DeleteSequence,() => owner.Runtime.TrySelected(),() => { owner.Runtime.StopAll(); owner.Say("All rule actions stopped"); },
                owner.CycleAction,owner.UseTarget,() => owner.Step(-1),() => owner.Step(1),owner.AddStep,owner.DeleteStep,
                owner.CycleTime,owner.CycleGesture,owner.ToggleClipLoop,owner.ToggleRepeat,owner.CyclePolicy,owner.ToggleWhileState,
                owner.CycleEvent,owner.UseSource,owner.CycleCondition,owner.AddBinding,owner.NextBinding,owner.RemoveBinding,
                () => owner.AddButton(ButtonMount.LeftController),() => owner.AddButton(ButtonMount.RightController),() => owner.AddButton(ButtonMount.Room),owner.RemoveButton,owner.Undo,owner.Redo };
            actionPage=new GameObject("Action controls"); actionPage.transform.SetParent(transform,false);
            propPage=new GameObject("Prop controls"); propPage.transform.SetParent(transform,false);
            for (int i=0;i<labels.Length;i++) Tool(actionPage.transform,i,labels[i],commands[i],i >= 24 ? plum : teal);
            string[] propLabels={ "Use prop","Prop hand","Fit prop","Prop release","Release time","Clear prop","Try action","Stop actions","Prev step","Next step","Undo rules","Redo rules" };
            Action[] propCommands={ owner.UseProp,owner.CyclePropHand,owner.FitProp,owner.CyclePropRelease,owner.CyclePropTime,owner.ClearProp,
                () => owner.Runtime.TrySelected(),() => { owner.Runtime.StopAll(); owner.Say("All rule actions stopped"); },() => owner.Step(-1),() => owner.Step(1),owner.Undo,owner.Redo };
            for (int i=0;i<propLabels.Length;i++) Tool(propPage.transform,i,propLabels[i],propCommands[i],i < 6 ? teal : plum);
            var tabRoot=new GameObject("Rule prop tab"); tabRoot.transform.SetParent(transform,false); tabRoot.transform.localPosition=new Vector3(.525f,.28f,-.049f);
            var tabCollider=tabRoot.AddComponent<BoxCollider>(); tabCollider.size=new Vector3(.10f,.076f,.08f);
            tab=tabRoot.AddComponent<RuleToolAction>(); tab.Command=() => ShowProps(!PropsVisible);
            Part(tabRoot.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.045f,.015f,.045f),plum).transform.localRotation=Quaternion.Euler(90,0,0);
            tabLabel=Label(tabRoot.transform,new Vector3(0,-.052f,-.02f),"Props",.0053f);
            summary = Label(transform,new Vector3(0,.34f,-.027f),"",.0046f);
            status = Label(transform,new Vector3(0,-.414f,-.027f),"",.0047f);
            ShowProps(false);
            workshop.Changed += Refresh; Refresh();
        }
        void Tool(Transform page,int index,string label,Action command,Material paint)
        {
            var tool=new GameObject(label); tool.transform.SetParent(page,false); tool.transform.localPosition=new Vector3(-.375f+(index%6)*.15f,.19f-(index/6)*.12f,-.049f);
            var collider=tool.AddComponent<BoxCollider>(); collider.size=new Vector3(.10f,.076f,.08f);
            var action=tool.AddComponent<RuleToolAction>(); action.AccessibleName=label; action.Command=command;
            Part(tool.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.045f,.015f,.045f),paint).transform.localRotation=Quaternion.Euler(90,0,0);
            Label(tool.transform,new Vector3(0,-.052f,-.02f),label.Replace(" ","\n"),.0053f);
        }
        public void ShowProps(bool value)
        {
            PropsVisible=value; actionPage.SetActive(!value); propPage.SetActive(value);
            tabLabel.text=value ? "Rules" : "Props"; tab.AccessibleName=value ? "Show action controls" : "Show prop controls";
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

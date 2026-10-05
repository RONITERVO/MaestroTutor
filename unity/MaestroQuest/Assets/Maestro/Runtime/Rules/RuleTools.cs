// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
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
        public CapabilityQuickEdit Draft {get;private set;}
        TextMesh summary, status,tabLabel,precisionLabel;
        GameObject actionPage,propPage;
        RuleToolAction tab;
        public bool PropsVisible { get; private set; }
        public void Build(RuleWorkshop owner, RoomInteraction room)
        {
            workshop = owner;Draft=new CapabilityQuickEdit(owner,owner.Editor);
            var wood = Paint("C89D65"); var teal = Paint("2B8D88"); var plum = Paint("73534E");
            Part(transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.94f,.87f,.045f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.94f,.87f,.045f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item);
            var labels = new[] { "New action","Prev action","Next action","Delete action","Try action","Stop actions",
                "Block type","Set field","Prev block","Next block","Add block","Delete block",
                "Prev field","Next field","Value -","Value +","Apply draft","Discard draft",
                "Event","Event source","Condition","Add trigger","Next trigger","Remove trigger",
                "Left button","Right button","Room button","Remove button","Undo rules","Redo rules" };
            Action[] commands = {
                () => Saved(owner.NewSequence),() => Saved(() => owner.SelectSequence(-1)),() => Saved(() => owner.SelectSequence(1)),() => Saved(owner.DeleteSequence),
                () => Saved(() => owner.Runtime.TrySelected()),() => {owner.Runtime.StopAll();Draft.Say("All behaviour playback stopped");Refresh();},
                () => Edit(() => Draft.CycleCapability()),() => Edit(Draft.SetField),() => Edit(() => Draft.Step(-1)),() => Edit(() => Draft.Step(1)),() => Edit(Draft.AddBlock),() => Edit(Draft.DeleteBlock),
                () => Edit(() => Draft.FieldStep(-1)),() => Edit(() => Draft.FieldStep(1)),() => Edit(() => Draft.Adjust(-1)),() => Edit(() => Draft.Adjust(1)),
                () => Edit(() => Draft.Apply()),() => Edit(Draft.Reload),
                () => Saved(owner.CycleEvent),() => Saved(owner.UseSource),() => Saved(owner.CycleCondition),() => Saved(owner.AddBinding),() => Saved(owner.NextBinding),() => Saved(owner.RemoveBinding),
                () => Saved(() => owner.AddButton(ButtonMount.LeftController)),() => Saved(() => owner.AddButton(ButtonMount.RightController)),() => Saved(() => owner.AddButton(ButtonMount.Room)),
                () => Saved(owner.RemoveButton),() => Saved(owner.Undo),() => Saved(owner.Redo) };
            actionPage=new GameObject("Action controls"); actionPage.transform.SetParent(transform,false);
            propPage=new GameObject("Prop controls"); propPage.transform.SetParent(transform,false);
            for (int i=0;i<labels.Length;i++) Tool(actionPage.transform,i,labels[i],commands[i],i >= 24 ? plum : teal);
            string[] propLabels={ "Use prop","Prop hand","Fit prop","Prop release","Release time","Clear prop","Try action","Stop actions","Prev block","Next block","Undo rules","Redo rules",
                "Repeat","On interrupt","While state" };
            Action[] propCommands={
                () => Prop(owner.UseProp),() => Prop(owner.CyclePropHand),() => Prop(owner.FitProp),() => Prop(owner.CyclePropRelease),() => Prop(owner.CyclePropTime),() => Prop(owner.ClearProp),
                () => Saved(() => owner.Runtime.TrySelected()),() => {owner.Runtime.StopAll();Draft.Say("All behaviour playback stopped");Refresh();},
                () => Edit(() => Draft.Step(-1)),() => Edit(() => Draft.Step(1)),() => Saved(owner.Undo),() => Saved(owner.Redo),
                () => Saved(owner.ToggleRepeat),() => Saved(owner.CyclePolicy),() => Saved(owner.ToggleWhileState) };
            for (int i=0;i<propLabels.Length;i++) Tool(propPage.transform,i,propLabels[i],propCommands[i],i < 6 ? teal : plum);
            var bookRoot=new GameObject("Book editor control");bookRoot.transform.SetParent(transform,false);bookRoot.transform.localPosition=new Vector3(.525f,.05f,-.049f);
            var bookCollider=bookRoot.AddComponent<BoxCollider>();bookCollider.size=new Vector3(.10f,.076f,.08f);
            var bookAction=bookRoot.AddComponent<RuleToolAction>();bookAction.AccessibleName="Edit behaviour in book";bookAction.Command=OpenBook;
            Part(bookRoot.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.045f,.015f,.045f),teal).transform.localRotation=Quaternion.Euler(90,0,0);
            Label(bookRoot.transform,new Vector3(0,-.052f,-.02f),"Edit in\nbook",.0053f);
            var precisionRoot=new GameObject("Numeric precision control");precisionRoot.transform.SetParent(transform,false);precisionRoot.transform.localPosition=new Vector3(.525f,-.18f,-.049f);
            var precisionCollider=precisionRoot.AddComponent<BoxCollider>();precisionCollider.size=new Vector3(.10f,.076f,.08f);
            var precisionAction=precisionRoot.AddComponent<RuleToolAction>();precisionAction.AccessibleName="Change numeric step";precisionAction.Command=() => Edit(Draft.CyclePrecision);
            Part(precisionRoot.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.045f,.015f,.045f),teal).transform.localRotation=Quaternion.Euler(90,0,0);
            precisionLabel=Label(precisionRoot.transform,new Vector3(0,-.052f,-.02f),"",.0053f);
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
            if(value&&!Draft.Clean()) {Refresh();return;}
            PropsVisible=value; actionPage.SetActive(!value); propPage.SetActive(value);
            tabLabel.text=value ? "Rules" : "Props"; tab.AccessibleName=value ? "Show action controls" : "Show prop controls";
            Refresh();
        }
        void Edit(Action action) {action();Refresh();}
        void Saved(Action action) {if(Draft.Clean()) {action();Draft.Refresh();Draft.Say(workshop.Status);}Refresh();}
        void Prop(Action action) {if(Draft.SelectLegacyStep()) {action();Draft.Refresh();Draft.Say(workshop.Status);}Refresh();}
        void OpenBook() {
            if(!Draft.Clean()) {Refresh();return;}
            var agent=workshop.Editor.GetComponentInParent<RoomAgent>();
            if(agent&&agent.OpenRules(workshop.Selected?.id,out var error))Draft.Say("Full behaviour editor opened on the book");
            else Draft.Say("The book workspace is unavailable");
            Refresh();
        }
        void OnEnable()=>Refresh();
        void Refresh() {
            if(!isActiveAndEnabled||Draft==null||!summary||!status)return;
            if(Draft==null||!summary||!status)return;Draft.Refresh();
            summary.text=PropsVisible&&workshop.SelectLiteralNode(Draft.NodeId)?workshop.Summary:Draft.Summary+"\n"+workshop.TriggerSummary;
            if(precisionLabel)precisionLabel.text="Step\n"+Draft.PrecisionLabel;
            status.text=Draft.Status.Length>92?Draft.Status.Substring(0,92)+"…":Draft.Status;
        }
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

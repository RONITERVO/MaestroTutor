// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    public sealed class MovementTools : MonoBehaviour
    {
        readonly List<Material> paints=new();
        MovementControls controls;
        TextMesh summary,status;
        int selectedButton;
        static readonly string[] Buttons={ "X","A","Left stick click","Right stick click" };
        public void Build(MovementControls owner,RoomInteraction room)
        {
            controls=owner; var wood=Paint("C89D65"); var teal=Paint("2B8D88");
            Part(transform,Vector3.zero,new Vector3(.92f,1f,.04f),wood);
            var handle=gameObject.AddComponent<BoxCollider>(); handle.size=new Vector3(.92f,1f,.04f);
            var item=gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item,true);
            string[] labels={ "Maestro stick","Your movement","Virtual / MR","Maestro binding","Your binding","Swap sticks","Walk speed","Dead zone","Select button","Use action","Button command","Stop / MR","World origin","Backdrop","Real occlusion" };
            Action[] commands={ owner.ToggleAvatar,owner.ToggleUser,owner.ToggleView,() => owner.CycleStick(false),() => owner.CycleStick(true),owner.SwapSticks,Speed,DeadZone,SelectButton,() => owner.BindSelected(selectedButton),Command,owner.Recover,owner.ReturnToWorldOrigin,owner.CycleBackdrop,owner.ToggleRealDepth };
            for (int i=0;i<labels.Length;i++)
            {
                var tool=new GameObject(labels[i]); tool.transform.SetParent(transform,false); tool.transform.localPosition=new Vector3(-.3f+i%3*.3f,.13f-i/3*.115f,-.05f);
                var collider=tool.AddComponent<BoxCollider>(); collider.size=new Vector3(.14f,.065f,.065f);
                var action=tool.AddComponent<RuleToolAction>(); action.Command=commands[i]; action.AccessibleName=labels[i];
                Part(tool.transform,Vector3.zero,new Vector3(.075f,.027f,.045f),teal);
                Label(tool.transform,new Vector3(0,-.04f,-.025f),labels[i],.0048f);
            }
            summary=Label(transform,new Vector3(0,.28f,-.026f),"",.0047f);
            status=Label(transform,new Vector3(0,-.43f,-.026f),"",.0042f);
            owner.Changed+=Refresh; Refresh();
        }
        void SelectButton() { selectedButton=(selectedButton+1)%4; Refresh(); }
        void Command() { var current=controls.Preferences.buttons[selectedButton].command; controls.BindButton(selectedButton,(ControllerCommand)(((int)current+1)%3)); }
        void Speed() { var next=controls.Preferences; next.userSpeed=next.userSpeed < .64f ? .65f : next.userSpeed < .99f ? 1 : .35f; controls.Apply(next); }
        void DeadZone() { var next=controls.Preferences; next.deadZone=next.deadZone < .19f ? .2f : next.deadZone < .29f ? .3f : .1f; controls.Apply(next); }
        void OnEnable()=>Refresh();
        void Refresh()
        {
            if(!isActiveAndEnabled||!controls||!summary||!status)return;
            var prefs=controls.Preferences;
            summary.text="Maestro "+prefs.avatarStick+" / "+(controls.AvatarEnabled ? "ON" : "off")+" · You "+prefs.userStick+" / "+(controls.UserEnabled ? "ON" : "off")+" · "+("Backdrop "+Mathf.RoundToInt(controls.BackdropOpacity*100)+"%")+
                "\nYour speed "+prefs.userSpeed.ToString("0.00")+" m/s · Dead zone "+prefs.deadZone.ToString("0.0")+
                "\n"+Buttons[selectedButton]+" → "+controls.ButtonLabel(selectedButton);
            status.text=string.Join("\n",ModelText.Wrap(controls.Status,75).Take(2))+"\nB / Y and palm Recall always recover";
        }
        Material Paint(string color) { var value=IllustratedMaterials.Create(IllustratedMaterials.Hex(color)); paints.Add(value); return value; }
        static void Part(Transform parent,Vector3 point,Vector3 scale,Material paint)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(parent,false); part.transform.localPosition=point; part.transform.localScale=scale;
            part.GetComponent<Collider>().enabled=false; ArtResources.Release(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial=paint;
        }
        static TextMesh Label(Transform parent,Vector3 point,string text,float size)
        {
            var root=new GameObject("Movement marking",typeof(TextMesh)); root.transform.SetParent(parent,false); root.transform.localPosition=point;
            var label=root.GetComponent<TextMesh>(); label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=48; label.characterSize=size;
            label.text=text; label.richText=false; label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.color=IllustratedMaterials.TextColor(IllustratedMaterials.Ink);
            root.GetComponent<MeshRenderer>().sharedMaterial=IllustratedMaterials.TextMaterial(label.font); return label;
        }
        void OnDestroy() { if (controls) controls.Changed-=Refresh; foreach (var paint in paints) ArtResources.Release(paint); }
    }
}

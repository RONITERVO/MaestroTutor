// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // After held props, recipe animation and XRI. No work for loose/inactive tools.
    [DefaultExecutionOrder(300)]
    public sealed class DrawingTipView:MonoBehaviour
    {
        readonly List<HeldRoomProp> attachments=new();
        RoomEditor editor;
        RoomItem item;
        DrawingTip data;
        string target,encoded;
        bool blockedUntilSeparation;
        public void Apply(RoomEditor owner,string id,DrawingTip[] tips)
        {
            string next=JsonUtility.ToJson(tips?.FirstOrDefault());
            if(encoded!=next){Interrupt();blockedUntilSeparation=true;}
            editor=owner;target=id;item=GetComponent<RoomItem>();data=tips?.FirstOrDefault()?.Copy();encoded=next;
        }
        void LateUpdate()
        {
            bool grabbed=item&&item.Grab&&item.Grab.isSelected;
            GetComponents(attachments);bool held=grabbed||attachments.Any(p=>p.Holding);
            Sample(held,grabbed?RoomActorRole.Control:RoomActorRole.Program);
        }
        internal void Sample(bool held,RoomActorRole role)
        {
            var capture=editor?editor.GetComponent<SpatialDrawing>():null;
            if(!editor||data==null||!data.enabled){Finish();blockedUntilSeparation=true;return;}
            if(editor.Ownership.Suspended||editor.WriteGate.Frozen||editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){Finish();blockedUntilSeparation=true;return;}
            if(!held){Finish();blockedUntilSeparation=false;return;}
            Transform anchor=string.IsNullOrEmpty(data.part)?transform:GetComponent<RecipeObject>()?.Part(data.part);
            if(!anchor){Finish();blockedUntilSeparation=true;return;}
            Vector3 tip=anchor.TransformPoint(data.position),direction=anchor.TransformDirection(data.rotation*Vector3.forward).normalized;
            // World-space one-centimetre contact band on either side of the tip.
            var ray=new Ray(tip-direction*.01f,direction);
            bool contact=editor.FindDrawingSurface(ray,.02f,out _,out _,out _,out _,data.radius,target);
            if(!contact){Finish();blockedUntilSeparation=false;return;}
            if(capture&&capture.IsToolDrawing(target)){capture.MoveTool(target,ray);if(!capture.IsToolDrawing(target))blockedUntilSeparation=true;return;}
            if(blockedUntilSeparation)return;
            blockedUntilSeparation=true;
            if(!capture){capture=editor.gameObject.AddComponent<SpatialDrawing>();capture.Editor=editor;}
            capture.BeginTool(target,ray,data.color,data.radius,role,data.Mode=="erase");
        }
        void Finish(){if(editor)editor.GetComponent<SpatialDrawing>()?.EndTool(target);}
        void Interrupt(){if(editor)editor.GetComponent<SpatialDrawing>()?.InterruptTool(target);}
        void OnDisable(){Interrupt();blockedUntilSeparation=true;}
        void OnApplicationPause(bool paused){if(paused){Finish();blockedUntilSeparation=true;}}
        void OnApplicationFocus(bool focused){if(!focused){Finish();blockedUntilSeparation=true;}}
    }
}

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
    public sealed class SculptTipView:MonoBehaviour
    {
        readonly List<HeldRoomProp> attachments=new();
        RoomEditor editor;
        RoomItem item;
        SculptTip data;
        string target,encoded;
        bool blockedUntilSeparation;RoomActorRole contactRole;
        public void Apply(RoomEditor owner,string id,SculptTip[] tips)
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
            var capture=editor?editor.GetComponent<SpatialSculpting>():null;
            if(!editor||data==null||!data.enabled){Interrupt();blockedUntilSeparation=true;return;}
            if(editor.Ownership.Suspended||editor.WriteGate.Frozen||editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){Interrupt();blockedUntilSeparation=true;return;}
            if(held&&capture&&capture.OwnsTool(target)&&contactRole!=role){Interrupt();blockedUntilSeparation=true;return;}
            if(!held){Finish();blockedUntilSeparation=false;return;}
            Transform anchor=string.IsNullOrEmpty(data.part)?transform:GetComponent<RecipeObject>()?.Part(data.part);
            if(!anchor){Finish();blockedUntilSeparation=true;return;}
            Vector3 tip=anchor.TransformPoint(data.position),direction=anchor.TransformDirection(data.rotation*Vector3.forward).normalized;
            if(data.IsMaterial){SampleMaterial(capture,tip,direction,role);return;}
            // World-space one-centimetre contact band on either side of the tip.
            var ray=new Ray(tip-direction*.01f,direction);
            bool contact=editor.FindHeightField(ray,.02f,target,out _,out _,out _);
            if(!contact){Finish();blockedUntilSeparation=false;return;}
            if(capture&&capture.OwnsTool(target)){capture.MoveTool(target,ray);if(!capture.OwnsTool(target))blockedUntilSeparation=true;return;}
            if(blockedUntilSeparation)return;
            blockedUntilSeparation=true;
            if(!capture){capture=editor.gameObject.AddComponent<SpatialSculpting>();capture.Editor=editor;}
            contactRole=role;capture.BeginTool(target,ray,data.mode,data.radius,data.height,role);
        }
        void SampleMaterial(SpatialSculpting capture,Vector3 tip,Vector3 opening,RoomActorRole role){
            if(!editor.FindHeightFieldNear(tip,target,out var field,out var point)){Finish();blockedUntilSeparation=false;return;}
            if(capture&&capture.OwnsTool(target)){capture.MoveMaterialTool(target,field,role);return;}
            if(blockedUntilSeparation)return;
            var surface=editor.Find(field)?.GetComponent<HeightFieldView>();if(!surface)return;
            float alignment=Vector3.Dot(opening,surface.Surface.up);if(Mathf.Abs(alignment)<.6f)return;
            blockedUntilSeparation=true;contactRole=role;
            if(!capture){capture=editor.gameObject.AddComponent<SpatialSculpting>();capture.Editor=editor;}
            capture.BeginMaterialTool(target,field,point,alignment<0,data,role);
        }
        void Finish(){if(editor)editor.GetComponent<SpatialSculpting>()?.EndTool(target);}
        void Interrupt(){if(editor)editor.GetComponent<SpatialSculpting>()?.EndTool(target,false);}
        void OnDisable(){Interrupt();blockedUntilSeparation=true;}
        void OnApplicationPause(bool paused){if(paused){Interrupt();blockedUntilSeparation=true;}}
        void OnApplicationFocus(bool focused){if(!focused){Interrupt();blockedUntilSeparation=true;}}
    }
}

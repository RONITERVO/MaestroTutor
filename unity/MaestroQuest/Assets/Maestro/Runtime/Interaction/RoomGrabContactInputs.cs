// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
namespace Maestro.Quest.Interaction {
    // Last transformer, after the one/two-hand pose calculation and before XRI
    // stores its target or throw sample. A blocked target cannot enter either.
    public sealed class RoomGrabContactInputs:XRBaseGrabTransformer {
        RigidRoomItem rigid;RoomItem item;RoomEditor editor;RoomContactInputs inputs;
        bool ready=true,releasing;float throwAfter;string issue;
        protected override RegistrationMode registrationMode=>RegistrationMode.None;
        internal void Initialize(RoomItem value,RigidRoomItem physics){item=value;rigid=physics;value.Grab.AddSingleGrabTransformer(this);value.Grab.AddMultipleGrabTransformer(this);}
        bool Owned=>isActiveAndEnabled&&rigid&&rigid.ContactTracking&&item&&item.Grab&&(item.Grab.isSelected||releasing)&&editor&&rigid.PhysicsEditor==editor&&editor.Identity(item)!=null;
        internal bool Ready=>isActiveAndEnabled&&ready&&(inputs==null||inputs.Current(out _));
        internal bool ThrowReady=>Ready&&Time.unscaledTime>=throwAfter;
        // XRI removes the final selector before its public release callback.
        // Keep only this synchronous read authorized, then retire the old lease.
        internal bool FinishGrab(){releasing=true;try{return ThrowReady;}finally{releasing=false;Reset();}}
        public override void OnGrab(XRGrabInteractable grab){Reset();}
        internal void Reset(){inputs?.Dispose();inputs=null;editor=null;ready=true;throwAfter=0;issue=null;}
        internal void Suspend(){Reset();ready=false;issue="Waiting for a fresh grab target";if(item&&item.Grab)throwAfter=Time.unscaledTime+Mathf.Max(.05f,item.Grab.throwSmoothingDuration)+.05f;}
        void Refuse(string reason){ready=false;issue=reason;throwAfter=Time.unscaledTime+Mathf.Max(.05f,item.Grab.throwSmoothingDuration)+.05f;rigid.StopVelocity();rigid.Refresh();}
        public override void Process(XRGrabInteractable grab,XRInteractionUpdateOrder.UpdatePhase phase,ref Pose targetPose,ref Vector3 localScale){
            if(!item||!rigid||!grab.isSelected){Reset();return;}
            if(!rigid.ContactTracking){Suspend();return;}
            var current=rigid.PhysicsEditor;
            if(current!=editor||inputs?.SourceChanged==true){inputs?.Dispose();inputs=null;editor=current;ready=false;}
            if(!editor){if(!rigid.HasPhysicsBinding){ready=true;return;}Refuse("The contact world is unavailable");}
            else if(!RoomContactSweep.Item(item,targetPose,localScale,out var sweep)){Refuse("Held collision bounds are unavailable");}
            else {
                inputs??=new RoomContactInputs(editor,editor.Identity(item),()=>Owned);
                if(inputs.Require(sweep,out var error)&&rigid.ContactEnvironmentReady){ready=true;issue=null;rigid.Refresh();return;}
                Refuse(error??"The physics environment is unavailable");
            }
            targetPose=new Pose(item.transform.position,item.transform.rotation);localScale=item.transform.localScale;
        }
        internal JObject Observe(){
            if(!Owned)return RoomContactInputs.State("inactive",false,"No active physical grab");
            var result=inputs?.Observe()??RoomContactInputs.State("unavailable",false,issue);
            if(!ready){result["ready"]=false;if((string)result["state"]!="pending")result["state"]="unavailable";if(issue!=null)result["reason"]=Imports.ImportObservation.Text(issue);}
            return result;
        }
        void Update(){if(inputs!=null&&!Owned)Reset();}
        void OnDisable(){Reset();}
        protected override void OnDestroy(){Reset();base.OnDestroy();}
    }
}

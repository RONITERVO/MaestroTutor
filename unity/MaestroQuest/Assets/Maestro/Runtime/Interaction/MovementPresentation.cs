// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class MovementControls
    {
        public float BackdropOpacity => view ? view.BackdropOpacity : 0;
        public bool RealDepth => !view || view.RealDepth;
        internal JObject ObservePresentation()=>new(){
            ["stateId"]=CurrentModeId(),["backdropOpacity"]=BackdropOpacity,["realDepth"]=RealDepth,
            ["depthEligible"]=view&&view.WantsRealDepth,["virtualView"]=Virtual,
            ["headTracked"]=HeadReady,["focused"]=focused&&!paused
        };
        internal bool CanSetPresentation(string expected,float opacity,out string error)
        {
            error="Room view changed; read world.presentation again before changing it";
            if(expected!=CurrentModeId())return false;
            error="Backdrop opacity must be a finite number from zero to one";
            return VirtualRoomView.ValidOpacity(opacity)&&CanChangeMode("view.presentation",false,out error);
        }
        internal bool SetPresentation(string expected,float opacity,bool realDepth,out JObject result,out string error)
        {
            result=null;
            if(!CanSetPresentation(expected,opacity,out error)||!ChangePresentation(opacity,realDepth,out error))return false;
            result=ObservePresentation();return true;
        }
        bool ChangePresentation(float opacity,bool realDepth,out string error)
        {
            error=null;
            if(view&&opacity==BackdropOpacity&&realDepth==RealDepth)return true;
            if(!view||!view.SetPresentation(opacity,realDepth)){error="Wait for an active room view and an idle scan";return false;}
            // Presentation does not change locomotion permission. Re-centering
            // the input is still required after a visual transition.
            userGate.Reset();System.Array.Clear(buttonReady,0,buttonReady.Length);
            CurrentModeId();Say($"Backdrop {Mathf.RoundToInt(opacity*100)}% · real occlusion {(realDepth ? "on" : "off")}");
            Changed?.Invoke();return true;
        }
        public void CycleBackdrop()=>ManualPresentation(BackdropOpacity>=1?0:Mathf.Min(1,BackdropOpacity+.25f),RealDepth);
        public void ToggleRealDepth()=>ManualPresentation(BackdropOpacity,!RealDepth);
        void ManualPresentation(float opacity,bool realDepth)
        {
            if(!CanChangeMode("view.presentation",true,out var error)||!ChangePresentation(opacity,realDepth,out error))Say(error);
        }
    }
}

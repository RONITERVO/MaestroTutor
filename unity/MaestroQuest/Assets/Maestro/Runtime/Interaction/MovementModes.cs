// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Interaction
{
    public sealed partial class MovementControls
    {
        string modeId=Guid.NewGuid().ToString("N");
        (string configuration,bool avatar,bool user,string presentation,bool paused,bool focused,bool tracked) modeSignature;
        bool HeadReady=>headTracked?.Invoke()==true;
        internal bool RecoveryHeadReady=>isActiveAndEnabled&&!paused&&focused&&HeadReady;
        string CurrentModeId()
        {
            // Observe transitions, including focus/tracking loss while already off. Never
            // reuse an identity after recovery, rebinding or a manual change and reversal.
            var signature=(configurationId,AvatarEnabled,UserEnabled,view?view.PresentationId:null,paused,focused,HeadReady);
            if(signature!=modeSignature){modeSignature=signature;modeId=Guid.NewGuid().ToString("N");}
            return modeId;
        }
        internal JObject ObserveMode()=>new() {["stateId"]=CurrentModeId(),["configurationId"]=configurationId,["avatarEnabled"]=AvatarEnabled,["userEnabled"]=UserEnabled,["virtualView"]=Virtual,["headTracked"]=HeadReady,["focused"]=focused&&!paused};
        bool OtherActor(out string error)
        {
            error=null;
            foreach(var data in editor.Snapshot().objects){
                var item=editor.Find(data.id);if(!item)continue;
                var spatial=item.GetComponent<AvatarSpatialMotion>();var model=item.GetComponent<MaestroAvatar>();
                if(animations&&animations.ControlsTarget(data.id)||item.GetComponent<RigidRoomItem>()?.AnimationOwned==true||
                   spatial&&spatial.Active&&!spatial.OwnedBy(Owner)||model&&model.UpperBodyActive||item.GetComponent<RecipeObject>()?.IsPlaying==true){
                    error="Stop the current movement, gesture or animation authoring before changing control modes";return true;
                }
            }
            return false;
        }
        internal static bool QuietMode(string operation)=>operation=="maestro.enable";
        static bool ViewMode(string operation)=>operation=="view.virtual"||operation=="view.mixedReality"||operation=="view.presentation";
        bool CanChangeMode(string operation,bool manual,out string error)
        {
            error=null;
            if(!editor||!ConfigurationInitialized||!isActiveAndEnabled){error="Movement controls are unavailable";return false;}
            // Disabling sticks is always allowed through the manual controls, even while
            // tracking is lost. Shared requests still use the runtime's normal action gate.
            if(operation=="maestro.disable"||operation=="user.disable"||manual&&operation=="view.mixedReality")return true;
            if(editor.RuntimeGate.Held)error=editor.RuntimeGate.Reason;
            else if(editor.WriteGate.Frozen)error="Finish the current workspace boundary before changing control modes";
            else if(paused||!focused||!HeadReady)error="Wait for head tracking and focus before changing control modes";
            else if((manual?sample().manipulating:sample().busy)||editor.AnyHeld||rules&&rules.AnyButtonHeld)error="Release held items and controls before changing control modes";
            else if(!manual&&QuietMode(operation)&&OtherActor(out error))return false;
            if(error!=null)return false;
            if(operation=="maestro.enable"){
                if(preferences.avatarStick==MovementStick.None)error="Choose a Maestro binding first";
                else if(!avatar)error="Maestro movement is unavailable";
                else if(!avatar.CanBegin(AvatarSpatialMode.Manual,out error,allowAuthoringTakeover:manual))return false;
            }else if(operation=="user.enable"){
                if(!view||!view.CanEnter)error="Wait for the tracked world and room scan before enabling your movement";
                else if(preferences.userStick==MovementStick.None)error="Choose your movement binding first";
            }else if((operation=="view.virtual"||operation=="view.presentation")&&(!view||!view.CanEnter))error="Wait for the room view and scan to be ready before changing view";
            return error==null;
        }
        internal bool CanSetMode(string expected,string operation,out string error)
        {
            error="Control mode changed; read controller.mode again before changing it";
            return expected==CurrentModeId()&&CanChangeMode(operation,false,out error);
        }
        internal bool SetMode(string expected,string operation,out JObject result,out string error)
        {
            result=null;if(!CanSetMode(expected,operation,out error)||!ChangeMode(operation,false,out error))return false;
            result=ObserveMode();return true;
        }
        void ManualMode(string operation)
        {
            if(!ChangeMode(operation,true,out var error))Say(error);
        }
        bool ChangeMode(string operation,bool manual,out string error)
        {
            if(!CanChangeMode(operation,manual,out error))return false;
            bool same=operation switch {"maestro.enable"=>AvatarEnabled,"maestro.disable"=>!AvatarEnabled,"user.enable"=>UserEnabled,"user.disable"=>!UserEnabled,"view.virtual"=>Virtual&&!view.RealDepth,"view.mixedReality"=>!view||!view.PresentationChanged,_=>false};
            if(same)return true;
            if(ViewMode(operation)) {
                if(operation=="view.virtual") {
                    if(!view.Enter()){error="Wait for the room scan to finish before changing view";return false;}
                } else {
                    view?.Exit();
                }
                // Changing the view is not an ownership takeover or a world reset.
                // Input must return to neutral after a visual transition; opt-ins survive.
                userGate.Reset();Array.Clear(buttonReady,0,buttonReady.Length);
                CurrentModeId();Say(Virtual?"Virtual view — real room hidden; accepted ground supports walking":"Mixed reality — world and its activity retained");
                Changed?.Invoke();return true;
            }
            // Only a deliberate physical Maestro takeover interrupts its actor.
            if(manual&&operation=="maestro.enable"){
                animations.Stop();
                rules?.Scheduler.StopConflicting(new Rules.RuleStep {action=Rules.RuleActionKind.FollowUser,targetId="maestro"},true);
                avatar?.Stop();
            }
            Interrupt();
            if(operation.StartsWith("maestro.",StringComparison.Ordinal))AvatarEnabled=operation=="maestro.enable";
            else if(operation.StartsWith("user.",StringComparison.Ordinal))UserEnabled=operation=="user.enable";
            else input?.CancelAll();
            CurrentModeId();
            Status=operation switch {
                "maestro.enable"=>"Maestro stick on — center the stick, then move",
                "maestro.disable"=>"Maestro stick off",
                "user.enable"=>"Your movement on — center the stick, then move",
                "user.disable"=>"Your movement off",
                _=>"Virtual view — real room hidden; enable Your movement to walk"
            };
            Changed?.Invoke();return true;
        }
    }
}

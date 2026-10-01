// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Interaction
{
    public sealed partial class MovementControls
    {
        string configurationId=Guid.NewGuid().ToString("N");
        internal bool ConfigurationInitialized=>preferences!=null&&storage!=null;
        internal static readonly string[] ButtonIds={"x","a","leftStickClick","rightStickClick"};
        internal static string StickName(MovementStick value)=>value==MovementStick.Left?"left":value==MovementStick.Right?"right":"none";
        internal static string CommandName(ControllerCommand value)=>value==ControllerCommand.Sequence?"program":value==ControllerCommand.SnapLeft?"snapLeft":value==ControllerCommand.SnapRight?"snapRight":"none";
        bool ProgramAvailable(string id)=>workshop&&workshop.Snapshot().sequences.Any(s=>s.id==id&&s.Compile(out _)!=null);
        bool CanApplyPreferences(ControllerPreferences next,out string error)
        {
            error=null;
            if(!editor||preferences==null||storage==null||!isActiveAndEnabled)error="Controller settings are unavailable";
            else if(editor.WriteGate.Frozen)error="Finish the current workspace boundary before editing controls";
            else if(storage.ReadOnly)error="Controller settings storage is unavailable";
            else if(next==null||!next.Validate())error="Each movement needs its own stick; check the control values";
            else for(int i=0;i<next.buttons.Length;i++){
                var requested=next.buttons[i];var prior=preferences.buttons[i];
                if(requested.command==ControllerCommand.Sequence&&(prior.command!=requested.command||prior.sequenceId!=requested.sequenceId)&&!ProgramAvailable(requested.sequenceId)){
                    error="Choose an existing readable program before binding that controller button";break;
                }
            }
            return error==null;
        }
        internal bool CanConfigure(string expected,ControllerPreferences next,out string error)
        {
            error="Controller settings changed; read controller.settings again before editing";
            return expected==configurationId&&CanApplyPreferences(next,out error);
        }
        bool TryApplyPreferences(ControllerPreferences next,out string error)
        {
            if(!CanApplyPreferences(next,out error))return false;
            using var write=editor.WriteGate.TryWrite(out error);if(write==null)return false;
            if(!storage.Save(next,out error))return false;
            preferences=next.Copy();configurationId=Guid.NewGuid().ToString("N");
            if(preferences.avatarStick==MovementStick.None)AvatarEnabled=false;
            if(preferences.userStick==MovementStick.None)UserEnabled=false;
            Interrupt();CurrentModeId();Status="Controls saved — release sticks and buttons before using them";Changed?.Invoke();return true;
        }
        internal bool Configure(string expected,ControllerPreferences next,out JObject result,out string error)
        {
            result=null;if(!CanConfigure(expected,next,out error)||!TryApplyPreferences(next,out error))return false;
            result=new JObject {["configurationId"]=configurationId,["saved"]=true};return true;
        }
        internal JObject ObserveConfiguration()
        {
            if(preferences==null)return null;
            var buttons=new JArray();for(int i=0;i<4;i++){
                var binding=preferences.buttons[i];buttons.Add(new JObject {["button"]=ButtonIds[i],["command"]=CommandName(binding.command),["programId"]=binding.sequenceId??"",["available"]=binding.command!=ControllerCommand.Sequence||ProgramAvailable(binding.sequenceId)});
            }
            return new JObject {["configurationId"]=configurationId,["storageReady"]=storage!=null&&!storage.ReadOnly,["avatarStick"]=StickName(preferences.avatarStick),["userStick"]=StickName(preferences.userStick),["deadZone"]=Math.Round(preferences.deadZone,6),["userSpeed"]=Math.Round(preferences.userSpeed,6),["buttons"]=buttons,["live"]=new JObject {["avatarEnabled"]=AvatarEnabled,["userEnabled"]=UserEnabled,["virtualView"]=Virtual}};
        }
    }
}

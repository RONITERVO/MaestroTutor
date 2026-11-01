// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
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
        internal bool StartConfiguration(string expected,ControllerPreferences next,
            out CapabilityOperation operation,out string error,Action<string> checkpoint=null)
        {
            operation=null;if(!CanConfigure(expected,next,out error))return false;
            var captured=next.Copy();PendingConfiguration pending=null;
            return PersistedEditOperation.StartStateChange(editor,
                (out IPersistedRoomEdit edit,out string issue)=>editor.BeginPersistedStateEdit(write=>
                    configurationSave=pending=new PendingConfiguration(this,expected,captured,write,checkpoint),out edit,out issue),
                (out string issue)=>CanConfigure(expected,captured,out issue),
                ()=>pending?.Result??new JObject(),out operation,out error);
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

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class MovementControls
    {
        PendingConfiguration configurationSave;
        CapabilityOperation manualConfiguration;
        float configurationDeadline;
        internal bool ConfigurationPending=>manualConfiguration!=null||configurationSave!=null&&!configurationSave.Drained;
        bool BeginManualConfiguration(ControllerPreferences next)
        {
            if(manualConfiguration!=null){Say("Wait for the pending controller settings");return false;}
            if(!StartConfiguration(configurationId,next,out var operation,out var error)){Say(error);return false;}
            manualConfiguration=operation;configurationDeadline=Time.realtimeSinceStartup+30;
            Say("Saving controller settings — current bindings stay active until confirmed");
            PollManualConfiguration();return true;
        }
        void PollManualConfiguration()
        {
            var operation=manualConfiguration;if(operation==null)return;
            var state=operation.State(out var error);
            if(state==RuleActionState.Preparing&&Time.realtimeSinceStartup<configurationDeadline)return;
            if(state==RuleActionState.Ready&&!operation.Complete(out error))error??="Controller settings did not confirm completion";
            if(state==RuleActionState.Preparing)error="Controller settings timed out. "+(operation.InterruptionStatus??"No pending controller save was accepted.");
            manualConfiguration=null;operation.Stop(false);
            if(error!=null)Say(error);
        }
        void CancelManualConfiguration()
        {var operation=manualConfiguration;manualConfiguration=null;operation?.Stop(false);}

        // A receipt observer does not own accepted saving. Its owner-thread
        // continuation commits bindings after storage confirms, even after Stop.
        internal sealed class PendingConfiguration:IPersistedRoomEdit
        {
            readonly MovementControls owner;
            readonly ControllerPreferenceStorage storage;
            readonly ControllerPreferences next;
            readonly string expected;
            readonly Action<string> checkpoint;
            IDisposable write;
            Task<string> saving;
                        JObject result;
            string failure;
            bool accepted,ended,success;
            volatile bool orphaned;
            int released;
            internal PendingConfiguration(MovementControls owner,string expected,ControllerPreferences next,IDisposable write,Action<string> checkpoint)
            {this.owner=owner;this.expected=expected;this.next=next;this.write=write;this.checkpoint=checkpoint;storage=owner.storage;}
            public bool Accepted=>accepted;
            public bool Drained=>Volatile.Read(ref released)==1;
            internal JObject Result=>(JObject)result?.DeepClone();
            public string InterruptionStatus=>!accepted?null:success?
                "Controller settings were saved and applied. Stopping does not restore the previous bindings.":
                Drained?"Controller saving finished without confirmed live application. Inspect controller.settings before retrying.":
                "Controller saving has started and continues after Stop. Confirmed bindings can still apply; inspect controller.settings before another edit.";
            public bool Poll(out bool ready,out string error)
            {
                ready=success;error=failure;
                if(ended)return success;
                if(!accepted){
                    if(!owner||!owner.CanConfigure(expected,next,out error)){failure=error??"Controller settings owner ended";ended=true;Release();return false;}
                    accepted=true;
                    try{saving=storage.SaveAsync(next,checkpoint);_=FinishAsync();}
                    catch(Exception){failure=error="Controller saving could not start";ended=true;Release();return false;}
                }
                ready=success;error=failure;return !ended||success;
            }
            async Task FinishAsync()
            {
                try{
                    var error=await saving;
                    if(error!=null){failure=error;return;}
                    if(orphaned||!owner||!owner.editor){failure="Controller settings were saved after the previous runtime closed; reopen to use them.";return;}
                    // Rebinding is personal, never a movement opt-in or a program
                    // trigger. Preserve current mode choices and reset input gates.
                    owner.preferences=next.Copy();owner.configurationId=Guid.NewGuid().ToString("N");
                    result=new JObject {["configurationId"]=owner.configurationId,["saved"]=true};
                    if(next.avatarStick==MovementStick.None)owner.AvatarEnabled=false;
                    if(next.userStick==MovementStick.None)owner.UserEnabled=false;
                    owner.Interrupt();owner.CurrentModeId();success=true;
                    owner.Status="Controls saved — release sticks and buttons before using them";
                    owner.Changed?.Invoke();
                }catch(Exception){failure="Controller settings may be saved but native completion was not confirmed. Inspect or reopen before retrying.";success=false;}
                finally{
                    ended=true;
                    try{if(!orphaned&&owner&&failure!=null)owner.Say(failure);}
                    catch(Exception){/* A status listener cannot strand the accepted writer. */}
                    finally{Release();}
                }
            }
            public bool Complete(out string error)
            {if(!Poll(out var ready,out error))return false;if(!ready){error="Controller settings are still saving";return false;}return true;}
            public void Dispose()
            {if(accepted||ended)return;failure="Controller settings cancelled before saving";ended=true;Release();}
            internal void OwnerDestroyed()
            {
                orphaned=true;Dispose();
                if(accepted){failure="Controller settings owner closed; accepted saving still drains before reopening.";ended=true;success=false;_=ReleaseOrphanAsync();}
            }
            async Task ReleaseOrphanAsync()
            {try{if(saving!=null)await saving.ConfigureAwait(false);}catch(Exception){}finally{Release();}}
            void Release()
            {
                if(Interlocked.CompareExchange(ref released,-1,0)!=0)return;
                try{Interlocked.Exchange(ref write,null)?.Dispose();}
                finally{Volatile.Write(ref released,1);}
            }
        }
    }
}

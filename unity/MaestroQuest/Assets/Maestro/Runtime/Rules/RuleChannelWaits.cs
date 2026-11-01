// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleScheduler
    {
        const float ChannelPollInterval=.05f;
        static bool Overlap(BehaviourCatalog.Claim[] a,BehaviourCatalog.Claim[] b)=>a.Any(x=>b.Any(x.Conflicts));
        bool ChannelsAvailable(Run run,BehaviourCatalog.Claim[] claims,out string error){
            error=null;
            // Queue order applies only to overlapping program requests. Direct native
            // controls retain their priority; a waiting run never owns a channel.
            foreach(var earlier in channelWaits){
                if(earlier==run)break;
                if(Overlap(earlier.Active.Claims,claims)){error="An earlier program is waiting for a required channel";return false;}
            }
            if(running.Any(other=>other!=run&&Conflicts(other,claims))){error="An action already owns this target";return false;}
            return Ownership.CanAcquire(run.Id,RoomActorRole.Program,claims,out error);
        }
        void WaitForChannels(Run run,float now,string reason){
            lastNow=now;
            if(!run.WaitingForChannels){run.WaitingForChannels=true;run.ChannelDeadline=now+run.Machine.ChannelWaitSeconds;channelWaits.Add(run);}
            run.ChannelPoll=now+ChannelPollInterval;run.ChannelStatus="Waiting for channels: "+reason;
        }
        void PollChannelWait(Run run,float now){
            // An expired wait cannot start, even if the channels became free in a long frame.
            if(now>=run.ChannelDeadline){LastError="Timed out waiting for action channels";Stop(run,false,"failed",LastError);return;}
            if(now<run.ChannelPoll)return;
            // Do not advance the interpreter or re-evaluate arguments. StartAction
            // checks current readiness, revisions and reach before any native effect.
            StartAction(run,now);
        }
        void RemoveChannelWait(Run run){channelWaits.Remove(run);run.WaitingForChannels=false;run.ChannelStatus=null;}
    }
}

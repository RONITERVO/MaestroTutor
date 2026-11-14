// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleScheduler
    {
        bool PollEntityDemand(Run run,float now)
        {
            string error=null;var state=run.EntityDemand?.State(out error)??RuleActionState.Ready;
            if(state==RuleActionState.Preparing&&now<run.AcquisitionDeadline)return false;
            if(state!=RuleActionState.Ready){
                LastError=state==RuleActionState.Preparing?"Required objects took too long to load; try again":error??"Required objects could not be loaded";
                Stop(run,false,"failed",LastError);return false;
            }
            run.Acquiring=false;return true;
        }
        static void ReleaseEntityDemand(Run run)
        {
            run.EntityDemand?.Dispose();run.EntityDemand=null;run.Acquiring=false;run.AcquisitionChecked=false;
        }
    }
}

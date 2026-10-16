// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class LiquidPouring
    {
        readonly struct FlowReport {
            internal readonly string Id,Liquid;internal readonly RoomItem Item;
            internal readonly double Received,Spilled,Scooped,Rain;internal readonly int Receivers,Donors;
            internal FlowReport(Vessel vessel){Id=vessel.Id;Item=vessel.Item;Liquid=vessel.Live.liquid;Received=vessel.Received;Spilled=vessel.Spilled;Scooped=vessel.Scooped;Rain=vessel.Rain;Receivers=vessel.Receivers.Count;Donors=vessel.Donors.Count;}
        }
        internal bool Finish(out string issue){
            issue=null;if(!Active||publishing){HideStreams();return true;}
            // Commit may synchronize live views. Preserve immutable accepted-event
            // data before it runs, then end the write lease before any listener.
            var reports=vessels.Values.Where(v=>v.Received+v.Spilled+v.Scooped+v.Rain>0).Select(v=>new FlowReport(v)).ToArray();
            publishing=true;bool ok=false;
            try{
                ok=editor&&editor.CommitLiquidPour(original,contents,out issue);
                if(!ok){error=issue??"The liquid flow could not be saved; its liquid quantities were reverted";blocked=true;editor?.ReportStatus(error);}
            }finally{ClearEpisode();publishing=false;}
            if(!ok||!editor)return ok;
            uint epoch=flowEpoch;int revision=editor.Revision;var matrix=editor.transform.localToWorldMatrix;
            bool Current(FlowReport report)=>editor&&flowEpoch==epoch&&editor.Revision==revision&&matrix.Equals(editor.transform.localToWorldMatrix)&&editor.Find(report.Id)==report.Item;
            foreach(var report in reports){
                if(!Current(report))break;
                if(report.Received+report.Spilled>0)editor.Poured(report.Id,report.Received,report.Spilled,report.Receivers,report.Liquid);
                if(!Current(report))break;
                if(report.Scooped>0)editor.Scooped(report.Id,report.Scooped,report.Donors,report.Liquid);
                if(!Current(report))break;
                if(report.Rain>0)editor.RainCollected(report.Id,report.Rain);
            }
            return true;
        }
    }
}

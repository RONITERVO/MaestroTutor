// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
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
        readonly struct FlowParticipant {
            internal readonly string Id;internal readonly RoomItem Item;
            internal FlowParticipant(RoomEditor editor,string id){Id=id;Item=editor.Find(id);}
            internal bool Current(RoomEditor editor)=>editor.Find(Id)==Item&&Item;
        }
        internal void PublicationFailed(string issue){error=issue;blocked=true;ClearEpisode();if(editor)editor.ReportStatus(issue);}
        internal bool Finish(out string issue){
            issue=null;if(!Active||publishing){HideStreams();return true;}
            var reports=vessels.Values.Where(v=>v.Received+v.Spilled+v.Scooped+v.Rain>0).Select(v=>new FlowReport(v)).ToArray();
            var owner=editor;var targets=contents.Keys.Select(id=>new FlowParticipant(owner,id)).ToArray();
            publishing=true;bool ok=false;
            try {
                ok=owner&&owner.CommitLiquidPour(original,contents,out issue,()=>Deliver(owner,targets,reports));
                if(!ok){error=issue??"The liquid flow could not be accepted; its unfinished quantities were reverted";blocked=true;owner?.ReportStatus(error);}
            } finally {ClearEpisode();publishing=false;}
            if(owner)owner.PollLiquidCheckpoints();return ok;
        }
        static void Deliver(RoomEditor owner,FlowParticipant[] targets,FlowReport[] reports) {
            if(!owner||targets.Any(t=>!t.Current(owner)))return;
            // Amount events are in saved room identity, not a former viewing frame.
            // Moving the whole world while IO runs does not revoke accepted flow.
            var frame=owner.transform.localToWorldMatrix;
            uint epoch=owner.Liquids?owner.Liquids.flowEpoch:0;int revision=owner.Revision;
            bool Current(FlowReport report)=>owner&&owner.Revision==revision&&frame.Equals(owner.transform.localToWorldMatrix)&&
                (owner.Liquids?owner.Liquids.flowEpoch:0)==epoch&&owner.Find(report.Id)==report.Item;
            foreach(var report in reports) {
                if(!Current(report))break;
                if(report.Received+report.Spilled>0)owner.Poured(report.Id,report.Received,report.Spilled,report.Receivers,report.Liquid);
                if(!Current(report))break;
                if(report.Scooped>0)owner.Scooped(report.Id,report.Scooped,report.Donors,report.Liquid);
                if(!Current(report))break;
                if(report.Rain>0)owner.RainCollected(report.Id,report.Rain);
            }
        }
    }
}

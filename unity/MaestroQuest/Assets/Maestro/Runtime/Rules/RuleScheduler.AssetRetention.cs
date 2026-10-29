// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Programs;
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleScheduler
    {
        internal bool RetainsAssetIdentity(string identity,string maintenanceRunId,out bool uncertain)
        {var scan=new AssetIdentityRetention(identity);ReadAssetRetention(scan,maintenanceRunId);uncertain=scan.Uncertain;return scan.Found;}
        internal static void ReadAssetDocument(AssetIdentityRetention scan,RuleDocument value)
        {
            if(value?.sequences==null||value.sequences.Length>32){scan.Unknown();return;}
            foreach(var sequence in value.sequences){if(!scan.Step())break;ReadAssetSequence(scan,sequence);}
        }
        static void ReadAssetSequence(AssetIdentityRetention scan,RuleSequence sequence)
        {
            if(sequence==null){scan.Unknown();return;}
            scan.Text(sequence.name);scan.Program(sequence.Compile(out _));
        }
        internal void ReadAssetRetention(AssetIdentityRetention scan,string maintenanceRunId)
        {
            ReadAssetDocument(scan,document);
            var seen=new HashSet<Run>();
            void ReadRun(Run run)
            {
                if(run==null)return;
                if(!seen.Add(run)||!scan.Step())return;
                bool exempt=run.Id==maintenanceRunId;
                // An active invocation's envelope is its current call. A saved
                // program's remaining literals are never exempted by its run ID.
                if(!exempt||run.Invocation==null)ReadAssetSequence(scan,run.Sequence);
                if(run.Machine==null)scan.Unknown();else run.Machine.ReadAssetRetention(scan,exempt&&run.Invocation!=null);
                if(!exempt){scan.Json(run.Active?.Arguments);scan.Json(run.Invocation);}
                scan.Json(run.Output);
                ReadRun(run.Parent);
                if(run.Children!=null)foreach(var child in run.Children){if(!scan.Step())break;ReadRun(child);}
            }
            foreach(var run in running){if(!scan.Step())break;ReadRun(run);}
            foreach(var run in channelWaits){if(!scan.Step())break;ReadRun(run);}
            foreach(var run in checkpoints){if(!scan.Step())break;ReadRun(run);}
            foreach(var run in watched){if(!scan.Step())break;ReadRun(run);}
            if(queued.Count>8||eventQueue.Count>MaximumEvents)scan.Unknown();
            // Queued triggers resolve from the complete current document above.
            foreach(var pending in queued){if(!scan.Step())break;if(pending==null||document?.sequences==null||!System.Array.Exists(document.sequences,x=>x?.id==pending.SequenceId))scan.Unknown();}
            foreach(var message in eventQueue){if(!scan.Step())break;scan.Value(message.Value);scan.Json(message.Fields);}
            // Finished receipts are historical evidence; they cannot resume.
        }
    }
}

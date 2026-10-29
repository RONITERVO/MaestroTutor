// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Programs;
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleWorkshop
    {
        // Owner-thread observation only. The caller must recheck after saved-file
        // proof and hold the shared asset admission fence through removal.
        internal bool RetainsAssetIdentity(string identity,string maintenanceRunId,out bool uncertain)
        {
            var scan=new AssetIdentityRetention(identity);
            RuleScheduler.ReadAssetDocument(scan,document);
            if(undo.Count>32||redo.Count>32)scan.Unknown();
            foreach(var value in undo){if(!scan.Step())break;RuleScheduler.ReadAssetDocument(scan,value);}
            foreach(var value in redo){if(!scan.Step())break;RuleScheduler.ReadAssetDocument(scan,value);}
            // A dispatched save owns a detached candidate not necessarily equal
            // to the current document. Never infer its references while it drains.
            if(storage==null||ReadOnly||saveTask!=null||MemoryBlocked!=null)scan.Unknown();
            if(Memory==null)scan.Unknown();else scan.Include(Memory.RetainsLiveIdentity(identity,out var memoryUnknown),memoryUnknown);
            if(Modules==null)scan.Unknown();else scan.Include(Modules.RetainsLiveIdentity(identity,out var moduleUnknown),moduleUnknown);
            if(!Runtime||Runtime.Scheduler==null)scan.Unknown();else Runtime.Scheduler.ReadAssetRetention(scan,maintenanceRunId);
            uncertain=scan.Uncertain;return scan.Found;
        }
    }
}

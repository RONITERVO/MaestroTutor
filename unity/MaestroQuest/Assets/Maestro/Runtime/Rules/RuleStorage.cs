// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using System.Linq;
namespace Maestro.Quest.Rules
{
    public sealed class RuleStorage
    {
        readonly VersionedRoomFile<RuleDocument> file;
        public bool ReadOnly => file.ReadOnly;
        public RuleStorage(string directory) => file = new VersionedRoomFile<RuleDocument>(directory,"rules",256*1024,x => x.Validate(out _),x => x.Copy(),null,x => x.version = 3,3);
        public bool RetainsMotion(string id,out bool uncertain,bool force=false) => file.Retains(x => x.sequences.SelectMany(sequence => sequence.steps).Select(step => step.motionId),id,out uncertain,force);
        public RuleDocument Load(out string message) => file.Load(out message) ?? new RuleDocument();
        public bool Save(RuleDocument document,out string error) => file.Save(document,out error);
    }
}

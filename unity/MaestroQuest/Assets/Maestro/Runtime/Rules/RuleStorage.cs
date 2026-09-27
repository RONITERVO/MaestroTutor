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
        public RuleStorage(string directory) => file = new VersionedRoomFile<RuleDocument>(directory,"rules",512*1024,x => x.Validate(out _),x => x.Copy(),null,Upgrade,5,HasNewerProgram);
        static bool HasNewerProgram(RuleDocument document)
        {
            foreach(var sequence in document.sequences??System.Array.Empty<RuleSequence>()) {
                if(sequence?.UsesProgram!=true)continue;
                try {
                    using var reader=new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(sequence.program)) {MaxDepth=48,DateParseHandling=Newtonsoft.Json.DateParseHandling.None};
                    var value=Newtonsoft.Json.Linq.JObject.Load(reader)["version"];
                    if(value?.Type==Newtonsoft.Json.Linq.JTokenType.Integer&&(long)value!=1)return true;
                }catch(Newtonsoft.Json.JsonException) { /* Corrupt data follows the ordinary recovery path. */ }
            }
            return false;
        }
        static void Upgrade(RuleDocument document)
        {
            if (document.version < 4)
                foreach (var sequence in document.sequences)
                    for (int i=0;i<sequence.steps.Length;i++)
                    {
                        // Repeatable identities even before the first migrated save.
                        using var hash=System.Security.Cryptography.SHA256.Create();
                        var bytes=hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sequence.id+":step:"+i));
                        sequence.steps[i].id=System.BitConverter.ToString(bytes,0,16).Replace("-","").ToLowerInvariant();
                    }
            document.version=5;
        }
        public bool RetainsMotion(string id,out bool uncertain,bool force=false) => file.Retains(x => x.sequences.SelectMany(sequence => sequence.MotionIds()),id,out uncertain,force);
        public RuleDocument Load(out string message) => file.Load(out message) ?? new RuleDocument();
        public bool Save(RuleDocument document,out string error) => file.Save(document,out error);
    }
}

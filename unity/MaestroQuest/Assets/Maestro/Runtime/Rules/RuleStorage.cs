// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using System.Linq;
namespace Maestro.Quest.Rules
{
    public sealed class RuleStorage
    {
        readonly VersionedRoomFile<RuleDocument> file;
        readonly string directory;
        public bool ReadOnly => file.ReadOnly;
        public RuleStorage(string directory) { this.directory=System.IO.Path.GetFullPath(directory);file = new VersionedRoomFile<RuleDocument>(directory,"behaviours",512*1024,x => x.Validate(out _),x => x.Copy(),null,_=>{},2,HasNewerProgram,json=>json["sequences"] is Newtonsoft.Json.Linq.JArray sequences && sequences.All(RuleSequence.ValidWire),minimumVersion:2); }
        static bool HasNewerProgram(RuleDocument document)
        {
            foreach(var sequence in document.sequences??System.Array.Empty<RuleSequence>()) {
                if(string.IsNullOrEmpty(sequence?.program))continue;
                try {
                    using var reader=new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(sequence.program)) {MaxDepth=48,DateParseHandling=Newtonsoft.Json.DateParseHandling.None};
                    var root=Newtonsoft.Json.Linq.JObject.Load(reader);var value=root["version"];
                    if((value?.Type==Newtonsoft.Json.Linq.JTokenType.Integer || value?.Type==Newtonsoft.Json.Linq.JTokenType.Float)&&(double)value!=2)return true;
                    foreach(var node in root.Descendants().OfType<Newtonsoft.Json.Linq.JObject>()) {
                        if(node["op"]?.Type!=Newtonsoft.Json.Linq.JTokenType.String || (string)node["op"]!="invoke" || node["capability"]?.Type!=Newtonsoft.Json.Linq.JTokenType.String ||
                            (node["version"]?.Type!=Newtonsoft.Json.Linq.JTokenType.Integer && node["version"]?.Type!=Newtonsoft.Json.Linq.JTokenType.Float))continue;
                        var definition=Maestro.Quest.Programs.BehaviourCatalog.Action((string)node["capability"]);
                        if(definition==null || (double)node["version"]!=definition.Version)return true;
                    }
                }catch(Newtonsoft.Json.JsonException) { /* Corrupt data follows the ordinary recovery path. */ }
            }
            return false;
        }
        public bool RetainsMotion(string id,out bool uncertain,bool force=false) => file.Retains(x => x.sequences.SelectMany(sequence => sequence.MotionIds()),id,out uncertain,force);
        public RuleDocument Load(out string message)
        {
            var value=file.Load(out message);
            if(value==null && !ReadOnly && (System.IO.File.Exists(System.IO.Path.Combine(directory,"behaviours.v1.json")) || Enumerable.Range(1,5).Any(version=>System.IO.File.Exists(System.IO.Path.Combine(directory,"rules.v"+version+".json")))))
                message="Development behaviours were reset for the new program format. Create a behaviour in the book.";
            return value??new RuleDocument();
        }
        public bool Save(RuleDocument document,out string error) => file.Save(document,out error);
    }
}

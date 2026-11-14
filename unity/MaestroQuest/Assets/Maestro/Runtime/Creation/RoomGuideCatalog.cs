// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Bundled shared reference only. No user content, execution or scene dependency.</summary>
    public static class RoomGuideCatalog
    {
        static readonly JObject[] definitions=Load();
        public static bool Available=>definitions.Length>0;
        // Do not expose mutable cached JSON to catalog callers.
        public static JObject[] Definitions=>definitions.Select(x=>(JObject)x.DeepClone()).ToArray();
        static JObject[] Load()
        {
            var asset=Resources.Load<TextAsset>("RoomGuides");
            if(!asset)return Array.Empty<JObject>();
            try {
                var root=JObject.Parse(asset.text);
                if((int?)root["version"]!=1||root["guides"] is not JArray entries||entries.Count==0||entries.Count>32)throw new FormatException();
                var result=entries.OfType<JObject>().ToArray();
                if(result.Length!=entries.Count||result.Any(x=>x.Count!=6||x["id"]?.Type!=JTokenType.String||!((string)x["id"]).StartsWith("guide.",StringComparison.Ordinal)||x["version"]?.Type!=JTokenType.Integer||(int)x["version"]<1||x["label"]?.Type!=JTokenType.String||x["description"]?.Type!=JTokenType.String||x["body"]?.Type!=JTokenType.String||((string)x["body"]).Length>16000||x["requires"] is not JArray))throw new FormatException();
                var ids=result.Select(x=>(string)x["id"]).ToArray();
                if(ids.Distinct().Count()!=ids.Length||result.Any(x=>x["requires"].Any(r=>r.Type!=JTokenType.String||!ids.Contains((string)r))))throw new FormatException();
                return result;
            }catch(Exception) {Debug.LogError("Bundled room guides are invalid; guide discovery is unavailable.");return Array.Empty<JObject>();}
        }
    }
}

// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>Authored identity, independent of workspace generations, revision
    /// guards and physical tracking anchors. The current format has one region.</summary>
    [Serializable]
    public sealed class RoomWorldIdentity
    {
        public int version=1;
        public string worldId,regionId;
        public static RoomWorldIdentity Create()=>new(){worldId=Guid.NewGuid().ToString("N"),regionId=Guid.NewGuid().ToString("N")};
        public RoomWorldIdentity Copy()=>new(){version=version,worldId=worldId,regionId=regionId};
        internal static bool Id(string value)=>value?.Length==32&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        public bool Valid=>version==1&&Id(worldId)&&Id(regionId)&&worldId!=regionId;
        internal bool Same(RoomWorldIdentity other)=>other!=null&&version==other.version&&worldId==other.worldId&&regionId==other.regionId;
        internal static bool ValidWire(JObject room)
        {
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<23&&room["world"]==null)return true;
            if(room["world"] is not JObject value||value.Count!=3||value["version"]?.Type!=JTokenType.Integer||
                value["worldId"]?.Type!=JTokenType.String||value["regionId"]?.Type!=JTokenType.String)return false;
            return new RoomWorldIdentity{version=(int)value["version"],worldId=(string)value["worldId"],regionId=(string)value["regionId"]}.Valid;
        }
    }
    public sealed partial class RoomEditor
    {
        internal bool WorldIdentityReady {get;private set;}
        internal RoomWorldIdentity WorldIdentity=>WorldIdentityReady?journal?.WorldIdentity:null;
    }
}

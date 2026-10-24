// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Session-only presentation of optional tools; never owns room simulation or authoring data.</summary>
    public sealed class RoomToolVisibility : MonoBehaviour
    {
        internal static readonly string[] Names={"creation","animation","behaviours","imports","physics","avatar","controllers"};
        readonly Dictionary<string,RoomItem> trays=new();
        RoomEditor editor;
        string stateId=Guid.NewGuid().ToString("N");
        internal void Initialize(RoomEditor source,IDictionary<string,RoomItem> items)
        {
            if(trays.Count!=0||items.Count!=Names.Length||Names.Any(name=>!items.ContainsKey(name)||!items[name]))throw new ArgumentException("Every physical tool tray needs one owner");
            editor=source;
            foreach(var name in Names){trays.Add(name,items[name]);items[name].gameObject.SetActive(false);}
            stateId=Guid.NewGuid().ToString("N");
        }
        public JObject Observe()
        {
            var visible=new JObject();var held=new JObject();
            foreach(var name in Names){trays.TryGetValue(name,out var item);visible[name]=item&&item.gameObject.activeSelf;held[name]=item&&item.Grab&&item.Grab.isSelected;}
            return new JObject {["stateId"]=stateId,["visible"]=visible,["held"]=held};
        }
        public bool CanSet(string expected,string tray,bool visible,out string error)
        {
            error="Physical tools are unavailable";
            if(!isActiveAndEnabled||!editor||trays.Count!=Names.Length)return false;
            if(editor.WriteGate.Frozen||editor.RuntimeGate.Held){error="Finish the workspace boundary before changing tools";return false;}
            if(expected!=stateId){error="Tool visibility changed; read the current tools before trying again";return false;}
            if(tray!="all"&&!trays.ContainsKey(tray)){error="Choose an available physical tool tray";return false;}
            foreach(var pair in trays.Where(pair=>tray=="all"||pair.Key==tray))
            {
                var item=pair.Value;
                if(!item){error="A physical tool tray is unavailable";return false;}
                if(item.gameObject.activeSelf==visible)continue;
                if(item.Grab&&item.Grab.isSelected){error="Release the held tool tray before hiding it";return false;}
                if(!visible&&item.GetComponent<PhysicsTools>()?.Busy==true){error="Finish or cancel surface placement before hiding physics tools";return false;}
            }
            error=null;return true;
        }
        public bool Set(string expected,string tray,bool visible,out JObject result,out string error)
        {
            result=null;if(!CanSet(expected,tray,visible,out error))return false;
            bool changed=false;
            // Validate all targets first: hiding all must never partially hide tools around a held tray.
            foreach(var pair in trays.Where(pair=>tray=="all"||pair.Key==tray))if(pair.Value.gameObject.activeSelf!=visible){pair.Value.gameObject.SetActive(visible);changed=true;}
            if(changed)stateId=Guid.NewGuid().ToString("N");
            result=Observe();return true;
        }
        void OnDisable(){stateId=Guid.NewGuid().ToString("N");}
    }
}

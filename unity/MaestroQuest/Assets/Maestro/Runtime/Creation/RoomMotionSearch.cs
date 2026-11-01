// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class RoomMotionQuery
    {
        public string query;
        public int offset;
        public bool includeShort,favouritesOnly,archivedOnly;
        public bool Valid() => query != null && query.Length <= 80 && !query.Any(char.IsControl) && offset >= 0 && offset <= 1024;
        public RoomMotionQuery Copy() => (RoomMotionQuery)MemberwiseClone();
    }
    [Serializable] public sealed class RoomMotionEntry
    {
        public string id,name;
        public string[] tags;
        public float duration;
        public bool shortClip,favourite,archived,downloaded;
    }
    [Serializable] public sealed class RoomMotionView
    {
        public string targetId,modelHash="",status;
        public bool ready;
        public RoomMotionQuery query;
        public int offset,total,pageSize=MotionLibrary.SearchPageSize;
        public RoomMotionEntry[] entries=Array.Empty<RoomMotionEntry>();
    }
    // Read-only discovery: identical catalogue filters/page ordering to the book.
    // Re-observe the current model and files; never retain stale compatibility.
    public sealed class RoomMotionSearch
    {
        readonly RoomEditor editor;
        RoomMotionQuery query;
        string targetId;
        public RoomMotionSearch(RoomEditor editor) => this.editor=editor;
        public bool Execute(RoomAgentCommand command,out string status)
        {
            status="Invalid motion search";
            if (!RoomRecipe.ValidId(command.target) || command.motionQuery?.Valid() != true) return false;
            targetId=command.target;query=command.motionQuery.Copy();query.query=query.query.Trim();
            var page=Observe();status=page.status;return page.ready;
        }
        public RoomMotionView Observe()
        {
            if (query == null) return null;
            var view=new RoomMotionView {targetId=targetId,query=query.Copy(),status="This target needs a loaded imported model with a compatible animation rig."};
            if (editor.Motions.ReadOnly) {view.status=editor.Motions.Notice ?? "The saved motion library is unavailable; its files are preserved.";return view;}
            var item=editor.Find(targetId);var avatar=item ? item.GetComponent<MaestroAvatar>() : null;
            var model=RoomRuleActions.ClipModel(item);
            if (!item) {view.status="The motion search target no longer exists.";return view;}
            if (avatar && avatar.ModelBusy) {view.status="Wait for Maestro's model to finish loading, then search again.";return view;}
            if (!model || !model.Ready || string.IsNullOrEmpty(model.MotionRigHash)) return view;
            var page=editor.Motions.Search(query.query,model.MotionRigHash,query.includeShort,query.favouritesOnly,query.archivedOnly,query.offset);
            view.ready=true;view.modelHash=editor.Read(targetId)?.modelHash ?? "";view.offset=page.Offset;view.total=page.Total;
            view.entries=page.Entries.Select(x=>new RoomMotionEntry {id=x.id,name=x.name,tags=x.tags,duration=x.duration,shortClip=x.Short,favourite=x.favourite,archived=x.archived,downloaded=editor.Motions.Downloaded(x.id)}).ToArray();
            view.status=editor.Motions.ReadOnly ? editor.Motions.Notice : page.Total==0 ? "No compatible saved motions match this search." : "Found "+page.Total+" compatible motions. Search does not assign or play them.";
            return view;
        }
        public static bool ValidWire(JObject value)
        {
            bool Exact(JObject obj,string[] keys) => obj.Properties().Count()==keys.Length && keys.All(obj.ContainsKey);
            if (!Exact(value,new[] {"action","target","motionQuery"}) || value["target"]?.Type!=JTokenType.String || !RoomRecipe.ValidId((string)value["target"]) || value["motionQuery"] is not JObject query || !Exact(query,new[] {"query","offset","includeShort","favouritesOnly","archivedOnly"})) return false;
            return query["query"].Type==JTokenType.String && ((string)query["query"]).Length<=80 && !((string)query["query"]).Any(char.IsControl) && query["offset"].Type==JTokenType.Integer && (double)query["offset"]>=0 && (double)query["offset"]<=1024 && new[] {"includeShort","favouritesOnly","archivedOnly"}.All(k=>query[k].Type==JTokenType.Boolean);
        }
    }
}

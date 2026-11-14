// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RoomToolRecoveryCapability:CapabilityModule
    {
        internal const string Feature="toolRecovery.v1";
        public override string Id=>"room.tools.recall";
        public override string Label=>"Bring book and tools back";
        public override string Duration=>"instant";
        public override string Ownership=>"nativeBookPlacement";
        public override IReadOnlyList<string> Requirements=>new[]{"tools.recovery.current","workspace.available","tools.released","head.tracked"};
        public override string Description=>"Bring the book and seven authoring trays to their comfortable starting layout around the currently tracked viewer. Read room.tools.recovery and pass its exact stateId and book revision. This is the same Recall as B/Y, Home, palm and creation tray. It does not move or reset the world, Maestro, creations, constructed terrain, user-made buttons or construction handles, stop other programs/physics/audio, change view/locomotion modes, or reopen hidden trays. Held book/trays, inactive tracking, workspace holds, unsupported frames and unavailable saving refuse the entire action. It owns only book wholeTarget briefly at program priority; it refuses a competing book actor. The physical control may interrupt a lower-priority book actor only. Book placement is saved as one Undo; tray placement is transient and not undone. A temporary room keeps the book change temporary. Completed receipts do not replay Recall and Stop cannot undo an already completed placement. This is tool recovery, not world recentering or physical room alignment.";
        public override JObject InputSchema {get{var s=CurrentInputs(Object(new JObject { ["stateId"]=Text("^[a-f0-9]{32}$",32),["revision"]=Revision() }),"room.tools.recovery","stateId",null,"revision");s["x-current"]["guards"]=new JArray("stateId","revision");s["x-features"]=new JArray(Feature);return s;}}
        public override JObject OutputSchema=>Object(new JObject { ["stateId"]=Text("^[a-f0-9]{32}$",32),["revision"]=Revision(),["recovered"]=Number(1,8,true),["temporary"]=new JObject {["type"]="boolean"} });
        public override JObject Example=>new(){["stateId"]=new string('0',32),["revision"]=1};
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Book and tools are unavailable";return context.Editor&&context.Editor.CanRecallTools((string)args["stateId"],(int)args["revision"],false,out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(context,args,out error)||!context.Editor.RecallTools((string)args["stateId"],(int)args["revision"],false,out var result,out error))return false;operation=new CompletedCapability(result);return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("room.tools.recovery",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"revision\":\"number\",\"ready\":\"boolean\",\"reason\":\"text\"}}")),"Book and tool recovery","Read current Recall identity, book revision and admission. ready is sampled availability, not a reservation. Recall uses the current tracked view at execution; successful Recall, temporary-room boundaries and workspace replacement invalidate old IDs, and book edits invalidate old revisions. reason describes why recovery currently cannot run. No world action is performed by observation.",null,null,(context,args)=>context.Editor?ProgramValue.Literal(context.Editor.ObserveToolRecovery()):null,features:new[]{Feature});
    }
}

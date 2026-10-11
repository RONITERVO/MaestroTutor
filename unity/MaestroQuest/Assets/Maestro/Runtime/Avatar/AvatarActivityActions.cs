// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    [Serializable] public sealed class AvatarActivityEdit
    {
        public string operation,motionId;
        public int role;
        public TutorMotionChoice choice;
        public bool Valid() => role >= 0 && role < 4 && (operation == "assign" ? choice != null && choice.Valid() && AvatarWalkSelection.ValidId(choice.motionId) : operation == "clear" || operation == "remove" && AvatarWalkSelection.ValidId(motionId) && motionId.Length == 32);
    }
    [Serializable] public sealed class AvatarActivityRequest
    {
        public string modelHash,operation;
        public int revision;
        public AvatarActivityEdit[] edits;
        public bool Valid() => ModelLibrary.ValidHash(modelHash) && revision > 0 && (operation == "edit" ? edits != null && edits.Length > 0 && edits.Length <= 16 && edits.All(x => x != null && x.Valid()) : (operation == "undo" || operation == "redo") && (edits == null || edits.Length == 0));
    }
    [Serializable] public sealed class ActivityChoiceView
    {
        public string motionId,name;
        public int weight;
        public float speed,cooldown;
        public bool loop,available;
    }
    [Serializable] public sealed class ActivityRoleView
    {
        public int role;
        public ActivityChoiceView[] choices=Array.Empty<ActivityChoiceView>();
    }
    [Serializable] public sealed class ActivityProfileView
    {
        public string modelHash,status;
        public int revision;
        public bool canAssign,readOnly,canUndo,canRedo;
        public ActivityRoleView[] roles=Array.Empty<ActivityRoleView>();
    }
    /// <summary>One domain entry point and observation for book and agent profile edits.</summary>
    public static class AvatarActivityActions
    {
        public static bool Execute(RoomEditor editor,AvatarActivityRequest request,out string status)
        {
            status="This tutor-state request is invalid";
            if (request == null || !request.Valid()) return false;
            var avatar=editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            if (!avatar || avatar.ModelBusy || !avatar.CustomModel || avatar.ModelHash != request.modelHash) { status="Maestro changed. Review its state assignments and try again."; return false; }
            if (!editor.ActivityProfiles.Apply(request,avatar.CustomModel.MotionRigHash,editor.Motions,out status)) return false;
            status="Tutor-state assignments saved. Automatic motions resume with the conversation; assignment Undo is separate from room Undo."; return true;
        }
        public static ActivityProfileView Observe(RoomEditor editor,string selectedMotionId=null)
        {
            var avatar=editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            string model=avatar && !avatar.ModelBusy ? avatar.ModelHash ?? "" : "";
            string rig=avatar && !avatar.ModelBusy && avatar.CustomModel ? avatar.CustomModel.MotionRigHash : null;
            bool Available(string id) { var entry=editor.Motions.Find(id);return entry != null && editor.Motions.Downloaded(id) && !entry.Short && entry.rigHash == rig; }
            var profiles=editor.ActivityProfiles; var saved=profiles.Find(model);
            return new ActivityProfileView { modelHash=model,revision=profiles.Revision,readOnly=profiles.ReadOnly,
                canAssign=rig != null && !profiles.ReadOnly && (selectedMotionId == null || Available(selectedMotionId)),
                canUndo=rig != null && profiles.CanUndo(model),canRedo=rig != null && profiles.CanRedo(model),
                status=profiles.Notice ?? avatar?.ActivityMotionStatus ?? "Uses included animations where no state motion is assigned",
                roles=Enumerable.Range(0,4).Select(role => new ActivityRoleView { role=role,choices=(saved?.roles.FirstOrDefault(x => (int)x.role == role)?.choices ?? Array.Empty<TutorMotionChoice>()).Select(choice => new ActivityChoiceView {
                    motionId=choice.motionId,name=editor.Motions.Find(choice.motionId)?.name ?? "Missing saved motion",weight=choice.weight,speed=choice.speed,cooldown=choice.cooldown,loop=choice.loop,available=Available(choice.motionId)
                }).ToArray() }).ToArray() };
        }
        public static bool ValidWire(JObject command)
        {
            bool Exact(JObject value,params string[] keys) => value.Properties().Count() == keys.Length && keys.All(value.ContainsKey);
            bool Text(JToken value) => value?.Type == JTokenType.String;
            bool Number(JToken value,double min,double max,bool integer=false) => value != null && (value.Type == JTokenType.Integer || !integer && value.Type == JTokenType.Float) && double.IsFinite((double)value) && (double)value >= min && (double)value <= max;
            if (!Exact(command,"action","activities") || command["activities"] is not JObject request || !Text(request["operation"]) || !Text(request["modelHash"]) || !Number(request["revision"],1,int.MaxValue,true)) return false;
            if ((string)request["operation"] == "edit")
            {
                if (!Exact(request,"operation","modelHash","revision","edits") || request["edits"] is not JArray edits || edits.Count < 1 || edits.Count > 16) return false;
                foreach (var token in edits)
                {
                    if (token is not JObject edit || !Text(edit["operation"]) || !Number(edit["role"],0,3,true)) return false;
                    switch ((string)edit["operation"])
                    {
                        case "assign":
                            if (!Exact(edit,"operation","role","choice") || edit["choice"] is not JObject choice || !Exact(choice,"motionId","weight","speed","cooldown","loop") || !Text(choice["motionId"]) || !Number(choice["weight"],1,10,true) || !Number(choice["speed"],.25,2) || !Number(choice["cooldown"],0,60) || choice["loop"]?.Type != JTokenType.Boolean) return false;
                            break;
                        case "remove": if (!Exact(edit,"operation","role","motionId") || !Text(edit["motionId"])) return false; break;
                        case "clear": if (!Exact(edit,"operation","role")) return false; break;
                        default: return false;
                    }
                }
            }
            else if (!Exact(request,"operation","modelHash","revision")) return false;
            return JsonUtility.FromJson<AvatarActivityRequest>(request.ToString()).Valid();
        }
    }
}

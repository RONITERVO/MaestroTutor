// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class ObjectPhysicsSettings { public string mode, shape; public float mass; }
    [Serializable] public sealed class AvatarMovementSettings { public float distance, speed; }
    [Serializable] public sealed class RoomPhysicsObservation { public bool ready,running; public string status; }
    [Serializable] public sealed class AvatarMovementObservation
    {
        public bool active,canLook,canFollow;
        public string mode,status,lookReason,followReason;
        public float distance,speed;
    }
    /// <summary>Shared settings and runtime intentions for physical tools and the room executor.
    /// Saved settings use the room journal; transient motion has its own ownership and Stop.</summary>
    public static class RoomControls
    {
        public static readonly string[] Actions = { "physicsSettings", "avatarSettings", "physicsRun", "avatarMotion", "avatarWalk" };
        public static bool IsControl(string action) => Actions.Contains(action);
        public static bool Runtime(string action) => action == "physicsRun" || action == "avatarMotion";
        public static bool ValidPhysics(ObjectPhysicsSettings value) => value != null &&
            new[] { "fixed","solid","bouncy" }.Contains(value.mode) && new[] { "automatic","box","sphere" }.Contains(value.shape) &&
            float.IsFinite(value.mass) && value.mass >= .05f && value.mass <= 20;
        public static bool ValidMovement(AvatarMovementSettings value) => value != null &&
            float.IsFinite(value.distance) && value.distance >= .8f && value.distance <= 2.5f &&
            float.IsFinite(value.speed) && value.speed >= .2f && value.speed <= 1.2f;
        public static bool ValidCommand(RoomAgentCommand command) => command != null && (command.action switch {
            "physicsSettings" => RoomRecipe.ValidId(command.target) && command.target != "book" && command.target != "maestro" && ValidPhysics(command.physics),
            "avatarWalk" => command.target == "maestro" && AvatarWalkSelection.ValidId(command.motionId),
            "avatarSettings" => command.target == "maestro" && ValidMovement(command.movement),
            "physicsRun" => command.operation == "start" || command.operation == "pause",
            "avatarMotion" => command.target == "maestro" && new[] { "look","follow","stop" }.Contains(command.operation),
            _ => false
        });
        // JsonUtility intentionally ignores unknown fields. Check the new wire operations before
        // deserialisation can erase a missing, mistyped or unexpected argument. Legacy operations
        // keep their existing contract; this is not a replacement JSON validator for them.
        public static bool ValidWire(string json)
        {
            try {
                using var reader=new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json)) {MaxDepth=64,DateParseHandling=Newtonsoft.Json.DateParseHandling.None};
                var root=JObject.Load(reader,new Newtonsoft.Json.Linq.JsonLoadSettings {DuplicatePropertyNameHandling=Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error});
                if(reader.Read())return false;
                if (root["commands"] is not JArray commands || commands.Count < 1 || commands.Count > 8) return false;
                foreach (var token in commands)
                {
                    if (token is not JObject value || value["action"]?.Type != JTokenType.String) return false;
                    string action = (string)value["action"];
                    if(action=="execution") {if(commands.Count!=1 || !RoomExecutions.ValidWire(value))return false;continue;}
                    if(action=="catalog") {if(commands.Count!=1 || !RoomCapabilityCatalog.ValidWire(value))return false;continue;}
                    if(action=="motions") {if(commands.Count!=1 || !RoomMotionSearch.ValidWire(value))return false;continue;}
                    if(action=="avatarActivities") {if(commands.Count!=1 || !AvatarActivityActions.ValidWire(value))return false;continue;}
                    if(action=="rules") {
                        if(commands.Count!=1 || value["rule"] is not JObject rule)return false;
                        if((string)rule["action"]=="signal"&&!RuleScheduler.ValidSignalWire(value))return false;
                        if((string)rule["action"]=="edit") {
                            if(rule["edits"] is not JArray edits)return false;
                            foreach(var edit in edits)if(edit is not JObject change || (string)change["kind"]=="save" && !RuleSequence.ValidWire(change["sequence"]))return false;
                        }
                        continue;
                    }
                    if (!IsControl(action)) continue;
                    string[] keys = action switch {
                        "physicsSettings" => new[] { "action","target","physics" },
                        "avatarWalk" => new[] { "action","target","motionId" },
                        "avatarSettings" => new[] { "action","target","movement" },
                        "avatarMotion" => new[] { "action","target","operation" },
                        _ => new[] { "action","operation" }
                    };
                    if (!Exact(value,keys) || value["target"] != null && value["target"].Type != JTokenType.String ||
                        value["operation"] != null && value["operation"].Type != JTokenType.String) return false;
                    if (action == "avatarWalk" && value["motionId"].Type != JTokenType.String) return false;
                    if (action == "physicsSettings" && (value["physics"] is not JObject physics || !Exact(physics,new[] { "mode","mass","shape" }) ||
                        physics["mode"].Type != JTokenType.String || physics["shape"].Type != JTokenType.String || !Number(physics["mass"],.05,20))) return false;
                    if (action == "avatarSettings" && (value["movement"] is not JObject movement || !Exact(movement,new[] { "distance","speed" }) ||
                        !Number(movement["distance"],.8,2.5) || !Number(movement["speed"],.2,1.2))) return false;
                    if (Runtime(action) && commands.Count != 1 || !ValidCommand(JsonUtility.FromJson<RoomAgentCommand>(value.ToString()))) return false;
                }
                return true;
            } catch (Exception ex) when (ex is Newtonsoft.Json.JsonException || ex is ArgumentException || ex is OverflowException) { return false; }
        }
        static bool Exact(JObject value,string[] keys) => value.Properties().Count() == keys.Length && keys.All(key => value.ContainsKey(key));
        static bool Number(JToken value,double min,double max) => (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) && double.IsFinite((double)value) && (double)value >= min && (double)value <= max;
        public static ObjectPhysicsSettings Physics(RoomObjectData data) => new() { mode=data.physics.ToString().ToLowerInvariant(),shape=data.collisionShape.ToString().ToLowerInvariant(),mass=data.mass };
        public static bool SetPhysics(RoomObjectData data,ObjectPhysicsSettings settings,out string status)
        {
            status="Select a creation and valid physics settings";
            if (data == null || data.IsBuiltIn || !ValidPhysics(settings)) return false;
            data.physics=Enum.Parse<ItemPhysics>(settings.mode,true);data.collisionShape=Enum.Parse<ItemCollider>(settings.shape,true);data.mass=settings.mass;
            status="Physics settings saved";return true;
        }
        public static bool SetMovement(RoomObjectData data,AvatarMovementSettings settings,out string status)
        {
            status="Choose Maestro distance 0.8–2.5 m and speed 0.2–1.2 m/s";
            if (data?.kind != RoomObjectKind.Maestro || !ValidMovement(settings)) return false;
            data.followDistance=settings.distance;data.walkSpeed=settings.speed;status="Maestro movement preferences saved";return true;
        }
        public static bool AvatarMotion(RoomEditor editor,string operation,out string status)
        {
            var motion=editor.Find("maestro")?.GetComponent<AvatarSpatialMotion>();
            status="Maestro movement is unavailable";if (!motion || !new[] { "look","follow","stop" }.Contains(operation)) return false;
            var authoring=editor.GetComponent<AnimationWorkshop>();
            if (operation == "stop") {
                if(authoring && authoring.ControlsTarget("maestro"))authoring.Stop();
                editor.GetComponent<RoomRules>()?.Scheduler.StopConflicting(new RuleStep {action=RuleActionKind.FollowUser,targetId="maestro"},true);
                motion.Stop();status="Maestro movement and preview stopped";return true;
            }
            bool started=motion.Begin("direct",operation == "look" ? AvatarSpatialMode.Look : AvatarSpatialMode.Follow,out status);
            if (started) status="Maestro " + operation + " started; observe live movement status";
            return started;
        }
        internal static string[] WorkspaceCapabilities(Maestro.Quest.Persistence.WorkspaceHost host)=>!host?Array.Empty<string>():new[]{"workspaceMaintenance.v1","workspaceArchiveActivation.v1","execution.v1","executionReceipts.v1","actionRecovery.v1","factQueries.v1"}.Concat(host.Import?.Available==true?new[]{"workspaceArchiveSelection.v1"}:Array.Empty<string>()).Concat(host.Export?.Available==true?new[]{"workspaceArchiveExport.v1"}:Array.Empty<string>()).ToArray();
        public static string[] Capabilities(RoomEditor editor) => Actions.Where(action =>
            action != "physicsRun" || editor.PhysicsWorld).Where(action =>
            action != "avatarMotion" || editor.Find("maestro")?.GetComponent<AvatarSpatialMotion>()).Where(action => action != "avatarWalk" || editor.Find("maestro")?.GetComponent<MaestroAvatar>()).Select(action => action+".v1").Concat(new[] {"motions.v1","avatarActivities.v1","catalog.v1","catalogVocabulary.v1","roomOwnership.v1"}).Concat(editor.GetComponent<RuleWorkshop>() ? new[] {"behaviourPrograms.v3","eventPrograms.v1","eventFields.v1","eventSubscriptions.v1","factQueries.v1","conditionWaits.v1","structuredValues.v1","programModules.v1","moduleLibrary.v1","moduleLibraryFiles.v1","execution.v1","executionReceipts.v1","actionResults.v1","recipeCreation.v1", "objectEdits.v1","unavailablePrograms.v1","actionRecovery.v1","temporaryRoom.v1"} : Array.Empty<string>()).Concat(editor.GetComponent<Maestro.Quest.Persistence.WorkspaceExport>()?.Available==true?new[]{"workspaceArchiveExport.v1"}:Array.Empty<string>()).Concat(editor.GetComponent<Maestro.Quest.Persistence.WorkspaceImport>()?.Available==true?new[]{"workspaceArchiveSelection.v1"}:Array.Empty<string>()).Concat(WorkspaceCapabilities(editor.GetComponentInParent<Maestro.Quest.Persistence.WorkspaceHost>())).Distinct().ToArray();
        public static RoomPhysicsObservation ObservePhysics(RoomEditor editor) => !editor.PhysicsWorld ? null : new() {
            ready=editor.PhysicsWorld.SurfacesReady,running=editor.PhysicsWorld.Running,status=editor.PhysicsWorld.Status
        };
        public static AvatarMovementObservation ObserveAvatar(RoomEditor editor)
        {
            var motion=editor.Find("maestro")?.GetComponent<AvatarSpatialMotion>();if (!motion) return null;
            bool look=motion.CanBegin(AvatarSpatialMode.Look,out var lookReason,allowAuthoringTakeover:true),follow=motion.CanBegin(AvatarSpatialMode.Follow,out var followReason,allowAuthoringTakeover:true);
            return new() { active=motion.Active,mode=motion.Active ? motion.Mode.ToString().ToLowerInvariant() : "stopped",status=motion.Status,
                canLook=look,canFollow=follow,lookReason=lookReason ?? "",followReason=followReason ?? "",distance=motion.Distance,speed=motion.Speed };
        }
    }
}

# Shared avatar and physics controls

The original Maestro agent can use the versioned native operations below.
Current settings workflows prefer the shared catalog and exact native facts in
QUEST_SPATIAL_SETTINGS.md. Start/Pause now also has a shared catalog action and
fact in QUEST_PHYSICS_SIMULATION.md; the older operations remain for legacy runtimes and
atomic create/settings batches.
Their availability is declared by `scene.capabilities`; the web bridge and planner
refuse new operations against older native runtimes. The existing Gemini access,
handoff verification, task journal and same-chat result path remain authoritative.

| Operation | Shared manual path | Evidence returned | Undo |
|---|---|---|---|
| `physicsSettings` | Object mode, Mass, Collision shape through RoomEditor/RoomControls | Saved mode, mass and shape per object | One scene edit, including a create/settings batch |
| `avatarSettings` | Distance and Walk speed through RoomEditor/RoomControls | Saved preferences and live movement preferences | One scene edit |
| `physicsRun` start/pause | Start physics/Pause through RoomPhysicsWorld.SetRunning | Scan readiness, running and status | No; pausing cannot reverse a throw |
| `avatarMotion` look/follow/stop | Look at me/Follow me/Stop through RoomControls.AvatarMotion | Active mode, live status and prerequisite reasons | No; stopped position is remembered |

Saved edits use existing object revisions (global revision for the older v1
request envelope). Legacy runtime controls must be alone in a request; legacy physics start/pause
checks the scene revision, while avatar controls check the Maestro revision.
Invalid setting batches do not partially change the document. Manual and agent
primitive creation now agree: new balls are bouncy, blocks/cylinders solid, and
assemblies fixed. Existing saved objects retain their settings.

Settings must include all fields; preserve unrequested values from observations.
Physics applies only to creations, never the book or Maestro. Movement settings
apply only to Maestro. Native validation remains authoritative. The four new
operations additionally validate raw JSON keys/types before Unity can discard
unknown or missing fields. Thirty-two shared wire examples run in TypeScript and
Unity; this is scoped conformance, not a claim that every older operation has a
shared validator.

A start receipt establishes starting, not completion of future motion. Follow
uses the existing navigation, body clearance, head tracking and gait system;
`canFollow` checks initial prerequisites, not the entire route. Observations expose
later blocked/stopped status. Direct controls stop incompatible previews/rules,
as the physical buttons do; a failed start may therefore have stopped a previous
preview. Editing preferences stops incompatible motion and does not restart it.
Stopping the agent task only stops its planning; stopping following/physics is a
separate explicit room action. No per-frame model movement is involved.

Physics requires a loaded, aligned scan and active application. Losing focus or
alignment pauses simulation and returning never restarts it automatically. Scan,
room access prompts and surface placement remain manual. Controller bindings and
explicit movement/view activation now share the native catalog; see
QUEST_CONTROLLER_CONFIGURATION.md and QUEST_CONTROLLER_MODES.md. Avatar and model selection use their later
shared capabilities; see QUEST_AVATAR_SELECTION.md and QUEST_MODEL_IMPORT.md. Existing bounded visual rules can
still invoke their supported motion actions through the original scheduler.

The wire observation uses live object positions plus held/simulating state; saved
object revisions still describe the durable document. Unity's serializer expands
null serializable classes into empty objects, so RoomAgentWire explicitly preserves
absent inspection/recipe/rules/selection/physics/avatar/movement values. Movement
endpoints are serialized as bounded decimals rather than expanded float32 values.
This also fixes rejection of an otherwise empty native inspection/rule selection.

## Verification and remaining gates

Unity tests execute settings/Undo/storage, stale revisions, unavailable scans,
focus loss, actual follow motion, tracking loss and agent/manual Stop through the
real pointer router. Shared fixtures test limits, missing/wrong/extra fields and
mixed runtime batches. Browser checks consume actual Unity serialized captures;
their acknowledgement simulation tests transport only, not headset execution.

Evidence is stored in `.quest-evidence/room-controls`. The development APK is not
a store release. Quest and real-provider conversational acceptance, device lifecycle,
performance, accessibility and release signing/store gates remain pending. The
headset remains untouched while the user rests and charges it.

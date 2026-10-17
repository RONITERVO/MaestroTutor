# Shared object-edit actions

These five version-1 actions in the shared catalog let the original-app agent,
book controls and saved programs edit existing objects:

| Action | Arguments | Effect |
| --- | --- | --- |
| `object.position.set` | target, x, y, z | Placement in room metres after preparation; keeps rotation/scale and resets velocity. |
| `object.rotation.set` | target, pitch, yaw, roll | Orientation in degrees; keeps current position/scale and resets velocity. |
| `object.scale.set` | target, scale | Uniform scale; keeps current position/rotation and resets velocity. |
| `object.color.set` | target, red, green, blue | RGB tint; preserves live position and velocity. |
| `object.delete` | target | Removes a user-created object; room Undo can restore it. |

The included book and Maestro support placement, rotation and scaling. Their scale ranges
are respectively 0.65–1.8 and 0.3–1.5. User-created objects allow 0.1–4.
Positions stay within 25 m of the room origin; RGB channels are 0–1.
Painting/deletion reject the included book and Maestro. These calls do not
replace a model, flatten a recipe or erase its recorded animation data.

## Execution, ownership and storage

Each action uses the existing scheduler, input schema, target revisions, native
start identity and durable receipts. It reserves the target's whole-object
channel and rejects held targets, active authoring or a conflicting animation.
Other objects continue running. Existing physics is permitted: tint does not
teleport a moving ball; explicit placement or resizing resets velocity and lets
normal gravity resume. These actions do not start room physics.

Position, orientation and scale share one completion operation. The native editor
captures and validates a candidate, waits for an earlier save, then prepares its
private saved bytes and preflights storage on a worker. Only the target body's
pose is held. World time, viewer movement and unrelated objects keep running.
An authored edit invalidates a stale candidate instead of being overwritten.
Concurrent placement actions wait under the scheduler's existing capacity and
timeout limits before capturing a candidate. Waiting does not hold the body's
pose or copy another room. Target revision and temporary-session identity are
checked again before starting; cancellation cannot later apply a waiting edit.
Storage preparation remains single-owner while independent native loads proceed.

Unity accepts the candidate and reconciles its native objects once, then the
worker publishes the save. The body resumes immediately after application;
success waits for confirmed publication. Gravity may therefore move a live object
past its saved placement before the receipt completes. Stop before acceptance
preserves its previous pose. Stop after acceptance cannot undo it or cancel the
pending publication; the directory owner remains until that write drains.
A publication conflict preserves existing files and blocks further saves until
inspection. Do not retry a possibly accepted edit under a new identity.

In a temporary room these actions change live placement and local Undo without
publishing the room. An explicit kept snapshot makes them durable. Their stopped
status describes that temporary outcome without claiming a pending disk save.

The journal, storage coordinator, native reconciliation and Undo remain shared.
Synchronous manual editor APIs keep their existing completion contract. Painting
and deletion retain their synchronous save-before-application path. These are
different entry-point lifecycles, not separate room databases or histories.

Each changed value/removal is a room Undo edit; Stop and later program failures
do not roll back completed edits. Delete restores the saved object data on Undo,
including geometry, recipe, model references and animations. It does not delete
source model files or source animation collections. A no-op value does not add
an additional journal change.

Completed receipts are historical. Replaying a completed delete returns its
old result even after Undo restored the object; it does not delete again.
A newly requested delete needs a new issued identity and current target revision.
After a lost acknowledgement, inspect the previous identity; never replay with
a fresh one to guess at recovery. If the process dies between a saved room change
and its terminal receipt, the existing interrupted/uncertain-outcome rules apply.

## Composition and shared authoring

Version-3 programs can create an object, bind its `objectId` to a local, then
paint, resize, move or delete that exact object. The original native authority
check still rejects guessed IDs. Deleting an object does not authorize a new
object with the same ID; subsequent actions against the missing target fail.

The book provides scalar controls for position, size and tint, with the same
named calls behind the function/source editor. Program saving requires
`objectEdits.v1`. Existing native-only or older clients reject unsupported calls.
No additional provider connection, agent execution loop or button system was
introduced.

`test-fixtures/browser/objectEditProgram.json` comes from a native test that
creates, paints, resizes and moves a ball alongside unrelated playback.
`objectEditResults.json` contains actual native paint/deletion observations.
Web parsing and the original-app agent test use these contracts. The agent test
uses a simulated model, so it is not evidence of live-provider acceptance.
`scripts/probe-object-edits.mjs` verifies the real Chrome authoring interface
with simulated edit acknowledgements and starts no native action.

Initial object-edit checkpoint validation: 1,285 app tests across 155 files; 126 Unity EditMode and 94
PlayMode tests; TypeScript, lint and shared-code guards. Three optional private
model/collection tests are skipped without external files. Native integration
checks cover persistence, Undo/Redo, deletion replay, current physics motion,
unrelated animation, stale revisions, held targets, protected built-ins and
failed saves.

## Remaining release work

These are explicit property placements, not walking trajectories or incremental
scale animations. Position, rotation and scale now prepare and publish saved
bytes in the background through one implementation. Their IDs, versions, schemas,
limits, target ownership and Undo behavior are unchanged; position and scale now
advertise completion duration so every client waits for their saved outcome.

Snapshot capture, native reconciliation, other synchronous edit paths and small
receipt writes still need physical frame-time assessment. These desktop checks
do not establish headset latency, live-provider acceptance or Quest Store readiness.
The current validation checkpoint is recorded in [the v1 plan](QUEST_V1_PLAN.md).

Rotation was added on 2026-09-28 through the named module path, with no numeric
tray adapter. The book's capability/function editor generates its fields. Pitch,
yaw and roll use Unity Euler angles, each -180 to 180. Undo restores the previous
journal pose rather than an unsaved physics frame. Completed receipts never
reapply rotation after Undo. The shared program-rotation.json fixture is authored
in the browser and executed in Unity. See [module verification](QUEST_CAPABILITY_MODULES.md).

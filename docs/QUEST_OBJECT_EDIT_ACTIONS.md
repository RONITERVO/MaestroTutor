# Shared object-edit actions

The catalog now exposes 18 native actions. Four new version-1 actions let the
original-app agent, book controls and saved programs edit existing objects:

| Action | Arguments | Effect |
| --- | --- | --- |
| `object.position.set` | target, x, y, z | Immediate placement in room metres; keeps rotation/scale and resets velocity. |
| `object.scale.set` | target, scale | Uniform scale; keeps current position/rotation and resets velocity. |
| `object.color.set` | target, red, green, blue | RGB tint; preserves live position and velocity. |
| `object.delete` | target | Removes a user-created object; room Undo can restore it. |

The included book and Maestro support placement and scaling. Their scale ranges
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

The native editor builds and validates a room candidate, waits for an earlier
save and atomically saves the candidate before applying it and reporting success.
The journal and reconciliation path remain the same as ordinary room tools.
There is no second scene database or undo stack. Manual painting, erasing,
surface placement and avatar resizing use these same edit methods after their
existing human-priority interruption checks.

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

PC validation: 1,285 app tests across 155 files; 126 Unity EditMode and 94
PlayMode tests; TypeScript, lint and shared-code guards. Three optional private
model/collection tests are skipped without external files. Native integration
checks cover persistence, Undo/Redo, deletion replay, current physics motion,
unrelated animation, stale revisions, held targets, protected built-ins and
failed saves.

## Remaining release work

These are immediate property edits, not trajectory animation, incremental
scaling or arbitrary code execution. Recipe replacement, object import, drawing
creation and other room operations still need broader catalog coverage.
Legacy batched room commands retain their established path.

Durable writes currently serialize on Unity's main thread. Quest measurements
and, if necessary, an asynchronous completion lifecycle remain release work,
especially for large rooms and repeated edits. This checkpoint does not claim
headset acceptance, real-provider completion or Quest Store readiness.

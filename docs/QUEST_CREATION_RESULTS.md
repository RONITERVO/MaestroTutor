# Creation results shared by chat, programs and book controls

The native catalog now includes `object.create.primitive` version 1. It creates a
ball, block or cylinder through the same `RoomEditor.CreatePrimitive` path as the
physical creation tray. The scheduler, typed input validation, native-issued start
ID and durable receipt remain shared with other actions.

Inputs are `shape`, `name`, `x`, `y`, `z`, `scale`, `red`, `green`, and `blue`.
Coordinates are metres in room axes, with a combined distance limit of 25 m.
Scale is the existing shape multiplier, 0.1–4; RGB channels are 0–1. The created
ball is bouncy, the other shapes solid, and mass is 0.5 kg. Room physics still
requires the normal scan/start conditions before simulation or pushing works.
Creation does not start physics.

The action returns `{objectId: "exact native-created ID"}`. The catalog exports a
typed output schema; the original-app validator, book and agent consume it.
Creation saves the validated room candidate before applying it and reporting
success. It waits for any existing background save to finish so an older snapshot
cannot overwrite the creation. Read-only/future room files and failed writes
prevent applying a new live object. Each creation is one room Undo edit. Undo can
remove that object; Stop only stops future program work. A later action failure
does not roll back earlier creations.

Creating an object does not reconfigure unchanged avatars, physics or recipe
players. Existing owned motion can continue. This uses the existing journal and
reconciliation logic, without a second scene store.

## Composing results

When native advertises `actionResults.v1`, a version-3 invoke can add
`results: {objectId: "ball"}` to assign its output to an existing text local named
`ball`. Later calls can bind `target: {var: "ball"}`. The example in
`unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-create.json` creates a ball
then applies a physical impulse to that exact returned ID.

The literal target in a bound input remains schema-valid; a 32-zero GUID is a
placeholder, not authorization. The interpreter checks the resolved target against
declared existing resources or validated object IDs created by that run. It rejects
guessed/foreign IDs. Result destinations must match the catalog's type and cannot
overlap within a call. The book's function blocks expose result-variable creation
and scalar argument-variable selection; source and blocks still edit one program.

A run may create at most 16 objects, with the existing room limit of 64 creations.
The limit is checked before the next native effect. Creation remains an instant
action: one effect per run per tick, no budget renewal and no recursive catch-up.
A new run has new locals and can create new objects; saving alone creates nothing.
Result bindings are not flattened through the simple sequence editor.

## Recovery and evidence

A one-off receipt contains its typed `output`. Duplicate delivery of the same
native-issued ID returns that same output without another creation. The result
survives receipt reconstruction. A completed receipt records history; it does not
assert that the object still exists after an Undo, deletion or later edit. After an
interrupted save/start, the existing uncertain-outcome rules prohibit automatic
replay. Inspect the current room before using a retained object ID.

Native tests cover create→result→real PhysX impulse, continued unrelated recording,
room persistence, Undo/Redo, duplicate delivery, retained results, failed future-file
writes, result type/authority checks and the 16-object bound. Actual native output is
retained in `test-fixtures/browser/creationResult.json` and accepted through the
same web observation parser. Web tests cover capability adapters, malformed or
contradictory outputs, runtime feature gating and human result wiring.

## Remaining boundaries

Recipe creation is now also a named action; see [editable recipe actions](QUEST_RECIPE_ACTIONS.md).
Drawings and imported avatars/objects retain their existing creation paths;
this work does not yet make every edit/import a program capability.
The new generic scalar-result contract is the foundation for that work. Programs
still do not execute arbitrary engine code.

Creation currently performs a serialized room write during the instant action.
Quest storage latency and sustained creation bursts require hardware measurement;
a background completion lifecycle may be needed before release if this causes
visible frame stalls. No headset installation or performance claim accompanies
this checkpoint. Full v1 acceptance, real-provider journeys and store setup remain.

Current PC checks: 1,264 app tests, 123 Unity EditMode and 89 PlayMode tests
passed, plus TypeScript and lint. Three optional private-model tests remain skipped.
The Chrome probe in `scripts/probe-program-results.mjs` verifies visible result
wiring and the exact submitted program using simulated edit acknowledgements;
it does not claim headset or provider execution.

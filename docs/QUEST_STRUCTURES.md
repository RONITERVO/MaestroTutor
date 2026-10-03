# Persistent structures and shared baselines

A structure is a saved definition of named slots, exact member IDs and room-local
baseline poses. Its pieces remain ordinary independent room objects. The record
survives a member being deleted, so absence is observable instead of silently
changing the meaning of the structure. This increment does not create joints,
snap sockets, replacement geometry or a separate blueprint library.

## Shared authoring

`structure.save` either captures current member poses or accepts explicit baseline
slots. An empty ID and revision zero create a new identity. Editing uses the exact
existing ID and latest structure revision. Capture and definition edits require
all selected objects to exist, be released and be available for authoring. Saving
a definition never moves its objects. A piece may belong to several definitions;
each baseline is explicit and can describe a different arrangement.

`structure.reset` takes the structure ID, revision and the complete list of member
IDs. That list is an authorization/ownership guard, not a second definition.
Its set must exactly match the current saved membership. The scheduler claims all
pieces together; stale metadata, a missing member, a grip, active authoring or an
ownership conflict refuses the whole operation. Reset uses the same atomic layout
operation, including live pre-reset poses for Undo and zeroed member velocities.
It preserves current geometry/settings and does not start physics or certify
stability. A ball used to knock down a castle should have an explicit reset policy
too; leaving it inside the rebuilt stack can immediately destabilize the pieces.

`structure.forget` removes the definition only. Objects remain. Undo restores the
definition with a fresh revision; interrupted actions and old receipts are never
replayed. Programs referencing a forgotten definition report it unavailable.

The missing-member policy is explicit refusal. To replace a piece, save that slot
with the chosen replacement ID and intended baseline. To exclude it, save a new
membership list. No object is chosen by display name and no deletion is silently
reversed. Saving validates up to sixteen structures, each with 1–16 distinct slot
keys and member IDs. Baselines use the existing room placement bounds.

## Observation and programs

`structure.list` pages through metadata. `structure.definition` returns its exact
revision and tolerances. `structure.slot` reads one stable slot/baseline by index.
`structure.state` compares live member origins, rotations and uniform scales with
the baseline, returning present, displaced, missing and held counts. Position
uses room-local metres, rotation uses degrees, and scale tolerance is an absolute
uniform-scale difference. Reads grant no authority to edit returned object IDs.

Unavailable transforms or paused app runtime make the aggregate unavailable;
partial counts must not be treated as a complete outcome. Missing objects remain
distinct from unavailable transforms. A hand-held piece can be intentionally
displaced. These facts do not prove which collision caused movement, detect mesh
or cloth deformation, or assert that a structure is physically stable. Programs
combine the facts with their chosen thresholds, timers and contact observations.
There is no structure-specific transition subscription in this increment.

The shared [capture/reset program](../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-structure-reset.json)
uses ordinary capability results to carry the new structure ID/revision into a
reset. Member IDs remain explicit program resources. Arrays remain literal in
this program format: a batch creation's returned list cannot yet be assigned to a
whole action-array argument. The room agent can read the returned IDs and save a
structure in its next action. Persistent membership does not imply a change to
that input-binding boundary.

## Persistence and compatibility

Structures are part of the same room document and journal, not another save file.
A definition edit saves once and adds one Undo. Fork, keep, discard and combined
room/program-memory snapshots include the metadata. A missing member does not
make the room unreadable. Unknown future structure versions preserve the primary
file instead of falling back to a backup that could silently lose the definition.

The current room format is v3, paired snapshot intent v2, and portable workspace
archive v2. Clean legacy room v1/v2 files can still be read with empty structures;
original files remain. Old in-flight snapshot evidence and old portable archives
are preserved but not interpreted as the new format. This is a pre-release format
change under the owner's development-reset permission, not permission to silently
break released users' saved worlds. No headset data is reset by this code change.

## Acceptance

Tests cover detached copies, journal/fork/keep/discard, one Undo, failed saves,
stale revisions, missing members, exact member authority, native program result
bindings, archive round-trip and future-format preservation. Native play tests
also cover a real simulated ball knocking down loose bricks followed by a saved
structure reset. Full-app and generated-form checks exercise the shared catalog.
The accompanying storage regression also verifies that a background library
reference scan cannot race an atomic file replacement.
These are development checks; Quest interaction, performance and scan alignment
remain separate release gates.

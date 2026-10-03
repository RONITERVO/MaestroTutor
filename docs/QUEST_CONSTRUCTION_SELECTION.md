# Shared construction selection

`constructionSelection.v1` provides an ordered, transient set of up to 16 created
objects. The inline room observation, `room.selection` fact, book controls and
physical **Collect pieces** tool expose the same native state. The first member
is the origin when preparing a reusable construction. Single-object inspection
remains independent and highlights the current focus among collected pieces.

The optional workshop lets a user select named pieces, locate one in the room,
change their order and review the ordinary `program.module.captureConstruction`
action. Duplicate names receive distinguishing ID prefixes; those labels never
replace object identities. Opening the capture draft does not start capture or
follow later selection changes. Current member revisions are loaded through the
existing shared form before execution. The original chat stays on the book.

The physical tool is a solid button on the movable creation tray. While collecting,
controller/hand taps toggle creations and do not emit their normal item-tapped
behaviours. Grips still move items. Finish retains the chosen set; Clear removes
it. Book/Maestro remain separate. Entering drawing ends picking; retained drawing
must be resolved before starting collection. Suspension ends picking without
resuming it automatically. Physical headset reach/readability still needs acceptance.

## Contract and lifetime

`room.selection.set` replaces `{stateId, members, collecting}` atomically. The
caller supplies the current state ID and exact ordered member IDs. Duplicate,
missing, built-in and oversized membership is refused. A stale state ID cannot
overwrite someone else's selection. Selection does not change saved object
revisions, move items, add room Undo entries or grant authority for later edits.
Action receipts record one-off changes through the same native execution path.
Repeated no-op selection calls keep the existing state ID.

Deleting members prunes them and changes the selection identity. Undo restores
objects without silently selecting them again. Beginning/ending temporary rooms
clears selection; workspace reload starts empty. The selection itself is not a
saved structure, blueprint or persistent room component. Use the existing
structure and module capabilities for those purposes. Selection remains visible
in the delegated agent's ordinary room context, with no second agent scene.

The solid construction handle now moves, turns and resizes selected pieces with
one save/Undo; see [construction movement](QUEST_CONSTRUCTION_MOVEMENT.md). It
requires paused physics and finished collection. Socket snapping and automatic
safe placement remain unimplemented. Collected pieces remain independent
physical objects after placement.

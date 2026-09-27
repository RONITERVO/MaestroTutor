# Catalog-driven physical quick edits

Development checkpoint, 2026-09-28. The movable behaviour tray now selects
capabilities and fields from the same native catalog as the book and agent.
Named-only actions such as object.rotation.set are available without extending
RuleActionKind, RuleStep or the tray's action list.

## Human workflow

Select a behaviour, choose a block, and use Block type to cycle catalog entries.
Prev/Next field visits its schema-defined values. Value -/+ changes bounded
numbers, choices, booleans or compatible object references. The Step control
chooses Auto or an increment from 0.01 to 15; integer fields stay integral.
Set field assigns the
currently selected room object or includes/removes an optional field. Nested
object fields and existing array entries are traversed from the schema.

Changes remain a detached draft. Apply validates and saves the complete program
as one behaviour Undo edit; it never starts playback. Discard loads the latest
saved version. Try action requires a clean draft so it cannot silently run an
older saved version while displaying different values. Stop remains available.

Edit in book opens the existing full behaviour workspace and the same selected
program. Text, detailed motion selections, expressions, array structure and
function editing belong there. The tray has no second text keyboard or source
editor. Its buttons remain solid 3D interaction targets; full authoring stays
on the book pages.

## Shared semantics and conflicts

Quick edits operate on named invocation nodes, including nodes inside branches.
They preserve node IDs, surrounding control flow, declarations, expressions and
result wiring. Fields supplied by expressions are read-only on the tray, and
changing a wired block's type is refused. Result-producing blocks are deleted
in the book where their consumers are visible.

Apply uses the same revision-checked RuleWorkshop edit request as the book and
agent. An external change preserves the draft and rejects Apply until the user
discards/reloads; it cannot silently overwrite the newer source. Invalid values
remain in the draft, with no journal or playback changes. Existing declared
resources are retained and new literal references are added. Computed references
retain their creation-result authority, never becoming guessed object IDs.

Block selection is shared with the existing book motion-library assignment, so
choosing a saved motion edits the same literal block the tray displays.

The prop-fitting tab still uses its existing specialized adapter for simple
literal steps. It follows the selected block and refuses unsupported structured
blocks. Motion-library assignment and older simple-editor adapters remain
explicit compatibility boundaries; this checkpoint does not claim their removal.
Repeat, interruption and while-state controls remain on the prop/options page.
Other event and mounted-button controls remain on the main tray.

## Verification and limits

Native tests click the actual tray through BookPointerRouter, edit and apply a
rotation action, open its program in the book, execute it through the shared
scheduler, and undo the program edit. They check every registered type appears
in the selector. Additional tests cover nested branches, concurrent agent/book
edits, exact result/binding preservation and rejection of invalid quaternion
drafts without persistence or playback.

The Chrome probe replays the actual native observation, opens the full book
editor and checks an exact further save request. Only the transport session is
substituted; the native program and object observation are retained. This probe
does not execute or acknowledge that later save in Unity. Native and book
screenshots are inspected separately.

Quest readability, comfort and end-to-end headset interaction remain unverified
for this updated layout. No device installation or user-data reset is included.
Vocabulary consolidation, grouped runtime commits, arbitration and richer
program/world types remain separate before-release work.

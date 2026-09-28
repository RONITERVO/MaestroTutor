# Temporary room storage boundary

Development foundation, 2026-09-28. This checkpoint adds the native editor and
journal boundary plus PC tests. It deliberately has no book control, public
catalog action or agent prompt yet. Existing user-facing creation still saves
normally. The API must not be advertised as an available user feature until the
shared async operation, receipts, observation and controls are integrated.

## Chosen semantics

Temporary play uses one explicit live fork of the room document. Manual edits,
agent edits, object creation, drawing, pose/recording edits and periodic physics
placement all use that same fork. The saved journal and its Undo history stay
separate. This is a room-wide mode, not an invisible transaction owned by one
program. It cannot silently become the default for ordinary creations.

Begin stops current authoring using the existing editor boundary, captures the
base and saves it before opening the fork. This initial write still uses the
existing synchronous writer; moving that lifecycle off the main thread is open.
The fork avoids a write per gameplay edit. It has its own bounded local Undo
history. Saved Undo history is neither flooded nor discarded.

Keep captures a detached snapshot and writes it on a worker. It keeps temporary
play active. Only after that exact write succeeds is its delta accepted into the
saved journal as one Undo edit. No transform is reset, action restarted or
creation repeated when the write finishes. Edits made after capture remain in
the live fork and are not part of that save. Repeated Keep saves successive
snapshots; a no-op does not add an Undo entry. The normal Save control delegates
to Keep while a temporary room is active.

A failed Keep changes neither the saved journal nor its Undo history. The fork
remains editable and can be saved again or discarded. Only one writer runs at a
time. Discard refuses while an already-dispatched Keep is pending; it cannot cancel a
write whose durable outcome may already have happened. Pause/quit finish that
dispatched snapshot, but never flush the later live fork. Autosave and periodic
physics capture cannot bypass this boundary.

Discard stops existing authoring/actions, pauses physics and returns to the most
recent saved base (including successful Keeps). It restores document state, not
historical velocity, a program counter, a thrown ball's trajectory or a running
animation. The restored room's saved Undo is available. Forks share a monotonic
object-revision clock so Undo, removal/recreation and Discard cannot make old
observations valid again. Unchanged objects retain their revisions. Motion
references in the base, its Undo history and an in-flight snapshot remain
retained while the live fork uses different references.

Held objects and held avatar joints prevent Begin/Keep/Discard. Keep also
requires authored object movement to finish. Physics may continue after its
placement has been sampled. Later physical movement stays temporary, just like
later manual edits. Begin and Discard use the existing global Editing event;
they must not be naively called inside a scheduler Start handler, which could
stop itself reentrantly.

## Scope

The boundary covers RoomDocument only. Imported model/motion files, behaviour
programs, activity profiles, controller preferences, room scans and chat/account
state retain their existing stores. Discard is not a rollback of external
assets, API use, speech, sounds, physics history or user movement. Nothing resets
installed app data or moves source model libraries.

The room remains bounded by its existing object/geometry/animation budgets.
Keeping a snapshot is a bounded document operation, not a general transaction
for arbitrary game effects. It is suitable for a build/try/keep workflow and
provides the storage foundation for grouped gameplay commits.

## Before public exposure

- Give asynchronous completion its own scheduler contract. Do not disguise disk
  I/O as a short animation, renew an instruction budget because a save finished,
  or report success merely because a worker was queued.
- Route agent, program, book and native controls through one catalog operation
  and receipt path. Explicitly identify the temporary session and its save
  request; stale/duplicate requests must not keep a different session's changes.
- Add room observation for mode, pending save, last completed save and failure.
  Show the scope clearly in the familiar book. Creation results in temporary
  mode must explicitly describe live, unsaved IDs rather than promise durability.
- Define scheduler-safe Begin/Discard ownership, and permission for a room-wide
  change. Stop after dispatch must report an uncertain/already-dispatched save,
  never pretend to retract a write or silently replay it.
- Prove the controls and lifecycle through actual native/browser receipt evidence
  and Quest acceptance. Existing user-facing defaults stay unchanged until then.

## Verification

Native tests cover fork isolation, one grouped saved Undo, preservation of earlier
Undo/Redo, rejection of invalid snapshots, no-op commits and monotonic revisions.
PlayMode tests exercise the actual editor, files, PhysX and XRI: per-action edits,
physics capture, autosave, focus/pause/destroy isolation, edits during a Keep,
failed writes and retry, restoring placements, hand ownership, and finishing only
the dispatched snapshot on pause. These are PC tests, not headset acceptance.

Current native verification: 162 EditMode and 114 PlayMode tests pass, with three
optional private-model/collection skips. Catalog export/source checks pass. This
checkpoint is not packaged or installed; the previous typed-creation APK remains
the latest packaged build.

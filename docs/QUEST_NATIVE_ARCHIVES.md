# Native workspace archives

This checkpoint provides native snapshot capture, a portable archive codec and
verified restore staging. **Book/agent commands, Android file publication/picking,
restore activation and recovery UI are not connected yet.** It does not make native
backup/restore a finished user feature. Original chat backup remains separate.

## Snapshot boundary

`WorkspaceArchiveCapture.Start` is called on Unity's owner thread with the room,
behaviour and movement owners. It takes nonblocking model/motion writer gates and
copies accepted room records, behaviour definitions/bindings/buttons, controller
preferences, avatar activity choices and exact reusable module definitions without
an intervening await. A worker serializes these detached copies and collects the
stable model/motion inventories. Existing library writes must finish first; new
imports and motion maintenance wait until capture releases the gates. Ordinary
scene interaction is not suspended. The gates release on success, cancellation and
failure, independent of Unity's pause/resume state.

This captures accepted authored documents, including accepted edits whose normal
autosave has not yet finished. It does not capture in-progress takes, physics
velocities, current program variables, execution positions, Undo history, room
scans/anchors, chat, credentials, action receipts or caches. Temporary rooms must
be kept/discarded first so a temporary fork is not silently presented as the saved
workspace. Unavailable storage and damaged module copies stop portable capture;
forensic recovery-file export remains separate work.

The result is a flushed/closed **private snapshot file**, not a user-visible saved
file receipt. The publication adapter must confirm its own flush/close/publication
before telling a user their export is saved. Failed capture removes its own partial
file. A completed snapshot is independent of later room or library edits.

## Version 1 format

The ZIP includes `manifest.json` with exactly `format`, `version` and `entries`.
The format is `maestro-native-workspace`, version is `1`, and every entry has exact
`path`, `bytes` and lowercase SHA-256. Hashes detect mismatched content; they do not
authenticate the author. The manifest hash identifies the inspected inventory.

Required documents:

- `room.v2.json`
- `behaviours.v2.json`
- `controls.v2.json`
- `avatar-activities.v2.json`
- `motions/motions.v2.json`

Optional entries are content-addressed `models/<hash>.glb`, derived model information
`models/<hash>.txt`, `motions/<hash>.motion.glb` and
`program-modules.v1/<hash>.json`. Paths, case and hashes are strict. Arbitrary files,
absolute paths, traversal, recovery copies, action receipts and executable code
files are not accepted. Split/encrypted/ZIP64 containers are unsupported; the
bounded writer does not need them.

Limits include 1,400 entries, a 512 KiB manifest/central directory, 512 MiB archive
and extracted content, the existing 32-model/256 MiB and 1,024-motion/128 MiB library
budgets, 256 modules and each native document's existing size limit. Model info
sidecars are limited to 128 KiB and strict UTF-8; larger sidecars stop capture rather
than being silently truncated. The complete original model, including its embedded
attribution, remains in the GLB. Parsing checks the ZIP directory before allocating
its entry collection, bounds actual decompressed bytes and verifies every digest.
Only one asset's bounded bytes are validated at a time; large-library memory and
frame-cost acceptance still require headset profiling.

Native validators check each document, model and motion. Downloaded motion entries
must have exactly one matching payload with the same bytes, rig, duration and curve
count. Removed downloads retain their catalogue IDs/tombstones without payloads.
Module definitions retain their canonical content IDs and embedded pins. Unsupported
behaviour source is preserved as unavailable, consistent with ordinary store loads;
a single unavailable program does not discard the rest of the workspace.

## Restore staging

`WorkspaceArchive.Stage` accepts a bounded seekable stream and an existing private
staging parent. It verifies the complete inventory, document versions, payload
hashes and native formats, then returns an owned isolated staging directory.
Failures/cancellation remove only that stage. Disposing a preview abandons it.
No live store is replaced and no behaviour runs during staging.

The preview summary identifies known missing room/avatar models, walk/activity
motions and controller-bound programs. Those references remain unchanged. It also
counts unavailable programs. This is not exhaustive static dependency analysis:
programs can calculate IDs, and explicit dependency inspection/rebinding remains
unfinished. Restoring a reference never means silently choosing a different asset.

Next integration must publish/pick archives through the native file boundary,
provide the same discoverable operation and completion evidence to the book and
agent, and switch to a verified generation only through an explicit restore action.
Keep the prior workspace recoverable, invalidate stale requests, and leave imported
behaviour triggers/movement/physics paused until reviewed. Copying staged files over
live stores one by one would not meet the restore contract. Activation must not
restore old execution receipts or replay interrupted actions.

## Evidence and remaining acceptance

EditMode tests round-trip real fixture GLB/motion bytes and nested module definitions
through ordinary native stores. They cover immutable capture, missing references,
unavailable programs, tombstones, active library write exclusion, traversal and
unknown paths, missing/extra/duplicate entries, tampered content, invalid models
with correct hashes, malformed manifest versions/lengths and directory limits,
cancellation and output failure. A PlayMode test captures the actual room/rule/
controller owners, edits after capture and proves the snapshot retained the earlier
definitions; a failed output releases both library gates.

These are PC checks. They do not establish Android Downloads/picker behaviour,
restore activation/restart, interrupted power-loss durability, large-library memory
or headset performance. Native portable backup/restore and the wider v1 release
remain incomplete until that integration and acceptance work passes.

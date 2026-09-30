# Native workspace archives

Native snapshot capture and Android Downloads publication are available through
`workspace.archive.export` in the shared action catalog. The book, agent and saved
programs use the same operation and execution receipts. **Archive selection,
restore activation and recovery UI are not connected yet.** Original chat backup
remains separate, and portable backup/restore is not a finished release feature.

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

## Publication

On Quest, open the workshop's Action catalog, search for “Export native workspace”,
inspect it and run with empty inputs. An agent discovers and invokes the same
capability; no extra agent-specific file tool is required. An older or non-Quest
runtime without `workspaceArchiveExport.v1` cannot run it. No caller can supply a
private source path, destination path, filename or arbitrary files to include.

A single `WorkspaceExport` owner captures to an app-private cache directory and
streams the closed ZIP through a native worker into a pending Downloads/Maestro
MediaStore row. The Java boundary accepts only that directory and generated
archive filenames, rejects links/noncanonical paths and enforces the 512 MiB bound
while streaming. It never sends binary bytes through the browser or a chat tool.
Text backup export keeps its existing text-only validation. The sink closes the
provider stream, reads the provider's resolved filename and publishes the row
before returning success. Copy, close or publication failure aborts its pending row.
The owned private ZIP is cleaned up after either publication or failure.

The completion receipt contains the published location, archive size in KiB,
manifest hash and inventory/missing-reference counts. KiB preserves exact byte
precision while keeping the largest value within the program numeric bounds. A private ZIP is not a saved
receipt. The existing one-off execution ID prevents duplicate delivery from
publishing twice; a new explicit invocation creates a new snapshot. Only one export
can be pending per workspace. Export has a bounded ten-minute native wait; other
action/loading deadlines remain unchanged. Stop ends waiting and program
continuation but cannot retract an already dispatched publication. After Stop,
timeout, receipt-write failure or process loss, inspect Downloads/Maestro before
requesting another export. A cancelled receipt is not later rewritten as success.
App-private cache/pending provider entries left by process loss are not evidence
of a completed export; crash cleanup and power-loss acceptance remain work.

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

Next integration must pick archives through the native file boundary, provide a
shared inspection/review flow, and switch to a verified generation only through an
explicit restore action.
Keep the prior workspace recoverable, invalidate stale requests, and leave imported
behaviour triggers/movement/physics paused until reviewed. Copying staged files over
live stores one by one would not meet the restore contract. Activation must not
restore old execution receipts or replay interrupted actions.

## Recoverable workspace generation store (native foundation)

`WorkspaceGenerationStore` prepares an imported archive in a new private generation,
retaining the verified manifest outside its writable data directory. It rechecks
all document/asset bytes and the exact inventory before activation; a changed
preview cannot be selected. A single versioned selection file identifies the
active and previous generations, a revision, an action-history epoch and a persisted
review requirement. Existing development data remains the original `room` directory. The first mutation
persists that original selection before any activation attempt, so the first switch
also has a retained baseline. Preparing an archive does not migrate or clear current
data and never selects the import.

An exclusive filesystem writer lease serializes mutation, including separate store
instances. Each activation first writes a durable attempt with the expected old
revision and exact proposed selection. A flushed pending selection is then moved
or atomically replaced; the old selection is retained as `.previous`. The exact
same activation can reconcile or retry an interrupted commit without minting a
second workspace/history identity. An altered identity, stale selection revision,
newer format, corrupt pointer or missing active directory stops the operation. A
corrupt selection is never silently replaced with the original or its backup. A
missing pointer after an activation attempt also requires explicit recovery, even
if its pointer backup is missing.

Both activation and returning to the previous workspace assign a new action-history
epoch and persist `ReviewRequired`. Completing review keeps that epoch and changes
only the selection record, without rewriting programs or controller preferences.
Returning to an earlier authored workspace preserves changes made after its import;
it is not revalidated against the original, now historical archive manifest. A
missing previous workspace does not hide a healthy active one, but recovery refuses
to treat that missing folder as an empty replacement. Discard only removes an
unactivated, unreserved preview, with owned-path and link checks. There is a 64
generation retention bound; reaching it requires reviewed maintenance rather than
automatic deletion of user content. Orphan cleanup and retained-generation browsing
are still integration work.

**This store is not connected to production startup or a restore command yet.**
The runtime host must pass the selected root to room, behaviour and controller
owners, pass the separate epoch directory to invocation receipts, recreate owners
with a fresh bridge session, and enforce the persisted review hold before imported
state can receive events, controller input or physics actions. A stored review flag
alone is not runtime enforcement. The host must also define current-document review
validation, failure recovery UI and writer/lifecycle transitions before exposing
activation. Before switching, it must stop new edits and confirm that the current
accepted documents and asset writes are durably retained (or create and verify a
complete recovery snapshot). Merely retaining the old folder does not prove that
its latest autosaves succeeded; a failed retention step must keep the old owners
and their accepted in-memory state alive. These native APIs must stay behind the
shared reviewed operation; an agent cannot supply a filesystem path.

The Android picker needs a separately tracked request: `RoomRules` intentionally
stops active actions when the app loses focus, and opening system file selection
can cause that pause. Do not put the entire picker interaction inside an awaited
action and then claim it survived cancellation. File selection, verified preparation,
review and activation must each have observable outcomes across pause/resume.

Fault-injection tests interrupt preparation, activation and previous-workspace
recovery around the pointer commit, reopen the store and verify either complete
old or complete new selection. They check identity reconciliation, pending review,
new receipt epochs, real native room/model loads, stale requests, writer exclusion,
changed manifests/content/inventory and corrupt-pointer behaviour. They are PC
filesystem evidence; directory durability after actual device power loss is not
proved by an atomic file replacement or these simulated interruptions.

## Evidence and remaining acceptance

EditMode tests round-trip real fixture GLB/motion bytes and nested module definitions
through ordinary native stores. They cover immutable capture, missing references,
unavailable programs, tombstones, active library write exclusion, traversal and
unknown paths, missing/extra/duplicate entries, tampered content, invalid models
with correct hashes, malformed manifest versions/lengths and directory limits,
cancellation and output failure. A PlayMode test captures the actual room/rule/
controller owners, edits after capture and proves the snapshot retained the earlier
definitions; a failed output releases both library gates.

Native PlayMode checks verify pending/published/failed/cancelled receipts and
duplicate delivery through the actual shared executor. Android JVM tests verify
binary streaming, private-path and size bounds, failures and pending MediaStore
publication with the provider-resolved name. These are PC checks. They do not
establish actual headset Downloads/picker behaviour,
restore activation/restart, interrupted power-loss durability, large-library memory
or headset performance. Native portable backup/restore and the wider v1 release
remain incomplete until that integration and acceptance work passes.

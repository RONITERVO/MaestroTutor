# Native workspace archives

Native snapshot capture and Android Downloads publication are available through
`workspace.archive.export` in the shared action catalog. The book, agent and saved
programs use the same operation and execution receipts. Native archive selection
and verified previews also use the shared catalog. Activation now connects the
reviewed preview to the persistent host through that same catalog. Review completion
independently verifies the exact inspected content before releasing its hold.
**Previous-workspace recovery is not connected yet.** Original chat backup
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

The shared activation and review operations below switch to a verified generation
only through an explicit restore action.
Keep the prior workspace recoverable, invalidate stale requests, and leave imported
behaviour triggers/movement/physics paused until reviewed. Copying staged files over
live stores one by one would not meet the restore contract. Activation must not
restore old execution receipts or replay interrupted actions.

## Native file selection and verified previews

`workspace.archive.select` opens one Android file chooser and immediately returns
an exact request ID. Its completed action receipt acknowledges a tracked request;
it does not mean a file was chosen, verified or restored. Android's chooser pauses
the app and stops ordinary room actions. The selection owner survives that pause,
prepares the selected ZIP after returning and never resumes the stopped actions.
The requesting chat/agent turn may also be interrupted; the retained native receipt
and request status let the next turn inspect the outcome without opening another
chooser or replaying the action.

Read `workspace.archive.selection` with that ID through the same typed fact query
used by the book, agent and programs. It reports selecting, copying, preparing,
prepared, cancelling, cancelled or failed. A prepared preview includes its exact
generation/manifest identities and counts for objects' supporting files, models,
motions, modules, unavailable programs and known missing references. Display text
and structured values stay within the shared program budgets. Reads grant no
filesystem access, edit permission or execution authority. Unknown IDs are
unavailable; a previous request cannot cancel or impersonate the next one.

`workspace.archive.cancel` cancels the exact selection or discards its unused
prepared generation. Cancellation during verification also discards a worker result
that finishes before its cancellation is observed. It never deletes the selected
source ZIP, changes the active workspace or starts imported code. A discard failure
keeps the preview for inspection/retry. Only one choice/preview is offered at once.

The native picker accepts a user-granted content URI and copies at most 512 MiB into
its private session cache. Existing URI/provider-UID checks exclude app-private
providers and arbitrary paths. C# accepts only the native-owned cache layout,
rejects linked paths, checks the byte limit again and stages the verified archive on
a worker. No URI, private path or binary archive crosses the book/agent bridge.
Duplicate start IDs cannot reopen the chooser, late callbacks/releases cannot affect
a newer owner, and a retiring provider worker blocks another selection until its
stream closes—even when the provider ignores cancellation. Provider cancellation
and preparation have bounded waits; an uncooperative stream can leave selection
unavailable until it closes rather than allocating unbounded workers.

**Selection and preview are connected; activation is not.** The current original
room remains active throughout these operations. Generation ownership, review,
accepted-current-state retention and startup/recovery handling below are still
required before a restore action can be exposed. Headset picker acceptance remains
unverified while device work is on hold.

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

**Production startup now resolves this selection before creating room owners.**
The persistent shell keeps XR, the book/browser and the agent connection alive.
The host supplies selected data and receipt roots and acquires a required activity
hold before loading any content. A content replacement verifies the exact committed
selection and retains the edit hold through destruction of old owners, then opens a
new session. No user-facing restore command is enabled yet. Current-document review
validation, recovery controls and coordinated writer/lifecycle transitions are needed
before exposing activation. Before switching, it must stop new edits and confirm that the current
accepted documents and asset writes are durably retained (or create and verify a
complete recovery snapshot). Merely retaining the old folder does not prove that
its latest autosaves succeeded; a failed retention step must keep the old owners
and their accepted in-memory state alive. These native APIs must stay behind the
shared reviewed operation; an agent cannot supply a filesystem path.

Ordinary room and behaviour autosaves retain their unsaved state after a failed
write. They retry the latest accepted document after a bounded delay; a successful
older snapshot cannot clear later edits. Pause/focus/quit flushes keep failed edits
pending, and their checked flush path reports failure instead of relying on a
status label. Successful retries clear stale save errors without replacing a newer
user-facing status. Faulted workers follow the same failure path. A rejected immediate
object edit cannot forget an earlier failed autosave. Temporary-room lifecycle
callbacks still finish only dispatched baseline/Keep snapshots; retrying ordinary
saves cannot keep the later temporary fork. These fixes preserve in-memory edits
while the process lives, but do not guarantee recovery after termination during an
unresolved storage failure. They also do not yet constitute the coordinated
all-store retention required before switching workspaces.

The Android picker uses the separately tracked request described above: `RoomRules` intentionally
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

## Runtime activity holds

Native workspace owners now share a `RoomRuntimeGate`. A restore host can acquire
its hold before `RoomEditor.Initialize`, then pass the selected data directory and
an independent receipt directory. Rules and controller preferences inherit that
same data directory; action receipts use the supplied history epoch.

A hold prevents rule/event starts, one-off effects, recipe autoplay and explicit
recipe playback, avatar activity/gestures, animation authoring, controller bindings
and both movement modes, and physics startup. Entering a hold interrupts current
owners and stops simulation. App focus/pause changes cannot release another native
owner's hold. Nested holders must release their own leases; a cleanup failure keeps
activity held. Read-only inspection stays available while the app is focused.

Releasing a hold does not replay stopped actions, resume physics or recipes, or
accept a controller button that remained pressed. A fresh input is required, and
the first tutor-state reading establishes the rule baseline. New recipe objects
inherit the hold before evaluating saved animation. Imported model loaders already
disable file autoplay independently of this gate. Direct import previews and room
object clip controls also stop on the hold and require a new Play after release.

An activity hold alone permits document editing and manual object placement. The
separate accepted-edit boundary below closes those paths during retention. The
startup host now resolves the stored selection and applies its activity hold before
loading owners. A corrupt selection leaves the book and agent observation available
without silently creating a replacement room. Activation keeps that hold in place;
review completion below binds its release to the exact inspected contents.


## Preserving accepted edits before activation

`WorkspaceEditHold` coordinates the activity hold with `WorkspaceWriteGate` on the
Unity owner thread. It refuses to start during an accepted write, an unfinished
stroke, held object/joint/button, a temporary room, unavailable archive storage or
an unfinished module update. It stops effects and finishes authoring before freezing
edits, so the final accepted pose/take can be included. Failed acquisition releases
only its own leases. A runtime cleanup failure keeps activity stopped.

The same write gate covers the actual mutation paths: room/rule edits and their
Undo/Redo, temporary-room boundaries, physical selection and Recall, controller
preferences, avatar assignments, and model/motion/module library changes. Model
acceptance and batch imports hold leases across awaits through their final accepted
updates. Queued library writes count before they acquire their file semaphore;
module publication remains in flight until the owner observes its result. Ordinary
autosaves can finish because they only persist previously accepted documents.

Keep the edit hold through archive capture, preparation and activation. Existing
`WorkspaceArchiveCapture` copies the accepted documents, including unsaved edits,
and keeps model/motion bytes stable through archive close. The retained archive is
prepared as its own verified generation. The native store now requires that exact
retained generation and manifest hash when activating the imported generation.
One pointer commit selects the import as Active and the retained snapshot as
Previous, each with a fresh receipt epoch and review requirement. Original roots
and receipt files stay in place. This costs an additional bounded snapshot and copy;
storage failures must leave the live owners and accepted in-memory state intact.

An activation record binds the complete origin selection and both manifest
identities. Interrupted retries must use that exact pair. A reserved retained
snapshot cannot be discarded as an unused preview or reused for another activation,
even if the pointer commit has not happened yet. Both snapshots are reverified before
an uncommitted retry; an already committed retry reconciles its saved outcome without
rewriting authored content or switching twice. The record format is unreleased; old
incompatible development activation records fail closed without migration.

Native integration tests preserve unsaved room/rule edits and actual preferences and
modules, reject writes through the shared manual/agent paths while held, and reopen
the retained generation using ordinary stores. Failure tests keep the same live
owners, accepted data and Undo; queued-worker and module-observation tests cover the
async gaps. Filesystem tests cover changed retained content, substitution on retry,
reservation protection and interruptions around the pointer commit.

The production activation coordinator below now owns private archive cleanup and
shared maintenance execution. Content-bound review completion is described below.
The edit lease lasts until all retention/activation workers settle; native owners
are preserved on failed retention. Previous-workspace recovery still needs an exact operation identity
before exposure. An activation retry after releasing the edit hold or restarting
must first verify that the retained snapshot still represents the accepted current
state; otherwise the host needs a new import/retention pair. Selection revision
alone does not track edits within a room. Headset power-loss and storage/performance
acceptance remain open.


## Persistent book and workspace content

`MaestroRoom` creates the XR rig, physical room origin, scanned surfaces, book and
browser once. `WorkspaceHost` resolves the selection before `WorkspaceContent`
constructs room documents, avatar, objects, tools and library owners under an
identity-local child. Ordinary per-store read-only handling remains independent:
a damaged controls file does not hide a healthy room or overwrite that file.
Missing or corrupt selection metadata creates no fallback content. Failed partial
initialization removes its owners, registrations and input links while retaining
the shell and a descriptive agent observation.

A committed replacement requires the exact current editor's accepted-edit hold.
It verifies the selected pointer before removing anything, closes the old motion
library view, cancels room pointer gestures and unregisters old room items. The
book/browser survive, including page gestures. Old subscriptions are destroyed
before new owners load. Disabling the host during that frame releases its consumed
hold; re-enabling waits for destruction before reopening the durable selection.
A required review hold is applied before loaded recipes or other activity can run.

The persistent agent rotates its session when content is detached or replaced;
matching object IDs and revisions cannot authorize an old request in the new room.
Without content, catalog discovery, workspace maintenance and returning to chat
remain available, while room effects and edits are refused. Native integration tests cover selection,
replacement, interruption, registration cleanup, damaged stores, observer failures
and stale requests. A captured native unavailable-room state is accepted by the web
bridge regression, which also checks replacement invalidates pending work.

This is the startup and owner-lifetime boundary used by activation below.
Previous-workspace recovery remains unfinished. Review completion and maintenance
execution use the persistent domain below.


## Maintenance while room content is held or unavailable

The native capability catalog declares `domain: "workspace"` for archive selection,
cancellation and export, and for the selection-status fact. Omitted domain means
ordinary room execution. Invocation arguments cannot override the domain. Saved
programs still belong to their room scheduler and remain suspended during review;
the separate domain routes explicit one-off maintenance requests.

`WorkspaceHost` owns the archive chooser, exporter and `WorkspaceRuntime` for the
shell lifetime. The persistent runner uses the same `RuleScheduler`, interpreter,
capability handlers and write-ahead receipt implementation as room actions. App
pause/focus loss still interrupts active invocations. A room review hold or damaged
selection does not suspend maintenance. Export requires available native stores;
selection/cancellation and its read-only status do not require a loaded room.
The exporter rebinds new room owners without discarding already captured operations.

Room outcomes remain in `execution`; maintenance outcomes are in
`execution.workspace`. Each has its own native-issued next ID, errors, recovery
identity and bounded history. The shared app helper chooses the correct issued ID
from the catalog before task journaling and dispatch. A supplied old ID is retained
for reconciliation, never substituted. Native dispatch independently chooses the
same scheduler and refuses an ID issued by the other one. Inspect, Stop and history
recovery resolve the exact observed identity. An error in one history does not
invalidate the other. The book shows both histories and uses the same command path
as the agent.

Maintenance receipt storage lives outside selected room generations. Replacing
content rotates the room bridge session but preserves chooser/runtime ownership,
tracked selections and maintenance outcomes. A restart marks unfinished actions
interrupted using the normal receipt rules; replaying a completed picker-opening
receipt does not open another chooser. A request whose chooser no longer exists
has no invented selection-status fact.

Real-host tests exercise an unavailable room, selection/read/cancel, app pause,
restart reconciliation, replacement interruption, export under a review hold and
refusal of another domain's issued ID. Captured native results are validated by web
tests; the book Run control is exercised with unavailable room history and healthy
maintenance. These checks do not establish a complete restore journey. Activation
and review completion now use that route. Previous-workspace recovery and
retained-generation maintenance are still unfinished.

## Tracked activation through the book and agent

After selecting and inspecting a prepared archive, read `workspace.current` and call
`workspace.archive.activate` with the exact selection request, generation ID,
manifest hash and current revision. It acquires `WorkspaceEditHold`, copies accepted
current documents on the owner thread and retains a separately verified archive on
a worker. Only then does it commit the exact imported/retained pair. The persistent
host replaces content owners and applies the imported workspace's review hold
before its first frame. XR, the book/browser and the original agent transport stay
alive; the room session changes. The latest library page state stays in the browser
and is retried after pause/navigation until the page acknowledges its session and
revision, including after the old library owner has been destroyed.

The action's output is an activation `requestId`, not a completed switch. Read
`workspace.archive.activation` with it. Its `preview` and `retained` records keep
exact identities; `committedRevision` records a confirmed switch. `review` means
content opened under its required activity hold. `unavailable` can mean the pointer
committed but content initialization failed. The current-workspace fact reports
content availability separately. Opening-action receipts and actual operation
outcomes must never be conflated.

The operation owns a bounded, versioned `workspace-activation.v1/latest.json`
journal outside room generations. It records the retained pair before activation.
Only the latest operation is retained; older IDs have an unavailable status rather
than a fabricated result. Startup reads that exact pair and reconciles it against
the durable selection, without calling Activate again. A previous failed/unavailable
acknowledgement can therefore resolve to a healthy reviewed workspace on restart.
Malformed or unsupported history is preserved and blocks another activation until
explicit recovery; no automatic migration or reset is performed.

`workspace.archive.activation.cancel`, app pause, focus loss and host disable request
cancellation before commit. The editing lease remains held while workers can still
write, even while the host is disabled. Teardown cancels and drains the worker before
releasing its owner-thread lease. A cancellation request cannot undo a committed
pointer; reconciliation distinguishes post-commit failure from an unchanged room.
If the pointer is committed but live replacement cannot be verified, old content
remains held rather than accepting edits into a workspace that will not reopen.

An archive selection cannot be cancelled/discarded while activation owns it. An
unused preview can be reused after a cancelled preservation; an already reserved
pair becomes retained and requires a new selection. After editing resumes, the old
pair is never automatically retried with its stale snapshot. Capture ZIPs are
private and removed after worker completion. Unreserved failed retention generations
are discarded when possible; reserved generations remain protected for recovery.
Storage cleanup failure cannot change a confirmed commit into a reported failure.

Eleven native checks cover actual host activation, accepted-edit retention, stale
revision rejection, shared receipt reconciliation across restart, cancellation under
a disabled host, failed retention, synchronous capture failure, failure after pointer
commit, failed content opening followed by restart, unreadable commit outcomes that keep
old owners frozen, reserved-pair retry refusal,
teardown with a live worker, and browser close acknowledgement. Web tests consume a
captured native activation/retention result and exercise the shared Run and fact
controls. These are PC and simulated-transport checks; headset storage, chooser,
pause/power-loss, readability and performance acceptance remain open.

Review completion below binds approval to the current inspected contents, which
can be edited while under review. Previous-workspace recovery, corrupt-selection
recovery and retained-generation browsing/cleanup remain release gates. The
existing storage-only helpers are not exposed as complete user recovery commands.


## Completing an exact content review

The book and agent share `workspace.review.prepare`, `workspace.review.complete`
and `workspace.review.cancel`, with the typed `workspace.review` fact. These are
ordinary catalog entries in the persistent workspace domain; room programs remain
held. `workspace.current.reviewRequestId` identifies the latest retained request.
The opening receipt acknowledges a tracked operation. It does not mean inspection
or approval has completed. Read its fact for the eventual outcome.

Prepare requires the exact current selection revision. A short accepted-edit hold
captures documents, library indexes and asset hashes through the same validated
manifest encoder used by archive export, without writing another ZIP. The prepared
fact exposes a content hash, the generation/revision identity and bounded counts
for assets, programs and known missing references. Inspect the actual workspace
and these counts before completing review. This is not a claim that all computed
program references have been statically resolved. Only the latest review candidate
is retained. A prepared candidate can survive restart; it never authorizes content
without another native check.

Complete accepts only that prepared request ID, hash and selection revision. Native
code reacquires the edit hold, flushes accepted room and behaviour documents, and
independently captures their content again. Preferences, activity assignments and
libraries already save before accepting their changes; in-flight writers prevent
the hold. Changed contents produce `stale` and require a fresh inspection. A failed
save cannot approve the live edit or lose it from the editor. The caller cannot
supply a replacement capture or bypass native comparison.

A bounded durable approval record binds the exact origin selection and content
hash to the next selection revision before the pointer commits. Approval preserves
the generation, previous-workspace identity and action-history epoch. The host
verifies that exact durable selection and its edit lease before releasing only the
review activity hold, keeping the same content owners, Undo and agent session.
Stopped programs, recipe/file playback, physics and held controller inputs do not
restart from approval. Other native holds still apply. Fresh user intent and later
normal Maestro activity remain possible after the holds are released.

Cancellation drains the worker before releasing editing. It cannot retract a
committed approval. Failure after commit and startup reconcile the exact approval
record without replaying completion. An unreadable outcome or unverified owner
keeps editing/activity held; corrupted journal files are preserved rather than
replaced silently. Terminal activation history remains history after review changes
the current revision. Applications should consult `workspace.current` for the
current requirement, and the review fact for that operation's outcome.

Native tests cover unchanged and changed captures, restart, pending cancellation,
failed accepted saves, faults before/after pointer commit, unreadable outcomes and
another owner's hold. Captured native review results are validated by the web
contract and exercised through the ordinary book Run and fact controls. These are
PC/simulated-transport checks. Headset storage, power-loss, lifecycle and user
acceptance still need device evidence. In-process workspace recovery must also
wait for any retiring host's workers before constructing fresh content owners.

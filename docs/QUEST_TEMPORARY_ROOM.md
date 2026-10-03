# Temporary rooms and shared snapshot actions

Development checkpoint, updated 2026-10-02. The book, solid 3D tool tray, original-app
agent and saved programs now share `room.session` through the same native
capability, scheduler and durable one-off receipt path. Ordinary creations remain
saved normally. Temporary play requires an explicit room-wide request.

## User workflow

- **Begin temporary room** captures the current base and immediately opens one
  shared live fork of room and remembered values. The native tray labels this **Try room**. Its baseline write
  runs off-thread, after any earlier autosave; Begin reports completion only after
  that write succeeds. New edits already stay temporary while it is starting.
  Other room actions and held items must finish first; a busy room is rejected.
- **Save snapshot** captures the current room and memory forks and saves that exact snapshot on a
  worker. The tray labels this **Keep snapshot**; its existing Save tool also uses
  this path inside temporary mode. Temporary play continues after saving.
- **Discard unsaved & end** returns to the latest kept snapshot, or the captured
  starting state if nothing was kept, and ends the mode. The tray labels this **End / discard**. Physics pauses; document placement
  is restored without replaying velocity, animation or program progress.

The book's Workshop displays the current mode, the starting-baseline write and
its readiness separately from subsequent snapshots, plus any failure. The tray status always identifies temporary or saved
mode. Returning to chat does not end temporary mode. Behaviour definitions,
imported files, activity profiles, controller preferences, room scans and chat
retain their separate stores. This operation does not undo speech, API use,
external assets, user movement or earlier physical effects.

## Storage and Undo

All room-document edits use the same live fork: manual and agent edits, creation,
drawing, poses/recordings and periodic physics placement. Autosave, focus loss,
pause and quit cannot flush that fork into saved state. The fork has its own
bounded Undo; the prior saved Undo remains separate.

After a Keep write succeeds, only its captured delta becomes one saved Undo edit.
A no-op does not add an Undo entry. Edits made after capture remain temporary;
completion does not reset transforms or repeat actions. Local Undo can revisit a
previously kept state; doing so is another temporary edit until explicitly kept.
Discard restores the saved journal and its Undo history. Motion references in
the base, base Undo and in-flight snapshot remain retained.

One writer runs at a time. Begin transfers an earlier autosave task to its worker
chain, so an older write cannot overwrite its baseline and Unity never waits for
that task during dispatch. The baseline is detached before the fork is edited;
completion does not incorporate later changes or reset the scene. The prior
journal keeps the original Undo history and asset references.

A failed baseline leaves the live fork temporary and editable, with a failed
Begin receipt. Save snapshot explicitly retries by saving the current fork;
Discard restores the captured starting state and its Undo, then resumes normal
saving for any pre-existing unsaved changes. It does not silently make trial edits
permanent or lose those pre-existing changes. A failed Keep likewise preserves
the previous save and Undo; the fork can be retried or discarded. Discard refuses
while a dispatched write is pending. Pause/quit finishes only that dispatched
snapshot, never later fork edits. Monotonic object revisions make changed or
removed/recreated objects stale after Undo/Discard; unchanged objects keep their
revisions.

## Public execution contract

`room.session@1` accepts `{operation: "begin" | "keep" | "discard", sessionId}`.
The caller must supply the exact currently observed 32-hex scope ID, including
before Begin. Programs can bind the `room.sessionId` fact. Accepted Begin and
successful Discard rotate that ID; stale calls cannot affect a different session.
The new scope is visible while Begin is pending, but dependent program steps wait
for its real result. The
catalog's zero-filled example is a placeholder, not an executable scope token.

Results contain `sessionId`, `saveId` and `savedRevision`. Observation exposes
`temporaryRoom` with `active`, `pending`, `id`, `saveId`, `phase`, `error` and
`savedRevision`. `starting` means the baseline is pending; `ready` means it saved
successfully with zero kept revisions. Later Keeps use `pending`/`saved`; failures
use `failed` with a diagnostic. Even a baseline has an exact `saveId`. Each write
retains its own completion handle: a later write
cannot replace the identity/result awaited by an earlier call. Book and agent
requests use native-issued execution IDs; duplicate receipts do not repeat the
effect. Creation results inside temporary mode identify live unsaved objects,
not a promise that those objects survive closing the app.

The generic scheduler supports a `completion` duration. Both Begin and Keep
remain preparing until their real writes complete; neither uses a fake animation
duration. Waiting
for I/O does not renew instruction or causal budgets. Failure or the 30-second
completion deadline stops dependent steps. Stop cancels waiting/continuations,
but cannot retract a dispatched write: its receipt reports that the snapshot may
still save, has failed, or was already saved. Cancelling Begin leaves temporary
mode active; it never silently discards edits made while the baseline was saving. Inspect the save status before any
explicit retry. Restart never automatically replays uncertain effects.

Begin/Discard require the other scheduler runs and queue to be empty. Native
manual ownership and animation checks also apply; loaded-avatar changes, held
objects/joints, authored movement and active recipes must finish. Public calls
avoid the editor's reentrant authoring-stop event. The same generic quiet-room
contract drives catalog readiness and execution. Keep may coexist with unrelated
work, but authored movement must finish before snapshot capture; continuing
physics after capture remains temporary.

## Verification and remaining release work

PC checks pass: **1,345 app tests, 166 EditMode tests and 125 PlayMode tests**,
with three optional private-model/collection skips. TypeScript, lint, prompt and
core boundaries, catalog export/source checks, production build and 25 Android
bridge tests pass. The ARM64 IL2CPP development APK is built and not installed.

Native tests exercise real files, writer failure/retry, XRI ownership, PhysX and
lifecycle isolation, exact session scopes, duplicate starts, saved/local Undo,
cancellation after dispatch, a create-16/keep/discard program and physical pointer
activation of all three new tools. Deferred-operation tests prove completion,
failure, timeout, cancellation and finite work despite repeated I/O completion.
New integrations delay an earlier writer while Unity frames and manual edits
continue, verify two real writes finish in order, exercise baseline failure and
explicit Keep/Discard recovery, retain prior Undo/dirty placements, and confirm
pause completes only the starting snapshot. A faulted earlier task becomes a
failed receipt instead of escaping the completion contract.
Chrome consumes those actual native observations and verifies the book's exact
requests and statuses. Its transport acknowledgements are simulated; the browser
probe does not execute Unity. Native and browser screenshots were inspected.

Baseline and Keep file writes run off-thread during normal use. Main-thread
snapshot capture/copy and grouped Undo reconciliation still need Quest profiling.
Pause/quit intentionally wait for an already-dispatched snapshot; ordinary saved
edits and durable receipt I/O retain their existing write paths. Device latency
and durability acceptance remain release work. The room retains its bounded geometry/object/animation
budgets. This is a document snapshot mechanism, not a transaction over arbitrary
physical effects. Headset layout, comfort, lifecycle/durability acceptance and a
real-provider voice-to-session journey remain unverified. No installed data reset,
service deployment, Meta setup or store submission was performed.

## Paired room/memory publication foundation — 2026-10-02

At the earlier foundation checkpoint, `RoomSnapshotTransaction` was **not yet
connected to live room or memory stores** and temporary remembered values remained
blocked. The live integration below supersedes that restriction.
It prepares coordinated Keep saves without weakening the existing guard while
only half the persistence path has been adapted.

The coordinator captures detached, exact room/memory file identities and
publishes a bounded pair under an exclusive filesystem owner. A flushed
`room-snapshot.v4.json` intent records the before/after documents and fingerprints
of the retained backups. The current intent contains v3 room definitions, including
structures; unknown or old in-flight intents remain preserved for explicit recovery.
Before its committed marker is published, recovery
restores the complete previous pair; after that marker, recovery completes the
new pair and retains the previous primaries as backups. Interrupted recovery can
repeat. It does not restore running programs, replay actions or select documents
by modification time.

Every primary and backup is checked before further changes. An outside edit,
changed intent, invalid/newer format, orphan primary backup or unfinished staging
file is preserved and blocks this path. This includes unfinished writes from the
current room/memory stores. A stale captured base cannot overwrite a later save.
The intent is at most 16 MiB; its room documents are at most 4 MiB each and memory
documents at most 1 MiB each. File contents and path ownership are checked before
publication or recovery. Expensive encoding and I/O belong on the worker path.

The 38 focused EditMode cases cover complete and first-ever saves, interrupted
publication at nine boundaries, interrupted recovery, retained backups, outside
changes before/during publication or recovery, concurrent readers, detached
snapshots, damaged/oversized/duplicate-key intents, future formats and unfinished
writes. These are fault-injected filesystem tests on Windows. They do not prove
Quest power-loss durability or storage latency.

The foundation checkpoint tracked these integration requirements (implemented
in the later live-integration section, except device profiling and recovery UX
limitations described there):

1. Route ordinary room/memory readers and writers through one serialized owner,
   recover an intent before either store loads, and handle contention as pending
   work. No old reader may observe primaries between the paired writes. Preserve
   unsupported/staged evidence through the existing recovery UI before releasing
   a storage hold.
2. Fork remembered values at Begin. Checkpoints and direct memory edits then
   update only that fork. Keep captures immutable room and memory together;
   later edits remain temporary. Discard stops behaviour work and restores the
   last successfully kept pair. In-flight writes must drain at boundaries.
3. Connect exact session/revision guards, native receipts and memory observations
   to both the book and agent. Define room Undo versus remembered-value semantics
   explicitly; no silent memory rewind or resumed interpreter is implied.
4. Include pending intent references in asset retention and hold archive/recovery
   capture until publication resolves. Test real Begin/Keep/Discard programs,
   lifecycle interruption, failure/retry and restart through native entry points,
   then profile on Quest and build/install only when device work is authorized.

The existing channel-wait APK predates this foundation. No new APK or headset
operation is part of this checkpoint.

Final local verification for this foundation: **529 EditMode and 410 PlayMode**
tests passed, with the three expected optional private-file skips. The native
verification helper exited 0. Source/mirror C# hashes match (227 runtime, 123 test
and 11 editor files). The paired-save helper remains unconnected to live stores.


## Live room and memory integration — 2026-10-02

The real `RoomStorage` and `ProgramMemoryStore` now coordinate startup reads,
ordinary saves, retention and archive inspection. Startup recovers the pair before
loading either cache. Read-only maintenance creates no files and cannot recover an
inactive room. Ordinary writers cannot overwrite an unresolved intent.

Begin creates an immutable memory base and a separate live fork. The baseline and
each Keep publish room and memory together on the worker. Checkpoints and manual
memory edits update only the fork while temporary mode is active. Captures retain
their exact referenced downloads; later edits cannot alter a dispatched snapshot.
Discard restores both last-confirmed caches. Begin/Keep/Discard refuse a pending
memory edit. Begin/Discard use the existing quiet-room scheduling contract.

An interrupted prepared save rolls back both files; a committed save completes
both. Keep acknowledges success only if the exact candidate pair is confirmed.
A failure that leaves the previous pair intact can be retried explicitly. An
outside change or unresolved recovery holds boundaries and remembered behaviours;
it does not silently choose a discard base. Recovery preservation remains
available and includes separately labelled saved and temporary room/memory data.
Unavailable documents are labelled unavailable, and original raw files remain
included in the bounded recovery artifact. Recovery export does not itself repair
or activate those files.

The book and agent see `rules.memory.temporary` and `sessionId`. `program.memory.edit`
requires that exact scope plus memory/rules revisions. A draft from before Begin
or Discard cannot apply to the new scope even if all values stayed the same.
`temporaryMemory.v1` gates this contract. The shared schema feature scanner retains
requirements on both a variant container and its chosen branch.

Room Undo continues to affect layout only. Behaviour definitions, modules, chat,
imports and preferences keep their existing stores; temporary memory does not
make them transactional. No running program, animation, velocity or external
action is replayed. Lifecycle flush finishes only an accepted paired save; it
never saves later unkept memory. Workspace retirement waits for accepted writes.

This remains desktop-verified development work. Quest power-loss behaviour, storage
latency, user comfort and lifecycle acceptance still need device testing. No new
APK or headset operation is included in this integration checkpoint.


Final local verification for the live integration: **560 EditMode and 412 PlayMode** tests passed, with the three expected optional private-file skips; the native helper exited 0. All **1,764 web tests in 207 files**, TypeScript, lint, core boundaries, native-catalog provenance and production web build passed. C# source/mirror hashes match: **229 runtime, 126 test and 11 editor files**. The updated browser fixture comes from the native remembered-values journey. No APK or headset operation was performed.

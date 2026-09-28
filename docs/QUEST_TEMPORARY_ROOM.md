# Temporary rooms and shared snapshot actions

Development checkpoint, 2026-09-28. The book, solid 3D tool tray, original-app
agent and saved programs now share `room.session` through the same native
capability, scheduler and durable one-off receipt path. Ordinary creations remain
saved normally. Temporary play requires an explicit room-wide request.

## User workflow

- **Begin temporary room** keeps the current base and opens one shared live fork.
  The native tray labels this **Try room**. Other room actions and held items must
  finish first; the operation rejects a busy room rather than interrupting it.
- **Save snapshot** captures the current fork and saves that exact snapshot on a
  worker. The tray labels this **Keep snapshot**; its existing Save tool also uses
  this path inside temporary mode. Temporary play continues after saving.
- **Discard unsaved & end** returns to the latest successful snapshot and ends the
  mode. The tray labels this **End / discard**. Physics pauses; document placement
  is restored without replaying velocity, animation or program progress.

The book's Workshop displays the current mode, pending write, saved snapshot
number and any failure. The tray status always identifies temporary or saved
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

One writer runs at a time. A failed Keep preserves the previous save and Undo;
the live fork remains editable and can be retried or discarded. Discard refuses
while a dispatched write is pending. Pause/quit finishes only that dispatched
snapshot, never later fork edits. Monotonic object revisions make changed or
removed/recreated objects stale after Undo/Discard; unchanged objects keep their
revisions.

## Public execution contract

`room.session@1` accepts `{operation: "begin" | "keep" | "discard", sessionId}`.
The caller must supply the exact currently observed 32-hex scope ID, including
before Begin. Programs can bind the `room.sessionId` fact. Successful Begin and
Discard rotate that ID; stale calls cannot affect a different session. The
catalog's zero-filled example is a placeholder, not an executable scope token.

Results contain `sessionId`, `saveId` and `savedRevision`. Observation exposes
`temporaryRoom` with `active`, `pending`, `id`, `saveId`, `phase`, `error` and
`savedRevision`. Each save retains its own completion handle: a later write
cannot replace the identity/result awaited by an earlier call. Book and agent
requests use native-issued execution IDs; duplicate receipts do not repeat the
effect. Creation results inside temporary mode identify live unsaved objects,
not a promise that those objects survive closing the app.

The generic scheduler supports a `completion` duration. A Keep remains preparing
until its real write completes; it is not treated as a short animation. Waiting
for I/O does not renew instruction or causal budgets. Failure or the 30-second
completion deadline stops dependent steps. Stop cancels waiting/continuations,
but cannot retract a dispatched write: its receipt reports that the snapshot may
still save, has failed, or was already saved. Inspect the save status before any
explicit retry. Restart never automatically replays uncertain effects.

Begin/Discard require the other scheduler runs and queue to be empty. Native
manual ownership and animation checks also apply; loaded-avatar changes, held
objects/joints, authored movement and active recipes must finish. Public calls
avoid the editor's reentrant authoring-stop event. The same generic quiet-room
contract drives catalog readiness and execution. Keep may coexist with unrelated
work, but authored movement must finish before snapshot capture; continuing
physics after capture remains temporary.

## Verification and remaining release work

PC checks pass: **1,345 app tests, 166 EditMode tests and 120 PlayMode tests**,
with three optional private-model/collection skips. TypeScript, lint, prompt and
core boundaries, catalog export/source checks, production build and 25 Android
bridge tests pass. The ARM64 IL2CPP development APK is built and not installed.

Native tests exercise real files, writer failure/retry, XRI ownership, PhysX and
lifecycle isolation, exact session scopes, duplicate starts, saved/local Undo,
cancellation after dispatch, a create-16/keep/discard program and physical pointer
activation of all three new tools. Deferred-operation tests prove completion,
failure, timeout, cancellation and finite work despite repeated I/O completion.
Chrome consumes those actual native observations and verifies the book's exact
requests and statuses. Its transport acknowledgements are simulated; the browser
probe does not execute Unity. Native and browser screenshots were inspected.

Beginning a session still writes its initial baseline synchronously. Moving that
lifecycle off the main thread and measuring save/serialization latency on Quest
remain release work. The room retains its bounded geometry/object/animation
budgets. This is a document snapshot mechanism, not a transaction over arbitrary
physical effects. Headset layout, comfort, lifecycle/durability acceptance and a
real-provider voice-to-session journey remain unverified. No installed data reset,
service deployment, Meta setup or store submission was performed.

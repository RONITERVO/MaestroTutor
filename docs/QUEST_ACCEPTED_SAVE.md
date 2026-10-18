# Saving accepted content during workspace review

Completing a restored-workspace review now waits for room and behaviour saves
without synchronously waiting for those writes on the Unity owner thread.
Book controls and agent calls continue using workspace.review.complete and its
separate workspace.review observation. A returned request ID acknowledges the
operation; only phase completed establishes that approval was applied.

## Ownership and ordering

The existing workspace edit/activity hold captures the accepted documents and
keeps them stable. Each store takes an immutable document snapshot and sequences
its background write after its previously dispatched writer. An older failed save
does not prevent attempting the latest accepted snapshot. A successful older save
cannot replace it. The two stores may write concurrently because their files and
store instances are independent.

A save-dispatch lease prevents autosave and lifecycle callbacks from starting or
waiting on another writer during this operation. Their existing dirty/error state
is consumed on the owner thread; worker code does not mutate Unity scene objects.
Approval waits for both saves and the independently captured content fingerprint.
The fingerprint must still match the exact earlier inspection. This is not a
cross-file atomic transaction: one store may save even if the other fails, and
approval remains held until the complete accepted content is verified.

Cancellation stops approval, not an already accepted write. Save failure, failure
to start/capture the fingerprint and cancellation all drain dispatched work before
releasing the edit hold. Closing the workspace also waits for these workers before
a new host can open its storage. Live accepted edits and Undo remain available on
a failed review, and failed ordinary saves retain their existing retry behavior.
No program, animation or physics run is resumed by successful review.

## Verification and limits

Native tests delay already-dispatched writers while advancing Unity frames. They
verify that completion returns before those writers finish, that the latest room
and behaviour snapshots reach disk after older failures, and that cancellation or
one failed worker cannot release another live worker. A store becoming read-only
after preflight also drains its earlier writer before reporting failure. Additional tests cover a
failure after save dispatch but before capture and reopening after owner retirement.
Existing stale-content, commit-reconciliation and damaged-selection tests remain.

The tests prove scheduling and ordering on the PC runtime. They do not establish
a Quest frame-time budget. Snapshot copying, small review/operation receipt writes,
other synchronous edit paths remain separate performance work. This change does
not make every save asynchronous or promise
durability after a forced process kill. Physical Quest acceptance remains pending.

## Lifecycle checkpoints and detached owners

Pause, focus loss, quit and destruction dispatch accepted room and behaviour
snapshots without waiting for disk on the Unity thread. Snapshot copying and
current placement capture still run on that thread. Each store has one writer
and one replaceable waiting snapshot: repeated notifications retain the newest
captured state without an unbounded queue. Writes follow the previously
dispatched writer; the completion represents the newest queued snapshot.
Later accepted edits stay dirty until their own save completes.

`room.persistence` exposes both stores through the shared capability catalog.
`saving` is pending, `unsaved` includes newer journal changes, `unconfirmed`
retains a failed save and its error, and `idle` means that store has no pending or
dirty journal snapshot. Completed tasks remain pending until the owner observes
their result. `dispatchHeld` identifies review/recovery ownership. Reading this
fact never starts a save, retry or temporary-room Keep.

Temporary forks remain temporary. Lifecycle callbacks only drain an explicitly
dispatched starting baseline or Keep snapshot; they never capture later fork
changes. An ordinary save status does not cover unaccepted drawing/animation
buffers, program memory, imported assets or the module library's separate
publication receipt. Explicit `TryFlush` and module `Flush` callers keep their
synchronous completion contract.

Workspace retirement includes ordinary checkpoints even though these carry no
editing lease. It retains the previous generation until its scene objects and
detached writers finish, including a final retry dispatched by a destruction
callback after an earlier failure. A retiring module library rejects new writes and drains
its dispatched load/write off-thread, releasing the write lease even when there
is no longer a live owner to poll it. File recovery and accepted-primary checks
still apply; save dispatch does not certify durability or bypass damaged files.

These are lifecycle best efforts, not a forced-termination guarantee. Unity's
[quit callback documentation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnApplicationQuit.html)
warns that mobile termination may omit that callback. Focus and
[pause notifications](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnApplicationPause.html)
provide earlier checkpoints, but the OS can still terminate unfinished workers.
Confirmed primary/backup files remain authoritative. Physical Quest frame time,
suspend/resume and forced-stop recovery still require device acceptance.

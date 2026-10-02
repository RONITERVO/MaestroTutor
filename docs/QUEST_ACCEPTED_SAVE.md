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
one failed worker cannot release another live worker. Additional tests cover a
failure after save dispatch but before capture and reopening after owner retirement.
Existing stale-content, commit-reconciliation and damaged-selection tests remain.

The tests prove scheduling and ordering on the PC runtime. They do not establish
a Quest frame-time budget. Snapshot copying, small review/operation receipt writes,
other synchronous edit paths and ordinary shutdown flushes remain separate
performance work. This change does not make every save asynchronous or promise
durability after a forced process kill. Physical Quest acceptance remains pending.

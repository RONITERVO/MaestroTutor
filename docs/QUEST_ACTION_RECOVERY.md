# Durable one-off action recovery

PC development checkpoint, 2026-09-27. This covers one-off catalog actions. It
does not make saved programs resume after restart or make all room commands durable.

## Identity and dispatch

Native advertises `executionReceipts.v1` alongside `execution.v1`. Its execution
observation adds `nextRunId` and `storageError`. The ID is a one-use, native-issued
token. The original app attaches it as the start request's `runId` **before** saving
the task journal and dispatching. The optional book controls use the same bridge.

Native saves a bounded receipt containing the ID, exact call and resources before
the action handler can run. It then rotates the next ID. A repeated start with an
already retained ID returns its existing outcome before checking old target
revisions; it does not execute, reserve channels or reset progress. Reusing the ID
with different arguments fails. Unknown, expired, evicted or previous-process IDs
cannot authorize a new action. An issued ID that was never dispatched also expires
when Unity restarts.

Start without an ID remains compatible with the old v1 client: native assigns and
saves an ID, but that old client cannot identify a lost acknowledgement in advance.
Current book and agent clients always attach the native ID when advertised. There
is no automatic retry or background replay, including after timeout.

## Evidence and restart

`action-receipts.v1.json` lives beside the native room save. It is separate from
room/behaviour Undo and the original chat database. Atomic replacement and a
flushed temporary file protect each update. No older backup is substituted for a
damaged receipt file: older evidence could falsely omit an executed action.
Newer-format or unreadable files are preserved and block new one-off starts.

The journal retains eight active and sixteen terminal one-off receipts. This is
separate from the scheduler's session history for saved programs; active actions
still share the scheduler's total capacity of eight. Eviction loses detailed
evidence, never makes an old ID usable again.

Completion, cancellation and failure are saved from the existing scheduler's
terminal path, including manual grabs, Stop, pause and target editing. A process
restart changes unfinished saved receipts to `interrupted`. This means **some
effects may have happened and the final outcome was not saved**. It does not mean
rollback, successful completion, or permission to try again. No run is restored.

If a disk write fails before dispatch, no action handler runs. If saving the final
result fails after an effect, current in-memory evidence still reports the actual
result and `storageError` warns that it may not survive restart. New starts stop;
existing Stop/inspect controls keep working. This is not a claim of transactional
physical effects or immunity to device/filesystem failure.

## User and agent recovery

Read-only `execution.inspect` accepts the exact ID from the task journal and returns
the same native details shown in the optional book catalog. Unknown/evicted results
stay unknown. Cancelling a retained terminal or interrupted action leaves its
outcome unchanged. The agent's existing restriction on mutations following an
unconfirmed prior task stays in place; inspection does not silently grant replay.

The default familiar chat/book view is unchanged. Storage errors and recovered
phases appear in the existing action catalog. Normal chat and unrelated room edits
remain available.

## Verification and remaining scope

Native tests cover a real animated item across a replacement browser executor,
stale revisions, changed arguments, actual completion, process-style journal
reconstruction, no playback after reconstruction, bounded eviction, write faults
before/after effects, corruption and future-format preservation. Browser tests
cover issued identity before dispatch, task-journal ordering and uncertain results.

PC verification: 1,255 app tests and 119 EditMode / 84 PlayMode Unity tests pass.
Three private-asset PlayMode checks are skipped without their external inputs.
Actual native disk-recovery observations are retained in
`test-fixtures/browser/actionReceiptStates.json` and exercised through the book bridge.

Durable saved-program state, receipts for all room edits/imports, cross-device
receipt synchronization, a user-facing damaged-storage recovery workflow, device
power-loss/storage timing and headset acceptance remain. Source models, existing
room/chat saves and old prototype installations were not reset.

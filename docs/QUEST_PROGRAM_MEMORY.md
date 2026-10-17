# Remembered values for behaviour programs

Status: implemented in the shared native runtime and book editor, behind
`rememberedVariables.v1`; temporary forks and scoped editing additionally advertise
`temporaryMemory.v1`. This is a development feature, not Quest Store or
headset acceptance. The complete Quest v1 goal remains active.

## Authoring and execution

Per-run state remains the default. Version-3 programs can opt into
`memoryVersion: 1` and give a root state declaration a stable `memory` identity:

```json
{"name":"count","initial":0,"memory":"cccccccccccccccccccccccccccccccc"}
```

The book's **State & signals** editor exposes **Per run** and **Remember between
starts**. Source and blocks edit the same validated program. Renaming preserves
its memory identity. Copying a behaviour gets a new behaviour ID and starts with
its initial values. A type change requires explicitly resetting that cell or
choosing a new identity; there is no implicit conversion.

An explicit start enters the program at its entry function, with fresh locals and
matching saved values. A missing cell uses its initial value. Loading, pending,
unavailable or mismatched memory rejects start before effects or cancellation of
an existing run. Ordinary programs remain usable without memory. Stored object,
model and motion IDs are passive values: they grant no resource authority.

`{id, op:"checkpoint"}` is the **Save remembered values** block. It saves all of
this behaviour's declared remembered variables together and waits for confirmed
publication before continuing. Assignments alone do not persist. The existing
scheduler admits queued checkpoints in FIFO order, at most one per second across
its runs, including no-op saves. A save completion does not renew the activation
instruction budget. Writes to different behaviours merge against the latest
whole-document revision while verifying the initiating run's own group identity.

Parallel branches retain private state snapshots. A checkpoint reached in a
branch fails before writing; return results, assign them in the parent and then
checkpoint. Reusable modules cannot own remembered declarations. They take and
return caller values; the caller owns persistence. There is one interpreter and
one capability catalog, not a separate agent execution system.

Stop, pause and reload do not implicitly save or resume execution. A queued save
is cancelled by Stop. An already dispatched save can finish after Stop, and the
outcome says so; later blocks stay cancelled. The next explicit start waits for
accepted writes to drain. No instruction pointer, call stack, subscriptions,
physics state, animation position or action receipts are persisted. Checkpointing
a flag and throwing a ball are not an exactly-once transaction. Never repeat an
uncertain native effect merely because a remembered flag is absent.

## Shared inspection and editing

The book's **Remembered values** panel and the agent use the same observation and
native operation:

- `rules {action:"memory", target:programId, page:0}` returns the selected group,
  exact memory revision, room `sessionId`, `temporary` scope, readiness/pending/busy status and four typed cells per
  page. It includes saved values, unsaved declarations, and retained groups whose
  behaviours were removed. Values/types are complete bounded JSON, not truncated
  display strings. Native observations are covered by browser contract tests.
- `program.memory.edit` sets one cell or resets one/all cells. It requires fresh
  memory and rules revisions plus the observed room `sessionId`, a stopped target with no queued starts, and no
  pending write. Reset-all uses an empty `variableId`. No operation starts a run.
  Set uses the existing declaration type, or the stored type for an orphan cell.
- Human drafts keep their original guards. Changes to the observed memory, room session or
  declarations disable the draft; the callback and native handler independently
  reject stale submission. The book asks for explicit reset confirmation.
- Edits, deletion and behaviour Undo never prune memory. Retained cells count
  toward capacity and remain inspectable/resettable. Memory edits have no Undo.

The standard native action receipt reports set/reset completion. Stop cannot
retract accepted IO. After a timeout or uncertain result, inspect memory and the
receipt rather than replaying the request. An unconfirmed program checkpoint
stops its run and leaves earlier effects intact.

With `temporaryMemory.v1`, Begin forks remembered values alongside the room.
Checkpoints and explicit set/reset update that in-memory fork without writing the
saved memory file. Keep captures immutable room and memory documents together;
later edits remain temporary. Discard restores the last confirmed pair and stops
behaviour work through the shared room-session action. Room Undo changes layout
only; it never rewinds memory. Pending memory writes must finish before a boundary.
Begin and Discard rotate the room session guard even if values did not change.
Older native observations stay readable in the book but cannot submit new scoped
memory edits. Existing pre-release `program.memory.edit` invocations need an
explicit current `sessionId` (or a `room.sessionId` binding); old invalid sources
remain preserved for repair. Ordinary remembered programs using checkpoints do
not need a new declaration format. Keep receipts reflect the actual paired publication and recovery.
See QUEST_TEMPORARY_ROOM.md for interrupted-save semantics.

## Storage, portability and recovery

`ProgramMemoryDocument` stores exact behaviour and declaration IDs, names,
structural types and values in `program-memory.v1.json`. There are no CLR types,
executable payloads or external paths. Numbers, booleans, text, lists and records
use the existing `ProgramDataType` / `ProgramValue` bounds; empty lists retain
their declared item type. Limits are 64 behaviour groups, 512 cells overall,
32 retained cells per group, 16 cells per checkpoint and a 1 MiB document.
Existing live-memory, nesting and instruction limits remain unchanged.

The immutable store loads and writes off-thread under the workspace write gate.
It validates exact revisions and the complete on-disk identity, flushes before
atomic publication and keeps the preceding valid copy as `.backup`. Accepted
writers release their leases without a Unity Update/Poll callback, so workspace
retirement can drain them. Competing owners, full capacity, changed types,
unsupported formats and unsafe paths fail without truncation or silent retries.
Every content change, including reset, gets a fresh revision.

A corrupt primary never automatically rolls back to a backup: that could repeat
logical effects on a later start. A missing primary with retained backup/pending
evidence is also unavailable. Damaged retained evidence is preserved. The app's
workspace recovery or a reviewed verified workspace restore is the recovery
path; a fresh workspace leaves the old generation retained. This increment does
not add automatic per-file repair or raw-file recovery export.

Live export/fingerprinting captures the accepted immutable memory at the same
owner-thread boundary as room, rules and controls, and refuses pending or
unavailable memory. Portable archives strictly validate and retain the optional
memory document. Old archives without it start empty. Retained-generation export
checks unknown formats and backup-only evidence instead of silently omitting
memory. Import/activation keeps the existing workspace review gate; nothing runs
merely because its values were restored.

Motion retention audits include current, backup and pending memory references.
Unknown/changing evidence protects downloads until resolved. Model payloads stay
covered by the existing complete model-library archive capture. Memory IDs never
make removed assets or objects usable automatically.

## Verification and remaining release work

Desktop checks cover explicit start/save/restart, per-run defaults, rename/copy,
stale and failed writes, FIFO admission, Stop before/after dispatch, type changes,
parallel rejection, budgets, passive references, shared edit receipts and portable
archive round trips. The book is tested with captured native observations,
including typed draft editing, stale guards and reset confirmation.

The full development package verification and exact-commit CI are recorded in
`QUEST_V1_PLAN.md`. Headset work remains on hold. Before release, measure storage
latency and frame timing on Quest, verify long sessions and interruption/recovery
on hardware, and review the one-second checkpoint admission rate for real usage.
This does not add background execution while the app is suspended, automatically
resume programs, or establish general exactly-once effects.


## Coordinated publication — 2026-10-02

Room and memory startup, ordinary writers and retained readers share the snapshot
coordinator. Startup recovers a pending pair before either store loads. Ordinary
writes and read-only exports never recover or overwrite an unresolved intent.
Memory loading and retained inspection still create no directories or lock files.
Competing cached memory owners fail promptly; ordinary room/memory operations
serialize while an accepted writer finishes.

Recovery evidence version 2 includes labelled saved and temporary memory caches,
with availability flags distinct from raw on-disk evidence. In-flight Keep values,
the saved base and the live fork retain their referenced models/motions. Workspace
retirement waits for accepted publication. This does not persist interpreter
state or make actions and memory checkpoints one transaction.

# Remembered values for behaviour programs

Status: storage foundation, not an enabled app feature. Existing programs still
initialize state on each start and keep it only for that run. No native capability,
book control, language extension, automatic checkpoint or restart is advertised
by this increment. Runtime, authoring and portable-workspace integration must land
together before users can opt in. The complete Quest v1 goal remains active.

## Intended execution contract

Keep the existing per-run scope as the default. An explicitly remembered variable
will have a stable declaration identity, separate from its human-readable name,
and belong to its exact saved behaviour in the selected workspace. Renaming a
variable must preserve its identity; copying a behaviour must not inherit another
behaviour's memory. Changing a remembered variable's type needs a new identity or
an explicit reset. There is no implicit conversion of old values.

An explicit start will begin at the program entry with fresh locals and load the
remembered values that match its declarations. A new run must wait for accepted memory writes to drain before loading values.
A missing value uses the declared initial value. Unreadable or incompatible memory must be reported before any
program effect, rather than silently treating it as a missing value.

Use an explicit checkpoint block to save changed remembered values together.
Ordinary assignments stay in run memory until that checkpoint. The block waits
for durable completion before later blocks can act. An unconfirmed checkpoint stops the run and requires inspection before retry;
it does not undo earlier effects.
Checkpoint IO completion must not renew the activation instruction budget; it is
not an event/timer wake. A checkpoint saves values, not the instruction pointer,
pending event, call stack,
physics, animation position, object ownership or proof of an external action.

Stop and pause do not implicitly checkpoint or restart a program. A dispatched
checkpoint may finish after Stop; later blocks must remain cancelled. Reload does
not resume execution or replay missed events. Starting again is explicit and
begins at entry using the last completed remembered values. Code that checkpoints
a counter and then throws a ball does not gain an exactly-once transaction between
those operations. Authors must not treat a remembered flag as a native action
receipt or retry an uncertain effect merely because that flag is absent.

Parallel branches retain their existing private state snapshots. A successful
join returns explicit values to the parent; the parent can then checkpoint them.
The first integration should reject a checkpoint reached in a child branch before
writing, rather than silently racing sibling writes or merging private state.
Imported modules remain reusable code, not a new ambient memory namespace; their
integration must make the caller's selected durable destinations explicit.

An explicit reset will use the same shared book/agent/native operation and fresh
memory identity. It must cancel affected runs and queued starts before committing,
leave behaviour definitions intact, and never start a program. A declaration edit,
Undo or temporarily omitted variable must not silently delete remembered values.
Orphaned values remain bounded and need a visible inspect/reset path.

## Implemented storage boundary

`ProgramMemoryDocument` is an immutable, detached codec for passive typed values.
It stores a random revision, exact behaviour IDs, exact variable IDs, display names,
structural types and values. Numbers, booleans, text, lists and records reuse
`ProgramDataType` and `ProgramValue`; empty lists keep their declared item type.
There are no CLR types, scripts, paths, continuations or executable payloads.

The current bounds are 64 retained behaviour groups, 512 total cells, 32 cells per
group, 16 cells per atomic checkpoint and a 1 MiB document. Individual values retain
the existing nesting/node/character bounds, and encoded type descriptions are also
bounded. Retained entries beyond the current declaration list count toward capacity.
The store rejects a full candidate instead of truncating it or deleting old cells.
These storage limits do not enlarge the interpreter's live-memory or run budgets.

Updates preserve other cells. Explicit reset can remove one cell or a behaviour's
whole memory group. Every content change gets a new revision, including a reset
back to an empty collection; this prevents an old request from becoming current
again after a reset. Identical writes keep their revision but still validate the
on-disk identity. Readers receive immutable values and detached JSON/byte copies.
Nested asset-ID strings can be enumerated for future retained-reference checks;
those values never grant authority to edit an object or play a removed asset.

`ProgramMemoryStore` loads and writes off-thread under a workspace write lease.
An accepted writer publishes its immutable in-memory snapshot only after flushing
and atomically replacing the file. It then releases its lease itself, without
needing a Unity Update/Poll callback. Workspace retirement waits for accepted IO;
stopping an observer cannot abandon the writer. A per-file OS lease also excludes
another store owner. No implicit retry or last-writer-wins merge is performed.

The caller supplies the observed revision. Before publication, the worker also
compares the complete canonical disk identity with its original snapshot, catching
out-of-owner edits even if they reused the revision. An out-of-date owner becomes
unavailable until reopened. A failed write does not publish candidate values.
Future callers must observe the result before reporting a completed checkpoint.

The file is `program-memory.v1.json`. Replacement retains the previous valid copy
as `.backup`. A corrupt primary never automatically rolls back to that older copy:
such a rollback could repeat a logical action on the next explicit start. A missing
primary with backup/pending evidence is also unavailable. Unknown/newer formats,
unsafe paths and damaged retained backup files are preserved. A damaged backup
blocks subsequent replacement without hiding an otherwise readable primary.
Only the current writer's unpublished temporary file is cleaned up on its failure.
Opening an empty store alone creates no files or directories.

## Integration required before release

- Add a versioned, feature-gated language declaration/checkpoint contract, with
  the same validator and stable identity editing in the book and agent views.
- Load selected remembered values before a run's first effects; connect checkpoint
  waits, cancellation, ownership, budgets and typed error observations to the one
  existing interpreter and scheduler. No second agent or execution engine.
- Provide shared inspection and explicit reset, including pending/failed results,
  stale guards, unavailable-storage recovery and capacity management.
- Include exact memory in archive capture, fingerprints, generations, temporary
  workspace handling, external import validation and reviewed restore. All owners
  must be quiescent or reject capture during an accepted write. Archives without
  this optional document start with no remembered values; newer archives must
  never silently drop it on export or downgrade.
- Feed primary/backup/pending memory references into retained asset checks, and
  preserve raw unavailable data during recovery. Runtime memory IDs do not make
  old object/model/motion references automatically usable.
- Add end-to-end save/start/checkpoint/Stop/reload/reset/backup tests through the
  actual native executor, shared book controls and captured observations. Measure
  write latency and enforce a checkpoint rate budget on Quest before release.

No existing workspace owner constructs this store yet, so this commit cannot
create a memory file that current workspace export would omit. The included
EditMode tests exercise isolated temporary directories only. Native compilation
and the ordinary EditMode/PlayMode suites remain the verification boundary for
this increment; no headset installation or Store/provider acceptance is implied.

PC verification: the final Verify-Quest.ps1 process exited 0 with **455 EditMode
and 401 PlayMode passes**, including **28 new memory tests** and only the three
existing optional private-file skips. The catalog source/equality checks passed.
All six new native source/metadata files matched the verified build mirror.
No runtime owner, portable archive or installed APK was changed by this foundation.

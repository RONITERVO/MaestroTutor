# Current action inputs

The optional book action editor can load actual native settings before editing them.
The native capability schema declares `x-current`: the fact ID/version, fact input
fields copied from the action draft, action fields copied from fact paths, and
concurrency guards. This metadata reaches both the book and agent through the same
catalog. No action-specific React map, extra tool or new catalog operation is used.

Six actions currently declare it (14 resolved variants): object physics settings,
Maestro distance/speed, walking-animation selection, controller configuration,
controller live modes, and physics Start/Pause. Walking selection reads only the
revision: it never replaces the chosen source, model, clip index or exact motion ID.
Controller configuration reads its identity, plus speed/dead zone for movement
variants. The desired stick assignment, button and program stay explicit because a
current assignment may conflict with the newly selected variant.

`Load current values` is a read-only, explicit snapshot. The page lists precisely
which draft values it will replace. A missing target, unavailable fact, different
query response, invalid value or value outside the action's bounds leaves the draft
unchanged. Copied values are checked atomically. The editor never starts an action,
saves settings, advances a guard from a later observation, or retries a rejected
operation by silently reading a new guard.

The initial example cannot be checked, run or added until a snapshot is loaded.
Users can then change preference values while preserving the revision. A changed
query target, variant, guard or native session requires another snapshot. Guards
are read-only in simple fields; advanced JSON permits explicitly supplied snapshots.
The native availability check and execution remain authoritative. Load again after
a stale-edit error, review the intended change, and run explicitly.

Inputs are disabled during a pending native request. An additional draft epoch,
mounted-state and session check rejects late reads after edits, navigation or
session replacement. Failed reads do not manufacture defaults. Action insertion
retains the exact reviewed values: it does not claim those literal guards will be
reusable. Recurring programs must use the existing typed fact expressions/bindings
to read current guards at the intended execution point. This release increment
adds snapshot editing, not automatic program dataflow generation.

Native EditMode checks and web CI validate metadata references, field types and
guard mappings. Web tests cover native snapshots, unrequested settings, unavailable
facts, variant selection, dependency changes, expert snapshots and late responses.
The four existing spatial/controller/simulation browser probes now press Load
current values and verify the resulting native identities before replaying recorded
native receipts. These browser probes do not run headset physics or provider calls.

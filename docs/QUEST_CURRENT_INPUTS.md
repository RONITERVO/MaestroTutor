# Current action inputs

The optional book action editor can load actual native settings before editing them.
The native capability schema declares `x-current`: the fact ID/version, fact input
fields copied from the action draft, action fields copied from fact paths, and
concurrency guards. This metadata reaches both the book and agent through the same
catalog. No action-specific React map, extra tool or new catalog operation is used.

Actions using this metadata include object copying, drawing edits,
retained-stroke resolution, recipe editing, object physics settings,
Maestro distance/speed, walking-animation selection, controller configuration,
controller live modes, physics Start/Pause, room setup/visibility, and surface
placement. Drawing edits load only the exact target revision; chosen point edits
and thickness stay explicit. Retained-stroke resolution loads only its native
session ID and preserves the chosen retry/discard operation. Recipe editing loads
revision, duration and loop; requested part and track patches stay explicit. Surface placement loads only the room identity; target and ray choice
remain explicit. Room setup loads its native state identity; cancelling the current setup also loads the
request identity. Neither read opens permission or scanning. Walking selection reads only the
revision: it never replaces the chosen source, model, clip index or exact motion ID.
Controller configuration reads its identity, plus speed/dead zone for movement
variants. The desired stick assignment, button and program stay explicit because a
current assignment may conflict with the newly selected variant.

`Load current values` is a read-only, explicit snapshot. The collapsed Argument
reference lists precisely which draft values it will replace. A missing target, unavailable fact, different
query response, invalid value or value outside the action's bounds leaves the draft
unchanged. Copied values are checked atomically. The editor never starts an action,
saves settings, advances a guard from a later observation, or retries a rejected
operation by silently reading a new guard.

The initial example cannot be checked, run or added until a snapshot is loaded.
Users can then change preference values while preserving the revision. A changed
query target, variant, guard or native session requires another snapshot. Guards
are read-only under Current room references in simple fields; advanced JSON permits
explicitly supplied snapshots.
The native availability check and execution remain authoritative. Load again after
a stale-edit error, review the intended change, and run explicitly.
This one-off execution path always uses the reviewed snapshot; program authoring
has a separate current-value option described in QUEST_REUSABLE_INPUTS.md.

Inputs are disabled during a pending native request. An additional draft epoch,
mounted-state and session check rejects late reads after edits, navigation or
session replacement. Failed reads do not manufacture defaults. Action insertion
now offers either literal snapshots or visible fact-read blocks
with selected live preferences and fresh guards. The agent and book use the same
typed program representation; no hidden retry or new execution path is added.

Native EditMode checks and web CI validate metadata references, field types and
guard mappings. Web tests cover native snapshots, unrequested settings, unavailable
facts, variant selection, dependency changes, expert snapshots and late responses.
The four existing spatial/controller/simulation browser probes now press Load
current values and verify the resulting native identities before replaying recorded
native receipts. These browser probes do not run headset physics or provider calls.

## Manual editor presentation

The normal action fields start expanded. Editable settings come first; read-only
concurrency guards stay in the collapsed Current room references section. The
Argument reference contains the full capability description, exact identity and
current-value mappings. Advanced action arguments, below the normal Check/Run
controls, provides the same JSON editor when explicitly opened. These disclosures
only change presentation: loading, checking, confirming and running remain separate
operations, and required snapshots cannot be bypassed by hiding their fields.

This remains the optional manual path beside the original chat. The real
native-book browser journey verifies readable controls without horizontal overflow
at 1,024 by 768 and 819 by 614 pixels, opens the references to verify the guard is
read-only, and checks that resizing or inspecting fields sends no room commands.
Desktop viewport checks do not replace Quest readability and hand-input acceptance.

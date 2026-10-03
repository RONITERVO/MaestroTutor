# Pinned program modules

Implemented composition and managed library, 2026-09-30. Native publication, bounded
paged discovery and explicit book import/replacement share the agent action/query
contracts. Device acceptance and release gates remain open.

A saved version-3 program can opt into `moduleVersion:1` and `imports:[]` when native
`programModules.v1` is advertised. An import has exactly `alias`, `hash`, `module`
and `signals`. Its module contains exactly `version:1`, `name` (1–64 characters),
`exports` (local function names) and `program` (a complete version-3 program).
`program-modules.json` and `program-modules-nested.json` are executable examples.

The entire definition is embedded and pinned by its SHA-256 content hash. There is
no runtime lookup, mutable latest-version reference or remote code loading. Editing
an external definition cannot alter a saved behaviour. Replacing a snapshot and
its pin is an explicit program edit, validated atomically under the existing rules
revision and cancellation policy. A hash identifies content; it is not a signature,
permission grant or proof of trust. Agents must preserve inspected pins and cannot
invent them; native publication returns the actual hash.

## Composition and ownership

Call an exported function with
`{id:"call1",op:"call",module:"counter",function:"add",args:[{value:2}],result:"total"}`.
Ordinary local calls keep their existing representation. An imported entry function
is validated but never automatically invoked. Only explicit calls run module code.
Exports must name the definition's own functions; a wrapper can intentionally expose
an inner module's function. Private functions cannot be called from the caller.

Every imported instance has private state initialized once per run. Two instances of
the same snapshot have different state; their calls share one `ProgramMachine`,
created-object authority, instruction budget, retained-value budget, scheduler and
native effect handlers. Stop, pause, focus loss, edit and reload use the existing
cancellation semantics. New runs start from declared initial values. Imported code
cannot access caller state directly; pass arguments and return values deliberately.

Every custom signal declared by the module requires an explicit `signals` mapping
to a caller-declared event of exactly the same scalar type. Nested mappings compose.
The root declarations own the actual room-wide event names. Signals retain their
existing queue, wait-generation and causal limits; imports do not create implicit
subscriptions or channels. Renaming caller signals in the declaration editor updates
its import mappings atomically without changing the pinned module contents.

Every caller explicitly declares all imported resource requirements, recursively.
Imports never add resources on behalf of the caller. IDs in arguments, records or
state grant no authority: effects still need declared objects or exact IDs created
by the same run. Structured values require `dataVersion:1` in each scope using them
and every importing caller; an enabled parent cannot excuse an invalid child.

Each scope is validated, then linked into the same existing program AST and validated
again as a whole. Function/state/block names in source are plain 1–32 character ASCII
letters, digits or underscores. Generated names such as `outer.inner.count` and
`outer.inner.add` identify state and execution in observations. Source cannot forge
those qualified names to reach private definitions. Locals remain function-local.
Literal records and native argument data are never interpreted as AST references.
The exact source snapshots remain in storage and inspection, separate from the linked
execution tree. There is no second interpreter.

## Limits and tradeoffs

At most four import instances across the whole tree and three nested import levels.
Existing combined limits remain: 16 functions, 16 state variables, 128 blocks,
512 expressions, eight call levels, 24,000 source characters, bounded data memory
and the same 65,536 instructions per activation. Unused imported functions count,
so a large library must be split into modest modules. Recursive calls remain invalid.
Embedded snapshots duplicate stored bytes; that buys reliable offline/self-contained
execution. It is a deliberate v1 composition choice, not permission to grow storage
or payload limits silently. Compilation is cached by the existing sequence cache;
no linking or hashing occurs on every interpreter tick.

The book lists pins, exports, required objects, signal connections and imported
function blocks. Imported blocks are read-only and can highlight the native running
node. Exported calls use the same visual argument/result editor as local functions.
Human and agent see the same source, trace and state. The library picker edits this
same source; it cannot silently substitute a newer definition.

## Managed native library

`moduleLibrary.v1` adds `program.module.publish` and `program.module.remove` to the
existing capability registry, schemas, preflight, one-off action receipts and native
scheduler. Publish accepts a saved sequence ID, exact rules revision, name and local
export names. It validates the saved definition's importability and copies it without
editing or starting the original. Version-2 source is normalized to version 3 in the
copy. The completed output returns `hash`, library `revision` and `changed`; identical
content deduplicates. There is no separate model/provider API or second executor.

Catalog search/inspect with `category:"modules"` discovers these definitions six at a
time. Search matches name, export names and exact hash. Inspection uses the returned
hash as `capability`, `version:1`, and includes the full definition or an explicit
unavailable diagnostic. `ready`, `pending` and `revision` describe library state;
querying never publishes, imports or starts anything. Cached catalog pages refresh
when loading or writes change these values. Agents inspect only relevant entries.

In the book's Reusable modules panel, publish a saved behaviour with chosen exports,
search and inspect a version, then choose a new import or explicitly replace an
existing alias. Each signal is wired to a displayed caller name with its exact type.
New signal names are scoped from behaviour/alias by default; choosing an existing
name deliberately connects that room-wide signal. Additional object declarations
require the displayed grant. The combined program is revalidated before changing the
draft; incompatible upgrades, changed pins and stale drafts leave it intact. Apply
uses the existing revision check and cancellation policy; Start remains separate.
Imported blocks remain read-only, while exported calls use the normal block editor.

Storage contains at most 256 immutable `program-modules.v1/<hash>.json` files. Each
module remains within 24,000 JSON characters and 96,000 UTF-8 bytes, and must fit the
combined program limits when imported. Loading/file validation and write/flush/rename
run on a worker. Owner-thread polling commits observations. Unknown or damaged files
are preserved and isolated instead of disabling other definitions. The existing
saved-motion audit protects references in valid modules and conservatively keeps
assets when library references are uncertain or a write is pending.

Publication writes and flushes a unique temporary file before renaming it to its exact
content ID. It never overwrites an existing damaged copy. Removal is an explicit
permanent deletion of the chosen library copy, with no library Undo. Embedded copies
in saved behaviours remain usable offline and unchanged. A dispatched write may
finish after Stop: the operation reports this and the agent inspects library/receipts
instead of replaying uncertain actions. App pause/quit flush pending work. Individual
definitions can travel between installations through the module files below; the
library itself is local. Whole-library backup and dependent asset transfer remain
unfinished. Content hashes do not authenticate authors.

## Canonical pin encoding

Hash SHA-256 over ASCII `Maestro.Module.v1\n` followed by recursive encoding of the
whole definition. Null is `N`; booleans `T`/`F`; numbers are `D` and 16 lowercase hex
digits for IEEE-754 binary64 bits, big endian, normalizing negative zero to positive
zero and rejecting nonfinite values. A string is `S`, decimal UTF-16 code-unit count,
`:`, then four lowercase hex digits per code unit. Arrays are `A<count>[<items>]`.
Objects are `O<count>{<encoded key><encoded value>...}`, keys in UTF-16 ordinal order.
Whitespace, JSON key order and integer-versus-float formatting do not change a pin.
Array order and content do. Hash input is bounded to depth 48 and 32,768 nodes; normal
program source has the tighter 24,000-character boundary. Use well-formed Unicode JSON.
Web hashing uses pinned `@noble/hashes` 2.4.0; its MIT notice ships in
`public/licenses/noble-hashes.txt`. Native uses SHA256. Independent fixture
vectors include Unicode, fractional numbers and zero normalization.

## Evidence

Shared JS/native contract cases cover hash tampering, private access, missing/wrong
signal wires, missing authority, explicit feature opt-in, nested composition and
combined function/state/block/expression/instance limits. Native tests execute two
stateful instances, nested signals/returns, instruction exhaustion and imported
collection creation/authority. A PlayMode test saves and runs the exact nested fixture
through the room executor, rejects a tampered edit without interrupting it, stops,
reloads unchanged snapshots and verifies pause cancellation. The browser replay shows
those actual native observations and changes an exported call while preserving all
pins. Browser acknowledgements are simulated; it does not execute Unity or test Quest.

Managed-library EditMode tests cover deduplication, exact reload (including ISO date
strings), detached reads, isolated corruption, storage failure, safe IDs and asset
retention during removal. The PlayMode integration publishes through the shared
executor and durable receipt, queries the actual catalog, rejects a stale revision,
publishes changed content, then proves that deletion does not affect the running or
reloaded embedded copy. Book tests reject incompatible upgrades/tampering/missing
object grants and use the same catalog/action contracts. Browser replay uses these
native observations with explicitly simulated acknowledgements.


## Portable module files

With `moduleLibraryFiles.v1`, the book library exports its exact inspected definition
as a UTF-8 `.json` envelope: `{format:"maestro-program-module",version:1,hash,definition}`.
The original platform writer is used; a completed-file receipt appears only after
close/publication. The import picker bounds files to 96,000 bytes, rejects invalid
UTF-8, duplicate keys, unknown envelope versions/fields, hash mismatches and programs
that cannot be imported. Selection only previews the file. **Import file to library**
starts the catalog action `program.module.import` with the same exact hash and
structured definition that an agent supplies. Native code recompiles the definition,
checks the pin, and uses the existing immutable library write/receipt path. A hash is
content identity, not trust or a signature. Dispatched writes may finish after Stop.

Import creates no binding, button, running behaviour or object permission. Existing
library content is deduplicated; damaged copies still require explicit removal.
Saved/running caller imports remain embedded exact copies. Reusing the new entry
in a behaviour remains a separate draft edit with explicit resource grants and
signal connections. Files contain the program and its embedded module definitions;
room objects, avatars, model assets and motion files are not bundled. Resource IDs
are shown as known names or unavailable IDs in the preview; exact motion/model
references remain visible in the complete definition. Computed dependencies cannot
be resolved statically. Nothing is silently substituted. Transferring those assets
and explicit rebinding/variant authoring remain separate portability work.

The catalog's static `programModule` field carries a structured document without
double-escaping its JSON. Ordinary arguments keep their depth/node/scalar bounds;
this field uses the module's shape, canonical identity and source-size bounds, then
full compilation before native IO. Commands stay within 32,768 characters and
arguments within 24,000. Transport/receipt JSON allows depth 64 so it can contain
the separately bounded module document. The generic book action editor supports
editing/pasting that document. Physical quick edits show it as a document to edit
in the book, and visit only actual local program blocks, never embedded module data.

PC tests cover file validation/round-trip, native receipt replay, storage/reload,
no automatic execution, file-close acknowledgement and explicit UI dispatch.
Browser native responses/export acknowledgements are simulated; headset picker,
Downloads and flash durability still require device acceptance.


## Included modules (2026-10-03)

The native library now exposes up to 16 shipped definitions alongside up to 256
private files. Both the book and agent use the same module search/inspection.
Inspection marks a shipped entry with `included: true`. Included entries require
no first-use file writes, are not removable, and can be exported or copied into
an editable draft. A private file of the same hash takes precedence, including a
damaged copy: it remains visible and removable, and removing it reveals the
included definition. Loading or inspecting a module never starts it. Explicit file import/publication
writes a durable private copy even when the same content is currently included;
a future app update cannot remove that user-saved library entry.

Imports embed the complete definition under its exact content hash. Updating the
app's examples does not upgrade existing imports or create a dependency on future
bundled files. Portable archives include private library files and saved programs
with their embedded imports; unused included defaults are supplied by the app and
do not consume the archive's 256-module-file limit. Editing a copy creates a different hash. The book offers both a
pinned import and **Replace draft with editable copy**; the latter replaces only
the draft and retains the existing explicit grant for additional object resources.
Apply and Start are separate. The agent can inspect the same definition and use
its program as an ordinary saved draft. No new agent execution route is added.

The first included example is **Structure state waits**, exporting
`waitForState(structureId, expectedRevision, disturbed, threshold, stableSeconds,
timeout)`. It is ordinary version-3 source, with no object claims or mutations.
Its standalone entry is empty; call its exported function from a behaviour.

- `disturbed=true` waits for displaced + missing members to reach `threshold`
  (1–16). `disturbed=false` waits for both counts to be zero.
- Both states require `available=true` and no held members. The wait uses the
  supplied stable period and the existing report-initial-condition policy.
- Return values are `matched`, `definitionChanged`, `timeout`, or
  `invalidThreshold`. Check the result before acting. Missing/forgotten definitions
  or invalid timing inputs fail through the existing interpreter.
- Definition revision changes wake the wait after its stable period and return
  `definitionChanged`; they are not interpreted as another physical disturbance.
  A gap resets stable time. Stop/pause/reload cancel without automatic resumption.
- `matched` means a sampled condition qualified. It does not attribute movement
  to a ball, prove support/stability, or promise that the condition still holds
  when a later action begins. Read current facts and use normal action guards.

The shared `program-structure-watch.json` example waits for restoration, arms for
one disturbance, emits `user.structureDisturbed` with its count, and then rearms
only after restoration. A changed definition ends the example and emits
`user.structureWatchStopped` with the returned reason. Missing pieces count as a
disturbance; reconstruction does not silently respawn them. Keep the projectile
outside the observed structure, even when a separate reset group includes it.
Users can connect their own reactions and reset policies with ordinary blocks.

Verification for the included example covers pinned identity in both validators,
read-only first use, durable explicit copies, damaged private duplicates, portable
workspace restoration without installed defaults, held/unavailable conditions,
timeouts, revision changes and cancellation. PlayMode throws a rigid ball at a
six-piece structure, observes one disturbance, resets and rearms, then stops on a
changed definition. Chrome copies the captured native module into editable blocks
and displays the captured live states; catalog acknowledgements in that replay
are simulated. The full-app native transport also runs the exact pinned watcher
through save/start/move/reset/rearm/stop. No provider call or headset acceptance
is implied by these checks.

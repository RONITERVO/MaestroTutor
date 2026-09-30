# Pinned program modules

Implemented composition foundation, 2026-09-30. Managed library storage, publication,
paged discovery, a visual import/upgrade workflow and device acceptance remain open.
This is not a complete shared-library product or a release approval.

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
invent them; native library creation/discovery is a later capability.

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
Human and agent see the same source, trace and state. The library picker and explicit
visual version-upgrade workflow still need implementation.

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

# Programmable behaviours: foundation and roadmap

Updated 2026-09-27. Every behaviour now uses one canonical program representation. This is a development milestone, not Quest release approval.

## Implemented foundation

`BehaviourProgram` and `ProgramMachine` validate and interpret version-2 JSON
programs inside Unity. Functions have typed parameters, local variables and return
values. Supported statements are named capability invocation, assignment, if/else, switch/case,
repeat, call and return. Expressions support arithmetic, comparisons, short-circuit
logic and three explicit facts: Maestro state, physics readiness and physics running.
No provider call occurs while a saved program runs.

The revision-checked `rules` command requires one `program` string. There is no
separate saved `steps` array or linear scheduler path. The same native scheduler handles ownership,
loading, completion, state/object triggers, physical buttons, Stop, grabbing,
app pause and interruption policy. Run-now uses `rules.play`; saving does not run
a program and a physical button is optional. Programs reserve their declared
resources conservatively across branches. Edits and Undo cancel active old runs.
Native actions cover the existing nine rule kinds, not every room-agent control.
Functions currently live within one program; cross-program libraries are future work.

The optional book workspace renders nested blocks and functions, highlights the
current native node, and shows native locals and recent outcomes. Users can add,
reorder and remove blocks/functions, edit a block/function's JSON, edit full source,
or switch between simple action controls and the function/source editor without
converting data. Simple controls derive literal action blocks from a single-function
program and write back to that same tree, preserving node IDs, prop/motion fields,
function names and extra resource declarations. Expressions, branches, locals and
multiple functions require the function editor; simple controls cannot flatten them.
Complete-program validation runs before accepting an editor draft and again before
native execution. Unfinished edits are retained when a physical edit makes their
revision stale. JSON is the current advanced text representation; this is not a
JavaScript/C# editor, Blockly integration or a finished drag-and-drop editor.

The planner uses the same save/play/inspect/bind/button contract, gated by native
`behaviourPrograms.v3`. Original Maestro still owns Gemini, subscriptions/BYOK,
conversation and handoff verification. The chat stays the default book interface.
Program authoring is an optional workspace, and no new floating flat controls are
introduced outside the pages.

Behaviour storage now uses `behaviours.v2.json`. This deliberately resets the
owner's pre-release behaviour collection instead of migrating `rules.v1`–`rules.v5` or numeric-program `behaviours.v1`.
Old files remain untouched but are no longer loaded; saved triggers and mounted
buttons belonging to them are reset with the behaviours. Loading an old development
installation shows a reset message. Room creations, downloaded models/motions,
source collections and the original web chat/backup formats are unaffected.

Atomic pending writes, last-good backup recovery, Undo and motion-reference
protection remain. A newer outer document, embedded program/capability or collection filename
(including recovery copies) makes the collection read-only instead of rolling back
to an older backup. Current save files and incoming native save commands reject
legacy/mixed sequence fields before deserialization can silently discard them.
New opcodes still require a program-version change.

Invocations store `{id, op:"invoke", capability:"avatar.gesture.play", version:1,
arguments:{target:"maestro", gesture:"greeting", seconds:1}, bindings:{}}`.
The program root version is 2; each capability has its own version. Names and
typed fields replace serialized RuleStep enums. Bindings accept only scalar
arguments actually declared by that capability; gesture values are names.
Native schemas generate web structural checks and agent signatures. Existing native
domain/readiness checks remain authoritative. Unknown arguments, mismatched types,
unsupported versions and undeclared computed targets fail before dispatch.
The internal RuleStep adapter is used only by current controls and native handlers.
Catalog search, live availability queries and transient direct calls remain pending.

Motion-library assignment and physical action/prop controls remain available for
literal action-only programs. Complex programs keep their source and receive an
explanation when a simple assignment is attempted. Library usage includes program
references with the same conservative reference analysis as storage and Undo.

Limits are 16 functions, 8 parameters/16 locals per function, 128 statement nodes,
512 expression nodes, nesting/call depth 8, 16 reserved targets, 24,000 source
characters per program and 128,000 across a saved collection. Text values are
bounded to 128 characters; numeric values to magnitude 1,000,000. Calls cannot
recurse. Repeat counts are 0–10,000. The interpreter yields between statements
around a 32-instruction tick budget, charging expression evaluation too; an atomic
expression may finish past that boundary. A run fails after 65,536 instructions.
Eight disjoint runs and eight queued requests are the existing scheduler maximum.
Explicit whole-program repeat resets the per-run budget and local variables each
cycle until stopped; persistent event-driven state is the next runtime stage.

Native actions yield while loading/playing. Computed native arguments and resource
ownership are revalidated before dispatch. Errors stop the run and release its
owned operations. Running node/function/locals and up to 16 session-local terminal
outcomes are exposed to both human and agent. These outcomes are not durable run
receipts, and a successful start receipt does not establish eventual completion.

Room observations allow up to 192 KiB across Unity, Java and web so selected
program source plus concurrent traces fit. Library messages retain their 32 KiB
limit; command batches retain 28,000 characters (32 KiB native envelope). JSON
escaping counts toward the command limit. Transport refuses oversized input;
it does not silently truncate source or traces.

Shared JSON conformance cases exercise the TypeScript authoring validator and
native parser. The prime-number example exercises functions, returns, arithmetic,
branching and loops. Unity PlayMode verifies an agent-saved program moving a real
recipe robot, tutor-state triggering, a left-controller button pressed by the other
controller, cancellation, completion, pause, Undo and reload. Browser tests replay
actual Unity observations; browser edit acknowledgements are explicitly simulated.
Hardware frame timing and this editor's Quest readability remain unverified.

## Typed-invocation checkpoint (2026-09-27)

The current development APK is `MaestroQuest-capabilities-257F8326.apk`.
SHA256: `257F8326DC027D078FF920088C350E68931603486BAC3950449B8E4F5102300E`.
Its v2 signature verifies, all 91 runtime source files match the build mirror,
and the bundled web file matches the production build byte-for-byte.
It has not been installed; no headset reset or acceptance has occurred.

- 1,226 app tests passed, plus lint, TypeScript and production build.
- 95 Unity EditMode and 76 PlayMode tests passed; the same three optional
  private-file cases were skipped. Native tests exercise named calls through
  real object animation, state triggers, controller buttons, Stop, Undo and reload.
- 25 Android browser, 25 Functions and 31 gateway tests/builds passed.
- Shared native/web parser cases cover named arguments, capability versions,
  computed bindings, domain validation, resource declarations and malformed input.
  Unknown newer capability IDs/versions preserve saves instead of using old backups.
- Chrome replays native observations and checks simple/source editing preserves
  IDs, props and other functions. Browser edit receipts are simulated.
- Catalog/source drift, prompt ownership, core boundaries and release config passed.

Evidence is in ignored `.quest-evidence/capability-invocation/`. Paged capability
search, live availability queries and direct transient invocation remain pending.
Current program calls still adapt to the nine existing domain handlers; adding
new operation families requires native handlers and extending the simple-control
adapter or using a future generic editor. This is not complete catalog coverage.

## Earlier canonical-storage checkpoint (2026-09-27, numeric program format)

Development APK SHA256: `F9F5222307E7CAD36A3724604DDB8EF32FAB2C56A8FBA1F3213878A4BCBAA18C`.
It has not been installed on Quest. Its v2 signature verifies, the bundled web
asset matches the production build byte-for-byte, and all 90 runtime source files
match the Unity build mirror.

- App: 1,204 passing tests, full app/shared lint, TypeScript and production build.
- Unity: 87 EditMode and 76 PlayMode passing tests; three optional private-file
  tests skipped because no external files were supplied.
- Android browser: 25 tests, release AAR and lint passed.
- Functions: 25 tests/build; Live gateway: 31 tests/build passed.
- Catalog drift, prompt ownership, core boundaries and release configuration passed.
- Chrome replays actual native observations and round-trips simple editing to
  source while preserving block IDs and prop settings. A complex edit preserves
  the other function. Browser acknowledgements are simulated; actual native
  execution, buttons and library assignment are checked in Unity PlayMode.
- Native regressions cover detached simple views, resource replacement, complex
  program protection, canonical wire validation, development reset, prop and block
  persistence, backup recovery and newer-version preservation.

Ignored local evidence is in `.quest-evidence/program-storage/`. The first packaging
attempt stalled during Unity shutdown and was rejected; the complete retry exited
successfully. Headset, real-provider and store release acceptance remain separate.

## Still to implement

Readable text syntax with parser/printer round trips, friendlier structured field
editors and drag/drop, typed asset pickers, cross-program function libraries,
timer/custom-event entry points, event-await with timeouts, controlled parallel
branches, durable native run receipts, and a unified extensible capability catalogue.
No claim of support for these follows from the current JSON editor. The remaining
sections describe that long-term direction, not additional shipped behavior.

## One program, multiple editors

Use a versioned, typed behaviour program as canonical domain data. A syntax tree
stored as JSON can represent real programming concepts; JSON is the storage format,
not a limit to flat recipes. The agent, visual blocks and optional readable text
edit this same program through revision-checked operations. Keep stable node IDs,
comments and editor layout metadata so human edits survive an agent patch.

The first language should provide typed literals and expressions, variables,
parameters, functions, if/else, switch/cases, bounded loops, sequence, waiting for
an event/action result with timeout, and controlled parallel branches. Later state
machines can build on the same event and state model. Expressions read an explicit
snapshot of supported facts; arbitrary reflection and Unity object access are not
part of the language. A function can call other approved functions/actions with a
bounded call stack; unrestricted recursion is unnecessary for the initial release.

A compact readable syntax and blocks need a defined common subset and a parser/
printer with round-trip tests. Do not claim automatic lossless conversion of
arbitrary JavaScript or C# into blocks. Unknown newer nodes should be preserved
read-only rather than dropped. Blockly is a plausible web editor in the existing
book workspace, not the authoritative runtime or a replacement chat interface.
Its own workspace JSON is presentation state alongside our program document.

## Functions instead of a tool for every combination

Expose a small agent authoring surface: discover capabilities, inspect a program,
validate/preview a revision, apply a patch, run, stop and observe. These are proposed
interfaces, not new callable tools yet. The program calls a catalogued domain API,
for example animation playback, object properties, motion intentions and existing
recipe construction. User functions can combine those primitives into thousands
of named reusable behaviours without a native tool per combination.

Each native capability still needs one actual implementation, typed arguments,
availability, completion/cancellation semantics, permissions and tests. Code cannot
invent room scanning, networking or an unsupported avatar rig. Generate block
metadata, agent-facing signatures and contract checks from the same catalogue;
manual tools, agent commands and program execution must reach the same services.
Do not expose the whole Unity API or secrets. Retrieve relevant function signatures
and reusable behaviours on demand instead of putting every library entry in every
Gemini prompt. Maestro's original app retains all Gemini/account ownership.

## Entry points are independent of buttons

A function has no required physical button. Bind the same program entry to:

- Run now from the agent or user, returning a run handle immediately.
- An explicit tutor-state transition, such as entering Speaking.
- A controller/room button or a supported object event.
- A timer/delay while the room runtime is active.
- A custom event emitted by another program.

Saving, running once and enabling a persistent trigger are distinct operations.
A request to perform an action now must not silently create an always-on trigger.
Future wall-clock scheduling while the headset is closed needs separate backend/
platform scheduling and cannot be promised by an in-room timer. Define transition
versus level conditions, cooldown, re-entry, cancellation and stop-on-exit explicitly.
No catch-up storm of stale events after resuming or loading a saved room.

Illustrative future syntax (not currently executable):

```text
function greet(visitor) {
  if (visitor.distance < 2 metres) {
    await maestro.play(wave, timeout: 10 seconds)
  } else {
    await maestro.lookAt(visitor, duration: 2 seconds)
  }
}
on maestro.enterState(speaking) -> greet(user)
```

The agent can also invoke `greet(user)` once, or attach it to a controller button.
The branches and calls would appear as editable visual blocks. Functions can take
model/animation/object references as parameters so a new clip usually adds data,
not a new native action type.

## Execution and human/agent collaboration

Use an interpreter shipped inside the Unity app. Native AOT code is compiled at
build time; the interpreter evaluates validated program data at runtime. Start
with an AST interpreter and introduce bytecode only if measured performance calls
for it. Every wait yields; no user loop can monopolize the render/physics thread.
Budget instructions per tick, event queue size, concurrent runs, memory, objects,
call depth and total work. Unlimited stored possibilities are different from
unlimited simultaneous work on Quest.

Use indexed event subscriptions and load/cache relevant programs, rather than
polling thousands of conditions each frame. Keep high-rate animation, navigation,
controller tracking and rigid-body simulation in their existing native systems.
The program starts an operation and awaits its actual result. Define completion
separately from starting; a blocked walk follows a failure branch or timeout.

Run handles expose program revision, current node, locals, waiting reason,
operation IDs and outcome. The UI highlights the running block; the agent sees
that same trace. Stop cancels owned operations; manual grabs take precedence.
Use target ownership and the existing interruption policies for conflicting runs.
An edit produces a new immutable program revision; an active run either finishes
its old revision or is explicitly stopped/restarted. Never change its meaning
halfway through a throw or silently replay it after recovery. Undo changes the
saved definition; it cannot undo every physical consequence.

Tests should run the same interpreter with simulated events, clock and native
action adapters, then verify real Unity input/runtime and Quest hardware. Shared
text/block/program fixtures establish semantic parity. Keep durable run receipts
and uncertain-outcome handling separate from ordinary scene save/Undo; these are
still open native-foundation work.

## Development transition and release compatibility

Legacy v4 rules have sequential steps, repeats, waits, seven fixed event kinds,
limited tutor-state conditions, physical buttons, recorded/imported/library and
recipe animation, spatial actions and interruption handling. Those legacy sequences do not have
general branching, variables or user functions; the v5 program alternative adds
them. Readable text syntax and timer bindings remain unimplemented.
The room planner's three batches/eight commands limit bounds model-driven edits;
it is not a suitable language limit for future saved programs.

The owner approved resetting prototype behaviour saves instead of maintaining
v4/v5 and numeric-program migrations. The new canonical collection preserves
last-good recovery and refuses unknown newer formats. Original web chat/backups,
room creations and source model/motion collections are outside that reset.

Continue with capability discovery and availability, then event/state scheduling,
layered ownership and broader action coverage. After release, versioned capability
contracts and persistent programs become supported compatibility commitments;
future upgrades need explicit migration or preserved read-only handling.

## Primary references checked

- Blockly custom generators, including JSON, and the separation of code generation
  from execution: https://docs.blockly.com/guides/create-custom-blocks/code-generation/overview/
- Blockly workspace persistence: https://docs.blockly.com/guides/get-started/save-and-load/
- Unity 6.3 IL2CPP/AOT restrictions (including runtime code emission):
  https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-restrictions.html

The architecture above is our recommendation; those sources describe the
underlying editor/runtime facilities, not an already-integrated Maestro language.


## Approved catalog and event-runtime direction (2026-09-27)

The owner authorizes simplifying development-only formats and resetting their
prototype saves if needed. The accepted design, review qualifications, tradeoffs
and staged acceptance are in [QUEST_CAPABILITY_ARCHITECTURE.md](QUEST_CAPABILITY_ARCHITECTURE.md).
The first native vocabulary registry now generates web action/event labels, fact
types, per-capability argument schemas and prompt signatures, with drift checks.
Typed invocation now serves every saved program. Paged capability discovery,
live availability queries, full room-command coverage, timers/state machines and
per-channel ownership remain follow-up work. No development data was reset here.

# Programmable behaviours: foundation and roadmap

Updated 2026-09-26. The first executable program subset is implemented alongside
existing linear rules. This is a development milestone, not Quest release approval.

## Implemented foundation

`BehaviourProgram` and `ProgramMachine` validate and interpret version-1 JSON
programs inside Unity. Functions have typed parameters, local variables and return
values. Supported statements are native action, assignment, if/else, switch/case,
repeat, call and return. Expressions support arithmetic, comparisons, short-circuit
logic and three explicit facts: Maestro state, physics readiness and physics running.
No provider call occurs while a saved program runs.

The existing revision-checked `rules` command saves either linear `steps` or one
`program` string with empty steps. The same native scheduler handles ownership,
loading, completion, state/object triggers, physical buttons, Stop, grabbing,
app pause and interruption policy. Run-now uses `rules.play`; saving does not run
a program and a physical button is optional. Programs reserve their declared
resources conservatively across branches. Edits and Undo cancel active old runs.
Native actions cover the existing nine rule kinds, not every room-agent control.
Functions currently live within one program; cross-program libraries are future work.

The optional book workspace renders nested blocks and functions, highlights the
current native node, and shows native locals and recent outcomes. Users can add,
reorder and remove blocks/functions, edit a block/function's JSON, edit full source,
and convert a linear sequence without losing step IDs or prop/motion fields.
Complete-program validation runs before accepting an editor draft and again before
native execution. Unfinished edits are retained when a physical edit makes their
revision stale. JSON is the current advanced text representation; this is not a
JavaScript/C# editor, Blockly integration or a finished drag-and-drop editor.

The planner uses the same save/play/inspect/bind/button contract, gated by native
`behaviourPrograms.v1`. Original Maestro still owns Gemini, subscriptions/BYOK,
conversation and handoff verification. The chat stays the default book interface.
Program authoring is an optional workspace, and no new floating flat controls are
introduced outside the pages.

Rules storage is now v5. Loading v1–v4 preserves the old files and stable IDs;
existing rules remain linear until explicitly converted. Backups and Undo retain
program source and referenced library motions. A newer outer document version or
embedded program version makes the collection read-only, preserving the original
instead of falling back to an older backup. New opcodes must bump program version.

Limits are 16 functions, 8 parameters/16 locals per function, 128 statement nodes,
512 expression nodes, nesting/call depth 8, 16 reserved targets, 24,000 source
characters per program and 128,000 across a saved collection. Text values are
bounded to 128 characters; numeric values to magnitude 1,000,000. Calls cannot
recurse. Repeat counts are 0–10,000. The interpreter yields between statements
around a 32-instruction tick budget, charging expression evaluation too; an atomic
expression may finish past that boundary. A run fails after 65,536 instructions.
Eight disjoint runs and eight queued requests are the existing scheduler maximum.
Explicit whole-sequence repeat resets the per-run budget each cycle until stopped.

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

## Verified development checkpoint (2026-09-27)

APK SHA256: `AAB1F7AECA92CB67347FDC25C8F587F3B289946FC1BAA23B3EF6958A78C766DB`.
This is development signing, built by `Build-QuestDevelopment.ps1`, and has not
been installed or tested on the headset. The packaged web bundle was compared
byte-for-byte with the successful production web build.

- Full app suite: 1,118 passing tests; full app lint and TypeScript pass.
- Unity: 79 EditMode and 72 PlayMode passing tests; three explicitly optional
  private-model/collection tests skipped because no external files were supplied.
- Native Android browser: 25 passing tests, release AAR build and lint pass.
- Functions: 25 passing unit tests/build. Live gateway: 31 passing tests/build.
- Shared prompt ownership, core boundaries and release-config checks pass.
- Browser replay: actual native current-node/locals displayed; a block edit
  validates and preserves other functions before one simulated save receipt.

Local evidence is under ignored `.quest-evidence/programs/`. The full emulator,
real-provider, headset and Meta Store release checks are separate gates; no pass
is implied for those by this checkpoint.

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

## Migration from the current application

Legacy v4 rules have sequential steps, repeats, waits, seven fixed event kinds,
limited tutor-state conditions, physical buttons, recorded/imported/library and
recipe animation, spatial actions and interruption handling. Those legacy sequences do not have
general branching, variables or user functions; the v5 program alternative adds
them. Readable text syntax and timer bindings remain unimplemented.
The room planner's three batches/eight commands limit bounds model-driven edits;
it is not a suitable language limit for future saved programs.

1. Finish the shared capability/observation/receipt foundation around current
   actions. Preserve current rule/room documents and validated manual paths.
2. Add a new program document and interpreter with variables, expressions,
   branches, function calls and waits. Migrate v4 sequences to equivalent sequence
   nodes and bindings, preserving IDs, interruption and stop-on-exit semantics.
3. Add entry-point bindings, run-now and typed parameter/reference selection.
   Integrate original chat handoff and user-authored animation/recipe references.
4. Add visual blocks with execution highlighting and a tested text representation;
   the agent uses the same revision-checked edit API from the start. Free-form text
   editing is enabled only once its parser and round-trip support are complete.
5. Expand primitives and reusable behaviour libraries through the catalogue, with
   compatibility migrations, resource limits, deterministic traces and Quest
   performance/acceptance evidence. Do not require users to become programmers.

## Primary references checked

- Blockly custom generators, including JSON, and the separation of code generation
  from execution: https://docs.blockly.com/guides/create-custom-blocks/code-generation/overview/
- Blockly workspace persistence: https://docs.blockly.com/guides/get-started/save-and-load/
- Unity 6.3 IL2CPP/AOT restrictions (including runtime code emission):
  https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-restrictions.html

The architecture above is our recommendation; those sources describe the
underlying editor/runtime facilities, not an already-integrated Maestro language.

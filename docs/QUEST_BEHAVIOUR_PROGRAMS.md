# Proposed long-term programmable behaviours

Design recommendation, 2026-09-26, responding to the user's code/visual-block
example. This document does not claim that the language, text editor, branching
runtime or timers below are implemented. Preserve the working v4 rule system
while introducing this incrementally.

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

Current v4 rules have sequential steps, repeats, waits, seven fixed event kinds,
limited tutor-state conditions, physical buttons, recorded/imported/library and
recipe animation, spatial actions and interruption handling. They do not have
general branching, variables, user functions, a text language or timer bindings.
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

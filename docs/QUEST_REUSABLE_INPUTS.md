# Reusable current-value actions

The book action catalog can insert either an exact snapshot or a read followed by
an action. It uses the native schema's `x-current` mappings and the existing typed
program language. No extra native action, private executor or arbitrary code is
introduced. The shared room-agent catalog guide describes the same single-read,
live/fixed binding pattern and forbids implicit retries or fallback values. The normal one-off Run action remains an exact reviewed snapshot.

For a behaviour, current-value mode creates two ordinary editable blocks at the
start of the entry function: read the declared fact once into a typed local, then
invoke the action with selected field bindings from that local. Guards always
come from the same read. Unedited preferences default to live; editing a preference
pins that field. The visible checkboxes can override either choice. Exact desired
operations, targets, motion/model identities, buttons and programs remain literal.
A failed/unavailable read stops before invocation; a stale or blocked invocation
fails normally without an implicit retry or second read. A scheduler slice between
the read and action cannot bypass native stale checks.

The generated source is shared with the agent and validated by both program
validators. Function bodies, state, imports and existing blocks remain intact.
Names are allocated without collisions. Resources are declared normally. The
usual size, local-variable, memory and feature limits remain enforced; failure
leaves the original draft intact. Defining, adding and saving never starts a run.
Place both blocks inside the intended loop when every iteration needs a fresh
read. Editing the resulting program remains explicit code/dataflow authoring.

Literal mode remains available, including on older runtimes without structured
values. The function selector makes the insertion destination explicit: new blocks
are inserted at that function's start, and run whenever it is called. Choosing the
entry function does not put actions into an existing nested loop automatically.

The book can explicitly convert a version-2 sequence Repeat flag, or make a
nonrepeating version-2 behaviour repeat, using ordinary version-3 blocks. The user
chooses a 0.1–3600-second delay. A new entry calls the original entry and then sleeps
inside a Forever block. Original function bodies, returns, local initial values,
resources and stable IDs remain. Each call resets cycle locals; an early return
ends the cycle, allowing the next iteration. Native validation rejects function,
call-depth, node or source limits without changing the draft. Conversion and save
never start playback. The catalog destination becomes the original cycle function
and remains visible/editable, so both current-value reads and actions can repeat.

This is an explicit semantics change: a delay is added and version-3 action
ownership is released between actions and while waiting. Other actors can use the
same channels and later conflicts fail normally. Existing run-wide creation and
resource limits still apply; conversion does not reset them every cycle. New state
variables can retain values within the run. Stop and lifecycle suspension cancel
the loop; focus return does not restart it. No source or module edit may silently
clear the older Repeat flag. Rejected source buffers and module previews remain
open; users can cancel that editor and convert, or explicitly disable Repeat first.

`insertProgramCapability` in the shared core produces the source. Its generated
`current-input-program.json` fixture is compared exactly in web tests and executed
by native tests. Native coverage proves one fact read, failure without fallback,
and two runs after intervening preference/revision changes, preserving distance
while applying a fixed speed. The Chrome probe checks visible live/fixed choices,
the saved source and absence of execution commands. It is browser authoring replay,
not headset or provider acceptance. The independent native test executes the same
generated block pattern through the actual scheduler and persistence path.

The generated `program-repeat-conversion.json` fixture is checked against the core
converter and run by the native scheduler. It covers fresh cycle locals, early
returns, actual timer waits, released ownership, Stop and no automatic lifecycle
restart. Browser coverage checks explicit conversion, function selection,
read/action insertion into the cycle, and save without execution commands. Browser
receipts remain fixture replay, not headset acceptance.

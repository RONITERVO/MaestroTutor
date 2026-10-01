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
values. An older sequence-level Repeat flag is deliberately not silently converted
when adding structured reads: turn it off and author an explicit Repeat until
stopped block with a wait. The catalog retains the draft and explains this if the
old flag is present. An assisted conversion of that older repeat form remains
editor work; ordinary newly created behaviours do not enable that flag.

`insertProgramCapability` in the shared core produces the source. Its generated
`current-input-program.json` fixture is compared exactly in web tests and executed
by native tests. Native coverage proves one fact read, failure without fallback,
and two runs after intervening preference/revision changes, preserving distance
while applying a fixed speed. The Chrome probe checks visible live/fixed choices,
the saved source and absence of execution commands. It is browser authoring replay,
not headset or provider acceptance. The independent native test executes the same
generated block pattern through the actual scheduler and persistence path.

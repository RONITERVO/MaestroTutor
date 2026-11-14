# Parallel behaviour functions

The book, app agent and native interpreter share an explicit run-together block.
It starts two to four function calls, waits for all, then continues the caller.
This supports, for example, moving two objects together or a compatible Maestro
upper-body gesture alongside gaze/walking. Catalog ownership still determines
which combinations can coexist; parallel does not bypass it.

## Versioned contract

A version-3 program opts in with `parallelVersion:1`. Native advertises
`parallelPrograms.v1`; web authoring refuses unsupported clients. This is an
explicit versioned language extension, like `dataVersion`, rather than a silent
addition to existing saved programs. Older native parsers reject the new root
field and preserve unsupported source using existing per-program isolation.
Saving never starts it. No storage reset, second interpreter or provider tool.

```json
{"id":"together","op":"parallel","branches":[
  {"function":"moveBox","args":[{"value":1}],"result":"boxResult"},
  {"module":"gestures","function":"wave","args":[],"result":"waveResult"}
]}
```

All arguments are evaluated before any child can act. The normal parameter,
return-type, recursion and call-depth validation applies to every branch.
`module` names an existing pinned export. Imported parallel modules require the
extension in their caller; library files retain their exact content identities.

Each child has fresh function locals and a snapshot of the parent's current
state. Calls within that child share its private state. No child writes its
parent's or sibling's variables. Return destinations are optional, must have the
right type, and must be distinct. After all children succeed, destinations are
assigned in declaration order. Other parent values remain unchanged. Structured
returns use the existing dataVersion/type/value limits.

## Scheduler and failure semantics

The parent and live descendants share the existing eight scheduler slots. The
parent retains a slot while joining and owns no objects. All siblings are admitted
before their first effects; insufficient capacity fails the group without queuing
or starting a partial sibling set. Nested groups are allowed within the same
capacity and call-depth bounds. This is cooperative scheduling on Unity's owner
thread, not multithreaded scene mutation or guaranteed simultaneous frames.

Each child uses the ordinary invocation, preparation, completion, event wait,
condition watcher and timer paths. Object/channel claims remain exclusive under
the same native ownership service. If two children need a conflicting claim, the
second fails and cancels the group. Room-wide actions requiring quiet cannot run
inside a group. Use sequential operations for dependencies on the same resource.

Failure or interruption of any child cancels the root group and all descendants,
including pending loads and subscriptions. Stop, manual grip, edit, Recall and
pause use this policy. Unrelated behaviours remain independent. Completed physical
and saved effects remain; cancellation is not a rollback. A parent does not receive
partial return values. Resume/reload never automatically restarts the group.

Events retain their original arrival/receiver generation and causal limits. An
emission cannot reach a sibling that has not subscribed yet. Branch declaration
order makes per-tick scheduling stable but is not a synchronization guarantee.
Stop drops queued deliveries to those retired instances.

## Shared bounds and observations

Creation capacity is sixteen per root run across all branches, including native
results still reserved. A child inherits only already-authorized IDs at fork;
new IDs from another child are not ambient edit permission. A successful join
passes creation authority back to the caller. Explicit returned IDs let following
blocks address those creations.
Normal declared-resource checks still apply.

The entire group's retained values share the 1,024-node/8,192-character budget.
Source, function, expression, stack and statement limits remain. Each branch
inherits its caller's current activation instruction count. Pure fork/join charges
child work back to the caller and cannot refresh the 65,536-instruction budget.
Actual event/timer/timed-action completion can renew an activation. Computing
siblings still have their own ceiling even while another branch receives events.
Event-chain depth passes through forks and joins without a reset.

Native running observations include `parentRunId`. The book shows the parent wait
and each child's function, current block, state and locals. The agent reads the
same observations. Only the root emits a terminal program outcome; a completed child cannot be
mistaken for the requested behaviour finishing. A failing child supplies the
fault node and function in that outcome. These remain bounded session records;
they are not new durable program checkpoints or a promise of background execution.

## Editing and verification

The existing Functions & code editor offers **Run together**, branch function and
module selection, typed arguments, typed return destinations and branch add/remove.
It preserves rejected drafts. Function/parameter/local/state renames traverse
branch calls using the same canonical representation. No new external flat UI.

The shared fixture is `Tests/Fixtures/program-parallel.json`. Native tests cover
concurrent playback admission, joined results, private state, failure/Stop/pause,
preparation cancellation, ownership conflicts, nested capacity, instruction and
retained-memory limits, event delivery, creation quotas and module imports. A
PlayMode scenario saves through the native room executor, moves two actual objects,
observes both branches, joins results and cancels/restarts explicitly. Web tests
cover the same definition, capability gates, modules, renames and visual controls.

Device timing, headset readability and real-provider authoring remain acceptance
work. This does not provide race/first-winner semantics, background scheduling,
durable state/resume or arbitrary parallel native code.

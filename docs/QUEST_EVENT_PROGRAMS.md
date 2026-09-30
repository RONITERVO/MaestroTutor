# Event programs and session state

Development contract, 2026-09-27. This extends the existing canonical program and
interpreter. It does not add a second playback engine or move Gemini into Unity.

## Lifecycle

Version-3 programs are saved through the existing revision-checked rules edit.
Saving does not run them. Run now (rules.play or a physical button) creates an
instance. Existing explicit bindings may start one on their configured event.
A new run initializes its declared state and function locals.

An awaitEvent block subscribes only while waiting. A sleep block registers a
monotonic session delay. Stack, function locals and typed shared state survive
these waits. A forever block can compose them into a continuing behaviour;
switch/if plus state can express state machines. Use sequence repeat:false:
whole-program repetition would reset state and is rejected for version 3.

Stop this behaviour cancels its instance and queued starts. Stop all cancels every
program/one-off action and empties the event queue. Pause/focus loss, definition
edits/Undo and reload cancel without automatic restart. Returning focus establishes
a new activity baseline. No missed events or timer backlog replay. Explicitly
starting again initializes state. This is state retained during a run, not durable
state across app restarts or resumable suspended physics.

Assignments take effect when executed and appear in the running trace.
Failure/cancellation does not roll back earlier state or physical effects.
Ended instances discard live variables; terminal outcomes are bounded session
records. Durable native receipts are separate work.

## One canonical program

Version 3 adds required state and events arrays to the version-2 root. Version-2
programs still use the same interpreter without conversion or another stored
representation. No additional save reset is needed.

- state: up to 16 {name,initial} scalar declarations shared across functions.
  Read with {state:name}; write with setState / variable / value expression.
- events: up to 16 {name,type} declarations. Names use user. followed by 1–32
  letters, digits or underscores. Types are number, boolean or text. The same
  name must have the same type across the saved collection.
- forever: body blocks; retains variables until return, failure or Stop.
- sleep: seconds expression, 0.1–3600 seconds, measured from reaching the block.
  A long frame never produces a catch-up loop.
- awaitEvent: event ID, literal source filter, timeout expression, received local
  and value local. Zero timeout waits indefinitely; otherwise 0.1–3600 seconds.
  An event sets received true and the typed value local. Timeout sets received
  false and leaves value unchanged.
- emitEvent: declared custom event ID and matching scalar value expression.
  Programs cannot forge built-in engine events.

Built-in catalog events retain a primary text value: Maestro's activity or the source object ID.
Some events also define typed fields, described below.
Object events allow an exact source ID or empty string for any; all other events
require an empty source. Initial activity is a baseline, not an entered-state event.
No per-frame model call is needed.

The optional book editor exposes event waits, delays, Forever wrapping, state and
named-event declarations. Blocks/source are views of the same program. JSON remains
the expert editing surface; friendlier declaration editing and drag/drop remain.
Conversational authoring remains the default interface.

## Ownership, ordering and bounded work

Version-3 resource declarations bound what a program may use; idle subscriptions
do not reserve objects. Each native invocation acquires its target and prop through
the existing scheduler/handlers using [catalog channels](QUEST_AVATAR_CHANNELS.md). Conflicting claims reject that invocation; no
hidden queue or preemption. Grabbing cancels a currently owning action. Waiting
listeners can still observe grabs/releases. Room edits cancel programs before
targets change. Version 2 retains its whole-run reservation policy.

Receivers are captured from indexed subscriptions at emission, including their
wait generation. Events cannot reach future subscribers or a later wait in the
same run. An event emitted after the wait deadline loses to timeout. Events while
a program computes or plays are not retained for its next wait. This is not a
durable message queue.

The queue holds 64 messages and dispatches at most 16 per tick. Eight total active
runs include waiting programs and one-off actions. Overflow rejects that event
and increments a visible counter. Program-generated event chains are limited to
16 causal hops; an external event, elapsed delay or completed timed native action starts a fresh activation.
Dispatch never recursively invokes code. Existing per-advance, expression, stack,
source and node limits remain. The 65,536 instruction ceiling resets on an actual
event/timer wake or timed native action completion, not a computing yield or emitted signal. Forever without waits
still exhausts its work budget.

## Shared interface

Native advertises eventPrograms.v1; version-3 saves and signals require it.
The original app owns provider access and writes the same program.
rules.inspect is a read-only query, including for uncertain earlier actions.

A user or agent emits a custom signal through one standalone command:

~~~json
{"action":"rules","rule":{"action":"signal","revision":12,
 "eventName":"user.wave","value":3}}
~~~

Revision must match the saved collection; value must match a declared type.
Signals are runtime actions with no Undo/document mutation. Receipts report queued
receivers or explicitly no receivers; they never prove later actions completed.
Never retry an unconfirmed signal. Existing inbox duplicate protection applies.

rules.running adds waiting, waitEvent, waitSeconds and state to node/function/locals.
rules.eventQueue/eventsDropped show backpressure. rules.stop with a target sequence
ID stops that behaviour; omit target for Stop all. Book and agent read the same
native observations. Existing query/action budgets still apply.

## Verification and release boundaries

Native tests cover state retention, filtered events, no replay, timer skips, queue
overflow/generations, payload/wire validation, causal cycles, instruction exhaustion,
occupied targets, preparation cancellation and pause/reload. PlayMode saves through
the native room executor, sends a hydrated custom signal, observes real recorded
object movement, signals again, stops and checks unchanged saved definitions.
Web tests use the same program fixture and shared bridge.

This is not a completed Quest release. Hardware timing/readability and real-provider
planning remain unverified. Wall-clock/background scheduling, durable/resumable state,
parallel branches, broader animation layering, richer state-machine authoring, full capability
coverage and durable receipts remain open.


## Typed native contact events (2026-09-30)

`eventFields.v1` extends an awaitEvent node with optional
`fields: {fieldName: localVariableName}`. Native event registrations own the field
schema, description and required feature; the committed catalog supplies the web
validator, book controls and agent guide. The destinations must be existing scalar
locals with matching types, distinct from each other and from received/value.
Programs may bind any subset. Runtime validates the entire native payload before
changing destinations. A timeout changes received to false and retains all earlier
payload values. Event IDs and bindings preserve the existing version-3 format;
older runtimes are rejected by the shared feature check. No save migration/reset.

The first producer is `object.collided`, emitted on actual PhysX contact entry
for registered rigid room items while room physics is ready and running. The
primary value is the source object ID and the existing exact-source/any-source
filter applies. Fields:

| Field | Type | Meaning |
| --- | --- | --- |
| otherId | text | Other registered room object ID, or empty for environment/controller geometry |
| otherKind | text | object, scannedRoom, controller or environment |
| speed | number | Relative linear speed in metres per second, not force/energy |
| x, y, z | number | One contact point in world-space metres |

The source can be a fixed or dynamic item; actual PhysX collision requirements
still apply. Grabbed items can generate contacts when physical interaction is
active. Animated/carried source items, disabled items and paused/unready worlds
do not produce these events. The collision object is never retained: native code
copies the values at emission and the scheduler detaches them before queueing.
Payload construction is skipped when no matching program is waiting.

This is contact entry, not every physics frame or a contact-exit event. Compound
colliders may produce more than one entry. Scanned geometry is reported as
scannedRoom; floor/wall semantics are not guessed from a normal. Contact points
have the accuracy of the configured collision geometry and speculative CCD, not
visual mesh or live camera reconstruction. World coordinates are not automatically
converted to a room-local action's coordinates.

These observations do not acquire ownership, write saves or grant access to new
object IDs. Actions using a field ID still require a declared or native-created
resource and the usual readiness/ownership checks. Only native producers can emit
catalog events; the app/agent signal route remains restricted to declared user.*
scalar events. Event-time receivers, wait generations, deadlines, queue64,
dispatch16, causal budgets and no-replay lifecycle rules remain in force.

The optional book editor lets users select typed destination variables for these
fields. The normal chat remains the default authoring interface. Existing scalar
custom events remain supported; typed custom records, proximity/condition-edge
subscriptions, vector/list values, collision exit/continuous contacts and semantic
room-surface identities remain future work.

PC verification covers strict field/schema validation, detached observations,
source filters, overflow/generation/deadline handling, feature gating, human
visual editing and denied undeclared-object edits. A PlayMode scenario saves via
the actual room executor, drops a rigid ball onto a registered object and observes
the collision wake a program that drives recorded motion. It then checks scanned
floor contacts, pause and no automatic restart. Quest timing/performance and
real-provider conversational acceptance are still pending.


## Progressive event and fact discovery (2026-09-30)

The same native catalog used for actions now supports `category:"events"` and
`category:"facts"` on search and inspect when `catalogVocabulary.v1` is advertised.
Omitted category retains action discovery. Search returns at most six ID/version/
label entries; inspect returns only the requested exact definition. Agent prompts
explain the query contract instead of expanding the full growing vocabulary.
Native event registrations expose their version, primary value type, source rules,
feature requirements and optional typed fields. Facts expose version, scalar type
and meaning. Unknown IDs or unsupported versions return an explicit null definition.
The shared web validator checks definitions against the committed native export.

Fact inspection also returns `available` and `value` through the same native reader
used by program expressions. Unavailable means null; false, zero and empty text are
valid values when available. The selected reading refreshes in observations without
another query. Disabled/paused runtimes do not expose stale facts, and Maestro state
is unavailable until an activity baseline exists or while audio is suspended.
Reading physics readiness neither starts physics nor guarantees navigation.
Search results and static definitions are cached per query; live readings are not.

The optional book catalog offers Actions, Events and Room facts. Event details show
payload fields and source rules; facts show the current value or Unavailable. These
views use the same queries as the agent and never expose action execution buttons.
Use the existing event-wait and expression editors to add them to programs. Browsing
does not subscribe, emit, save, reserve resources, start or interrupt anything.
Action-only clients retain their existing wire and controls. No save format changes.

Native tests verify detached pages/schemas, strict categories/versions, real playback
continuing while discovery runs, unchanged documents/revisions, false-to-true live
readings and disabled/paused/audio-suspended availability. Shared tests cover the
same wire, feature gates, book controls and a simulated-agent discovery/save journey.
Chrome replays actual native observations through the real book/bridge with explicitly
simulated transport acknowledgements; it does not execute Unity or a real provider.
Headset readability and conversational provider acceptance remain pending.


## Parameterized spatial subscriptions (2026-09-30)

`eventSubscriptions.v1` adds private native watchers to existing version-3 event
waits. A registered event with `input` and `example` requires `version`, `arguments`
and `bindings` on its awaitEvent block. Version must match the inspected definition.
The arguments use the same strict native schema validator as actions; scalar bindings
may read locals, state, facts or calculations. They are evaluated once on reaching
the wait and validated again before sampling. Plain events reject these extra fields.
No new storage format or model tool is introduced.

The first registration, `object.proximity.changed`, watches an explicit pair of
active room objects. Its ordinary `source` filter is empty; the subscription's
arguments name source and target IDs. The pair must differ and both must exist.
`radius` is 0.05–10 metres; `hysteresis` is 0.01–2 metres. `transition` selects
enter, exit or either. Native geometry is explicitly transform-origin distance in
world metres, not mesh separation, collision, visibility or reachable path distance.

Starting a wait reads a baseline but emits no event. An outside pair enters at
`distance <= radius`; an inside pair exits at `distance >= radius + hysteresis`.
The band between thresholds suppresses jitter. At most ten samples per second per
wait are read from actual native transforms. A long frame samples once, with no
catch-up; fast crossings between samples can be missed. Use `object.collided` for
actual physics contact. Physics need not run for distance observations: controller
movement and animations also change these positions. An action still performs its
own physics/readiness and ownership checks.

The event's primary text value is the source object ID; fields are `otherId` (text),
`inside` (boolean) and `distance` (number in metres). Sampling is private to that exact
wait generation; two waits for the same event with different inputs cannot wake each
other. Native broadcast and user signals cannot manufacture a sampled observation.
The same bounded event queue/dispatch and timeout ordering apply. Queue overflow
drops that crossing visibly; staying across the boundary does not replay it later.

A watch costs one of the existing eight run slots and owns no objects or animation
channels. Polling uses a fixed eight-slot array, with no per-frame schema expansion.
Missing/disabled objects or invalid positions fail the waiting run instead of
inventing distance or an exit. Stop, timeout, edit, app pause/focus loss and reload
dispose watches. Returning to a wait establishes a new baseline: crossings during
an action or while stopped are not buffered. Observation IDs never grant authority
to edit undeclared objects, even when input metadata marks an object reference.

The book's existing Event wait editor generates input controls from the same native
schema, supports variable/calculation bindings and typed field destinations, and
preserves the full canonical program. The catalog and agent inspect that identical
contract. Saving, browsing and inspecting still do not start a run.

PC verification includes jitter/baselines, scoped routing, missing objects, timeouts,
computed-value validation, bounded sampling and pause/no replay. A PlayMode scenario
moves actual room transforms across the boundary, observes a program drive real
recorded object motion, then verifies exit and pause. Browser tests replay native
observations and check canonical visual editing. Quest timing/comfort and actual
provider planning remain unverified. This is not semantic room zones, collision
prediction, contact exit, body-part tracking or arbitrary condition-edge evaluation.

## Visual state and signal declarations (2026-09-30)

The optional book's Functions & code workspace now includes **Edit state & signals**.
Users can add, name, type and remove program state and custom signals without JSON.
State initial values use the same bounded scalar/list/record controls as functions.
Signals keep the existing scalar payload contract and `user.` prefix. There are still
at most 16 state variables and 16 custom signals per program.

State renames update state reads and assignments across every function, including
nested branches and native action/subscription input expressions. Custom-signal
renames update that program's waits and sends. Local variables, literal strings,
record field names, native arguments, resource declarations and stable block IDs
are unchanged. Simultaneous swaps retain declaration identity; deleting a referenced
declaration is rejected even if a new declaration reuses its name.

Named signals are shared by programs using the same name. Renaming one program's
signal does not rename other programs or emit an event. The editor states this scope;
coordinated changes across behaviours use existing revision-checked batch edits.
Changing a signal's payload type must fit local blocks and all other declarations of
that signal. Native collection validation rejects conflicts before saving or stopping
valid runs. Invalid/stale editor drafts stay available for repair and do not overwrite
the valid program. A successful behaviour edit retains the existing cancellation
policy; saving does not start a subscriber or send a signal.

State is still per-run memory. Changing its type resets the draft initial value,
which must pass the whole-program validator. Compatible expressions stay intact;
coordinated incompatible body/type changes can be made in Source. Emptying an inferred
list keeps its declared item type. Runtime values are observations, never new saved
initial values, and a new run resets state. No format, interpreter, storage migration
or provider tool was added.

`program-declarations.json` is authored without JSON in a Chrome walkthrough and a
web integration test. Unity saves that exact source, starts two behaviours, sends
2 and 4 to the first, and verifies a retained total of 6, list `[2,4]` and a custom
signal received by the second. It also verifies conflict rejection without interrupting
runs, source/initial-value preservation, Stop, a fresh run, pause and file read-back.
The browser displays the actual native values and pause result; its surrounding room
and command acknowledgements are synthetic. Screenshots were inspected. Headset and
real-provider acceptance remain open; no APK installation is included.

Shared versioned function libraries, coordinated record-field renames, cross-container
block movement, richer subscriptions and the broader v1 release gates remain unfinished.

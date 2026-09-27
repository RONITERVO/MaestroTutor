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

Built-in catalog events carry text: Maestro's activity or the source object ID.
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
the existing scheduler/handlers. Occupied resources reject that invocation; no
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
parallel branches, animation channels, richer state-machine authoring, full capability
coverage and durable receipts remain open.

# One-off native actions

PC development, 2026-09-27. A single requested action can now run through the
shared capability contract without saving a behaviour or creating a button.
The original app owns the agent/provider; Unity owns execution.

## Shared command and observation

When `execution.v1` is advertised, send one standalone command:

```json
{"action":"execution","execution":{"operation":"start","call":{
  "id":"avatar.gesture.play","version":1,
  "arguments":{"target":"maestro","seconds":2,"gesture":"greeting"}
}}}
```

The same call may first be checked through the read-only catalog. A check does
not reserve anything. Start requires a version-2 room envelope with observed
revision conditions for every schema-declared target and prop. The book and agent
client construct these conditions from their observed objects. Native rejects
stale/missing revisions and revalidates arguments, readiness, ownership and capacity.

A successful start acknowledges preparing/running, not future completion.
`scene.execution.selected` includes its exact call and native run ID. Query
`{"action":"execution","execution":{"operation":"inspect","runId":"exact ID"}}`
for that run. Cancel with `operation:"cancel"` and the same ID. Cancellation affects
only the specified one-off run. Saved behaviour run IDs are not accepted by this
operation; their existing Stop controls remain available.

`execution.running` contains compact active summaries. `execution.outcomes`
contains retained terminal summaries. Details include preparing/running/completed/
cancelled/failed, resources and status. Inspect returns the exact original
arguments. Active executions and saved programs share a total capacity of eight.
The session scheduler retains 16 terminal runs; each view projects its own
kind. The durable one-off journal separately retains its latest 16 terminal calls. Unknown or evicted outcomes remain unknown, never presumed complete.
When `executionReceipts.v1` is advertised, one-off receipts also persist across
restart with native-issued IDs; see [recovery semantics](QUEST_ACTION_RECOVERY.md).
Saved-program terminal history remains session-only.

The existing transport consumes each request sequence once within its handshake.
Duplicate delivery cannot start another action. A lost acknowledgement, cancelled
chat request or new handshake does not authorize retrying a start. Inspect current
native state and retain uncertainty where its outcome cannot be established.
Cancelling an already retained terminal run returns that outcome unchanged.

## Ownership and lifecycle

- One-off calls reject occupied resources and full capacity. They do not queue,
  replace another action, change object selection, edit saved definitions or enter
  Undo history. A saved program's explicit restart policy can still preempt them.
- One-off calls compile to a temporary single-invocation program and use the same
  ProgramMachine, scheduler, native handlers, async preparation and terminal path.
  There is no independent playback implementation or saved temporary behaviour.
- Preparation reserves resources. Loading time does not consume playback time.
  Existing preparation deadlines and action-duration limits apply.
- Stop all, pause, focus loss, target editing and manual grabs retain their native
  cancellation semantics. Grabbing keeps the current animated placement. Cancelled
  loads never resume motion when their assets finish loading. Reload never replays
  a pending invocation.
- The optional book catalog offers Run action now, per-run Stop, current phases,
  exact arguments and recent results. Starting does not Apply an open behaviour
  draft. Conversational requests remain the default interface.
- The agent uses its existing bounded action allowance for start/cancel. Execution
  inspection uses its separate query allowance and is allowed when investigating
  an unconfirmed prior request; automatic replay remains prohibited.

## Verification and boundaries

Tests cover shared ownership, capacity, loading, cancellation, synchronous and
asynchronous failure, bounded/evicted history, target and prop revisions, paused
execution, exact wire hydration and duplicate transport delivery. A Unity
integration starts a recorded scene item through the actual executor, observes
movement, cancels/completes it, and checks unchanged saved room/behaviour data.
The XR interaction manager also verifies manual grab cancels the same run without
snapping the object back.

Actual observations are in `test-fixtures/browser/executionStates.json`. Browser
tests use them through the real client to check Run/Stop/status and verify no
behaviour save is issued. Headless Chrome replays those observations; it does not
claim to execute Unity. Selected details and compact histories are bounded, with
a 320 KiB native/web/Android room-observation envelope; commands remain bounded
at the existing 28,000-character batch and 32 KiB native input limits.

The catalog now includes ten invocation capabilities, including an explicit
[upper-body layer](QUEST_AVATAR_CHANNELS.md). [Event programs](QUEST_EVENT_PROGRAMS.md)
provide session state and timers. [One-off receipt recovery](QUEST_ACTION_RECOVERY.md)
is implemented. Full room-command catalog coverage, durable program state and receipts for other commands, broader clip layering, actual provider planning quality, headset performance and store release
acceptance remain. No headset install or service deployment is included.

# Waiting for busy action channels

Development feature: `channelWaits.v1`. This is PC implementation and does not
establish physical Quest or Store acceptance.

Version-3 `invoke` blocks may include `waitForChannels`, a number expression that
must evaluate to 0.1–30 seconds. Omitting it keeps immediate conflict failure.
For example, this explicitly waits up to three seconds before playing a gesture:

```json
{
  "id": "wave", "op": "invoke", "capability": "animation.play", "version": 1,
  "arguments": {
    "target": "maestro", "source": { "kind": "gesture", "gesture": "greeting" },
    "channel": "upperBody", "seconds": 2
  },
  "bindings": {}, "waitForChannels": { "value": 3 }
}
```

The block editor exposes **Busy channels → Wait for free channels**, with the
same typed value/variable/calculation editor as other expressions. The connected
runtime must advertise the feature. Full source, nested functions, parallel
branches and pinned modules retain this field; declaration renaming visits its
expressions. Version-2/simple-step conversion cannot silently discard it.

## Admission, execution and interruption

The interpreter evaluates the timeout and all native arguments once. A busy
request remains at that exact block without taking ownership or starting its
native operation. It neither advances the interpreter nor refreshes instruction
or causal budgets. Its timeout starts at the first refusal, not at program start.

Pending requests are ordered by arrival among overlapping channel sets. Disjoint
work can proceed. Every claim must be available together; waiting for one part
never interrupts an ambient actor on another part. New saved programs and one-off
calls cannot jump an earlier overlapping wait. Older whole-object Queue Latest
requests remain queued until those waits clear. Native manual controls keep their
existing higher priority. Program JSON and the agent cannot elevate priority. Re-triggering the same
behaviour keeps its configured Ignore/Restart/Queue Latest policy; an explicit
restart replaces that behaviour's own pending run.

Each pending run checks at most 20 times per second without catch-up. The existing
limit of eight active runs, including parallel children, bounds pending waits.
An expired wait fails even when its channels became available during a long frame.
No indefinite waiting, unbounded queue, background timer or persistent reservation
is introduced.

When channels become available, the native handler checks current readiness,
exact revisions, loaded resources and any pickup reach guard before starting.
A stale revision, departed object or other readiness failure ends the run; it is
not retried with newer values. A new action duration begins at its actual start,
so waiting does not consume playback time. Readiness and preparation keep their
existing deadlines.

This waits for ownership conflicts only. Missing assets, workspace-wide quiet
requirements, scheduler capacity and other readiness failures retain their normal
errors. One-off calls still refuse busy channels; an explicitly authored program
is required when the user wants a wait.

Stop, rule edits, target cancellation, app pause and reload remove pending work.
A new physical grip on the target cancels it through the existing grip path;
release alone does not restart it. Once an action starts, its ordinary ownership
and interruption policy applies. This feature does not suspend/resume interrupted
animations, retry effects, replay receipts or undo earlier effects. A failed
parallel branch still cancels its entire group. Waiting on a sibling's channel can
therefore time out; it is not a way to bypass ownership.

## Shared observations and verification

The existing run observation exposes `waiting: true`, the pending block/function,
a **Waiting for channels** status and remaining `waitSeconds`. `scene.ownership`
contains only actual owners, so a waiting program is absent there. The book and
app agent read the same observations and use the existing Stop operation.

The shared fixture is `program-channel-wait.json`. Native scheduler checks cover
arrival order, disjoint work, whole-object legacy requests, atomic claims,
timeouts, Stop/pause/edits, readiness after waiting, pinned module scope, parallel
sequencing and instruction budgets. Native integration plays real recipe joint
animations in sequence, verifies actual XRI grip cancellation, and rejects a
queued prop pickup when its previous owner returns it outside current reach.
The browser fixture is captured from that native execution, not a second browser
implementation of the scheduler.

Full package, CI and exact checkpoint evidence are recorded in QUEST_V1_PLAN.md.
Interrupted-action resume, autonomous reflex policy and physical Quest timing,
comfort and long-session acceptance remain separate release work.

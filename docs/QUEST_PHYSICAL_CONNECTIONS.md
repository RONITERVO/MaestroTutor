# Shared physical connections

The saved connection component supports rotating hinges and rigid joins. The
agent, generated book forms and event programs use `object.connection.edit`
(`physicalConnections.v1`), with the same current-revision guard, member claims,
save boundary and Undo. There is no separate toy interpreter or agent-only world.

## Authoring and play

- `attach` makes a rigid join at the two objects' current poses, without moving
  either piece. Physics must be paused. The owner needs solid/bouncy physics to
  simulate; the connected member may be fixed or dynamic. It records local frames.
- `configure` saves an explicit `hinge` or `fixed` definition. Common settings are
  enabled, local frames, breakForce and breakTorque. Hinges additionally have limits
  and passive/spring/motor drive. Configuration does not align or start physics.
- `align` explicitly places the owner against the other member's current frame.
  Hinge angle is bounded by the saved limits; fixed joins require angle zero.
  This saves both current member poses once and creates one live-pose Undo.
- `rearm` clears a broken connection's live latch only if both exact members are
  already aligned. It stops their previous velocities without teleporting, saving
  or adding Undo. Starting physics remains a separate action.
- `remove` removes the saved component. Failed validation, stale revisions or failed
  saves leave the original live poses and saved connection intact.

Zero breakForce/breakTorque means unbreakable; positive values are bounded to
10,000 newtons/newton-metres. These map to Unity Joint limits, with zero converted
to infinity. Unity reports constraint failure, which may result from forces,
contacts or other joints; it does not establish who caused damage. See
[Unity Joint.breakForce](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Joint-breakForce.html)
and [OnJointBreak](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnJointBreak.html).

A native break releases the dynamic body and stays broken through physics
pause/resume. Successful configure/attach/align or explicit rearm resets it.
Broken status is play state, not persistent damage: workspace recreation reads
the saved connection and still requires alignment. Games that need persistent
damage can remember it in program state and explicitly disable/remove the
connection. Stop on an already completed edit cannot undo it; pause physics or
change/remove its component.

## Observation and events

`object.connection` returns the object revision, configured flag, exact connected
ID and common settings. `object.connection.frame` returns one local frame, and
`object.connection.hinge` returns hinge limits/drive (unavailable for a fixed or
absent connection). Read all required fields at the same revision. Splitting
these facts keeps every value inside the existing program-value budget.

`object.connection.state` reports phase, active, broken, angle and error. Angle
uses the authored hinge frame; it is not fixed-joint freedom or a revolution count.
`object.connection.broken` emits once per admitted physical break while simulation
is running. Its primary value/source is the owner ID; typed fields are connected,
kind, forceLimit and torqueLimit. Limits are configuration, not measured impact.
Pause, Stop and retirement do not emit damage events, and missed events are not
replayed. The ordinary event scheduler can branch, remember damage, play a
reaction, score a game or ask Maestro to respond using this event.

Native contacts and [structure displacement](QUEST_STRUCTURES.md) remain separate
observations: a stacked castle can fall without a connection breaking. Pair the
observations in a program when the game needs both; do not claim causal certainty
from proximity or a single frame.

## Admission, ownership and limits

Both frames must meet within 3 cm; hinge axes within 5 degrees and enabled angle
limits within 3 degrees tolerance. Fixed joins require complete-frame rotation
agreement within 5 degrees. +X is the hinge axis and +Y its zero direction.
Missing, misaligned, loading or animation-owned members suspend the owner.
An already admitted joint remains physical during normal controller gripping.
Explicit teleport or scale change invalidates it. Physics/focus suspension drops
old velocities and does not silently start simulation afterward.

Initial budgets are sixteen connections per room, one outgoing connection per
created object, no cycles and anchors within ten local metres. Hinge limits/targets
are within ±170 degrees, spring 0–100, damper 0–20, motor speed ±360 degrees/second
and force 0–20. These are admission bounds, not headset performance measurements.
Both ends must be included in an atomic construction movement.

[Connected blueprints](QUEST_BATCH_CREATION.md) use version 3 with slot-based
`connections`; each instance binds fresh object IDs. The ordinary construction
capture path preserves internal rigid/hinge definitions and break limits, then
publishes pinned editable constructor source. It never captures the transient
broken latch. Independent version-1 blueprints remain supported. Version 2 used
the old hinge schema and is refused instead of silently dropping links.

## Development save boundary and verification

Current room files are `room.v7.json`, paired intents `room-snapshot.v6.json` and
portable archive manifests version 6. Earlier connected v6 rooms, older in-flight
intents and old archives remain preserved/refused for recovery. No device data is
wiped. Clean older room formats supported by RoomDocument still validate; unknown
component versions cannot load as empty connections. This prerelease boundary
uses the owner's approved reset policy, without maintaining a second runtime.

Native checks cover gravity/impulse retention, hinge motor/spring/limits, actual
break and typed event delivery, pause/rearm, save failure, exact revisions, one
Undo, capture with fresh identities, and old-format preservation. The full Editor
app journey and generated Chrome controls exercise matching calls and receipts.
These are separate from Quest acceptance. See [device QA](QUEST_DEVICE_QA.md).
Socket snapping, sliding joints, cyclic mechanisms, mesh welding, soft bodies,
cloth/hair and liquids are not implemented by this increment.

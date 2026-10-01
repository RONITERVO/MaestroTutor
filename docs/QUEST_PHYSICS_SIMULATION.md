# Shared room physics simulation

`physics.simulation.set` exposes **Start physics / Pause** from the physical tray
through the same native `RoomPhysicsWorld` service. Discover it when
`physicsSimulation.v1` is advertised. The book generates its fields from the same
catalog schema used by original Maestro's room agent and native programs.

Read `physics.simulation`, then send `operation: "start"` or `"pause"` and the
exact `stateId`. The fact and completed result contain:

| Field | Meaning |
| --- | --- |
| `stateId` | Native identity of this simulation state, not a saved-object revision |
| `ready` | The scanned-room service reports its surfaces ready |
| `running` | Physics is currently enabled |
| `active` | This physics component is enabled, focused and not application-paused |
| `held` | A native workspace/lifecycle hold prevents starting |
| `canStart` | Scan and lifecycle prerequisites currently permit starting |
| `status`, `reason` | Bounded display text; `reason` explains an unavailable start |

`canStart` does not authorize an action, approve real-world alignment, guarantee
all imported geometry is ready, or bypass a workspace boundary. Loading, explicit scanning and surface visibility can be requested through
`room.environment.set` ([room setup](QUEST_ROOM_ENVIRONMENT.md)). System permission
and setup screens, and checking actual alignment, still require the user. A ready scan also does not prove
a clear walking path. Fixed, held, carried and animated objects retain their own
physics/ownership rules while the world is running.

Manual Start/Pause, scan changes, focus/pause/enable transitions and runtime holds
invalidate earlier identities. This includes pressing physical Pause while already
paused, and changing away and back before a delayed request arrives. A stale
shared call fails without changing the accepted state or status. Readiness checks
are also read-only. An already satisfied shared command does not reset physical
motion observations. Change listeners see the final state identity.

These transitions do not reserve every actor or globally stop the scheduler.
Unrelated gestures, timers and programs can continue. Pause makes dynamic bodies
kinematic, clears old throw velocities and may end walking/actions that require
physics; their existing outcomes explain the interruption. Starting again never
replays those velocities or restarts cancelled movement. Existing held/animated
object owners remain intact. Physical Pause remains immediate, including during
workspace preservation; shared calls obey the normal runtime/admission gates.

The transition itself does not edit the room document or add Undo. Normal settled
placement tracking remains separate. A durable completed action receipt records
the transition, not a continuing lease on physics: read the fact again for current
state. Duplicate receipts cannot restart simulation after Pause. Stopping a
completed receipt does not undo it; use a fresh explicit pause request.

Programs can bind `stateId` to `field(physics.simulation, "stateId")` with
`dataVersion: 1`. This reads a fresh identity at each invocation. Defining or saving
a program does not execute it. Existing `physics.running` and `physics.ready`
boolean facts remain useful for conditions; the older `physicsRun` bridge route
remains compatible, while current planners prefer catalog discovery and receipts.

Native verification covers actual falling/frozen bodies, cleared throw speed,
manual/lifecycle/scan reversal, failed readiness, workspace holds, bounded facts,
coexisting animation owners, walking interruption without replay, duplicate
receipts and a start/wait/pause program. Browser fixtures replay captured native
state through generated fields; they do not prove headset or provider operation.
Quest lifecycle/comfort/performance and real conversational acceptance remain
release gates. Device work is still on hold.

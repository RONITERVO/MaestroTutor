# Shared object layouts

`layoutEdits.v1` adds `object.layout.apply` and the read-only `object.placement`
fact. This is the batch placement building block for structures and board layouts;
it is not yet a saved assembly/blueprint library or a snapping/joint system.

## One editable operation

Supply 1–16 distinct existing creation IDs and an explicit room-local position,
unit-quaternion rotation and uniform scale for each. All targets are declared
schema resources, so the generated book controls, optional source/blocks, ordinary
programs and the room agent use the same ownership and execution paths. Layout
placement retains its own 16-member bound. Programs may declare up to the generated
66-resource ceiling; that does not increase layout, creation-per-run or physics budgets. Arrays currently stay literal: saved
reset programs pin exact existing IDs, rather than silently discovering members
or acquiring authority over an expanding group.

The action checks every member before changing any. Held targets, active authoring,
competing animation/ownership, missing/disabled members, invalid poses, duplicate
IDs or failed persistence refuse the whole edit. One-off requests retain the
existing per-object optimistic conditions. The book and Maestro are excluded.
No missing object is recreated and no names/tags substitute another object.

The room writer saves one candidate before acceptance. All placements enter one
Undo record. The before-poses come from the actual live transforms, even when the
periodic physics snapshot has not caught up; a failed save changes neither those
transforms nor the journal. If live placement already matches but saved placement
lags, the accepted snapshot catches up without adding an empty Undo entry.
Geometry, physics preferences, identity and recorded animation data remain intact.
Temporary play applies the same transaction to its fork; Keep/Discard retain their
existing meanings. An already completed receipt never reapplies a reset after Undo.

Applying a placement clears that member's linear/angular velocity. Gravity and
collisions may immediately continue; the operation neither starts nor pauses room
physics, and it does not disturb unrelated objects. Undo restores poses and scale,
not prior velocities or a rewind of the simulation. Completion proves placement,
not stable stacking, overlap-free placement or a settled physical outcome.

## Capturing and reusing an arrangement

`object.placement {target}` returns the current room-local pose/scale, temporary
flag and last authored object revision. Its live pose can differ from
`object.definition`, which reports accepted saved/temporary data, and from
`object.position`, which uses Unity world axes. The revision does not identify
every physics frame. Reads do not save, reserve or authorize an edit; paused,
missing, disabled or invalid targets are unavailable. Multiple reads are separate
observations, not an atomic capture while objects move.

An agent or user can retain chosen placement fields in an ordinary named program
and trigger it from a button, manual start or existing program event. Before a
one-off reset, inspect the current members and use fresh normal request conditions.
Room axes may change after alignment/recentering; these values are not persistent
real-room anchors. Capture a stationary arrangement deliberately, and reset only
when the user intends to interrupt the pieces' physical play.

A native integration scenario creates six loose building bricks, lets them settle,
uses a real simulated ball contact to displace a stack, applies the shared layout,
and checks that the rebuilt stack settles again. Separate tests cover all-member
ownership, stale/held/missing targets, save failure, exact live-pose Undo, replay,
temporary discard and room-local readback. This is Unity simulation evidence, not
Quest 3 input, room alignment or frame-time acceptance.

## Next layers

Saved assembly definitions should own stable member slots, initial placements,
explicit missing-member policy and pinned construction sources. [Batch instancing](QUEST_BATCH_CREATION.md) now accounts for every created object
and commits once, using literal template/inline-recipe blueprints in ordinary
programs. Persistent membership is still separate. Snap sockets, editable
breakable connections and bounded displacement/settling observations then build
on those definitions. A collapse rule should expose the supporting member changes
and recent contacts; proximity/contact alone must not claim a certain cause.

Those layers, convenient multi-selection/capture controls and an assembly library
remain unfinished. This increment provides a usable common reset operation and
live baseline facts without inventing a second scene, geometry format or scripting
engine. See [world authoring](QUEST_WORLD_AUTHORING.md) for the complete kit.

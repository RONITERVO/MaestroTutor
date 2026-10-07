# Shared surface placement

`object.surface.place` uses the same native placement calculation and saved object
edit as the physical Place surface control. `surfacePlacement.v1` is advertised
with the scanned-room service. Availability still checks active MR, tracking,
supported live surface detection, an unheld/editable target and workspace access.
It does not require physics to run and never starts it.

Read `room.environment` for `stateId`, select an exact target, and choose:

- `below`: a downward ray from just above the object's collision bounds.
- `gaze`: the user's head ray, captured when the action begins.

Both rays are sampled once. Preparation does not continuously follow the user's
head or object. Live detection reaches four metres and accepts upward-facing
surfaces with world normal y at least 0.7. No hit, steep surfaces, low-confidence
normals or missing collision bounds fail without guessed placement or retries.

The object's combined collision bounds determine the support offset and a 1 cm
gap. Position changes; current rotation and scale remain. This tests one support
point, not full surface coverage, wall clearance, visual fit or room alignment.
Large objects can overhang a table. Imported collider quality affects the result.
Existing rigid physics can settle the object afterward.

The action returns the actual saved target revision, origin position, contact point,
normal and temporary-room flag. Vectors use room axes; positions use room metres.
A changed placement has one normal room Undo entry. Outside temporary play, saving
precedes completion; in a temporary room, it changes only the fork until Keep.
Velocity resets through the existing object-placement path. Selection and other
actors remain unchanged. Replaying an existing receipt returns history, including
after Undo, and cannot raycast or move again.

## Lifecycle and shared ownership

The action reserves only its target while preparing. It refuses another actor's
ownership, active authoring and held objects. A physical interruption can cancel
it. Stop, changed room identity, changed authored target revision, disabled service,
lost focus/tracking, virtual view or workspace holds prevent a late placement.
The sampled ray cannot apply after cancellation. Failures do not create Undo edits.

Preparation may request room permission. The system focus handoff cancels this
placement; finish access through the system screen (or Load room), then make an
explicit fresh request. A pending permission result cannot move the object after
return. Permission requests are coalesced by the existing native permission path.
No automatic retry or replay is introduced.

The physical tray still accepts the controller's ray and retains human interruption
priority, then calls the same placement service. Cancel/Recall/focus loss invalidate
pending arming so a delayed permission result cannot arm the tray again.

## Evidence and release gates

Native tests use a controlled surface-provider boundary and real colliders,
placement calculation, room journal, action scheduler and persistent receipts.
They check actual support height, gaze sampling, offset bounds, Undo and replay,
late cancellation, stale identities, failures, temporary-room storage, competing
ownership and the physical tray adapter. Web tests validate captured native
requests/results against the generated catalog. Browser replay checks the same
book action controls; it cannot prove live Meta surface detection.

Physical Quest acceptance still needs permission UI, tracking, actual floor/table
hits, imported model bounds, readability, latency and placement comfort. The
2026-10-07 learner lesson exposed a live-ray miss and no subsequent room reload;
the apple stayed on its table. This feature is not a Store acceptance claim.

## Explicit scanned floor or tabletop

`object.scan.place` (`scanPlacement.v1`) complements live detection when the user
names a specific surface such as the floor. A downward live ray can hit a table
first; that is not evidence of reaching the requested floor.

Read `room.scan`, then its paginated `room.scan.surfaces` and the selected
`room.scan.surface`. Supply the exact scan `stateId`, `anchorId`, target object
and plane-local `x/y`. Meta's plane normal is local +Z. The supplied location
represents the projected collision-bounds centre, not the object's origin.
Select the inspected FLOOR anchor for a floor request; names and guessed Y=0
are not substitutes for observed geometry.

The native service requires loaded, tracked, active room geometry and an upward
normal. It projects the complete world collision AABB onto the selected plane
and checks its rectangle and polygon boundary, including concave notches. This
is conservative for rotated/compound colliders. It preserves rotation/scale,
adds the same 1 cm support gap, saves through the normal object edit and returns
the exact room/anchor IDs and actual placement. Undo, temporary play, receipt
replay, write guards and target ownership apply. No persistent attachment is
created; the saved object does not follow a later scan revision.

This does not check an unobstructed travel path or occupancy by other objects.
Moving obstacles and holes absent from the scan remain outside its evidence.
Neither placement nor a successful load verifies real-room alignment or starts
physics. A missing/stale/steep plane or a footprint that does not fit leaves the
object unchanged. The shared catalogue exposes the same action to the agent,
editable programs and generated book/workshop controls.

The planner may load an available saved scan once as a prerequisite for requested
scan-dependent work. Pending, declined or cancelled requests are not silently
retried. User permission/setup and alignment checks remain explicit when needed.
Result narration must distinguish inspections from actual placement attempts;
the initial Live handoff precedes execution and cannot confirm success.

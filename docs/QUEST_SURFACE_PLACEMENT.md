# Shared live surface placement

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
hits, imported model bounds, readability, latency and placement comfort. Device
work remains on hold; this feature is not a Store acceptance claim.

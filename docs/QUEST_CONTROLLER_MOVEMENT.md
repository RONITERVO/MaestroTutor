# Controller movement and bindings

Development implementation, updated 2026-10-18. One authored world has independent
presentation and collision participation. Desktop evidence does not establish
headset comfort or acceptance.

## Physical controls

The movable **Movement and controller bindings** tray contains solid 3D buttons.
No new overlay covers conversation pages. Both movement modes begin disabled on
every launch and after focus loss, pause, head-tracking loss.

- Maestro stick enables direct walking using the selected avatar stick.
- Your movement enables user walking at any backdrop opacity.
- Virtual / MR changes presentation while keeping world position and activity.
  Stop / MR disables the movement opt-ins and restores the mixed view.
- Maestro binding and Your binding cycle Off, Left and Right, skipping the other
  target's assigned stick. Swap sticks exchanges assignments in one operation.
- Walk speed changes user speed through 0.35, 0.65 and 1 m/s. Maestro retains its
  own Walk speed preference on the existing Maestro movement tray.
- Dead zone changes the radial threshold through 0.1, 0.2 and 0.3.
- Select button chooses X, A, left stick click or right stick click. Use action
  assigns the currently selected visual-rule sequence by stable ID. Button
  command cycles None, Turn left 30 degrees and Turn right 30 degrees.

Defaults are right stick for Maestro and left stick for the user, with X/A for
left/right snap turns. Enablement is never saved. Assignments, speed and dead
zone use a bounded, validated controls.v2.json with atomic replacement and a
backup. Unknown versions are preserved without overwriting. Missing rule actions
remain missing; no different action is silently selected. Rule renaming retains
the same identity. Primary trigger and grip keep their existing click/grab roles;
B/Y, the system menus and palm Recall remain reserved. General remapping of
trigger/grip and accessible alternate movement methods remain release work.

A stick must be centered after enabling, rebinding, a tracking interruption or
manipulation before it can move anything. Buttons must likewise be released
before another press is accepted. Hand aim/pinch is not treated as controller
movement input. Pointing/clicking, grabbing and drawing interrupt stick input.
Controller rule buttons use the existing sequence scheduler, including duration,
loop, interruption and loading policies; they can play recorded or imported
motions just like controller-mounted 3D buttons and tutor-state triggers.

## Maestro and the room

Direct walking requires accepted navigation and Start physics, like Follow me.
Its ground follows the independent real-collision policy: aligned scanned
surfaces when enabled, accepted authored surfaces when disabled. It is
relative to the user's horizontal viewing direction and proportional to the
stick deflection after the dead zone. It uses the existing avatar scale, speed,
saved walking motion and navigation-owned root translation. Centering stops and
saves placement. It does not resume the previous Follow action.

A direct step must stay on a connected navigation surface and pass the same
swept body-capsule and overlap checks as following. A blocked direction stops;
reversing the stick permits backing away. This remains a body-clearance test,
not per-limb contact, foot planting or cloth/hair collision. Posing, authoring,
replacement, focus loss and conflicting rule ownership changes interrupt movement.

## User movement and mixed reality

Artificial user movement requires its own opt-in and a tracked, ready world.
Passthrough backdrop opacity, real-depth occlusion and each entity's physical
collision participation are independent. Full virtual presentation hides the real
room without creating a synthetic floor. Passthrough windows and authored
material opacity retain their own definitions. Physical tracking and world lock
continue independently; content moves relative to that fixed physical frame.

Walking and snap turns transform virtual content, including accepted ground,
bodies, velocities and navigation. Held objects and physically bound content keep
their physical poses. The complete frame transfer checks joints and sweeps each
participant that still collides with the scanned room. An actor with real-room
collision disabled can use virtual ground below the scanned floor while another
prop remains constrained by it. Changing backdrop opacity never changes these
collision policies.

Walking uses the shared ground motor, accepted authored ground and swept body
clearance. Loading ground/obstacles retain their preparation demand. Unsupported
ground, excessive steps/slopes or occupied paths refuse; there is no hidden flat
fallback. Supported terrain height changes move the virtual content frame. This
does not provide foot planting or constrain the real person's physical movement.

Returning to MR preserves the world pose, programs, physics and both movement
opt-ins. View changes reset input gates without taking actor ownership.
Focus, pause and head-tracking loss disable movement and restore ordinary
presentation without rewinding world placement. Other subsystem lifecycle
handlers still stop unsafe activity on actual interruption.

B/Y, Home, palm Recall and the creation tray recover only the book and seven
optional tool trays around the tracked physical viewer. The shared
`room.tools.recall` action uses the same native transaction with fresh
`room.tools.recovery` state and book revision. Recall keeps the world frame,
Maestro, creations, user-built buttons, physics, programs, audio and view/input
modes unchanged. Hidden trays remain hidden. Release a held book/tray first;
unrelated held objects stay in your grip. Only a physical Recall can interrupt a
lower-priority book actor; shared calls refuse a competing book owner. The saved
book placement is one Undo; recovered tray positions are transient. Workspace
holds, invalid frames, tracking loss and unavailable saving refuse all placement.

Explicit relocation and **World origin** use a separate completion operation,
`world.viewpoint.set`, with fresh tracked-head, ground, occupancy and transfer
checks followed by asynchronous saving. It is separate from Recall. Once the user
moves or turns the virtual world, a workspace-owned bookmark records the authored
floor position and horizontal heading. It is sampled at most once per second
while tracking is valid, with explicit save/flush capture where available. Reload
waits for fresh tracking and positions virtual content around that current view;
it never moves the XR origin or claims real-room anchor alignment. Movement and
physics still need their normal opt-ins. A fresh workspace uses its entry frame
and cannot inherit the previous workspace's offset.

The bookmark uses the current room coordinate bound (25 m from its origin).
Virtual input refuses movement/turns outside that bound before moving content.
Region streaming and larger coordinate ranges remain future spatial work.

The bookmark shares the versioned room document, room/memory snapshot and workspace
archive. It does not change entity revisions or add Undo steps. Geometry Undo and
temporary-room discard preserve the current viewpoint. Tracking loss, workspace
holds and frozen writes preserve the last accepted sample. Ordinary autosave
failure remains visible and retryable. Initial stationary MR rooms have no active
bookmark until virtual movement is used; merely opening them does not reposition
the world around the viewer. Scan loading, held items and workspace boundaries
continue to gate view changes. Camera sharing is independent.

The shared controller.mode.set action is version 2 because its old view-exit
contract reset the world. Old calls remain unavailable for explicit review;
saved sources are not silently upgraded. The controller.mode readback remains
version 1 with the same fields. User and agent use the same catalog definition.

## Evidence and remaining acceptance

Automated verification exercises independent targets, opt-in and neutral gates,
actual rig leg motion, scanned-wall rejection and reverse input, virtual body
collisions, snap-turn press edges, world-pose retention, pause/tracking loss,
persisted assignments, missing-action behaviour, and the physical tray. A test
also creates the installed OpenXR Oculus Touch device layout and reads its real
stick, primary-button and stick-click controls through BookControllerInput.
It also presses/releases a real ray-targeted solid view control, covering the
click-release ordering that a direct method-call test would miss.
Settings tests cover invalid/conflicting mappings, finite bounds, copying,
backup recovery and refusal to overwrite a newer version.

Current verification results are recorded in QUEST_V1_PLAN.md. Focused tests also
check live rigid-body velocity, accepted collider identity, navigation bake reuse,
autonomous follow and running program continuity across view changes. These are
real native Unity tests, not headset observations. Required Quest checks include
Touch Plus/controller-hand switching, simultaneous sticks, comfort and text
placement, MRUK alignment, passthrough lifecycle, grabs/throws during movement,
seated/standing heights and sustained performance.

Blend/opacity, passthrough windows, supported terrain travel and swept transfer
with active real-room participants now have native desktop coverage. Physical
alignment, Quest presentation/comfort, foot planting, broader imported worlds,
larger regions and the remaining release gates still require acceptance.
See [spatial settings](QUEST_SPATIAL_SETTINGS.md) and
[world authoring](QUEST_WORLD_AUTHORING.md) for individual collision policies.

# Controller movement and bindings

Development implementation, updated 2026-10-07. The spatial-state refactor is
in progress. Desktop evidence does not establish headset comfort or acceptance.

## Physical controls

The movable **Movement and controller bindings** tray contains solid 3D buttons.
No new overlay covers conversation pages. Both movement modes begin disabled on
every launch and after focus loss, pause, head-tracking loss or Recall.

- Maestro stick enables direct walking using the selected avatar stick.
- Your movement enables user walking only in the explicit Virtual view.
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
replacement, focus loss, Recall and rule ownership changes interrupt movement.

## User movement and mixed reality

Artificial user movement currently requires explicit Virtual view. That view
stops ARCameraManager passthrough and uses an opaque background; scanned surfaces
are visible as geometry. It creates no floor or other world object. The physical
tracking origin and MRUK world lock keep updating in both views. Virtual content
moves relative to that fixed physical frame. Loading/scanning and physical
surface placement currently wait for MR view.

Horizontal walking and snap turns transform virtual content, including its
accepted ground, bodies, velocities and navigation. Held objects and physically
bound content keep their physical poses. Unsupported cross-frame joints and
active physics against real-room colliders refuse movement. Pause physics or
explicitly disable real collisions before virtual travel; a view switch never
changes that policy. Swept admission for active real collisions remains open.

Walking uses a swept body capsule against virtual obstacles and checks accepted
authored ground under the current and destination footprints. There is no hidden
flat fallback. Current planar travel refuses missing ground, ledges and ground
outside the small level tolerance of the physical floor. Following slopes,
stairs, gravity for the user's body and teleportation remain unimplemented. The
headset still moves physically; application collision does not constrain the
real person or replace the Quest system boundary.

Returning to MR restores camera and passthrough settings and disables user
locomotion. It retains Maestro's separate controller opt-in, world pose, authored
ground, physics, programs and autonomous actors. Manual view changes use the same
path as the shared action. View entry/exit do not acquire actor ownership or cancel
animation authoring. Focus, pause and head-tracking loss disable both movement
opt-ins and restore MR without rewinding the world. Other subsystem lifecycle
handlers still stop unsafe activity on actual interruption.

B/Y and palm Recall remain explicit recovery: stop movement and relocate the book
and tools around the physical viewer through RoomInteraction. View selection is
not Recall. Offsets survive view changes within the session; persisting them
across restart/workspace changes is still required. Scan loading, held items and
workspace boundaries continue to gate view changes. Camera sharing is independent.

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

Independent blend/opacity, passthrough windows, terrain-following locomotion,
persisted world offsets and active real-collision world movement remain release
work in the shared spatial-state refactor.

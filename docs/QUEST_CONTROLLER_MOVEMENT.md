# Controller movement and bindings

Development implementation, 2026-09-26. Device installation and comfort checks
remain on hold while the user sleeps and charges Quest. This is not a release
or headset acceptance claim.

## Physical controls

The movable **Movement and controller bindings** tray contains solid 3D buttons.
No new overlay covers conversation pages. Both movement modes begin disabled on
every launch and after focus loss, pause, head-tracking loss or Recall.

- Maestro stick enables direct walking using the selected avatar stick.
- Your movement enables user walking only in the explicit Virtual view.
- Virtual / MR changes presentation; Stop / MR returns to the physical room.
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

Direct walking requires a checked scan and Start physics, like Follow me. It is
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

Artificial user movement is opt-in and limited to Virtual view. That view
stops ARCameraManager passthrough and uses an opaque background and a 20-metre
paper floor. Existing scanned surfaces are visible as geometry. MRUK updates
are suspended while the tracking origin is moved, preserving its entry-time
alignment transform rather than allowing world-lock updates to cancel the
locomotion offset. Loading/scanning and live-depth placement wait for MR view.

Horizontal user walking uses a body capsule against scanned surfaces, collidable
room objects and the book/avatar. It stops at an 8-metre radius from the virtual
floor centre. Snap turns rotate the origin around the current headset position.
This initial virtual space has a fixed floor height: slopes, stairs, gravity for
the user's body and teleportation are not implemented. The headset can still
move physically; application collisions do not constrain the real person or
replace the Quest system boundary.

Returning to MR restores the exact original origin transform and camera settings,
resumes the prior passthrough component state, ends artificial movement and pauses
physics for alignment review. The origin offset is not saved to the room. B/Y and
palm Recall invoke this return before relocating the book/tools around the real
viewer. Focus, pause and head-tracking loss also return to MR and require explicit
movement enablement again. Transitions refuse entry while holding items or while
a scan is loading. Existing audio-resume policy remains owned by the browser.

## Evidence and remaining acceptance

Automated verification exercises independent targets, opt-in and neutral gates,
actual rig leg motion, scanned-wall rejection and reverse input, virtual body
collisions, snap-turn press edges, origin restoration, pause/tracking loss,
persisted assignments, missing-action behaviour, and the physical tray. A test
also creates the installed OpenXR Oculus Touch device layout and reads its real
stick, primary-button and stick-click controls through BookControllerInput.
It also presses/releases a real ray-targeted solid view control, covering the
click-release ordering that a direct method-call test would miss.
Settings tests cover invalid/conflicting mappings, finite bounds, copying,
backup recovery and refusal to overwrite a newer version.

The latest full run passed 44 EditMode and 48 required PlayMode tests (three
optional private-file tests were explicitly skipped). The desktop tray render
is inspection evidence only. Required Quest checks:
actual Touch Plus/controller-hand switching, both simultaneous sticks, comfort
and text placement, return-to-MR alignment with MRUK world lock, passthrough
lifecycle, controllers following snap turns, grabs/throws around movement,
scanned corners and furniture, seated/standing heights and sustained performance.
No new device work has occurred during charging.

Implementation uses the installed XRI/OpenXR/Meta packages. Unity's locomotion
architecture treats movement as transformation of the XR Origin and distinguishes
continuous movement from snap turning:
https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/locomotion.html
The pinned Meta OpenXR camera editor documents ARCameraManager enable/disable as
the passthrough lifecycle control; pinned MRUK.Update owns world-lock correction.

The verified development APK is 7CCC2880 (uninstalled), with 108 required
Unity/Android checks and matching build-mirror source hashes. Exact artifact,
checksum and archived evidence are recorded in QUEST_DEVICE_QA.md.

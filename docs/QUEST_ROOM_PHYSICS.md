# Quest room physics development checkpoint

Accepted scope: rigid items obey gravity and respond to grabs, throws, controller
contact, and the scanned environment. Hair and cloth interaction simulation are
excluded. The user confirmed a basic physics test on Quest 3. Detailed acceptance
remains open; computer tests do not verify room alignment or tracking quality.

## Room surfaces and placement

Meta XR Core SDK and MR Utility Kit are pinned to 207.0.0. `ScannedRoom` loads
device scene data after the user requests room access. **Scan room** invokes the
headset's Space Setup; **Load room** loads the saved scan, requesting setup if
none exists. There is no fabricated floor in the shipping app.

MRUK EffectMesh generates hidden colliders for floor, ceiling, walls and supported
furniture volumes. Global mesh and wall opening cutouts are not used in this
checkpoint. Scene geometry remains outside the movable/recoverable content root.
The existing OpenXR/Input System pose drivers remain in control of head and hand
input. `MetaTrackingRig` supplies MRUK's OVRCameraRig tracking-space contract;
OVRManager and XROrigin both request floor-level tracking. MRUK world locking
adjusts their shared TrackingSpace. This combination still needs device QA.

The solid physics tray offers Load room, Scan room, Show room, Start physics,
Pause, Object mode, Mass, Place surface and Collision shape. **Show room** exposes
the scan surfaces for alignment inspection. **Start physics** is explicit and
requires a current tracked room, world locking, floor colliders and wall
colliders. Losing the room/tracking, entering another room or suspending the app
freezes physical items and clears throw velocity; resuming physics is explicit.
An item outside the loaded room is returned to its last valid position and
simulation is paused. B/Y recovery remains available. Hand users can point and
pinch the creation tray's Bring back tool. A solid Recall pebble follows each
tracked palm; point and pinch with the opposite hand to pause physics and recover
the room without needing any tray. It hides when palm tracking is lost and cannot
be activated by the hand carrying it. The user confirmed palm recovery and
retrieving a tray beyond a scanned wall on Quest 3.

**Place surface** uses EnvironmentRaycastManager independently of a room scan:
after permission, select an item, tap the solid tool, point at a real floor or
table, and tap trigger. Placement requires a valid hit with a suitable surface
normal. It offsets the item's collision bounds above that surface. A failed hit
does not move it. Live raycasting has camera-frustum limits and is not used as
the collision world for flying objects. It does not establish persistent object
anchors or guarantee whole-object clearance around irregular surfaces.

## Item behavior

- Existing saved objects retain fixed placement. Newly created balls use Bouncy;
  newly created blocks/cylinders use Solid. Imported models and drawings initially
  remain fixed. The book and Maestro retain fixed placement and are solid
  obstacles to loose items. Tool trays are unaffected by loose-item collisions.
- Users can choose Fixed, Solid or Bouncy, mass presets from 0.1 to 20 kg, and
  Automatic, Box or Sphere collision shapes. Imported football/basketball meshes
  can use Sphere. These are bounded approximations, not automatic concave mesh
  decomposition. Animated mesh deformation does not deform the collider.
- Unity gravity, friction, restitution and Rigidbody simulation handle flight,
  impact and settling. Speculative continuous collision detection covers small
  rotating items and kinematic controller contact. Throw speed is bounded to
  15 m/s and spin to 30 rad/s. The floor regression test exposed penetration with
  sweep-based CCD and now passes with the speculative mode.
- XRI velocity tracking moves held physical items against obstacles, and its
  sampled release velocity produces throws. Tracking cancellation suppresses
  throw velocity. Small tracked contact spheres can push loose items, excluding
  the item held by that same controller. They approximate contact rather than
  simulating each finger.
- Animation authoring and playback temporarily own an item's transform. Physics
  cannot simultaneously drive it. The rule builder's **Play then throw** action
  evaluates a recording's final pose, derives velocity/spin from its last 0.1 s,
  and releases it into physics. Ordinary cancellation never launches an object.
  Attachment to Maestro's hands and synchronized avatar/object throw authoring
  are still separate remaining work.
- Save/load and undo retain physics settings. Live placements are checkpointed
  without adding every simulation step to Undo. Unrelated edits do not reset a
  flying object's transform. Room placements are not cross-session spatial
  anchors; alignment and room-change recovery still need hardware validation.

Reserved project layers: 8 RoomObstacles, 9 TrackedPushers, 10 LooseItems,
11 ScannedEnvironment. Scanned surfaces collide with loose items but are excluded
from both trigger/pinch and XRI grip rays, allowing misplaced tools to be
retrieved. This fixes the user's reported inaccessible tray behind a wall after
scanning. Their collision matrix isolates physics from UI/tool colliders. Meta SDK license
and attribution notices are included in Resources/MetaXRNotices.txt.

## Verification and next hardware checks

Automated checks exercise real Unity physics for scan gating, gravity, floor
bounce, wall bounce, XRI release, tracking cancellation and animation ownership.
Additional tests cover the recorded-throw handoff, unrelated room edits, mass and
collision-shape persistence, Undo, invalid settings, and old-room compatibility.
The current source passes 30 EditMode and 21 PlayMode checks, including tracked
contact pushing a loose ball and restoring held-object exclusion after pause,
actual XRI grabbing/clicking through a scanned wall, and opposite-hand recovery
with tracking-loss cleanup.
The physical tray was rendered in Unity and its labels inspected on the desktop.

On Quest: grant/deny permission, cancel/retry setup, show/check scan alignment,
throw a ball at floor and each wall (including when looking away), pick up and
push items, resize and change collision shape, pause/resume, recenter/recover,
restart and change rooms. Verify live surface placement before loading a scan,
and performance with multiple imported/physical objects. Thin/omitted furniture,
scan accuracy, headset relocalization and controller feel remain limitations
until those tests pass. A saved scan does not automatically follow moved real
furniture.

The Meta OpenXR integration requires linear rendering. The raw RGBA8 browser
texture is explicitly decoded from sRGB in the page shader, while ordinary
imported textures retain Unity's standard sampling. A rendered pixel comparison
checks that phone-page colors are preserved. Final device color QA remains open.
Legacy TextMesh markings explicitly convert their colors for linear rendering
so the tool labels retain their dark ink contrast. A depth-tested, stereo-aware
font shader replaces the legacy overlay material: opaque pages and tools hide
lettering behind them. A render regression checks both visible and occluded ink.

The Windows build scripts clean up only their private ADB helper on port 5041.
A leftover helper inherited Meta's `TRiMSchema.json` file handle, making the next
editor abort inside the SDK's native cache removal. Removing that helper resolved
the crash without modifying the SDK. The user's default ADB server is left alone.
Meta 207's optional DevAgent is disabled, and its automatically injected editor
address/token are cleared by a later build callback. These credentials are never
copied into source control. Its unused media-projection activity/service and
foreground-service permissions are excluded during manifest merging. Startup
uses contextual passthrough with the Unity splash disabled.

Primary references inspected 2026-09-25:

- https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-manage-scene-data/
- https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-environment-raycast/
- Meta's package source and 207.0.0 package metadata from packages.unity.com

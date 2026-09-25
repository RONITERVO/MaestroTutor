# Maestro gaze and room following

Development implementation; physical Quest acceptance and performance checks
remain required. This does not complete the Quest v1 release gates.

The solid Maestro movement tray offers Look at me, Follow me, Stop, Distance and
Walk speed. Its movable controls stay outside the book pages. Look at me turns
the head toward the tracked viewer, with smoothed yaw/pitch limits of 60/30
degrees. Follow me walks toward the viewer and stops at the chosen distance.
The included Walk animation uses the common pose rig, so supported custom
humanoids receive the same gait. Reduced motion suppresses the gait and head
animation; an explicitly requested follow still moves the character.

Following requires an aligned room scan and Start physics. Owned navigation
data is built from the same scanned floor, wall and furniture colliders that
rigid items use. Agent clearance follows Maestro's scale; disconnected floors
and incomplete paths are rejected. A capsule check stops a proposed step if it
would hit a scanned surface or a collidable room item. This is a body-clearance
check, not articulated hand/foot contact or a guarantee that all animated limbs
avoid objects. Newly encountered loose objects can stop movement; the current
route does not navigate around them automatically. Real objects missing from
the scan are not inferred. Hair and cloth physics remain excluded.

Look at user and Follow user are also actions in the existing visual sequence
builder. The existing buttons, tutor activity transitions and room events can
trigger those sequences. Their duration and interruption rules still apply.
Stopping a follow keeps its current placement. Gaze/follow yield to gripping,
posing, recording, avatar replacement, room recall, tracking loss, pause or loss
of focus. Returning tracking or resuming the app does not restart them.

Follow distance and walking speed are validated, undoable room preferences.
Older room files retain defaults of 1.3 metres and 0.65 m/s. Movement periodically
saves its placement, and stopping captures the final position. Active movement
is not persisted or started by loading a room.

Still required: Quest checks with the included and custom avatar, irregular
floor/wall scans, scale extremes, moving obstacles, controller/hand grip
interruption, actual visual rules, prolonged use and comfortable tray placement.
Independent editable thumbstick bindings for avatar movement and opt-in user
locomotion remain accepted v1 work; this tray does not implement them.

The 2026-09-26 verification passed 33 EditMode and 32 PlayMode tests (the latter
include two optional local-model checks). Movement-specific checks exercise an
actual NavMesh route around a wall, disconnected floors, invalidated scans,
visible head rotation, tracking/recording/recovery/pause interruption, actual
walking and stopping distance, saved placement/preferences, Undo and a timed
visual-rule action. The included Walk clip loops, moves both legs and leaves
the root position unchanged. The movement tray and included gait were rendered
in Unity. These checks do not replace physical room/headset acceptance.

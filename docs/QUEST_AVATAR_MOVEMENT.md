# Maestro gaze and room following

Development implementation; physical Quest acceptance and performance checks
remain required. This does not complete the Quest v1 release gates.

On 2026-09-26 the user confirmed that Follow me moves the included avatar on
Quest 3, but reported stationary feet. Treat the gait as a hardware defect still
under investigation, not a missing user-supplied animation. Stop/distance and
custom-avatar gait acceptance remain open. The forthcoming Meshy default and
large animation library are recorded in QUEST_V1_PLAN.md.

The solid Maestro movement tray offers Look at me, Follow me, Stop, Distance and
Walk speed, Size, Walk clip and Preview walk. Its movable controls stay outside the book pages. Look at me turns
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

## Imported clips and confined rooms (development, 2026-09-26)

Size cycles through 0.35, 0.5, 0.75 and 1.0, persists with the room and supports
Undo. It keeps the feet placement; following recomputes clearance at that scale.
Preview walk plays in place without needing a scan. Stop ends the preview or
movement. Walk clip selects either the included gait or a loaded Maestro clip
at least 0.1 seconds long; the selection is saved against that model and resets
when the avatar is replaced. The included walk remains available on custom rigs.
Clip speed is a provisional gait-rate adjustment, not calibrated foot planting.

Imported tutor clips are sampled through the canonical rig before gaze and
retargeting. Horizontal hip travel is held at the fitted placement; navigation
owns following translation. This is in-place playback, not authored travel or
contact-aware locomotion. The import tray can play clips on selected Maestro;
Use Maestro now selects the tutor automatically. ImportedClip visual rules use
the exact model hash and clip index, and the Motion control selects a clip.
Missing/replaced models do not silently play a different motion. Stop, editing,
pausing and replacing avatars interrupt playback. Rules keep their 30-second
step cap; choose an explicit duration for a longer clip.

Blocked-step text distinguishes a scanned surface, book and created object.
The installed baseline's mask excludes default-layer tool trays; their position
was not established as the user's blocker. No connected route remains a
separate condition. Neither Size nor these messages disable scanned collisions.

These changes await device acceptance after charging. The larger motion-pack
library is specified in QUEST_ANIMATION_LIBRARY.md and is not yet implemented.

# Avatar-held props: development checkpoint

The shared runtime is now `Interaction/HeldRoomProp`. The original fitted-avatar
route remains supported; standalone recipe-part, avatar-hand and root attachment
uses `object.hold` through the same implementation. See QUEST_OBJECT_ATTACHMENTS.md.

A Maestro animation step can carry one created room item in the left or right
hand and return, drop or throw it. Included gestures, recorded avatar motions,
embedded model clips and compatible saved-library motions use the same workflow.
The action can be triggered by the existing room/controller-mounted buttons,
tutor activity events or VR item events. No separate script or tutor is added.

## Authoring a throw

1. Choose a Maestro animation step in the physical rules tray. For a downloaded
   animation, assign a compatible motion using the book library or Motion tool.
2. Select a created ball or another imported/created prop. Tap the tray's solid
   **Props** tab, then **Use prop**. Choose **Prop hand** as needed.
3. Position the object beside that hand, release your grip, then tap **Fit prop**.
   Its current position and rotation become the hand-relative fit. The fit is
   tied to this exact avatar model; after changing avatars, fit the prop again.
4. For flight, use the physics tray to make the item **Solid** or **Bouncy**.
   Load/check the scanned room and explicitly start aligned room physics.
5. Cycle **Prop release** to Throw. **Release time** moves in 5% increments of
   this step's duration. Choose a point while the hand is moving, clear of the
   body and room surfaces. **Try action** previews the complete action sequence.
6. Adjust the fit, motion and timing, then attach the sequence to a room/button
   or event through the existing rules. **Undo rules** restores an earlier edit.

**Return** carries for the step, then restores the prop's original placement;
it does not keep the item in hand between steps. **Drop** releases without an
initial impulse. **Throw** uses the displayed hand movement and rotation over
roughly the last 0.1 seconds, then Unity physics handles gravity and collisions.
Existing speed limits remain 15 m/s and 30 rad/s. The avatar animation continues
for the remaining step duration after release.

## Ownership and interruption

The scheduler reserves both Maestro and the prop, including while an imported
motion loads. The prop stays still during that load. Conflicting rule sequences
use the existing ignore/restart/queue policy. Recording/posing ownership and a
user's active grip prevent starting the action.

Grabbing the carried prop stops its action and hands over at the current
placement. Stop, interruption, an avatar replacement or a blocked path before
release restores the prop; ordinary cancellation never throws it. Released
objects continue under physics when the animation ends. Room/tracking loss or
app suspension cancels active rules, and a later resume does not replay them.
A running room becomes a requirement for the entire hold if it was active when
the hold started. Drop and Throw always require running aligned room physics.

A sphere sweep conservatively checks movement against room surfaces and items;
actual collider penetration checks validate each new pose. Release also checks
clearance from Maestro's body. Dense queries, unavailable geometry, stale motion
samples and invalid release positions stop the action with an explanation.
This is bounded collision checking, not a guarantee against every possible
penetration or a simulation of physical fingers.

## Persistence and limits

Rules now save to `rules.v3.json`. Valid v1/v2 rules load and upgrade on save,
with the original older files retained. Corrupt/newer current files cannot
silently fall back to older rule families. Unknown versions remain protected
from writes. Room, motion-library and activity-profile formats are unchanged.
Undo/Redo includes prop identity, hand, avatar identity, fit and release timing.

This checkpoint provides one prop per animation step. It has no automatic
pickup/catching, hand IK, finger fitting, two-hand grips, per-limb contact or
permanent attachments. Fit uses the displayed hand and avatar scale, not raw
imported bone units. Non-spherical props can stop early near a wall because of
the conservative sweep. A step starts by moving the prop toward the hand; author
its pickup trajectory separately if a visible approach is needed.

Release happens on the first presented frame reaching the selected time, using
the latest displayed pose. This is not a sub-frame exact animation event. Throw
needs at least 0.02 seconds of sampled movement; a pause longer than 0.25 seconds
invalidates the samples. Looping a motion does not repeatedly spawn or throw
objects. Repeating a rule can reattach the same released prop and should be
chosen deliberately.

## Verification

EditMode checks cover validation, both-target scheduling, cancellation, v2/v3
migration and protected future files. PlayMode checks exercise an actual
recorded throw with gravity/floor collision, a blocking wall, physics pause,
user grip takeover, saved Undo, a synthetic Mixamo import and saved motion,
and real pointer activation of the physical Props/Rules tab. The tab keeps the
existing tray size and no controls are added over the book's chat pages.

Quest acceptance remains required: fit and release readability, natural throw
timing, grip handover, small-room blocking, scan/tracking interruption and frame
rate with imported avatars/props. See QUEST_DEVICE_QA.md for the packaged build
and precise evidence. No new headset result is claimed by these desktop tests.

# Avatar action channels

PC development checkpoint, 2026-09-27. The same native scheduler and catalog now
support an explicit upper-body gesture alongside walking and gaze. This is an
initial composition feature; arbitrary imported clip masks, parallel branches,
foot planting, and Quest performance/comfort acceptance remain future work.

## Shared contract

| Capability | Target ownership |
| --- | --- |
| avatar.gesture.upperBody | upperBody: spine, chest and both arms/hands |
| avatar.look.user | gaze |
| avatar.follow.user | locomotion and gaze |
| time.wait | none |
| Other animation/gesture actions | wholeTarget |

The new upper-body action takes target `maestro`, seconds 0.1–30 and gesture
`greeting`, `pointing`, `listening`, `speaking` or `idle`. Walk and props are
deliberately absent from its schema. The existing avatar.gesture.play contract
keeps its full-body behavior, including Walk and prop handling.

One-off calls and each active version-3 invocation acquire catalog channel claims.
Two claims conflict when they name the same object and the same channel, or either
is wholeTarget. Prop claims always cover the entire prop. Waiting event programs
own nothing. Version-2 saved programs retain whole-object reservations across the
entire program; use event programs for concurrent persistent behaviors.

Catalog preflight and actual Start use the same scheduler conflict check and native readiness handlers. Direct tool/controller movement is also protected from conflicting program starts. A busy one-off or
v3 invocation fails without preemption or hidden queuing. The original app owns
the provider; agent, optional book controls and native handlers share these calls.
No new provider client or per-combination agent tool is introduced.

## Pose evaluation and Stop

The upper-body clip samples a transform-only copy of the included skeleton.
It blends into canonical torso/arm rotations over 0.15 seconds after base
animation/imported gait evaluation and before gaze and humanoid retargeting.
It never writes the room root, hips, legs, neck, head, scales or bone lengths.
A saved static pose is restored before each sample to prevent accumulation.
Imported compatible avatars use the same canonical composition and retargeter.
The imported gait's exact motion ID and lease remain owned by walking.

Stopping the upper-body run restores only its sampled bones and releases its
channel; it never restores room placement or stops the walking clip. Stopping
walking/gaze leaves the upper-body action running. A new direct movement command
or controller movement takes over conflicting channels while retaining an
upper-body action. Manual posing, whole-avatar grabbing, edits, recall, model
changes, pause/focus loss and Stop all still interrupt the relevant owners.
Resume never replays a cancelled layer.

Follow includes gaze. Separate Look cannot run alongside Follow; they would both
turn the head. Full-body imported clips, recordings and prop actions remain
exclusive. Automatic activity clips yield to an explicit upper-body gesture and
may resume their normal selection policy after that gesture ends.

## Acceptance

EditMode covers symmetric conflicts, full-body/prop exclusivity, event waits and
channel release, targeted cancellation and manual takeover. PlayMode measures
actual root movement and arm/leg rotations for walking plus gesture plus gaze,
isolated Stop without placement snapping, and visible imported-avatar retargeting
while the saved gait lease remains unchanged. Existing lifecycle tests continue.

Headset checks must still confirm appearance, comfortable transitions and frame
timing on Quest 3. No device installation or acceptance is implied by PC tests.

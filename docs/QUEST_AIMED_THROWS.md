# Shared aimed physical throws

`object.physics.launch@1` and `object.physics.trajectory@1` use the existing
rigid body, gravity, damping, room collision and pause behavior. The action is
available to the agent, typed programs and the generated book form. The capability
feature is `objectLaunch.v1`; there is no new provider tool or numeric tray action.

## Contract

- Choose a loaded Solid/Bouncy creation, a flight time of 0.2–2 seconds, and a
  maximum initial speed of 0.1–8 m/s. Launch replaces velocity and clears spin.
- Aim at a world-space point, or an exact object root, named recipe part or
  Maestro hand with a local offset. Anchor revisions/avatar hash guard identity.
  Both source and anchor object must be authorized program resources. Anchor
  offsets use the same holder-scale metres as `object.hold`.
- The destination is the desired collision-volume centre. It is sampled at
  launch, with no prediction of where a moving holder will go.
- The read-only trajectory fact returns `ready`, `reason`, origin, destination,
  velocity, rounded flight seconds, initial speed and enclosing radius. Failed
  readiness uses zero numeric placeholders; they are not a valid flight plan.
- Start recomputes the plan. A preview does not reserve objects or approve an
  effect. Held, animation-owned, disabled or unready objects and paused/unready
  physics refuse the launch. An active viewer is required.
- The fixed-step estimate includes current gravity and linear damping. Supported
  fixed steps are 0.005–0.05 seconds. Flight rounds up to a whole number of steps;
  at most 400 integration steps and 65 path samples are needed. Non-allocating
  collider queries have a fixed 48-entry buffer and refuse saturated results.
- Paths are checked against scanned surfaces, items, room environment and
  controller colliders; triggers are ignored. A 35 cm zone around the observed
  head position, expanded by the object's radius, also excludes a trajectory.
  This checks current geometry, not future movement or scan accuracy.
- A sphere uses its actual enclosing radius. Other objects use a conservative
  bounding sphere, which can require more clearance than the visible model.
  Enclosing radius is limited to one metre. There is no invisible repositioning
  to make an obstructed launch succeed.

Completion means **launched**, never arrived or caught. Physics remains authoritative:
new obstructions, collisions and moving destinations can change the outcome.
Programs can await the existing collision, proximity or motion subscriptions to
react afterward. Cancel cannot retract a completed launch; Pause clears velocities
without replaying them when physics resumes. Duplicate request IDs report the
original durable result and cannot throw again.

The action itself creates no saved edit or Undo entry. The existing room physics
autosave captures later placements; an explicit temporary room keeps changes in
its fork until Keep. Arm animation, a carried prop's release, and an aimed launch
can be composed through the shared programs. This capability does not add an IK
catcher, a moving-target predictor, a homing controller or a guaranteed landing.

## Verification boundary

PC PlayMode tests use real rigid bodies and floor contacts, including a launch
from floor height, actual free flight at desktop and 72 Hz fixed steps, changed
gravity, floor bounce, fresh/stale anchor identities, a newly inserted wall,
apex obstruction, room bounds, head clearance, speed refusal, disabled physics,
owner conflicts, pause and duplicate receipts. A saved program uses the same
launch handler. Captured native catalog, preview and receipt data are replayed
through shared web validation and the existing book component.

These are PC tests. Actual Quest timing, tracking loss, scan alignment, moving
obstacles and comfort acceptance remain device gates. This file makes no frame-time
or catch-success claim.

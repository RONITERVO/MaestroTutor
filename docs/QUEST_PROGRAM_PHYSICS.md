# Programmable rigid-body actions

PC development, 2026-09-27. The shared native catalog now includes two instant
physics capabilities alongside the ten animation/time capabilities:

- `object.physics.impulse`: `{target, x, y, z}`, with each component from -20 to 20.
  Components are Newton-seconds along room axes: right, up and forward. The
  impulse adds to existing linear momentum. Object mass affects velocity change;
  the existing 15 m/s maximum still applies. Angular velocity is retained.
- `object.physics.stop`: `{target}` clears linear and angular velocity once.
  Gravity and collisions continue. It does not freeze, pin or teleport the object.

Targets are created room objects, including compatible imported rigid objects.
Book and Maestro roots are excluded. A valid scanned room, running physics,
dynamic solid/bouncy profile, loaded collision geometry and an unheld object
are required. Animation and carried-prop ownership reject a competing physics
action. Existing manual grips and room physics pause keep their priority.

## One native execution path

Chat/catalog one-off calls, saved programs, event subscriptions and controller
buttons invoke the same scheduler and RigidRoomItem service. No provider calls
happen during simulation. The push uses the existing controller/recording launch
limits, collision and gravity behavior; it does not introduce another physics
system or an animation pretending to be physics.

The registration advertises `duration: "instant"` and owns the whole target only
for its invocation. Instant calls have no seconds argument. One-off success saves
a completed receipt after the effect has actually been applied. A duplicate ID
returns that receipt without applying momentum again. Completion means the push
or velocity reset was applied, not that a flying ball has landed.

Saved programs yield before the next effect, with at most one native effect per
run per scheduler tick. Instant completion never renews instruction or causal
budgets. A Forever loop containing only instant effects eventually exhausts its
activation budget; add an event wait or elapsed timer for continuous behaviour.
As before, v2 programs keep their conservative whole-run reservations; v3 releases
invocation claims and retains state while waiting.

Stopping a program prevents later steps. It cannot undo momentum already applied.
Use the explicit stop-motion capability to clear current velocity, or pause room
physics. Saving, restoring, reconnecting or inspecting never replays a push.

## User and agent authoring

The existing book action editor offers push components and stop motion without a
fake animation duration. These controls write named calls into the same canonical
program. Components are scalar typed arguments, so a program can calculate them
from variables/state. The generic catalog exposes the exact schema, units,
prerequisites and completion semantics to both user and agent.

The physical rule tray can select these actions and attach the usual triggers
and mounted buttons. Its initial push is 0.6 N·s upward; edit exact components in
the book or ask Maestro. User grips/throws still work through the normal controls.
The familiar chat remains the default book surface.

## Verification and boundaries

Tests cover real mass-dependent/additive impulses, existing speed limits, continued
gravity after velocity reset, rejecting unavailable/owned physics, correct room
axes, shared native one-off dispatch and duplicate-receipt protection. Scheduler
tests cover immediate truthful completion, no same-frame catch-up and instruction
exhaustion without an elapsed wait. Book tests save the same named calls and
preserve the no-duration contract.

PC verification: 1,259 app tests, 121 Unity EditMode tests and 86 PlayMode tests
pass, with three optional private-asset tests skipped. Native captures record a
0.6 kg ball receiving 0.6/1.2 N·s as 1/2 m/s, zero velocity after stop, gravity
resuming, and a rotated room changing the impulse direction without scaling units.

This does not add soft bodies, cloth/hair collision, contact IK, direct skeletal
ragdoll control, arbitrary collision meshes, or every room edit to the catalog.
Quest hardware performance/comfort and store acceptance remain separate gates.

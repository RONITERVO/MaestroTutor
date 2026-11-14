# Shared object attachments

`object.hold@1` temporarily carries a created object at an exact named recipe
part, Maestro hand or object root. It uses the same `HeldRoomProp` trajectory,
collision checks, motion sampling and release code as animation-fitted avatar
props. The advertised feature is `objectAttachments.v1`; this introduces no
second physics engine, provider tool, numeric action or saved constraint format.

## Contract

- `target` is a loaded created prop. `holder` is a discriminated choice:
  `recipePart` with objectId/part/revision; `avatarHand` with
  objectId=maestro/hand/avatarHash; or `object` with objectId/revision.
- Both object IDs must be authorized resources. Revisions guard admission;
  exact object/socket instances guard continued execution. Root movement is
  allowed, but a rebuilt recipe or replaced avatar cannot silently substitute.
- `offset` is a vector of length at most one, in holder-root-scale metres.
  `rotation` is a unit quaternion relative to the anchor. `object.anchor`
  reports the exact live world pose and root scale for explicit fitting.
- Optional `reach: {radius, physics}` rechecks the prop origin against the fitted
  anchor immediately before pickup. Radius is 0.02–1 world metres; `physics: true`
  also requires a freely simulating Solid/Bouncy prop. Out-of-reach or unavailable
  pickup fails before changing ownership/placement. Omitted reach preserves the
  existing explicit fitting operation. `anchorZones.v1` is required only when
  reach is supplied. A zone event is observation, never a pickup reservation.
- `seconds` bounds the operation to 0.1–30 seconds. `return` restores the
  original prop placement at completion or cancellation before release.
  `drop`/`throw` hands off to physics at seconds × releaseAt (0.05–1).
- Drop/throw requires a Solid/Bouncy prop and running aligned room physics.
  The actual recent trajectory supplies throw velocity, capped at 15 m/s and
  30 rad/s. A stationary holder cannot manufacture an aimed throw.
- The prop owns `wholeTarget`; the holder remains available for separate
  motion, including a parallel `animation.play` on a named recipe part.
  Both objects remain cancellation dependencies. Gripping either cancels the
  related program group and preserves the prop's current position at takeover.
- Blocked travel/release, interrupted tracking/physics, missing anchors and
  app lifecycle changes fail or cancel without releasing an unreleased prop.
  Released physical motion is not reversed by Stop. There is no automatic retry.
- Self-attachment and nested held-object chains are explicitly refused.
  These would need an ordered constraint solver, which this action does not claim.

`object.attachment` exposes holding/released/failed/idle for both attachment
routes. After the component retires it reads idle; the action result/receipt
preserves the actual outcome. A successful action returns target, holder,
phase (returned/dropped/thrown) and released. A program can branch on that
result after joining its motion and carry functions.

## Shared authoring and limits

Nested catalog variants now resolve typed paths identically in native and web
validation. Blocks can bind holder.objectId/revision and other scalar fields;
selectors remain static. Changing the anchor kind removes incompatible bindings.
The same generated form supports nested fact arguments. Reading an anchor never
grants authority to manipulate it.

Scheduled recipe-part playback samples the scheduler's unscaled clock, including
when physics time is scaled. Default whole-recipe autoplay keeps its existing
clock. Pause/focus/room interruption still cancels; no hidden resumption occurs.

Recipe parts remain visual joints with one stable assembly proxy collider.
This increment does not add per-joint collision, IK reaching/catching, physical
joint chains, aimed ballistic throws or cloth/hair simulation.

## Verification

Native tests exercise a recipe robot carrying a real rigid ball while its arm
animates in a parallel program, physical throw and floor bounce, joined return
values, root/avatar anchors, stale identities, pause, recipe replacement, real
XRI holder grip and nested-chain refusal. Existing avatar carry/throw checks use
the same renamed component. A clock regression runs part playback with physics
time paused. Native observations also validate on the web wire. Physical Quest
acceptance and performance remain outstanding.

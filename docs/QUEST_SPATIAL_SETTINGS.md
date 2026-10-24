# Shared spatial settings

The catalog exposes object physics, Maestro follow preferences, exact walking
animation selection and water traversal using the physical tools' shared
validators, definitions and Undo history. Catalog actions use background save
completion; existing direct physical editor APIs retain their synchronous
contract. Original Maestro owns planning and provider access. Discover each
action only when its declared feature is advertised by the room.

| Action | Read first | Saved change |
|---|---|---|
| `object.physics.configure` | `object.physics.settings(target)` | Creation's fixed/solid/bouncy mode, automatic/box/sphere collider and 0.05–20 kg mass |
| `avatar.movement.configure` | `avatar.movement.settings` | Follow distance 0.8–2.5 m and walk speed 0.2–1.2 m/s |
| `avatar.walk.select` | `avatar.walk.settings` | Included gait, exact compatible library motion ID, or an embedded clip tied to its model hash |
| `object.walk.select` | `object.walk.settings(target)` | Imported character's exact compatible library or embedded gait, or no gait |
| `object.water.traversal.configure` | `object.water.traversal.settings(target)` | Default, ignore, avoid or bounded wading policy |

Supply the exact object revision from the current fact. Both movement values and
all three physics values are required; preserve the values the user did not ask
to change. Manual edits, Undo/Redo and other changes invalidate stale revisions.
Each changed edit saves before success and has one room Undo. Identical values do
not add a spurious Undo. Duplicate action receipts do not save again. Refusal or
cancellation before native acceptance leaves preferences unchanged. After
acceptance, stopping cannot retract the live edit or its save. An unconfirmed
save requires inspection; never blindly repeat it. In temporary rooms the
changes stay in the fork until Keep; Discard restores the previous base.

These five actions have `completion` duration. A started request is not a saved
result: observe its receipt until completion. There is no successful output while
waiting or saving. The result contains the exact accepted target revision, even
if a later user edit has already changed it again. Inspect current values before
using that result as an edit guard.

Settings share bounded, arrival-ordered save admission with creation, placement,
paint and deletion. Waiting copies no room candidate and does not hold a body.
Inputs are captured, and target revision, native eligibility and exact motion
availability are checked again before acceptance. Automatic checkpoints of
unrelated moving objects may continue; their newer poses remain live and dirty
for a subsequent save. Authored edits, edited-target revision changes and
foreign-fork changes still invalidate the prepared candidate. Snapshot capture
and native reconciliation remain owner-thread work; this does not establish
Quest frame-time acceptance.

Shared actions reserve only the target and refuse competing movement, held items
or animation authoring. Physical controls retain manual interruption priority.
Changing one object's physics does not stop other objects or Maestro. The current
live placement is captured before saving so changing mass does not teleport a
falling object back to an older saved position. Collider/body reconfiguration can
affect its motion; pausing or undoing a save cannot reverse a throw. The book and
Maestro cannot receive editable item physics.

These actions never request following, physics start or animation playback. Walk
selection is a preference for later locomotion, not a preview. Model identity,
recorded motion and saved poses are preserved. Exact library motion references
are never silently changed to a different named/tagged motion. Missing or
incompatible saved choices remain visible in the walk fact; existing runtime
fallback and playback status remain explicit.

## Embedded walk discovery

Read `avatar.walk.settings` to obtain the selected model hash, then inspect
`avatar.walk.clips` with that hash and offset 0. It returns at most three entries,
with exact zero-based indexes, bounded names, durations and `selectable` flags.
Advance by `pageSize` until the returned range reaches `total`. Empty pages at
or after total are valid. Missing, changed or loading models are unavailable.
Never reuse an index against another hash or infer it from an untrusted name.
`selectable` checks duration, not whether the clip looks like a convincing gait.
A library search uses the existing compatible-motion discovery path instead.

The book derives fields from the same native action/fact schemas as the agent.
Older wire settings remain for legacy runtimes and atomic create/settings
batches; current planners prefer the catalog's native facts and durable receipts.
Controller bindings and movement/view opt-ins are separate, described in
QUEST_CONTROLLER_CONFIGURATION.md and QUEST_CONTROLLER_MODES.md. Physical scan,
provider conversation, headset comfort and Store acceptance remain release gates.

## Collision profiles and individual objects

The user and agent can assign a reusable environment profile to Maestro, the
book or any creation through `object.environment.assign`. A profile with
`realCollisions=false` lets its members use virtual terrain below the scanned
floor or beyond the scanned walls. Other objects can keep their inherited real
room collisions. The global real-room collision switch remains an upper bound;
profiles cannot turn it back on when it is globally off. Virtual ground and
virtual object collisions remain active. Rendering, passthrough depth and audio
are separate choices, so excluding a physical floor from a robot's movement does
not make it invisible or silent.

Read `environment.profiles` to find a saved definition, `environment.profile`
for its current revision and complete membership, and `object.environment.settings`
for an object's saved assignment and revision. `environment.profile.save`,
`environment.profile.remove` and `object.environment.assign` all have
`completion` duration and use the same bounded save queue as the settings above.
The book generates the profile picker from these definitions; agent and program
calls use the same native implementation. No successful output is emitted before
native application and saving. Profile results retain their exact accepted ID
and revision even after a later human edit; a successful removal returns revision
zero. Assignment results retain the accepted object revision.

The saved-settings fact reads the canonical record, including a temporary fork,
without allocating a native root, loading an asset or starting an action. It stays
available when a view is deliberately unloaded, disabled or unexpectedly missing.
A missing saved object returns unavailable. `realCollisions` is the requested
policy, not effective collision: inherited participation can still be disabled by
the global switch. No simulation-readiness field is fabricated for an absent view.

Use `object.presence` for native lifetime and `object.environment` for the existing
live readiness observation. The latter remains unavailable without an active view.
The generated assignment form loads current values from the saved-settings fact;
that read does not relax the assignment action's native/physical requirements.

A shared definition edit must explicitly claim every member. An unused profile
can be removed; a bound one cannot. A waiting edit rechecks membership, exact
revisions, released/native targets, ground availability and scanned-surface
penetration. It does not copy a whole room or reserve bodies while waiting. If a
real wall becomes obstructive while a save prepares, enabling collisions refuses
the entire change. It never teleports an actor to make a policy fit. Inputs and
new IDs are captured once per started action. Cancellation before acceptance
leaves the room unchanged; after acceptance its save drains and an interruption
identifies the affected resource for inspection. Undo and temporary Keep/Discard
remain available, and unchanged choices add no Undo.

Current saved limits remain 16 profiles with at most 16 bound objects per
profile. Virtual-only movement requires accepted authored ground within the
existing simulation bounds. This does not override the headset's system boundary
or establish physical room alignment. Those limits and the distinction between
collision, visual and acoustic policy are part of the shared contract.


## Consistent saved membership discovery

Use `resource.members` (version 1) with `kind` equal to `environment`,
`appearance`, `visibility` or `region`, the exact definition ID and an empty
`cursor`. Pass its `next` unchanged until empty. Each page contains at most
16 stable object IDs, the current definition revision and a membership generation.
An existing empty definition returns zero members; a missing definition is
unavailable. Appearance membership counts an object once even when several of its
parts or materials use the same definition.

The directory is canonical saved metadata. Reading it never loads a native view,
acquires ownership or edits the world. Object movement, native residency and
unrelated bindings do not invalidate continuation. Membership or definition edits,
including Undo/Redo, invalidate it; an old cursor never becomes valid merely
because Undo restores the previous list. Cursors also belong to the current
workspace/temporary scope. If continuation becomes unavailable, discard the
collected pages and restart rather than combine different generations.

The existing offset-based appearance, visibility and region facts remain
compatible. Appearance and visibility definition revisions alone do not detect
binding changes, so use the shared cursor fact for coherent multi-page discovery.
A completed read still does not reserve objects or freeze their revisions:
subsequent edits must claim all affected members, provide exact current guards
and pass the action's publication checks. This directory adds no saved format,
native loading, physics, profile-member or program-resource allowance.


## Collision-clearance geometry

Live profile changes use `RoomEnvironmentClearance` for selected-ground and
scanned-surface checks; their revision and ownership guards remain in the edit
path. The helper also handles staged collision geometry without enabling it.
A conservative envelope without scanned candidates can establish clearance; nearby candidates require
an enabled shape for the exact penetration test. Pinned Unity tests show that
`ComputePenetration` otherwise returns false for inactive intersecting shapes.
That false result is treated as unavailable, never as permission to simulate.

This is the geometry foundation for restoration, not a completed restoration
admission path. Returning objects still need explicit non-simulating publication,
a current exact check and visible recovery before unloaded collision edits can be
supported. A blocked object must stay inspectable and movable for repair; loading
or clearing an obstacle must not replay an old action or throw.

## Planned scaling of collision profiles

This section records the remaining implementation contract. The saved-settings
read above is available; the current 16-member and native-readiness requirements
for editing remain in force. Appearance and visibility already support unloaded members; collision
participation cannot use that path without physical admission work.

The durable world should store what an object is meant to collide with. A native
view should separately report what is currently prepared and safe to simulate.
That distinction lets a user edit a distant village's policy without loading the
whole village, while an NPC down a virtual hill remains independent of the real
floor. It also keeps rendered opacity and acoustic obstruction independent.

Implement in this order:

1. Keep the implemented saved assignment/revision read independent of native
   presence. Extend readiness and recovery observations to distinguish unloaded,
   preparing, unavailable ground and obstruction explicitly. An absent
   body is not evidence that the requested policy is disabled or that motion is
   ready. The human inspector and agent facts must return the same distinction.
2. The shared `resource.members` continuation now detects definition and
   membership changes without loading objects. Before raising the collision
   profile limit, add a versioned summary: the current `environment.profile`
   includes all members in one program value and cannot silently grow beyond its
   value budget. Every affected object still needs an exact guard and ownership
   claim. Saved programs retain their declared resource budget; a large one-off
   edit is not a license for an unbounded program action.
3. Integrate the shared geometry proof above into native restoration while keeping
   newly enabled bodies non-simulating. Inactive overlap candidates cannot supply
   exact penetration proof; repeat it with enabled shapes before releasing the
   simulation hold. Keep selected ground,
   current world transform, tracking and object ownership checks. Preparation may
   cross frames, so repeat the relevant checks at publication. Unsupported or
   saturated geometry queries remain unavailable rather than guessed clear.
4. Permit canonical policy edits for deliberately unloaded members once that
   admission contract exists. Preserve their saved poses and unloaded identity;
   do not acquire all roots to save a definition. Live members must pass the
   existing checks before the entire shared edit is accepted. Loading or retiring
   members cannot be treated as stable unloaded records.
5. When a native view returns, apply the saved policy before enabling physics.
   A moved scan wall, missing support or changed geometry must not create a
   penetration impulse, an accidental fall or automatic restart of a cancelled
   action. Preserve the authored object and provide a visible, non-simulating
   recovery path so user and agent can inspect or reposition it. Being blocked
   from simulation must not make the object impossible to repair.

`RoomEnvironmentEdits.CanChangeEnvironment` currently performs the live check;
`RoomNativeResidency.ActivateNativeTargets` prepares staged roots and publishes
identities together. They must use a coherent admission decision before dormant
policy edits are introduced. Existing `RoomSimulationEnvironment` bounds and
`RigidRoomItem` readiness gates remain necessary; a centre-in-bounds check does
not replace exact overlap proof.

Required regressions cover a shared profile spanning live and unloaded objects at
the native-root budget; stale membership; an intervening human edit; cancellation
and failed save; Undo/Redo and temporary Keep/Discard; a wall moved during native
preparation; an imported collider without supported overlap proof; and loss of
tracking or accepted ground. A below-floor NPC and a real-floor ball must coexist
without changing each other's collision, visibility, audio or loading policy.
Verify blocked-object repair through both generated Book controls and agent
operations. Headless and rendered journeys establish those software contracts;
actual Quest alignment, motion and sustained performance still need candidate
headset evidence.

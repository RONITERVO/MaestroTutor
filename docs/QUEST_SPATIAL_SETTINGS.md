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

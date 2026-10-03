# Shared snap points and construction placement

Snap points are saved root-local frames on ordinary created objects, including
imports. The agent, programs and generated book controls use the same definitions
and actions. The included building brick has editable `Top` and `Bottom` points
in family `Brick`; its visual geometry and simple collision proxy are unchanged.
Explicit placement and optional proximity previews on the construction move handle
use the same saved definitions and atomic action.

## Definitions and inspection

`object.snapPoint.edit` configures or removes one stable `point` ID using the
object's current revision. A definition contains a readable name, a case-sensitive
family and a position/unit-quaternion frame. Points of the same exact family may
be matched. IDs/families start with a letter and use letters, digits or underscore,
up to 32 characters. Names contain non-whitespace text, up to 64 characters.
Positions stay within ten local metres. At most 64 points per creation and 256 per
room; built-in book/Maestro are excluded. Animated recipe-part/bone points are not
supported by this component.

`object.snapPoints {target, offset}` returns total, revision and at most eight IDs.
Start at offset zero and advance by eight. `object.snapPoint {target, point}`
returns revision, configured and the definition. An absent point returns inert
editable defaults with configured=false. Point coordinates scale with the object;
read `object.placement` for its live room-local pose. This bounded paging keeps
facts within the shared program-value budget, including a 64-square board.

Frames coincide when snapped; these are matching coordinate frames, not opposite
outward face normals. `turn` rotates about the destination frame's local +Y. For
example a brick's bottom and top frames both use identity rotation, so a zero turn
stacks upright and a 90-degree turn rotates the upper brick on that plane.

## One shared placement and optional join

`object.layout.snap` receives ordered `members: [{target, revision}]`, the first
member's `point`, `destination: {target, revision, point}`, `turn` (-180 to 180
degrees) and mode `place` or `join`. Join additionally takes breakForce and
breakTorque (0–10000; zero means unbreakable). It translates/rotates 1–15 members
as one arrangement. Optional `scale` multiplies member sizes and offsets; omitted
means 1. A scaled call requires `constructionSnapping.v1`. The destination is a separate creation;
all sixteen possible affected objects must have current revisions. Include the
complete connected construction in the moving list, as for group movement.

Physics must be paused; all members/destination must be loaded, released, idle and
free of drawing/animation authoring. Geometry and poses are validated before a
save or live change. The complete final arrangement must remain within existing
room placement bounds. All affected whole-object channels are acquired through
the shared ownership layer; a user grip takes priority. Missing points, mismatched
families, stale revisions, incomplete connections and failed saves leave the
original arrangement intact.

Place changes poses only, suitable for loose building or board pieces. Join also
adds the existing fixed-connection component from the first moving member to the
destination. That first member must use solid/bouncy physics and have no outgoing
connection; choose an assembly's unlinked root first. It never replaces a link
silently. The accepted joint stores its own local frames, including turn and break
limits, so later editing/removing a snap point does not alter an established joint.
Connection limits, breaking and rearming retain their ordinary shared semantics.

One save and one Undo cover every moving member, the new link if requested, and
the stationary destination's current live pose. This avoids saving alignment
against a stale target placement. Exact receipt replay cannot snap twice. Temporary
edits stay in the room fork. Stop cannot reverse a completed saved edit; Undo can.

Points do not reserve exclusive occupancy, prove an overlap-free fit, enforce chess
rules, infer a destination or merge meshes. Pieces can later be moved by ordinary
interaction or physics. A rule can observe placements/structure baselines to track
board state; a reliable occupied-slot game needs an explicit rule policy.

## Physical construction grips

`room.selection.snapSettings` configures the construction handle through the same
catalog/form/agent path. Read `room.selection.snapping` to obtain current `stateId`, `mode`,
`distance`, `turnStep`, `breakForce` and `breakTorque`. A stale state ID or held
handle refuses configuration. Settings are session-local, create no room Undo and
reset when the workspace is reopened. They start **off**. The book's construction
section offers **Review grip snapping**, which opens the ordinary generated form;
loading current values and running it remain explicit.

Place previews alignment without a connection. Join previews a fixed connection
with the selected breaking limits. Zero force/torque means unbreakable. Use 1–15
moving pieces; a 16-piece selection can still move normally but has no spare slot
for a snap destination. Points on every moving piece are considered; join only
considers solid/bouncy pieces without outgoing connections. The selected point's
owner becomes the action pivot without changing the user's selection order.

Matching uses exact families, nearest point distance and stable object/point ID
ordering for ties. Distance is in room-local metres, bounded to 0.01–0.25 (default
0.08). Frames must be within 30 degrees of matching +Y. Twist around that axis is
rounded to `turnStep` degrees (default 90); zero preserves free twist. Changing the
step permits other construction styles without a brick-specific mechanic.

The construction itself previews the snapped pose, including two-hand resizing.
A small solid cross marks the mating point and the handle says **Release to snap**
or **Release to join** with the point names. Pull away to return to free placement.
`room.selection.snapPreview` exposes the exact visible source/destination IDs,
point IDs, turn and scale to programs and Maestro. No compatible preview means
`active=false` and empty IDs; reading never moves anything.

Release restores starting poses, relinquishes grip ownership, then runs ordinary
`object.layout.snap` with exact IDs, current revisions and the relative scale.
One save/Undo covers the whole result. A target that becomes held, unavailable or
changes identity/revision at release cancels the snap and restores the moving
pieces; it does not silently become a loose placement. Save failure also restores
all moving pieces. Pause, recall and teardown cancel without saving. Destination
objects are not reserved during the preview, so another hand can pick one up.

This applies to the explicit construction handle, including a single selected
piece. Ordinary direct object grips keep their existing physics/throw semantics.
There is no occupied-socket reservation, collision-free guarantee or scan snap.
Quest latency, reach and comfort need device acceptance.

## Persistence and reusable constructions

Copies, prototypes, template creation, captured modules and portable workspaces
retain detached point definitions. Captured object IDs are still replaced with
fresh IDs when instantiated; local point IDs stay stable. Optional snap-point
fields carry `snapPoints.v1` in the catalog, including nested prototype source.
Older strict schemas reject this field rather than silently dropping it.

Current room format/file is 8 (`room.v8.json`), paired snapshot intent is 7
(`room-snapshot.v7.json`), and archive manifest is 7. Clean earlier room formats
1–5 and 7 can load without snap points; connected room format 6 stays refused.
Earlier unfinished snapshot transactions/archives are preserved and require the
existing explicit recovery/reset flow; they are not guessed into the new format.
Future point component versions make the room read-only and preserve its bytes.

## Verification

The shared fixture covers valid configure/remove/place/join and invalid identity,
frame, membership, revision and bounds. EditMode covers copied data, per-object and
room budgets, old/future versions, prototypes, template points and room-space math.
PlayMode exercises rotated/scaled destinations, complete connected groups, actual
fixed joints under impulse, actual XR grip snapping/resizing, pull-away, destination
grab invalidation, setting guards, capture with fresh IDs, stale/missing/family failures,
failed-save rollback, current grip ownership, physics gating and temporary Undo.

The native full-app journey records point edits, paging, exact snap/join, connection
readback and one Undo in `snap-authoring.json`. Chrome replays those actual states
through the production generated form, requires both current revisions, rejects
an invalid turn and matches the exact native call and receipt. Desktop checks do
not establish Quest readability, reach, comfort or performance; device acceptance
remains pending while the headset hold is active.

The grip-settings journey additionally records the full native app configuration,
guard renewal, idle-preview fact and reset. Chrome matches that exact call and
receipt, requires current settings and rejects an excessive radius.

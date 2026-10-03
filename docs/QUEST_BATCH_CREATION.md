# Atomic structure creation and typed results

`object.batch.create` (`batchCreation.v1`) creates 1–16 pieces from an editable
blueprint. Version 1 keeps pieces independent; version 2 adds physical hinges with
`connectedBlueprints.v1` and `physicalHinges.v1`. The optional book form, source/blocks
and room agent use the same contract. This does not create a persistent assembly
entity, a separate blueprint library or snap sockets.

## Definition and placement

A blueprint contains ordered pieces with unique `slot` keys matching
`[a-zA-Z][a-zA-Z0-9_]{0,23}`, a display
name, local position/rotation/scale, and a source. Sources are either an exact
bundled template hash or an inline recipe with optional collision and physics.
Both expand through the same recipe/component preparation used by single-object
creation. Empty names use the template name or `Recipe piece`. Recipe tracks are
retained but pieces must start idle; start animations explicitly afterward.

The call's outer room-local position, unit rotation and uniform scale transform
all pieces. Local offsets are bounded to 10 metres; every final position must be
within 25 metres of the room origin and every resulting scale within 0.1–4.
Rotation composition normalizes the validated quaternions. Scale changes geometry;
it does not automatically change the configured mass. Placement is explicit,
without overlap avoidance, scan anchoring or a guarantee of physical stability.

The whole candidate is checked against existing room-object, recipe-part,
generated-vertex and collision budgets before saving. Six bricks can fit even
when sixteen 19-part robots cannot: individual validity is not aggregate capacity.
A failed candidate/save adds no partial objects. Accepted saved creation writes
once and adds one Undo; temporary creation stays in the existing fork. Undo removes
the whole batch and Redo restores the same IDs. Completed receipt replay cannot
recreate an undone batch. Stop leaves already accepted creations in place.

## Connected blueprints

Version 2 requires 1–15 `hinges`. Each entry names an `owner` slot, a distinct
`connected` slot and an ordinary hinge `definition` (enabled, local frames, limits
and drive). No room IDs or component versions belong in that definition. Each
instance binds fresh member IDs internally; a blueprint cannot connect itself to
an unrelated existing object. Each owner has at most one connection; cycles,
missing slots and duplicate owners fail before saving. Version 1 rejects links.

The complete transformed placement must have coincident anchors within 3 cm,
axes within 5 degrees and an angle inside enabled limits with 3 degrees tolerance.
Uniform scaling affects anchor distances too. All new links count against the
existing 16-hinge room budget before creation. Save failure cannot leave half a
mechanism. Creation does not start room physics or promise collision-free placement.

The included **Spring lever** module is ordinary editable source: a fixed mount,
a solid handle and a limited spring hinge. Its exported `create(position, rotation,
scale)` function returns the mount and handle IDs, in that order. Loading the
module does nothing. Invoke its function from a program; Start physics remains
explicit. See [program modules](QUEST_PROGRAM_MODULES.md). Adjust geometry, limits
and spring settings in a copy; no special lever tool or runtime is involved.

## Shared programs and object identity

Results contain `objectIds`, `slots` and `temporary`. The first two lists have the
same order and length as the input pieces. Each new object is independently
editable/grabbable and retains its full recipe/components. Display names never
select objects. The returned IDs are authorized only by validated native results;
copying arbitrary strings into a list does not grant edit authority.

A program reserves the full piece count before entering this action. Its existing
16-creations-per-run budget also applies across parallel branches. For example,
one earlier creation leaves insufficient budget for a 16-piece batch: the entire
batch is refused before any of its pieces is created. Returning an existing target
from a physics/settings edit consumes no creation allowance, so a program can
continue editing its successfully created pieces after reaching the limit.

Programs can keep this blueprint definition in ordinary saved source or pinned
reusable modules. Parameters can bind the outer pose fields. Each invocation
creates new identities; it does not reconnect to a previous instance. Exact
bundled hashes remain pinned, and missing templates fail rather than substituting
by name. Inline recipes are self-contained. Imported model/prototype sources and
a user blueprint asset library remain separate work with explicit asset-retention
requirements.

The [shared create-and-paint fixture](../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-batch-create.json)
creates six bricks, receives their IDs as a typed list, and loops over that list
to paint every piece through the normal object capability.

## Typed action results

`structuredResults.v1` extends result-variable bindings to fixed record/list
shapes derived from the native output schema. Variables use the existing
`dataVersion: 1` types and budgets: depth four, 32 list entries, eight record fields,
128 nodes and 1,024 estimated JSON characters per value. Optional/nullable/variant
output shapes are not exposed as fixed program types. A schema's structural type
does not waive these value budgets: an oversized output binding fails explicitly.

The compiler compares structural types rather than object identity. The visual
editor creates an explicitly typed empty list/record default when needed. Native
completion validates every bound value against its destination type before
changing locals or granting returned object IDs, including empty lists whose type
cannot be inferred from their contents. Existing scalar results remain compatible.

With `structuredInputs.v1`, fixed-shape input lists/records can be computed from
those results. The [build/capture/reset fixture](../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-build-structure.json)
creates six pieces, builds named member records, saves a persistent structure,
moves one piece and resets them all in one scheduled program. Each action retains
its own validation, ownership and Undo semantics. The same variable bindings are
editable in the book. See [persistent structures](QUEST_STRUCTURES.md) and
[typed values](QUEST_PROGRAM_DATA.md) for bounds and authority rules. Snapping and
a dedicated blueprint library remain further work.

## Evidence boundary

Native tests cover aggregate rejection, save failure, one Undo/Redo, replay,
transformed placement, independent editing, temporary discard and a real scheduled
program painting all returned pieces. Compiler tests cover creation-budget
reservation, later settings edits, detached list results, empty lists and rejecting
invalid bound values before partial local updates. Web tests use the same fixture
and verify structural typing and visual result-variable creation. These checks do
not replace Quest interaction or performance acceptance.


## Capture an existing construction

`program.module.captureConstruction` publishes ordinary editable program source
into the same reusable module library. It takes a name and 1–16 members, each
with an exact object ID, current revision and distinct slot. The constructor
exports `create(position, rotation, scale)` and returns new IDs in that order.
The first member defines the origin and orientation; each member retains its
own scale. Capture does not move the originals or start the constructor.

Pause physics and recipe playback, finish animation authoring and drawing
(including retained drafts), and release the members first. Capture reads live
poses and the saved components. It rejects changed revisions, unavailable
geometry, built-in Book/Maestro identities and hinge connections whose other end
is outside the selection. Internal hinges use slots and bind only the newly
created members. Capturing a temporary construction explicitly publishes a
library module; discarding the temporary room does not remove that library file.

The constructor uses the existing `object.batch.create` action and its new
`prototype` source. A version-1 prototype contains public string-named geometry
(block, ball, cylinder, drawing, recipe or model), paint, physics/collision,
drawing patches and their ink, a drawing tip, and optional recorded root motion.
It contains no saved-room enum ordinals or original object identities. Root-motion
positions, rotations and scales are relative to the captured object's pose;
instantiation transforms them with the new piece and checks every resulting
frame. Recipe tracks, component names and stroke IDs stay editable within their
new owner. Nothing plays automatically.

Imported geometry retains exact model hashes. Model bytes are checked before any
piece is committed; missing/damaged dependencies fail with no partial creation.
The usual asynchronous renderer subsequently loads those verified local assets.
A portable module file contains definitions, not GLB bytes; import the exact
models on another workspace/device, or use a workspace archive containing them.
Module references participate in the existing retention checks. No provider,
model download or silent substitute is introduced.

Capture is not a room/game backup: structure monitors, controller bindings,
buttons, running programs and unsaved drafts are separate shared entities.
Existing source-size, creation and room budgets still apply. Oversized capture
fails explicitly rather than dropping components. Creating a captured assembly
uses one room save and Undo; publishing/removing its module uses the library's
existing asynchronous receipt semantics and is not undone by room Undo. Stop
can stop waiting for an already dispatched library write, so inspect the receipt
instead of replaying it. Pinned imports keep their exact definitions.

Native support is advertised as `creationPrototypes.v1` and
`constructionCapture.v1`. The existing schema-generated forms, module inspection,
source/blocks editor and agent catalog expose the same definitions. Prototype
geometry and transformed-motion boundaries have shared native/web fixtures.

The generated capture form can select members by their room names, then **Load
current values** for all members together. Revision fields are read-only in the
form; advanced source can provide an explicit snapshot. Adding capture to a
behaviour produces visible per-member reads with indexed revision bindings,
using the shared current-input metadata and normal program limits. It does not
save a separate capture-only workflow or refresh stale revisions automatically.

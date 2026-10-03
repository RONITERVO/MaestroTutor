# Atomic structure creation and typed results

`object.batch.create` (`batchCreation.v1`) creates 1–16 independent pieces from an
editable version-1 blueprint. The optional book form, source/blocks and room agent
use the same contract. This is multi-object instancing; it does not yet create a
persistent assembly entity, a separate blueprint library, snap sockets or joints.

## Definition and placement

A blueprint contains ordered pieces with unique readable `slot` keys, a display
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
